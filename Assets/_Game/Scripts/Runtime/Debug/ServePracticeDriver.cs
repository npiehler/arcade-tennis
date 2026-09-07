using System;
using UnityEngine;
using ArcadeTennis.BallPhysics;
using ArcadeTennis.Characters;

namespace ArcadeTennis.DebugTools
{
    /// <summary>
    /// Keeps points starting so the serve can be practised before there are any
    /// rules. It decides only *when* the next serve happens, never who won
    /// anything -- the score arrives with the rule engine in milestone 6, and
    /// this component goes when it does.
    ///
    /// Successor to the ball feeder that stood in for the serve during milestone 4.
    /// </summary>
    public class ServePracticeDriver : MonoBehaviour
    {
        [SerializeField] ServeController serve;
        [SerializeField] Ball ball;

        [Tooltip("Pause after a point ends before the next serve is set up.")]
        [SerializeField] float pauseAfterPoint = 1.2f;

        [Tooltip("Pause after a fault before the same point is served again.")]
        [SerializeField] float pauseAfterFault = 0.9f;

        [Tooltip("A rally that never dies would leave the driver waiting forever.")]
        [SerializeField] float maxRallyTime = 20f;

        [SerializeField] bool logOutcomes = true;

        float wait;
        float rallyTime;
        bool rallyLive;
        bool nextIsNewPoint;

        public int ServesPlayed { get; private set; }
        public int Faults { get; private set; }
        public int DoubleFaults { get; private set; }
        public int ServesIn { get; private set; }
        public ServeOutcome LastOutcome { get; private set; }

        /// <summary>Fired after every judged serve, for anything that wants to show it.</summary>
        public event Action<ServeOutcome, bool> ServeJudged;

        void OnEnable()
        {
            if (serve != null) serve.Resolved += OnResolved;
        }

        void OnDisable()
        {
            if (serve != null) serve.Resolved -= OnResolved;
        }

        void Start()
        {
            if (serve != null) serve.NextPoint();
        }

        void OnResolved(ServeOutcome outcome, bool doubleFault)
        {
            ServesPlayed++;
            LastOutcome = outcome;

            if (outcome == ServeOutcome.In)
            {
                ServesIn++;
                rallyLive = true;
                rallyTime = 0f;
            }
            else
            {
                Faults++;
                if (doubleFault) DoubleFaults++;

                rallyLive = false;
                nextIsNewPoint = doubleFault;
                wait = pauseAfterFault;
            }

            if (logOutcomes)
                Debug.Log("Aufschlag " + outcome + (doubleFault ? " (Doppelfehler)" : "")
                    + "  -  drin " + ServesIn + "/" + ServesPlayed
                    + ", Fehler " + Faults + ", Doppelfehler " + DoubleFaults, this);

            ServeJudged?.Invoke(outcome, doubleFault);
        }

        void Update()
        {
            if (serve == null || ball == null) return;

            if (rallyLive)
            {
                rallyTime += Time.deltaTime;

                // Same crude end-of-point test the feeder used: it only has to be
                // good enough to start the next serve, not to award the point.
                bool over = !ball.IsMoving || ball.BounceCount >= 2 || rallyTime >= maxRallyTime;
                if (!over) return;

                rallyLive = false;
                nextIsNewPoint = true;
                wait = pauseAfterPoint;
                return;
            }

            if (wait <= 0f) return;

            wait -= Time.deltaTime;
            if (wait > 0f) return;

            if (nextIsNewPoint) serve.NextPoint();
            else serve.PrepareForServe();
        }
    }
}
