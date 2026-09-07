using System;
using UnityEngine;
using ArcadeTennis.BallPhysics;

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
        Vector2 aim;
        bool holding;

        /// <summary>Fired when the racket starts moving, before contact. The animator hangs off this in part 2.</summary>
        public event Action SwingStarted;

        /// <summary>Fired at the moment the racket passes the ball, hit or miss.</summary>
        public event Action<SwingContact> Contacted;

        public SwingConfig Config => config;
        public SwingPhase Phase => swing.Phase;
        public float Charge => swing.Charge;
        public float Power => swing.Power(config);
        public bool IsCharging => swing.Phase == SwingPhase.Charging;

        /// <summary>The last stroke's result, kept for the feedback layer.</summary>
        public SwingContact LastContact { get; private set; }

        /// <summary>Where the last successful stroke was aimed. Only meaningful when <see cref="LastContact"/> connected.</summary>
        public Vector3 LastTarget { get; private set; }

        void Awake() => character = GetComponent<TennisCharacter>();

        /// <summary>Raw button state. Holding charges; letting go swings.</summary>
        public void SetSwingHeld(bool held) => holding = held;

        /// <summary>Aim in the same screen space the move intent uses; x is what steers the shot sideways.</summary>
        public void SetAim(Vector2 value) => aim = value;

        void FixedUpdate()
        {
            if (config == null) return;

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
                    character.ReachRadius, config);
            }

            LastContact = contact;

            if (contact.Made)
            {
                float power = swing.Power(config);

                // Same deadzone as the legs, so an idle stick aims straight ahead
                // instead of quietly pulling the ball off centre.
                float deadzone = character.Config != null ? character.Config.InputDeadzone : 0f;
                Vector2 steered = SwingSolver.ApplyAimDeadzone(aim, deadzone);

                LastTarget = SwingSolver.ResolveTarget(
                    character.Side, power, steered, contact, config, character.Court,
                    UnityEngine.Random.insideUnitCircle);

                float apex = SwingSolver.ResolveApex(power, contact, contact.Point, config);
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
