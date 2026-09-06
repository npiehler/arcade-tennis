using UnityEngine;

namespace ArcadeTennis.BallPhysics
{
    /// <summary>
    /// Tuning for the ball simulation. Defaults are close to a real tennis ball
    /// on a hard court; drop the drag and raise the restitution to make rallies
    /// faster and flatter.
    /// </summary>
    [CreateAssetMenu(fileName = "BallPhysicsConfig", menuName = "Arcade Tennis/Ball Physics Config")]
    public class BallPhysicsConfig : ScriptableObject
    {
        [Header("Flight")]
        [SerializeField] float gravity = 9.81f;

        [Tooltip("Quadratic drag: acceleration = -drag * speed * velocity. " +
                 "0.020 matches a real tennis ball (0.5 * rho * Cd * A / m).")]
        [SerializeField] float dragFactor = 0.020f;

        [SerializeField] float radius = 0.033f;

        [Header("Bounce")]
        [Tooltip("Fraction of vertical speed kept after a bounce.")]
        [Range(0f, 1f)][SerializeField] float restitution = 0.75f;

        [Tooltip("Fraction of horizontal speed kept after a bounce.")]
        [Range(0f, 1f)][SerializeField] float surfaceFriction = 0.80f;

        [Tooltip("How much speed survives hitting the net. Low values make the ball drop dead.")]
        [Range(0f, 1f)][SerializeField] float netRestitution = 0.15f;

        [Header("Integration")]
        [Tooltip("Sub-steps are added until the ball moves no further than this per step, " +
                 "so fast balls cannot tunnel through the ground or the net.")]
        [SerializeField] float maxStepDistance = 0.05f;
        [SerializeField] int maxSubSteps = 24;

        [Tooltip("Below this speed a ball resting on the ground is considered stopped.")]
        [SerializeField] float restSpeed = 0.35f;

        public float Gravity => gravity;
        public float DragFactor => dragFactor;
        public float Radius => radius;
        public float Restitution => restitution;
        public float SurfaceFriction => surfaceFriction;
        public float NetRestitution => netRestitution;
        public float MaxStepDistance => maxStepDistance;
        public int MaxSubSteps => maxSubSteps;
        public float RestSpeed => restSpeed;
    }
}
