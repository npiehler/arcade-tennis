using UnityEngine;
using ArcadeTennis.Court;

namespace ArcadeTennis.Characters
{
    public enum ServePhase
    {
        /// <summary>Standing at the line, ball in hand.</summary>
        Ready,

        /// <summary>Ball in the air and the button still down: the zone can be chosen.</summary>
        Tossing,

        /// <summary>Struck at; the racket is on its way to the ball.</summary>
        Swinging,

        /// <summary>Struck and away. The rally owns the ball from here.</summary>
        Struck,
    }

    /// <summary>What the tick wants the scene to do. The solver never touches a ball itself.</summary>
    public enum ServeAction
    {
        None,

        /// <summary>Put the ball in the air.</summary>
        Toss,

        /// <summary>The racket has reached the ball; judge the contact.</summary>
        Contact,

        /// <summary>The toss came down untouched. No penalty -- catching a bad toss is free.</summary>
        TossDropped,
    }

    /// <summary>Whether a struck serve was legal.</summary>
    public enum ServeOutcome
    {
        /// <summary>Landed in the correct service box.</summary>
        In,

        /// <summary>Did not clear the net.</summary>
        NetFault,

        /// <summary>Cleared the net but missed the box.</summary>
        OutFault,

        /// <summary>The racket found nothing to hit. In tennis a swing and a miss is a fault.</summary>
        SwingAndMiss,
    }

    public struct ServeState
    {
        public ServePhase Phase;
        public float PhaseTime;
        public bool ContactResolved;

        /// <summary>1 or 2. A fault on the second is a double fault.</summary>
        public int ServeNumber;

        /// <summary>True for the right-hand court, which is where every game starts.</summary>
        public bool DeuceCourt;

        public bool IsSecondServe => ServeNumber >= 2;

        public static ServeState New() =>
            new ServeState { Phase = ServePhase.Ready, ServeNumber = 1, DeuceCourt = true };
    }

    /// <summary>
    /// The serve as pure functions, on the same terms as the rest of the game:
    /// nothing here knows about a scene, so all of it can be checked without
    /// entering play mode, and the AI can drive it in milestone 7 unchanged.
    ///
    /// The contact judgement is deliberately NOT reimplemented here --
    /// <see cref="SwingSolver.Evaluate"/> does that job for both strokes, fed by
    /// <see cref="ServeConfig.Contact"/>.
    /// </summary>
    public static class ServeSolver
    {
        /// <summary>
        /// Advances the serve by one tick. <paramref name="tossAlive"/> tells the
        /// solver whether the ball is still up; it cannot see the ball itself.
        /// </summary>
        public static void Tick(ref ServeState state, bool holding, bool tossAlive,
                                ServeConfig config, float deltaTime, out ServeAction action)
        {
            action = ServeAction.None;
            if (config == null) return;

            state.PhaseTime += deltaTime;

            switch (state.Phase)
            {
                case ServePhase.Ready:
                    if (holding)
                    {
                        Enter(ref state, ServePhase.Tossing);
                        action = ServeAction.Toss;
                    }
                    break;

                case ServePhase.Tossing:
                    if (!tossAlive)
                    {
                        // Ball came down untouched. Back to the line, no penalty.
                        Enter(ref state, ServePhase.Ready);
                        action = ServeAction.TossDropped;
                        break;
                    }

                    // One press does all of it: it throws the ball, holds the
                    // marker up while the zone is chosen, and swings on release.
                    if (!holding)
                    {
                        Enter(ref state, ServePhase.Swinging);
                        state.ContactResolved = false;
                    }
                    break;

                case ServePhase.Swinging:
                    if (!state.ContactResolved && state.PhaseTime >= config.ContactDelay)
                    {
                        state.ContactResolved = true;
                        action = ServeAction.Contact;
                    }
                    break;
            }
        }

        static void Enter(ref ServeState state, ServePhase phase)
        {
            state.Phase = phase;
            state.PhaseTime = 0f;
        }

        /// <summary>Where the ball leaves the hand: in front of the server, at toss height.</summary>
        public static Vector3 TossOrigin(Vector3 serverPosition, int serverSide, ServeConfig config)
        {
            if (config == null) return serverPosition;
            return serverPosition
                 + Vector3.up * config.TossHeight
                 + new Vector3(0f, 0f, -Sign(serverSide)) * config.TossReach;
        }

        /// <summary>
        /// Where the racket meets the ball: directly above the toss, so a straight
        /// toss passes through it twice and the player picks which pass to hit.
        /// </summary>
        public static Vector3 HitCentre(Vector3 serverPosition, int serverSide, ServeConfig config)
        {
            if (config == null) return serverPosition;
            return serverPosition
                 + Vector3.up * config.HitHeight
                 + new Vector3(0f, 0f, -Sign(serverSide)) * config.TossReach;
        }

        public static Vector3 TossVelocity(ServeConfig config) =>
            config == null ? Vector3.zero : Vector3.up * config.TossSpeed;

        /// <summary>
        /// How deep into the box the serve is sent. With no charge to hold, the
        /// contact is the whole skill: meet the toss well and the serve is deep,
        /// meet it badly and it drops at the net.
        /// </summary>
        public static float ResolveDepth(float zoneDepth, SwingContact contact, ServeConfig config)
        {
            if (config == null) return zoneDepth;
            return zoneDepth - config.DepthShortfall * (1f - Mathf.Clamp01(contact.Quality));
        }

        /// <summary>
        /// Where the serve is aimed. The server picks one of three lanes inside
        /// the box they have to hit -- wide, body or down the middle -- and the
        /// contact decides how deep into it the ball lands.
        /// </summary>
        public static Vector3 ResolveTarget(int serverSide, bool deuceCourt, AimZone zone,
                                            SwingContact contact, ServeConfig config,
                                            CourtDefinition court, Vector2 spread)
        {
            if (config == null || court == null) return Vector3.zero;

            int server = Sign(serverSide);
            Rect box = court.GetServiceBox(-server, deuceCourt);

            Vector2 zoneCentre = AimZones.ServeZoneCentre(court, server, deuceCourt, zone);
            float depth = ResolveDepth(zoneCentre.y, contact, config);

            float scatter = Mathf.Lerp(config.MaxSpread, config.MinSpread, contact.Quality);
            Vector2 offset = Vector2.ClampMagnitude(spread, 1f) * scatter;
            offset.x *= config.LateralSpreadFactor;

            float x = Mathf.Clamp(zoneCentre.x + offset.x,
                Mathf.Min(box.xMin, box.xMax) - config.OutMargin,
                Mathf.Max(box.xMin, box.xMax) + config.OutMargin);

            depth = Mathf.Clamp(depth + offset.y, 0.3f,
                court.ServiceLineDistance + config.OutMargin);

            return AimZones.ToWorld(server, x, depth);
        }

        /// <summary>
        /// Apex above the contact point. Clean contact flattens the serve -- but a
        /// serve into one of the short zones needs its own, far higher arc, for
        /// the same reason a short rally ball does: from above the head there is
        /// no flat line that both clears the net and comes down that early.
        /// </summary>
        public static float ResolveApex(AimZone zone, SwingContact contact, ServeConfig config)
        {
            if (config == null) return 0.3f;

            float quality = Mathf.Clamp01(contact.Quality);
            return zone == AimZone.Deep
                ? Mathf.Lerp(config.ApexWeak, config.ApexFull, quality)
                : Mathf.Lerp(config.ApexShortWeak, config.ApexShortFull, quality);
        }

        /// <summary>
        /// Was that serve legal? A net cord counts as an ordinary fault here --
        /// lets are deliberately out of scope, which spares the whole game a
        /// special case for a rare event.
        /// </summary>
        public static ServeOutcome Judge(Vector3 bounce, bool hitNet, int serverSide, bool deuceCourt,
                                         CourtDefinition court)
        {
            if (hitNet) return ServeOutcome.NetFault;
            if (court == null) return ServeOutcome.OutFault;

            return court.IsInServiceBox(bounce, -Sign(serverSide), deuceCourt)
                ? ServeOutcome.In
                : ServeOutcome.OutFault;
        }

        /// <summary>
        /// Books a fault. Returns true when that was the second one, which is the
        /// point lost -- the caller owns what that means, because the score is not
        /// this class's business.
        /// </summary>
        public static bool RegisterFault(ref ServeState state)
        {
            bool doubleFault = state.IsSecondServe;

            state.Phase = ServePhase.Ready;
            state.PhaseTime = 0f;
            state.ContactResolved = false;
            state.ServeNumber = doubleFault ? 1 : 2;

            return doubleFault;
        }

        /// <summary>Starts the next point: first serve again, and the other court.</summary>
        public static void NextPoint(ref ServeState state)
        {
            state.Phase = ServePhase.Ready;
            state.PhaseTime = 0f;
            state.ContactResolved = false;
            state.ServeNumber = 1;
            state.DeuceCourt = !state.DeuceCourt;
        }

        static int Sign(int side) => side >= 0 ? 1 : -1;
    }
}
