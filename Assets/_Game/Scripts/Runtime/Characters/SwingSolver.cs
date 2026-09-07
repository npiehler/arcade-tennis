using UnityEngine;
using ArcadeTennis.Court;

namespace ArcadeTennis.Characters
{
    public enum SwingPhase
    {
        /// <summary>Ready to start a stroke.</summary>
        Idle,

        /// <summary>Button held, power building.</summary>
        Charging,

        /// <summary>Button released, racket on its way to the ball.</summary>
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

        /// <summary>Raw hold time as a 0..1 fraction. Read <see cref="Power"/> for the usable value.</summary>
        public float Charge;

        /// <summary>Seconds spent in the current phase.</summary>
        public float PhaseTime;

        /// <summary>Set once the racket has passed the ball, so a swing resolves exactly once.</summary>
        public bool ContactResolved;

        /// <summary>Charge with the tap floor applied.</summary>
        public float Power(SwingConfig config) =>
            config == null ? Charge : Mathf.Clamp01(Mathf.Max(Charge, config.MinCharge));

        public bool CanStartStroke => Phase == SwingPhase.Idle;
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
                    // Level-triggered on purpose: a player who holds the button
                    // through the recovery starts charging the next stroke the
                    // moment they are able to, which is what a rally needs.
                    if (holding) Enter(ref state, SwingPhase.Charging);
                    break;

                case SwingPhase.Charging:
                    if (holding)
                    {
                        float rate = config.ChargeTime > 0.0001f ? deltaTime / config.ChargeTime : 1f;
                        state.Charge = Mathf.Clamp01(state.Charge + rate);
                    }
                    else
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
                    {
                        Enter(ref state, SwingPhase.Idle);
                        state.Charge = 0f;
                    }
                    break;
            }
        }

        static void Enter(ref SwingState state, SwingPhase phase)
        {
            state.Phase = phase;
            state.PhaseTime = 0f;
            if (phase == SwingPhase.Charging) state.Charge = 0f;
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
                                            float reachRadius, SwingConfig config)
        {
            var contact = new SwingContact { Point = ballPosition };
            if (config == null) return contact;

            Vector3 r = ballPosition - hitCentre;
            Vector3 v = ballVelocity - hitCentreVelocity;

            float closingSpeedSqr = v.sqrMagnitude;

            // A ball that is barely moving relative to the racket has no
            // meaningful moment of closest approach; judge it purely on where
            // it is. This keeps a stationary ball hittable instead of dividing
            // by nothing.
            float timeToClosest = closingSpeedSqr > 0.0001f ? -Vector3.Dot(r, v) / closingSpeedSqr : 0f;

            contact.MissDistance = (r + v * timeToClosest).magnitude;
            contact.TimingOffset = -timeToClosest;

            contact.SpatialScore = 1f - Mathf.InverseLerp(
                config.SweetSpotRadius, Mathf.Max(reachRadius, config.SweetSpotRadius + 0.01f),
                contact.MissDistance);

            contact.TimingScore = 1f - Mathf.InverseLerp(
                config.PerfectWindow, Mathf.Max(config.TimingWindow, config.PerfectWindow + 0.001f),
                Mathf.Abs(contact.TimingOffset));

            bool inReach = contact.MissDistance <= reachRadius;
            bool inTime = Mathf.Abs(contact.TimingOffset) <= config.TimingWindow;
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

            if (contact.Quality >= config.PerfectThreshold) contact.Grade = SwingGrade.Perfect;
            else if (contact.Quality >= config.GoodThreshold) contact.Grade = SwingGrade.Good;
            else contact.Grade = contact.TimingOffset < 0f ? SwingGrade.Early : SwingGrade.Late;

            return contact;
        }

        /// <summary>
        /// How much of the charge actually reaches the ball. Poor contact costs
        /// power, which is what makes a mistimed shot land short rather than
        /// merely land somewhere else.
        /// </summary>
        public static float EffectivePower(float power, SwingContact contact, SwingConfig config)
        {
            if (config == null) return power;
            return Mathf.Clamp01(power) * Mathf.Lerp(config.MinQualityPower, 1f, contact.Quality);
        }

        /// <summary>
        /// Where the return is aimed. Charge sets the depth, the aim stick sets
        /// the width, contact quality sets how far the ball may stray from that.
        ///
        /// <paramref name="spread"/> is handed in rather than drawn here so the
        /// function stays pure and the verification suite can pin it down; the
        /// live controller passes a random point in the unit circle.
        /// </summary>
        public static Vector3 ResolveTarget(int side, float power, Vector2 aim, SwingContact contact,
                                            SwingConfig config, CourtDefinition court, Vector2 spread)
        {
            if (config == null || court == null) return Vector3.zero;

            int s = side >= 0 ? 1 : -1;
            float effective = EffectivePower(power, contact, config);

            // Spans from the foot of the net to past the opponent's baseline, so the
        // charge can fail in both directions. A shot that lands in is one the
        // player judged, not one the solver guaranteed.
        float depth = Mathf.Lerp(config.MinTargetDepth, court.HalfLength + config.BaselineOvershoot, effective);

            // Aim mirrors exactly the way movement does, so "right" means right
            // on screen for whichever end is being played.
            float aimX = Mathf.Clamp(aim.x, -1f, 1f) * (s < 0 ? 1f : -1f);
            float lateral = aimX * court.SinglesHalfWidth * config.AimWidth;

            float scatter = Mathf.Lerp(config.MaxSpread, config.MinSpread, contact.Quality);
            Vector2 offset = Vector2.ClampMagnitude(spread, 1f) * scatter;

            float maxX = court.SinglesHalfWidth + config.OutMargin;
            float maxDepth = court.HalfLength + config.OutMargin;

            float x = Mathf.Clamp(lateral + offset.x, -maxX, maxX);

            // The near limit keeps a wild shot from being aimed into the net
            // apron: falling short has to stay a consequence of a weak stroke,
            // not something the aim can ask for.
            float finalDepth = Mathf.Clamp(depth + offset.y, config.MinTargetDepth * 0.5f, maxDepth);

            return new Vector3(x, 0f, -s * finalDepth);
        }

        /// <summary>
        /// Apex above the contact point. A weak shot is barely lifted, which is
        /// what lets it fall into the net; power buys both speed and height.
        ///
        /// The clearance floor that keeps a ball met off the ground playable is
        /// scaled by power as well. Applying it flat would hand every stab a
        /// trajectory that clears the net, and the whole low end of the charge
        /// would stop meaning anything.
        /// </summary>
        public static float ResolveApex(float power, SwingContact contact, Vector3 contactPoint,
                                        SwingConfig config)
        {
            if (config == null) return 1.5f;

            float effective = EffectivePower(power, contact, config);
            float apex = Mathf.Lerp(config.ApexWeak, config.ApexFull, effective);

            return Mathf.Max(apex, config.MinPeakHeight * effective - contactPoint.y);
        }
    }
}
