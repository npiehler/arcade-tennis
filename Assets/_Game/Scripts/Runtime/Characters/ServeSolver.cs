using UnityEngine;
using ArcadeTennis.Court;

namespace ArcadeTennis.Characters
{
    public enum ServePhase
    {
        /// <summary>Standing at the line, ball in hand.</summary>
        Ready,

        /// <summary>Ball in the air, power building.</summary>
        Tossing,

        /// <summary>Released; the racket is on its way to the ball.</summary>
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
        public float Charge;
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

                    if (holding)
                    {
                        float rate = config.ChargeTime > 0.0001f ? deltaTime / config.ChargeTime : 1f;
                        state.Charge = Mathf.Clamp01(state.Charge + rate);
                    }
                    else
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
            if (phase == ServePhase.Tossing || phase == ServePhase.Ready) state.Charge = 0f;
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

        /// <summary>Charge that actually reaches the ball, docked for poor contact.</summary>
        public static float EffectivePower(float power, SwingContact contact, ServeConfig config)
        {
            if (config == null) return power;
            return Mathf.Clamp01(power) * Mathf.Lerp(config.MinQualityPower, 1f, contact.Quality);
        }

        /// <summary>
        /// Where the serve is aimed. Charge sets the depth, from the foot of the
        /// net to past the service line; the aim slides the ball between the
        /// centre line and the sideline of the box it has to hit.
        ///
        /// The lateral range is anchored to the box rather than to the court, so
        /// a centred stick always aims at the middle of the box the server is
        /// actually serving to -- and because the box the server faces is always
        /// the diagonal one, pushing left still means left on screen.
        /// </summary>
        public static Vector3 ResolveTarget(int serverSide, bool deuceCourt, float power, Vector2 aim,
                                            SwingContact contact, ServeConfig config,
                                            CourtDefinition court, Vector2 spread)
        {
            if (config == null || court == null) return Vector3.zero;

            int server = Sign(serverSide);
            int receiver = -server;
            Rect box = court.GetServiceBox(receiver, deuceCourt);

            float effective = EffectivePower(power, contact, config);
            float depth = Mathf.Lerp(config.MinDepth,
                court.ServiceLineDistance + config.ServiceLineOvershoot, effective);

            float aimX = Mathf.Clamp(aim.x, -1f, 1f) * (server < 0 ? 1f : -1f);
            float lateral = box.center.x + aimX * box.width * 0.5f * config.AimWidth;

            float scatter = Mathf.Lerp(config.MaxSpread, config.MinSpread, contact.Quality);
            Vector2 offset = Vector2.ClampMagnitude(spread, 1f) * scatter;

            float x = lateral + offset.x;
            float finalDepth = depth + offset.y;

            // Missing has to stay possible, but not by half a court.
            float boxNear = Mathf.Min(box.xMin, box.xMax) - config.OutMargin;
            float boxFar = Mathf.Max(box.xMin, box.xMax) + config.OutMargin;
            x = Mathf.Clamp(x, boxNear, boxFar);
            finalDepth = Mathf.Clamp(finalDepth, config.MinDepth * 0.5f,
                court.ServiceLineDistance + config.OutMargin);

            return new Vector3(x, 0f, receiver * finalDepth);
        }

        /// <summary>Apex above the contact point. Power flattens the serve, as it should.</summary>
        public static float ResolveApex(float power, SwingContact contact, ServeConfig config)
        {
            if (config == null) return 0.3f;
            return Mathf.Lerp(config.ApexWeak, config.ApexFull, EffectivePower(power, contact, config));
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
            state.Charge = 0f;
            state.ContactResolved = false;
            state.ServeNumber = doubleFault ? 1 : 2;

            return doubleFault;
        }

        /// <summary>Starts the next point: first serve again, and the other court.</summary>
        public static void NextPoint(ref ServeState state)
        {
            state.Phase = ServePhase.Ready;
            state.PhaseTime = 0f;
            state.Charge = 0f;
            state.ContactResolved = false;
            state.ServeNumber = 1;
            state.DeuceCourt = !state.DeuceCourt;
        }

        static int Sign(int side) => side >= 0 ? 1 : -1;
    }
}
