using System;
using UnityEngine;
using ArcadeTennis.BallPhysics;

namespace ArcadeTennis.Characters
{
    /// <summary>
    /// Puts the ball into play. Like <see cref="SwingController"/> this is a
    /// capability of the body, not a decision-maker: a controller pushes the
    /// button state and the aim in, and the AI in milestone 7 will serve through
    /// exactly the same component.
    ///
    /// It owns the ball from the toss until the serve is judged, and holds the
    /// rally stroke back for that whole stretch.
    /// </summary>
    [RequireComponent(typeof(TennisCharacter))]
    public class ServeController : MonoBehaviour
    {
        [SerializeField] ServeConfig config;
        [SerializeField] Ball ball;

        [Tooltip("Held back while the serve owns the ball.")]
        [SerializeField] SwingController rallySwing;

        [Tooltip("Hidden during the toss, where a landing spot at the server's feet says nothing.")]
        [SerializeField] BallLandingMarker landingMarker;

        TennisCharacter character;
        ServeState serve = ServeState.New();
        Vector2 aim;
        bool holding;
        bool awaitingVerdict;

        /// <summary>Fired when the ball leaves the hand.</summary>
        public event Action Tossed;

        /// <summary>Fired at the moment the racket passes the ball, hit or miss.</summary>
        public event Action<SwingContact> Contacted;

        /// <summary>Fired once the serve is judged. The flag marks a double fault.</summary>
        public event Action<ServeOutcome, bool> Resolved;

        public ServeConfig Config => config;
        public ServePhase Phase => serve.Phase;
        public float Charge => serve.Charge;
        public int ServeNumber => serve.ServeNumber;
        public bool DeuceCourt => serve.DeuceCourt;
        public bool IsServing => serve.Phase != ServePhase.Struck;
        public bool IsCharging => serve.Phase == ServePhase.Tossing;
        public SwingContact LastContact { get; private set; }
        public Vector3 LastTarget { get; private set; }

        /// <summary>Where the server has to stand for the serve it is about to hit.</summary>
        public Vector3 ServePosition =>
            character != null && character.Court != null
                ? character.Court.GetServePosition(character.Side, serve.DeuceCourt)
                : transform.position;

        public Vector3 HitCentre =>
            ServeSolver.HitCentre(transform.position, character != null ? character.Side : -1, config);

        void Awake() => character = GetComponent<TennisCharacter>();

        void OnEnable()
        {
            if (ball == null) return;
            ball.Bounced += OnBounced;
            ball.HitNet += OnHitNet;
        }

        void OnDisable()
        {
            if (ball == null) return;
            ball.Bounced -= OnBounced;
            ball.HitNet -= OnHitNet;
        }

        public void SetSwingHeld(bool held) => holding = held;
        public void SetAim(Vector2 value) => aim = value;

        /// <summary>Starts the next point: first serve, other court, server on the line.</summary>
        public void NextPoint()
        {
            ServeSolver.NextPoint(ref serve);
            PrepareForServe();
        }

        /// <summary>Retakes the current serve without changing the count, after a fault.</summary>
        public void PrepareForServe()
        {
            awaitingVerdict = false;
            if (ball != null) ball.Stop();
            if (character != null) character.Teleport(ServePosition);
            ApplyRallyHold();
        }

        void FixedUpdate()
        {
            if (config == null || ball == null) return;

            bool tossAlive = ball.IsMoving && ball.State.Position.y > config.CatchHeight;

            ServeSolver.Tick(ref serve, holding, tossAlive, config, Time.fixedDeltaTime,
                out ServeAction action);

            switch (action)
            {
                case ServeAction.Toss:
                    ball.Launch(ServeSolver.TossOrigin(transform.position, character.Side, config),
                                ServeSolver.TossVelocity(config));
                    Tossed?.Invoke();
                    break;

                case ServeAction.TossDropped:
                    ball.Stop();
                    break;

                case ServeAction.Contact:
                    ResolveContact();
                    break;
            }

            ApplyRallyHold();
        }

        void ResolveContact()
        {
            SwingContact contact = SwingSolver.Evaluate(
                ball.State.Position, ball.Velocity,
                HitCentre, character.Velocity,
                config.Contact());

            LastContact = contact;
            Contacted?.Invoke(contact);

            if (!contact.Made)
            {
                // Swinging and missing the toss is a fault in tennis, and the one
                // way a toss can cost something.
                ball.Stop();
                Finish(ServeOutcome.SwingAndMiss);
                return;
            }

            float power = serve.Charge;

            LastTarget = ServeSolver.ResolveTarget(
                character.Side, serve.DeuceCourt, power, aim, contact, config, character.Court,
                UnityEngine.Random.insideUnitCircle);

            float apex = ServeSolver.ResolveApex(power, contact, config);
            ball.LaunchAt(contact.Point, LastTarget, apex);

            // From here the ball's own first event decides, rather than a second
            // prediction that could disagree with what the player watches happen.
            awaitingVerdict = true;
        }

        void OnBounced(Vector3 position)
        {
            if (!awaitingVerdict) return;
            Finish(ServeSolver.Judge(position, false, character.Side, serve.DeuceCourt, character.Court));
        }

        void OnHitNet(Vector3 position)
        {
            if (!awaitingVerdict) return;
            Finish(ServeSolver.Judge(position, true, character.Side, serve.DeuceCourt, character.Court));
        }

        void Finish(ServeOutcome outcome)
        {
            awaitingVerdict = false;

            if (outcome == ServeOutcome.In)
            {
                serve.Phase = ServePhase.Struck;
                ApplyRallyHold();
                Resolved?.Invoke(outcome, false);
                return;
            }

            if (outcome != ServeOutcome.SwingAndMiss) ball.Stop();

            bool doubleFault = ServeSolver.RegisterFault(ref serve);
            ApplyRallyHold();
            Resolved?.Invoke(outcome, doubleFault);
        }

        void ApplyRallyHold()
        {
            if (rallySwing != null) rallySwing.Suspended = IsServing;
            if (landingMarker != null) landingMarker.Suppressed = serve.Phase == ServePhase.Tossing;
        }
    }
}
