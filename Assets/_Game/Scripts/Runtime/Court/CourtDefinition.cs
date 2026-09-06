using UnityEngine;

namespace ArcadeTennis.Court
{
    /// <summary>
    /// Single source of truth for court geometry. Every value is in metres and
    /// matches ITF regulation dimensions, so models authored in Blender at real
    /// scale import 1:1.
    ///
    /// Coordinate convention used across the whole game:
    ///   - the net lies along the X axis at z = 0
    ///   - the court runs along Z, the baselines sit at z = +/- HalfLength
    ///   - "side" is +1 for the half with z > 0 and -1 for the half with z &lt; 0
    ///   - a player on side S faces along -S on the Z axis
    /// </summary>
    [CreateAssetMenu(fileName = "CourtDefinition", menuName = "Arcade Tennis/Court Definition")]
    public class CourtDefinition : ScriptableObject
    {
        [Header("Playing area (metres)")]
        [SerializeField] float halfLength = 11.885f;        // baseline to net
        [SerializeField] float singlesHalfWidth = 4.115f;   // singles sideline
        [SerializeField] float doublesHalfWidth = 5.485f;   // doubles sideline
        [SerializeField] float serviceLineDistance = 6.40f; // net to service line

        [Header("Net")]
        [SerializeField] float netHeightCenter = 0.914f;
        [SerializeField] float netHeightPost = 1.07f;
        [SerializeField] float netPostOffset = 0.914f;      // outside the doubles sideline
        [SerializeField] float netThickness = 0.04f;

        [Header("Markings and surroundings")]
        [SerializeField] float lineWidth = 0.05f;
        [SerializeField] float centreMarkLength = 0.10f;
        [SerializeField] float runOffBack = 6.40f;          // apron behind each baseline
        [SerializeField] float runOffSide = 3.66f;          // apron beside each sideline

        public float HalfLength => halfLength;
        public float Length => halfLength * 2f;
        public float SinglesHalfWidth => singlesHalfWidth;
        public float DoublesHalfWidth => doublesHalfWidth;
        public float ServiceLineDistance => serviceLineDistance;

        public float NetHeightCentre => netHeightCenter;
        public float NetHeightPost => netHeightPost;
        public float NetPostOffset => netPostOffset;
        public float NetHalfWidth => doublesHalfWidth + netPostOffset;
        public float NetThickness => netThickness;

        public float LineWidth => lineWidth;
        public float CentreMarkLength => centreMarkLength;
        public float RunOffBack => runOffBack;
        public float RunOffSide => runOffSide;

        /// <summary>Full ground footprint including the apron, as (width, length).</summary>
        public Vector2 GroundSize => new Vector2(
            (doublesHalfWidth + runOffSide) * 2f,
            (halfLength + runOffBack) * 2f);

        /// <summary>Half width of the live court for the given format.</summary>
        public float HalfWidth(bool doubles) => doubles ? doublesHalfWidth : singlesHalfWidth;

        /// <summary>
        /// True when a bounce at <paramref name="point"/> is in. Lines count as in,
        /// so the test is inclusive of half a line width on every edge.
        /// </summary>
        public bool IsInBounds(Vector3 point, bool doubles = false)
        {
            float halfW = HalfWidth(doubles) + lineWidth * 0.5f;
            float halfL = halfLength + lineWidth * 0.5f;
            return Mathf.Abs(point.x) <= halfW && Mathf.Abs(point.z) <= halfL;
        }

        /// <summary>
        /// The service box a serve must land in, as an XZ rectangle
        /// (rect.x/rect.y are min X and min Z).
        ///
        /// <paramref name="receiverSide"/> is the side the ball travels to.
        /// <paramref name="deuceCourt"/> selects the receiver's right-hand box,
        /// which is the diagonal partner of the server's own right-hand box.
        /// </summary>
        public Rect GetServiceBox(int receiverSide, bool deuceCourt)
        {
            int side = receiverSide >= 0 ? 1 : -1;

            // A player on side S faces -S, so their right hand points to -S on X.
            // The deuce box is on the receiver's right, the ad box on their left.
            float signX = deuceCourt ? -side : side;

            float minX = signX > 0 ? 0f : -singlesHalfWidth;
            float minZ = side > 0 ? 0f : -serviceLineDistance;

            return new Rect(minX, minZ, singlesHalfWidth, serviceLineDistance);
        }

        /// <summary>True when a serve bounce is inside the required service box.</summary>
        public bool IsInServiceBox(Vector3 point, int receiverSide, bool deuceCourt)
        {
            Rect box = GetServiceBox(receiverSide, deuceCourt);
            float pad = lineWidth * 0.5f;
            return point.x >= box.xMin - pad && point.x <= box.xMax + pad
                && point.z >= box.yMin - pad && point.z <= box.yMax + pad;
        }

        /// <summary>
        /// Net height at a given X. The real net sags from the posts towards the
        /// centre strap; a quadratic is close enough and costs nothing.
        /// </summary>
        public float NetHeightAt(float x)
        {
            float t = Mathf.Clamp01(Mathf.Abs(x) / NetHalfWidth);
            return Mathf.Lerp(netHeightCenter, netHeightPost, t * t);
        }

        /// <summary>Where a player stands to serve: behind the baseline, beside the centre mark.</summary>
        public Vector3 GetServePosition(int serverSide, bool deuceCourt)
        {
            int side = serverSide >= 0 ? 1 : -1;
            float signX = deuceCourt ? -side : side;
            return new Vector3(signX * singlesHalfWidth * 0.45f, 0f, side * (halfLength + 0.6f));
        }

        /// <summary>Neutral recovery spot for a player on the given side.</summary>
        public Vector3 GetBaselineCentre(int side) =>
            new Vector3(0f, 0f, (side >= 0 ? 1 : -1) * (halfLength + 0.4f));

        /// <summary>
        /// Keeps a character on their own side of the net, inside the court and
        /// its apron. A player may crowd the net but never stand in or past it,
        /// so <paramref name="netClearance"/> holds them just short of the cord.
        /// </summary>
        public Vector3 ClampToOwnHalf(Vector3 position, int side, float netClearance = 0.35f)
        {
            Vector3 clamped = ClampToPlayArea(position);

            if (side >= 0) clamped.z = Mathf.Max(clamped.z, netClearance);
            else clamped.z = Mathf.Min(clamped.z, -netClearance);

            return clamped;
        }

        /// <summary>Keeps a position inside the court plus its apron, ignoring the net.</summary>
        public Vector3 ClampToPlayArea(Vector3 position)
        {
            float maxX = doublesHalfWidth + runOffSide;
            float maxZ = halfLength + runOffBack;
            position.x = Mathf.Clamp(position.x, -maxX, maxX);
            position.z = Mathf.Clamp(position.z, -maxZ, maxZ);
            return position;
        }
    }
}
