using UnityEngine;

namespace ArcadeTennis.Characters
{
    /// <summary>
    /// Tuning for the rally stroke, shared by the player and -- from milestone 7
    /// on -- the AI, for the same reason <see cref="CharacterConfig"/> is shared:
    /// a difficulty setting may change when a controller swings, never how good
    /// the swing itself is allowed to be.
    ///
    /// There is no charge here any more. Holding the button raises the marker and
    /// lets the player pick one of three zones; letting go swings. How far into
    /// the chosen zone the ball actually gets is decided by the contact alone.
    ///
    /// Shot variants (topspin, slice, lob, drop) remain out of scope for part 1.
    /// They would arrive as additional assets of this type selected per stroke,
    /// which is why every number describing a shot's shape lives here.
    /// </summary>
    [CreateAssetMenu(fileName = "SwingConfig", menuName = "Arcade Tennis/Swing Config")]
    public class SwingConfig : ScriptableObject
    {
        [Header("Swing timing")]
        [Tooltip("Delay between the press and the racket meeting the ball. This is what " +
                 "makes the stroke a timing decision: the player has to swing before the " +
                 "ball arrives, not when it is already there. It is also the window in " +
                 "which the target zone can still be changed.")]
        [SerializeField] float contactDelay = 0.18f;

        [Tooltip("Largest timing error that still connects. Beyond this the racket misses.")]
        [SerializeField] float timingWindow = 0.17f;

        [Tooltip("Timing error that still counts as flawless.")]
        [SerializeField] float perfectWindow = 0.045f;

        [Tooltip("Total length of the swing. No new stroke can start until the swing and " +
                 "the recovery after it are through.")]
        [SerializeField] float swingDuration = 0.30f;

        [SerializeField] float recoverDuration = 0.16f;

        [Header("Sweet spot")]
        [Tooltip("Distance from the hit centre that still counts as flawless placement. " +
                 "The outer limit is the character's reach radius.")]
        [SerializeField] float sweetSpotRadius = 0.55f;

        [Range(0f, 1f)][SerializeField] float perfectThreshold = 0.82f;
        [Range(0f, 1f)][SerializeField] float goodThreshold = 0.45f;

        [Header("Depth")]
        [Tooltip("How far short of the chosen zone the worst contact that still connects " +
                 "falls. This is what makes the deep zone a decision: aiming short forgives " +
                 "bad contact because the zone is close anyway, aiming deep does not.")]
        [SerializeField] float depthShortfall = 1.50f;

        [Tooltip("How far outside the lines a shot may stray at all. Missing has to stay " +
                 "possible, but not by half a court.")]
        [SerializeField] float outMargin = 2.20f;

        [Header("Quality effects")]
        [Tooltip("Random target offset in metres at the worst contact that still connects.")]
        [SerializeField] float maxSpread = 3.0f;

        [Tooltip("Random target offset at flawless contact. Not zero: a game where the " +
                 "same input always lands on the same square centimetre feels mechanical.")]
        [SerializeField] float minSpread = 0.12f;

        [Tooltip("Share of the spread that is allowed to push the ball SIDEWAYS. Small on " +
                 "purpose. Depth is the player's own mistiming and reads as fair when it " +
                 "goes wrong, but direction is what they asked for -- scattering that reads " +
                 "as the game ignoring the input. Length pays for bad contact; aim is kept.")]
        [Range(0f, 1f)][SerializeField] float lateralSpreadFactor = 0.30f;

        [Header("Arc")]
        [Tooltip("Apex above the contact point for the worst contact: low, so a badly met " +
                 "ball is barely lifted and can fall into the net.")]
        [SerializeField] float apexWeak = 0.40f;

        [Tooltip("Apex above the contact point for flawless contact.")]
        [SerializeField] float apexFull = 1.45f;

        [Tooltip("Apex for a shot into one of the SHORT zones, worst contact. Far higher " +
                 "than the deep one and not a whim: to drop a ball a few metres past the " +
                 "net it has to be lofted and come down steeply, and a mishit short ball " +
                 "should sit up as an invitation rather than turn into a good drop shot.")]
        [SerializeField] float apexShortWeak = 1.90f;

        [Tooltip("Apex for a short shot at flawless contact: lower and crisper.")]
        [SerializeField] float apexShortFull = 1.00f;

        [Tooltip("Peak height a flawless stroke is guaranteed, so a ball met off the ground " +
                 "can still be got over the net. Scaled by contact quality: the guarantee is " +
                 "something the stroke earns, not something every stab gets for free.")]
        [SerializeField] float minPeakHeight = 1.75f;

        public float ContactDelay => contactDelay;
        public float TimingWindow => timingWindow;
        public float PerfectWindow => perfectWindow;
        public float SwingDuration => swingDuration;
        public float RecoverDuration => recoverDuration;

        public float SweetSpotRadius => sweetSpotRadius;
        public float PerfectThreshold => perfectThreshold;
        public float GoodThreshold => goodThreshold;

        public float DepthShortfall => depthShortfall;
        public float OutMargin => outMargin;

        public float MaxSpread => maxSpread;
        public float MinSpread => minSpread;
        public float LateralSpreadFactor => lateralSpreadFactor;

        public float ApexWeak => apexWeak;
        public float ApexFull => apexFull;
        public float ApexShortWeak => apexShortWeak;
        public float ApexShortFull => apexShortFull;
        public float MinPeakHeight => minPeakHeight;

        /// <summary>
        /// The contact numbers as the solver wants them. The reach comes from the
        /// body rather than from here, because how far a player can stretch is a
        /// property of the player, not of the stroke.
        /// </summary>
        public ContactTuning Contact(float reachRadius) =>
            new ContactTuning(sweetSpotRadius, reachRadius,
                perfectWindow, timingWindow, perfectThreshold, goodThreshold);
    }
}
