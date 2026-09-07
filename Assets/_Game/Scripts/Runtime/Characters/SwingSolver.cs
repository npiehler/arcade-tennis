using UnityEngine;
using ArcadeTennis.Court;

namespace ArcadeTennis.Characters
{
    public enum SwingPhase
    {
        /// <summary>Ready to start a stroke.</summary>
        Idle,

        /// <summary>Button held: the marker is up and the zone can still be changed.</summary>
        Aiming,

        /// <summary>Button released; racket on its way to the ball.</summary>
        Swinging,

        /// <summary>Stroke finished; no new stroke can start yet.</summary>
        Recovering,
    }

    public enum SwingGrade
    {
        Miss,
        Early,
        Late,
        Good,
        Perfect,
    }

    /// <summary>Where a stroke is, as plain data so it can be stepped outside play mode.</summary>
    public struct SwingState
    {
        public SwingPhase Phase;

        /// <summary>Seconds spent in the current phase.</summary>
        public float PhaseTime;

        /// <summary>Set once the racket has passed the ball, so a swing resolves exactly once.</summary>
        public bool ContactResolved;

        public bool CanStartStroke => Phase == SwingPhase.Idle;
    }

    /// <summary>
    /// The handful of numbers <see cref="SwingSolver.Evaluate"/> needs, lifted
    /// out of whichever config owns them.
    ///
    /// A serve and a groundstroke are judged by the same arithmetic but never by
    /// the same numbers: a serve is met above the head, off a ball that is barely
    /// moving at the top of the toss, and wants a far tighter sweet spot. Passing
    /// the numbers instead of the config is what lets both strokes share the one
    /// piece of judgement rather than growing a second copy of it.
    /// </summary>
    public readonly struct ContactTuning
    {
        public readonly float SweetSpotRadius;
        public readonly float ReachRadius;
        public readonly float PerfectWindow;
        public readonly float TimingWindow;
        public readonly float PerfectThreshold;
        public readonly float GoodThreshold;

        public ContactTuning(float sweetSpotRadius, float reachRadius,
                             float perfectWindow, float timingWindow,
                             float perfectThreshold, float goodThreshold)
        {
            SweetSpotRadius = sweetSpotRadius;
            ReachRadius = reachRadius;
            PerfectWindow = perfectWindow;
            TimingWindow = timingWindow;
            PerfectThreshold = perfectThreshold;
            GoodThreshold = goodThreshold;
        }
    }

    /// <summary>What the racket made of the ball.</summary>
    public struct SwingContact
    {
        /// <summary>False when the racket found nothing to hit.</summary>
        public bool Made;

        public SwingGrade Grade;

        /// <summary>0..1, combining placement and timing. Drives power and spread.</summary>
        public float Quality;

        /// <summary>Seconds off the ideal moment. Negative is early, positive is late.</summary>
        public float TimingOffset;

        /// <summary>How far the ball passes from the hit centre at its closest, in metres.</summary>
        public float MissDistance;

        public float SpatialScore;
        public float TimingScore;

        /// <summary>Where the racket met the ball. The return flight starts here.</summary>
        public Vector3 Point;
    }

    /// <summary>
    /// The stroke as pure functions, for the same reason the ball and the legs
    /// have them: the live swing, the verification suite and the AI in milestone
    /// 7 all run this identical code, and none of it needs play mode.
    ///
    /// The stroke is deliberately split in two. <see cref="Tick"/> only tracks
    /// where the swing is in time; <see cref="Evaluate"/> only judges the meeting
    /// of racket and ball. Nothing here knows about a scene.
    /// </summary>
    public static class SwingSolver
    {
        /// <summary>
        /// Advances the stroke by one tick. <paramref name="holding"/> is the raw
        /// button state, so a controller only has to report whether the button is
        /// down. <paramref name="contactDue"/> is true for the single tick on
        /// which the racket reaches the ball.
        /// </summary>
        public static void Tick(ref SwingState state, bool holding, SwingConfig config,
                                float deltaTime, out bool contactDue)
        {
            contactDue = false;
            if (config == null) return;

            state.PhaseTime += deltaTime;

            switch (state.Phase)
            {
                case SwingPhase.Idle:
                    // Level-triggered on purpose: holding the button through the
                    // recovery puts the player straight back into aiming, which
                    // is what a rally needs.
                    if (holding) Enter(ref state, SwingPhase.Aiming);
                    break;

                case SwingPhase.Aiming:
                    // Nothing accumulates here. Holding costs nothing and buys
                    // nothing except the time to choose a zone -- and the ball
                    // coming closer while you do.
                    if (!holding)
                    {
                        Enter(ref state, SwingPhase.Swinging);
                        state.ContactResolved = false;
                    }
                    break;

                case SwingPhase.Swinging:
                    if (!state.ContactResolved && state.PhaseTime >= config.ContactDelay)
                    {
                        state.ContactResolved = true;
                        contactDue = true;
                    }

                    if (state.PhaseTime >= config.SwingDuration)
                        Enter(ref state, SwingPhase.Recovering);
                    break;

                case SwingPhase.Recovering:
                    if (state.PhaseTime >= config.RecoverDuration)
                        Enter(ref state, SwingPhase.Idle);
                    break;
            }
        }

        static void Enter(ref SwingState state, SwingPhase phase)
        {
            state.Phase = phase;
            state.PhaseTime = 0f;
        }

        /// <summary>
        /// Judges the contact from the ball's path relative to the racket.
        ///
        /// Both halves of the judgement come out of the same piece of geometry.
        /// Over the few hundredths of a second a swing lasts, the ball travels
        /// close enough to a straight line that the closest approach of the two
        /// bodies has a closed form: for relative position r and relative
        /// velocity v the closest approach is at t* = -(r.v)/(v.v). Its distance
        /// says how well the ball was placed, and -t* says how far off the
        /// moment was -- positive when the ball already went past, so the sign
        /// is the difference between "too late" and "too early".
        ///
        /// Solving it beats simulating it: it is signed in both directions,
        /// whereas a forward simulation can only ever discover lateness after
        /// the fact.
        /// </summary>
        public static SwingContact Evaluate(Vector3 ballPosition, Vector3 ballVelocity,
                                            Vector3 hitCentre, Vector3 hitCentreVelocity,
                                            ContactTuning tuning)
        {
            var contact = new SwingContact { Point = ballPosition };

            Vector3 r = ballPosition - hitCentre;
            Vector3 v = ballVelocity - hitCentreVelocity;

            // How far the ball travels relative to the racket over a whole swing
            // window. When that is shorter than the sweet spot itself, there is
            // no meaningful "too early" or "too late" left to measure -- the ball
            // is hanging, and only placement can decide.
            //
            // This is not a nicety. At the top of a serve toss the relative speed
            // approaches zero, and -(r.v)/(v.v) divides a small number by a
            // smaller one: a ball sitting comfortably on the racket comes out as
            // tens of seconds early and grades as a miss. Below the threshold the
            // closed form is not just imprecise, it is meaningless.
            float travelPerWindow = v.magnitude * tuning.TimingWindow;
            bool timingDecides = travelPerWindow > tuning.SweetSpotRadius;

            float timeToClosest = timingDecides ? -Vector3.Dot(r, v) / v.sqrMagnitude : 0f;

            contact.MissDistance = (r + v * timeToClosest).magnitude;
            contact.TimingOffset = -timeToClosest;

            contact.SpatialScore = 1f - Mathf.InverseLerp(
                tuning.SweetSpotRadius, Mathf.Max(tuning.ReachRadius, tuning.SweetSpotRadius + 0.01f),
                contact.MissDistance);

            contact.TimingScore = 1f - Mathf.InverseLerp(
                tuning.PerfectWindow, Mathf.Max(tuning.TimingWindow, tuning.PerfectWindow + 0.001f),
                Mathf.Abs(contact.TimingOffset));

            bool inReach = contact.MissDistance <= tuning.ReachRadius;
            bool inTime = Mathf.Abs(contact.TimingOffset) <= tuning.TimingWindow;
            contact.Made = inReach && inTime;

            if (!contact.Made)
            {
                contact.Quality = 0f;
                contact.Grade = SwingGrade.Miss;
                return contact;
            }

            // Multiplied rather than averaged: a stroke is only as good as its
            // weaker half, and flawless timing must not paper over a ball met
            // at arm's length.
            contact.Quality = Mathf.Clamp01(contact.SpatialScore * contact.TimingScore);

            if (contact.Quality >= tuning.PerfectThreshold) contact.Grade = SwingGrade.Perfect;
            else if (contact.Quality >= tuning.GoodThreshold) contact.Grade = SwingGrade.Good;
            else contact.Grade = contact.TimingOffset < 0f ? SwingGrade.Early : SwingGrade.Late;

            return contact;
        }

        /// <summary>
        /// How far into the chosen zone the ball actually gets. A flawless stroke
        /// reaches the middle of it; a poor one falls short of it by up to
        /// <see cref="SwingConfig.DepthShortfall"/>.
        ///
        /// That is what makes the deep zone a decision rather than a free choice:
        /// aiming short forgives bad contact, aiming deep does not.
        /// </summary>
        public static float ResolveDepth(float zoneDepth, SwingContact contact, SwingConfig config)
        {
            if (config == null) return zoneDepth;
            return zoneDepth - config.DepthShortfall * (1f - Mathf.Clamp01(contact.Quality));
        }

        /// <summary>
        /// Where the return is aimed. The player picks one of three zones while
        /// the button is held; the contact decides how far into it the ball gets
        /// and how far it may stray from the middle of it.
        ///
        /// <paramref name="spread"/> is handed in rather than drawn here so the
        /// function stays pure and the verification suite can pin it down; the
        /// live controller passes a random point in the unit circle.
        /// </summary>
        public static Vector3 ResolveTarget(int side, AimZone zone, SwingContact contact,
                                            SwingConfig config, CourtDefinition court, Vector2 spread)
        {
            if (config == null || court == null) return Vector3.zero;

            Vector2 zoneCentre = AimZones.GroundZoneCentre(court, side, zone);
            float depth = ResolveDepth(zoneCentre.y, contact, config);

            float scatter = Mathf.Lerp(config.MaxSpread, config.MinSpread, contact.Quality);
            Vector2 offset = Vector2.ClampMagnitude(spread, 1f) * scatter;

            // Sideways slip is kept far smaller than length error. A shot that
            // lands short reads as "I mistimed that"; a shot that ignores the
            // zone the player chose reads as a broken game.
            offset.x *= config.LateralSpreadFactor;

            float maxX = court.SinglesHalfWidth + config.OutMargin;
            float maxDepth = court.HalfLength + config.OutMargin;

            float x = Mathf.Clamp(zoneCentre.x + offset.x, -maxX, maxX);
            depth = Mathf.Clamp(depth + offset.y, 0.4f, maxDepth);

            return AimZones.ToWorld(side, x, depth);
        }

        /// <summary>
        /// Apex above the contact point. A badly met ball is barely lifted, which
        /// is what lets it fall into the net; clean contact buys both speed and
        /// height.
        ///
        /// The clearance floor that keeps a ball met off the ground playable is
        /// scaled by quality as well. Applying it flat would hand every stab a
        /// trajectory that clears the net, and the bottom of the quality range
        /// would stop meaning anything.
        /// </summary>
        public static float ResolveApex(AimZone zone, SwingContact contact, Vector3 contactPoint,
                                        SwingConfig config)
        {
            if (config == null) return 1.5f;

            float quality = Mathf.Clamp01(contact.Quality);

            // The two zone kinds want different flight shapes, not the same one
            // scaled. A deep ball is driven flat; a short one has to be lofted or
            // it simply cannot come down that early without hitting the net.
            float apex = zone == AimZone.Deep
                ? Mathf.Lerp(config.ApexWeak, config.ApexFull, quality)
                : Mathf.Lerp(config.ApexShortWeak, config.ApexShortFull, quality);

            return Mathf.Max(apex, config.MinPeakHeight * quality - contactPoint.y);
        }
    }
}
