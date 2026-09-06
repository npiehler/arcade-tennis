using UnityEngine;
using ArcadeTennis.Court;

namespace ArcadeTennis.Characters
{
    /// <summary>
    /// The body both the player and the AI drive. It knows nothing about who is
    /// steering it: a controller pushes a move intent in, this turns that into
    /// motion inside the court's limits.
    ///
    /// Only the logic lives on this object. Everything visible hangs off the
    /// <see cref="visual"/> child, so replacing the placeholder capsule with a
    /// rigged model later is a matter of swapping that child out.
    /// </summary>
    public class TennisCharacter : MonoBehaviour
    {
        [SerializeField] CharacterConfig config;
        [SerializeField] CourtDefinition court;

        [Tooltip("Root of everything visible. Rotated for facing; never carries logic.")]
        [SerializeField] Transform visual;

        [Tooltip("Which half this character plays on: +1 for z > 0, -1 for z < 0.")]
        [SerializeField] int side = -1;

        Vector2 moveIntent;
        CharacterMotionState motion;

        public CharacterConfig Config => config;
        public CourtDefinition Court => court;
        public int Side => side < 0 ? -1 : 1;
        public Vector3 Velocity => motion.Velocity;
        public float Speed => motion.Velocity.magnitude;

        /// <summary>Where the racket meets the ball. Milestone 4 tests against this.</summary>
        public Vector3 HitCentre =>
            transform.position + Vector3.up * (config != null ? config.HitHeight : 1f);

        public float ReachRadius => config != null ? config.ReachRadius : 1.35f;

        /// <summary>The direction this character plays towards: across the net.</summary>
        public Vector3 Forward => new Vector3(0f, 0f, -Side);

        void Reset() => side = -1;

        void Awake() => motion = new CharacterMotionState(transform.position);

        /// <summary>
        /// Move intent in screen space: x is right, y is towards the net. The
        /// mapping to world axes accounts for which end the character plays,
        /// so both sides steer the same way from their own camera.
        /// </summary>
        public void SetMoveIntent(Vector2 intent)
        {
            moveIntent = Vector2.ClampMagnitude(intent, 1f);
        }

        /// <summary>Drops the character at a spot and kills its momentum.</summary>
        public void Teleport(Vector3 position)
        {
            Vector3 clamped = court != null ? court.ClampToOwnHalf(position, Side) : position;
            motion = new CharacterMotionState(clamped);
            transform.position = clamped;
        }

        void FixedUpdate()
        {
            if (config == null) return;

            float dt = Time.fixedDeltaTime;

            CharacterMotion.Step(ref motion, moveIntent, Side, config, court, dt);
            transform.position = motion.Position;

            UpdateFacing(dt);
        }

        void UpdateFacing(float dt)
        {
            if (visual == null) return;

            Vector3 netFacing = Forward;
            Vector3 target = netFacing;

            if (motion.Velocity.sqrMagnitude > 0.25f)
            {
                Vector3 runFacing = motion.Velocity.normalized;
                target = Vector3.Slerp(netFacing, runFacing, config.RunFacingBlend).normalized;
            }

            Quaternion wanted = Quaternion.LookRotation(target, Vector3.up);
            visual.rotation = Quaternion.RotateTowards(visual.rotation, wanted, config.TurnSpeed * dt);
        }

        void OnDrawGizmosSelected()
        {
            if (config == null) return;

            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.9f);
            Vector3 centre = transform.position;
            const int segments = 48;

            Vector3 previous = centre + new Vector3(config.ReachRadius, 0.02f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                Vector3 point = centre + new Vector3(
                    Mathf.Cos(angle) * config.ReachRadius, 0.02f, Mathf.Sin(angle) * config.ReachRadius);
                Gizmos.DrawLine(previous, point);
                previous = point;
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(centre, centre + Forward * 1.5f);
        }
    }
}
