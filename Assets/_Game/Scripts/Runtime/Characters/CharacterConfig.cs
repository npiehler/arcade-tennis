using UnityEngine;

namespace ArcadeTennis.Characters
{
    /// <summary>
    /// Movement and reach tuning shared by the human player and the AI, so both
    /// are bound by the same limits and a difficulty setting can only change the
    /// decisions a controller makes, never the body it drives.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterConfig", menuName = "Arcade Tennis/Character Config")]
    public class CharacterConfig : ScriptableObject
    {
        [Header("Movement")]
        [Tooltip("Top running speed. Real players peak near 6 m/s; a little more reads better in an arcade game.")]
        [SerializeField] float maxSpeed = 8f;

        [Tooltip("How hard the character gets up to speed.")]
        [SerializeField] float acceleration = 38f;

        [Tooltip("How hard the character slows when the stick is released. Higher than " +
                 "acceleration so stopping feels crisper than starting.")]
        [SerializeField] float deceleration = 52f;

        [Tooltip("Input below this magnitude counts as no input at all.")]
        [Range(0f, 0.5f)][SerializeField] float inputDeadzone = 0.15f;

        [Header("Body")]
        [Tooltip("Distance from the character's centre the racket can still meet the ball. " +
                 "Drives the reach ring and, from milestone 4 on, the hit test.")]
        [SerializeField] float reachRadius = 1.35f;

        [SerializeField] float height = 1.8f;

        [Tooltip("Height of the racket's sweet spot above the ground.")]
        [SerializeField] float hitHeight = 1.0f;

        [Header("Facing")]
        [Tooltip("Degrees per second the body turns.")]
        [SerializeField] float turnSpeed = 720f;

        [Tooltip("How far the body turns towards the running direction instead of " +
                 "squarely facing the net. 0 always faces the net, 1 faces the run.")]
        [Range(0f, 1f)][SerializeField] float runFacingBlend = 0.55f;

        public float MaxSpeed => maxSpeed;
        public float Acceleration => acceleration;
        public float Deceleration => deceleration;
        public float InputDeadzone => inputDeadzone;
        public float ReachRadius => reachRadius;
        public float Height => height;
        public float HitHeight => hitHeight;
        public float TurnSpeed => turnSpeed;
        public float RunFacingBlend => runFacingBlend;
    }
}
