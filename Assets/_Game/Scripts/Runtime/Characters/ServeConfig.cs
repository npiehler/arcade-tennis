using UnityEngine;

namespace ArcadeTennis.Characters
{
    /// <summary>
    /// Tuning for the serve. Deliberately self-contained rather than borrowing
    /// from <see cref="SwingConfig"/>: the serve is met above the head off a ball
    /// that is barely moving at the top of the toss, so its sweet spot, its
    /// windows and its arc all want different numbers. Only the judgement itself
    /// is shared, through <see cref="SwingSolver.Evaluate"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "ServeConfig", menuName = "Arcade Tennis/Serve Config")]
    public class ServeConfig : ScriptableObject
    {
        [Header("Toss")]
        [Tooltip("Height the ball leaves the hand.")]
        [SerializeField] float tossHeight = 1.70f;

        [Tooltip("Upward speed of the toss. Together with the hit height this sets the " +
                 "rhythm of the serve: how long the player waits before letting go.")]
        [SerializeField] float tossSpeed = 5.2f;

        [Tooltip("How far in front of the server the ball is tossed. The hit point sits " +
                 "directly above it, so the ball passes through the racket on the way up " +
                 "and again on the way down.")]
        [SerializeField] float tossReach = 0.45f;

        [Tooltip("Below this the toss is dead and may be taken again. Catching a bad toss " +
                 "costs nothing in tennis and should cost nothing here; only swinging and " +
                 "missing is a fault.")]
        [SerializeField] float catchHeight = 1.15f;

        [Header("Contact")]
        [Tooltip("Height of the racket at full stretch. Not a free choice: from much lower " +
                 "no trajectory both clears the net and drops inside the service line.")]
        [SerializeField] float hitHeight = 2.55f;

        [Tooltip("How far from that point the racket still finds the ball.")]
        [SerializeField] float reachRadius = 0.85f;

        [Tooltip("Distance from the hit point that still counts as flawless. Much tighter " +
                 "than a groundstroke's, because the ball hangs near the top of the toss " +
                 "and a generous sweet spot would make the serve a formality.")]
        [SerializeField] float sweetSpotRadius = 0.30f;

        [Header("Timing")]
        [SerializeField] float contactDelay = 0.16f;
        [SerializeField] float timingWindow = 0.16f;
        [SerializeField] float perfectWindow = 0.05f;
        [SerializeField] float swingDuration = 0.30f;
        [SerializeField] float recoverDuration = 0.20f;

        [Range(0f, 1f)][SerializeField] float perfectThreshold = 0.82f;
        [Range(0f, 1f)][SerializeField] float goodThreshold = 0.45f;

        [Header("Depth")]
        [Tooltip("How far short of the chosen zone a badly met toss falls. Large enough " +
                 "that the worst contact drops into the net: meeting the ball well is the " +
                 "whole skill of the serve now that there is no charge.")]
        [SerializeField] float depthShortfall = 1.10f;

        [Tooltip("How far outside the box a serve may stray at all.")]
        [SerializeField] float outMargin = 1.60f;

        [Header("Quality effects")]
        [Tooltip("Smaller than a groundstroke's: the service box is a small target and a " +
                 "groundstroke's scatter would turn every mishit into a fault.")]
        [SerializeField] float maxSpread = 1.50f;
        [SerializeField] float minSpread = 0.10f;

        [Tooltip("Share of the spread that may push the serve sideways. Same reasoning as " +
                 "the groundstroke's, and it matters more here: the service box is narrow " +
                 "and a sideways slip is the difference between a serve and a fault.")]
        [Range(0f, 1f)][SerializeField] float lateralSpreadFactor = 0.35f;

        [Header("Arc")]
        [Tooltip("Apex above the contact point for a soft serve.")]
        [SerializeField] float apexWeak = 0.15f;

        [Tooltip("Apex above the contact point at full charge: flatter and faster.")]
        [SerializeField] float apexFull = 0.08f;

        public float TossHeight => tossHeight;
        public float TossSpeed => tossSpeed;
        public float TossReach => tossReach;
        public float CatchHeight => catchHeight;

        public float HitHeight => hitHeight;
        public float ReachRadius => reachRadius;
        public float SweetSpotRadius => sweetSpotRadius;

        public float ContactDelay => contactDelay;
        public float TimingWindow => timingWindow;
        public float PerfectWindow => perfectWindow;
        public float SwingDuration => swingDuration;
        public float RecoverDuration => recoverDuration;
        public float PerfectThreshold => perfectThreshold;
        public float GoodThreshold => goodThreshold;

        public float DepthShortfall => depthShortfall;
        public float OutMargin => outMargin;

        public float MaxSpread => maxSpread;
        public float MinSpread => minSpread;
        public float LateralSpreadFactor => lateralSpreadFactor;

        public float ApexWeak => apexWeak;
        public float ApexFull => apexFull;

        /// <summary>The contact numbers as <see cref="SwingSolver.Evaluate"/> wants them.</summary>
        public ContactTuning Contact() =>
            new ContactTuning(sweetSpotRadius, reachRadius,
                perfectWindow, timingWindow, perfectThreshold, goodThreshold);
    }
}
