using UnityEngine;
using ArcadeTennis.BallPhysics;
using ArcadeTennis.Characters;
using ArcadeTennis.Court;

namespace ArcadeTennis.DebugTools
{
    /// <summary>
    /// Puts a ball in play so the stroke can be practised before there is a
    /// serve. It feeds from the far baseline to a rotating set of spots on the
    /// receiver's half, which forces the player to move to the ball instead of
    /// standing still and timing a metronome.
    ///
    /// Scaffolding, like the launcher it replaces: milestone 5 brings a real
    /// serve and this component goes with it.
    /// </summary>
    public class BallFeeder : MonoBehaviour
    {
        [SerializeField] Ball ball;
        [SerializeField] CourtDefinition court;

        [Tooltip("Who gets fed. The feed comes from the opposite baseline.")]
        [SerializeField] TennisCharacter receiver;

        [Header("Feed")]
        [SerializeField] float apexHeight = 2.6f;
        [SerializeField] float launchHeight = 1.15f;

        [Tooltip("Pause after a rally ends before the next ball comes.")]
        [SerializeField] float interval = 1.4f;

        [Tooltip("Feed again even if the ball is somehow still live, so a stuck " +
                 "ball never ends the practice session.")]
        [SerializeField] float maxRallyTime = 12f;

        [SerializeField] bool autoFeed = true;

        int feedIndex;
        float idleTime;
        float rallyTime;

        void Start()
        {
            if (autoFeed) Feed();
        }

        void Update()
        {
            if (!autoFeed || ball == null || receiver == null || court == null) return;

            rallyTime += Time.deltaTime;

            bool over = !ball.IsMoving || ball.BounceCount >= 2;
            idleTime = over ? idleTime + Time.deltaTime : 0f;

            if (idleTime >= interval || rallyTime >= maxRallyTime) Feed();
        }

        /// <summary>Sends the next feed and returns the spot it aimed at.</summary>
        public Vector3 Feed()
        {
            if (ball == null || receiver == null || court == null) return Vector3.zero;

            int side = receiver.Side;
            Vector3 origin = court.GetBaselineCentre(-side);
            origin.y = launchHeight;

            Vector3 target = GetTarget(feedIndex, side);
            feedIndex++;

            ball.LaunchAt(origin, target, apexHeight);

            idleTime = 0f;
            rallyTime = 0f;
            return target;
        }

        /// <summary>Centre, then each corner in turn: every feed asks for a different stroke.</summary>
        public Vector3 GetTarget(int index, int side)
        {
            // Chosen by simulating the feed: this depth and apex bring the ball
            // back down through hitting height right about on the receiver's
            // baseline, so a centre feed can be met from the home spot and only
            // the corner feeds ask the player to run.
            float depth = court.HalfLength * 0.50f;
            float wide = court.SinglesHalfWidth * 0.66f;

            float x;
            switch (index % 4)
            {
                case 0: x = 0f; break;
                case 1: x = -wide; break;
                case 2: x = 0f; break;
                default: x = wide; break;
            }

            return new Vector3(x, 0f, side * depth);
        }
    }
}
