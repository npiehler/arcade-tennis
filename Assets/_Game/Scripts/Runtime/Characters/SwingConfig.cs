using UnityEngine;

namespace ArcadeTennis.Characters
{
    /// <summary>
    /// Tuning for the stroke, shared by the player and -- from milestone 7 on --
    /// the AI, for the same reason <see cref="CharacterConfig"/> is shared: a
    /// difficulty setting may change when a controller swings, never how good
    /// the swing itself is allowed to be.
    ///
    /// Shot variants (topspin, slice, lob, drop) are deliberately out of scope
    /// for part 1. They would arrive as additional assets of this type selected
    /// per stroke, which is why every number that describes a shot's shape lives
    /// here rather than in the solver.
    /// </summary>
    [CreateAssetMenu(fileName = "SwingConfig", menuName = "Arcade Tennis/Swing Config")]
    public class SwingConfig : ScriptableObject
    {
        [Header("Charge")]
        [Tooltip("Seconds of holding the button to reach full charge.")]
        [SerializeField] float chargeTime = 0.60f;

        [Tooltip("Charge a bare tap is worth. Without it an instant stab would " +
                 "produce a shot with no power at all, which reads as a bug.")]
        [Range(0f, 1f)][SerializeField] float minCharge = 0.18f;

        [Header("Swing timing")]
        [Tooltip("Delay between releasing the button and the racket meeting the ball. " +
                 "This is what makes the stroke a timing decision: the player has to " +
                 "swing before the ball arrives, not when it is already there.")]
        [SerializeField] float contactDelay = 0.09f;

        [Tooltip("Largest timing error that still connects. Beyond this the racket misses.")]
        [SerializeField] float timingWindow = 0.17f;

        [Tooltip("Timing error that still counts as flawless.")]
        [SerializeField] float perfectWindow = 0.045f;

        [Tooltip("Total length of the swing. The character cannot start another " +
                 "stroke until the swing and the recovery after it are through.")]
        [SerializeField] float swingDuration = 0.30f;

        [SerializeField] float recoverDuration = 0.16f;

        [Header("Sweet spot")]
        [Tooltip("Distance from the hit centre that still counts as flawless placement. " +
                 "The outer limit is the character's reach radius.")]
        [SerializeField] float sweetSpotRadius = 0.55f;

        [Tooltip("Quality at or above this grades Perfect.")]
        [Range(0f, 1f)][SerializeField] float perfectThreshold = 0.82f;

        [Tooltip("Quality at or above this grades Good; below it the stroke is graded " +
                 "Early or Late by the sign of the timing error.")]
        [Range(0f, 1f)][SerializeField] float goodThreshold = 0.45f;

        [Header("Placement")]
        [Tooltip("Distance past the net a zero-charge shot aims at.")]
        [SerializeField] float minTargetDepth = 4.5f;

        [Tooltip("How far short of the opponent's baseline a full-charge shot aims. " +
                 "A clean shot therefore lands in; going out takes bad contact or a " +
                 "wide aim, not simply pressing hard.")]
        [SerializeField] float baselineMargin = 0.70f;

        [Tooltip("Fraction of the singles half width a full sideways aim reaches for.")]
        [Range(0f, 1.5f)][SerializeField] float aimWidth = 0.92f;

        [Tooltip("How far past the sidelines and baseline a shot may be aimed. Missing " +
                 "has to stay possible, but not into the stands.")]
        [SerializeField] float outMargin = 2.2f;

        [Header("Quality effects")]
        [Tooltip("Random target offset in metres at the worst contact that still connects.")]
        [SerializeField] float maxSpread = 3.0f;

        [Tooltip("Random target offset at flawless contact. Not zero: a game where the " +
                 "same input always lands on the same square centimetre feels mechanical.")]
        [SerializeField] float minSpread = 0.12f;

        [Tooltip("Share of the charge a worst-case contact still delivers. This is what " +
                 "makes a mistimed shot land short instead of merely landing elsewhere.")]
        [Range(0f, 1f)][SerializeField] float minQualityPower = 0.40f;

        [Header("Arc")]
        [Tooltip("Apex above the contact point for a soft shot.")]
        [SerializeField] float apexSoft = 2.30f;

        [Tooltip("Apex above the contact point for a full-power shot: flatter, so power " +
                 "reads as a faster ball rather than a higher one.")]
        [SerializeField] float apexHard = 1.45f;

        [Tooltip("Height the ball must peak at regardless of charge. A ball met near the " +
                 "ground would otherwise be given an arc that cannot clear the net.")]
        [SerializeField] float minPeakHeight = 1.75f;

        public float ChargeTime => chargeTime;
        public float MinCharge => minCharge;

        public float ContactDelay => contactDelay;
        public float TimingWindow => timingWindow;
        public float PerfectWindow => perfectWindow;
        public float SwingDuration => swingDuration;
        public float RecoverDuration => recoverDuration;

        public float SweetSpotRadius => sweetSpotRadius;
        public float PerfectThreshold => perfectThreshold;
        public float GoodThreshold => goodThreshold;

        public float MinTargetDepth => minTargetDepth;
        public float BaselineMargin => baselineMargin;
        public float AimWidth => aimWidth;
        public float OutMargin => outMargin;

        public float MaxSpread => maxSpread;
        public float MinSpread => minSpread;
        public float MinQualityPower => minQualityPower;

        public float ApexSoft => apexSoft;
        public float ApexHard => apexHard;
        public float MinPeakHeight => minPeakHeight;
    }
}
