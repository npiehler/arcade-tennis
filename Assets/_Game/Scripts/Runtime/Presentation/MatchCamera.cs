using UnityEngine;
using ArcadeTennis.Court;

namespace ArcadeTennis.Presentation
{
    /// <summary>
    /// The behind-the-baseline camera. It tracks the player sideways by only a
    /// fraction of their movement: following one-to-one would swing the whole
    /// court around and make the ball hard to read, while a fixed camera pushes
    /// a wide player to the edge of the frame.
    ///
    /// Milestone 8 adds the side change and the serve framing; this is the
    /// steady rally shot everything else is judged against.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class MatchCamera : MonoBehaviour
    {
        [SerializeField] CourtDefinition court;
        [SerializeField] Transform target;

        [Tooltip("Which baseline the camera sits behind: +1 for z > 0, -1 for z < 0.")]
        [SerializeField] int side = -1;

        [Header("Placement")]
        [SerializeField] float height = 10f;
        [SerializeField] float distanceBehindBaseline = 16.1f;
        [SerializeField] float aimHeight = 0.6f;

        [Tooltip("How far short of the net, on the camera's own side, the aim point sits. " +
                 "Pulling it towards the camera tilts the view down and fills the frame " +
                 "with court; pushing it past the net flattens the shot.")]
        [SerializeField] float aimDepth = 3f;

        [Header("Following")]
        [Tooltip("Share of the player's sideways position the camera copies. " +
                 "1 would glue the camera to the player and swing the court.")]
        [Range(0f, 1f)][SerializeField] float lateralFollow = 0.35f;

        [Tooltip("Seconds the camera takes to catch up. Small values feel snappy, large ones floaty.")]
        [SerializeField] float smoothTime = 0.32f;

        [Tooltip("Cap on lateral travel so the camera never leaves the stadium.")]
        [SerializeField] float maxLateralOffset = 4f;

        Vector3 positionVelocity;

        int Side => side < 0 ? -1 : 1;

        float BaselineDistance => court != null ? court.HalfLength : 11.885f;

        void Awake() => ApplyImmediate();

        void LateUpdate()
        {
            transform.position = Vector3.SmoothDamp(
                transform.position, DesiredPosition(), ref positionVelocity, smoothTime);

            // Rotation stays put. Letting the camera re-aim while it dollies
            // sideways changes its distance to the aim point, which tilts the
            // pitch and makes the whole court drift up and down the frame.
            transform.rotation = BaseRotation();
        }

        /// <summary>Snaps to the framing without easing. Used on spawn and after a side change.</summary>
        public void ApplyImmediate()
        {
            transform.position = DesiredPosition();
            transform.rotation = BaseRotation();
            positionVelocity = Vector3.zero;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        float LateralOffset(float follow)
        {
            if (target == null) return 0f;
            return Mathf.Clamp(target.position.x * follow, -maxLateralOffset, maxLateralOffset);
        }

        Vector3 DesiredPosition() => new Vector3(
            LateralOffset(lateralFollow),
            height,
            Side * (BaselineDistance + distanceBehindBaseline));

        /// <summary>The framing, measured from the centred position so it never changes.</summary>
        Quaternion BaseRotation()
        {
            var basePosition = new Vector3(0f, height, Side * (BaselineDistance + distanceBehindBaseline));
            var baseAim = new Vector3(0f, aimHeight, Side * aimDepth);
            return Quaternion.LookRotation((baseAim - basePosition).normalized, Vector3.up);
        }
    }
}
