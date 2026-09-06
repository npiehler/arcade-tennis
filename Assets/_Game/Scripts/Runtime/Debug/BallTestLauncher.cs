using UnityEngine;
using ArcadeTennis.BallPhysics;
using ArcadeTennis.Court;

namespace ArcadeTennis.DebugTools
{
    /// <summary>
    /// Scaffolding for milestone 2: fires the ball at a rotating set of targets
    /// so flight, bounce and marker can be watched without a player yet. Deleted
    /// once real strokes drive the ball.
    /// </summary>
    public class BallTestLauncher : MonoBehaviour
    {
        [SerializeField] Ball ball;
        [SerializeField] CourtDefinition court;

        [Header("Launch")]
        [SerializeField] Vector3 origin = new Vector3(0f, 1.1f, -12.2f);
        [SerializeField] float apexHeight = 2.2f;
        [SerializeField] float interval = 2.5f;
        [SerializeField] bool autoFire = true;

        int shotIndex;
        float timer;

        void Start()
        {
            if (autoFire) Fire();
        }

        void Update()
        {
            if (!autoFire || ball == null) return;

            timer += Time.deltaTime;
            if (timer >= interval)
            {
                timer = 0f;
                Fire();
            }
        }

        /// <summary>Fires the next shot in the cycle and returns the target it aimed at.</summary>
        public Vector3 Fire()
        {
            Vector3 target = GetTarget(shotIndex);
            shotIndex++;

            if (ball != null) ball.LaunchAt(origin, target, apexHeight);
            return target;
        }

        /// <summary>
        /// A deliberate spread: deep in, short cross-court, wide out and one
        /// into the net, so every marker colour shows up in a single cycle.
        /// </summary>
        public Vector3 GetTarget(int index)
        {
            if (court == null) return new Vector3(0f, 0f, 8f);

            switch (index % 4)
            {
                case 0: return new Vector3(0f, 0f, court.HalfLength - 0.6f);
                case 1: return new Vector3(-court.SinglesHalfWidth + 0.8f, 0f, 4.5f);
                case 2: return new Vector3(court.SinglesHalfWidth + 1.6f, 0f, 7f);
                default: return new Vector3(0f, 0f, 0.9f);
            }
        }
    }
}
