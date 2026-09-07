using System;
using UnityEngine;
using ArcadeTennis.BallPhysics;
using ArcadeTennis.Court;

namespace ArcadeTennis.Characters
{
    /// <summary>
    /// The stroke as a capability of the body, sitting beside
    /// <see cref="TennisCharacter"/> the same way movement does: this component
    /// knows how to swing, but never decides when to. A controller pushes the
    /// button state and the aim in -- <see cref="PlayerInputController"/> today,
    /// the AI in milestone 7 -- and both get exactly the same stroke.
    /// </summary>
    [RequireComponent(typeof(TennisCharacter))]
    public class SwingController : MonoBehaviour
    {
        [SerializeField] SwingConfig config;
        [SerializeField] Ball ball;

        TennisCharacter character;
        SwingState swing;
        AimZone zone = AimZone.Centre;
        bool holding;
        bool suspended;

        /// <summary>Fired when the racket starts moving, before contact. The animator hangs off this in part 2.</summary>
        public event Action SwingStarted;

        /// <summary>Fired at the moment the racket passes the ball, hit or miss.</summary>
        public event Action<SwingContact> Contacted;

        public SwingConfig Config => config;
        public SwingPhase Phase => swing.Phase;

        /// <summary>Which of the three zones the next stroke is aimed at.</summary>
        public AimZone Zone => zone;

        public bool IsSwinging => swing.Phase == SwingPhase.Swinging;

        /// <summary>
        /// Set while the serve owns the ball. Without it the rally stroke would
        /// happily swing at a ball that is still going up off the server's own
        /// toss, which is nobody's idea of tennis.
        /// </summary>
        public bool Suspended
        {
            get => suspended;
            set
            {
                if (suspended == value) return;
                suspended = value;

                // Come back idle rather than mid-stroke, so releasing the serve
                // never hands the rally a half-finished swing.
                if (suspended)
                {
                    swing = default;
                    holding = false;
                }
            }
        }

        /// <summary>The last stroke's result, kept for the feedback layer.</summary>
        public SwingContact LastContact { get; private set; }

        /// <summary>Where the last successful stroke was aimed. Only meaningful when <see cref="LastContact"/> connected.</summary>
        public Vector3 LastTarget { get; private set; }

        void Awake() => character = GetComponent<TennisCharacter>();

        /// <summary>Raw button state. The stroke fires on the press, not on the hold.</summary>
        public void SetSwingHeld(bool held) => holding = held;

        /// <summary>Picks the zone the next stroke is sent to.</summary>
        public void SetZone(AimZone value) => zone = value;

        void FixedUpdate()
        {
            if (config == null || suspended) return;

            SwingPhase before = swing.Phase;
            SwingSolver.Tick(ref swing, holding, config, Time.fixedDeltaTime, out bool contactDue);

            if (before != SwingPhase.Swinging && swing.Phase == SwingPhase.Swinging)
                SwingStarted?.Invoke();

            if (contactDue) ResolveContact();
        }

        void ResolveContact()
        {
            SwingContact contact = default;
            contact.Point = character.HitCentre;

            if (CanReachBall())
            {
                contact = SwingSolver.Evaluate(
                    ball.State.Position, ball.Velocity,
                    character.HitCentre, character.Velocity,
                    config.Contact(character.ReachRadius));
            }

            LastContact = contact;

            if (contact.Made)
            {
                LastTarget = SwingSolver.ResolveTarget(
                    character.Side, zone, contact, config, character.Court,
                    UnityEngine.Random.insideUnitCircle);

                float apex = SwingSolver.ResolveApex(contact, contact.Point, config);
                ball.LaunchAt(contact.Point, LastTarget, apex);
            }

            Contacted?.Invoke(contact);
        }

        /// <summary>
        /// A ball that has already crossed to the other half is not ours to hit,
        /// which is both the tennis rule and the thing that stops a player from
        /// volleying a ball back out of the opponent's court.
        /// </summary>
        bool CanReachBall()
        {
            if (ball == null || !ball.IsMoving) return false;
            return ball.State.Position.z * character.Side > 0f;
        }
    }
}
