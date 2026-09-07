using UnityEngine;
using ArcadeTennis.Court;

namespace ArcadeTennis.BallPhysics
{
    /// <summary>
    /// Puts a disc on the court where the ball will land, coloured by whether
    /// that spot is in or out. The prediction comes from the same simulation the
    /// ball runs, so the marker cannot disagree with the bounce.
    /// </summary>
    public class BallLandingMarker : MonoBehaviour
    {
        [SerializeField] Ball ball;
        [SerializeField] Transform marker;

        [Header("Colours")]
        [SerializeField] Color inColor = new Color(0.25f, 0.95f, 0.45f, 1f);
        [SerializeField] Color outColor = new Color(0.98f, 0.35f, 0.25f, 1f);
        [SerializeField] Color netColor = new Color(0.98f, 0.80f, 0.20f, 1f);

        [Header("Behaviour")]
        [Tooltip("Hide the marker once the ball has bounced; it is a warning, not a trail.")]
        [SerializeField] bool hideAfterFirstBounce = true;
        [SerializeField] float diameter = 0.9f;

        MaterialPropertyBlock properties;
        Renderer markerRenderer;
        bool suppressed;

        void Awake()
        {
            properties = new MaterialPropertyBlock();
            if (marker != null) markerRenderer = marker.GetComponent<Renderer>();
        }

        void OnEnable()
        {
            if (ball == null) return;
            ball.Launched += Refresh;
            ball.Bounced += OnBounced;
        }

        void OnDisable()
        {
            if (ball == null) return;
            ball.Launched -= Refresh;
            ball.Bounced -= OnBounced;
        }

        void OnBounced(Vector3 _)
        {
            if (hideAfterFirstBounce) Show(false);
            else Refresh();
        }

        /// <summary>
        /// Hides the marker without unwiring it. The serve sets this while the
        /// ball is going up off the toss, where a landing spot at the server's
        /// own feet is noise rather than information.
        /// </summary>
        public bool Suppressed
        {
            get => suppressed;
            set
            {
                if (suppressed == value) return;
                suppressed = value;
                if (suppressed) Show(false);
                else Refresh();
            }
        }

        public void Refresh()
        {
            if (ball == null || marker == null || suppressed) return;

            BallPrediction prediction = ball.PredictLanding();
            if (!prediction.HasResult)
            {
                Show(false);
                return;
            }

            Show(true);
            marker.position = new Vector3(prediction.Position.x, 0.012f, prediction.Position.z);
            marker.localScale = new Vector3(diameter, 0.004f, diameter);

            Color color;
            if (prediction.IsNetHit) color = netColor;
            else
            {
                CourtDefinition court = ball.CourtDefinition;
                bool inBounds = court != null && court.IsInBounds(prediction.Position);
                color = inBounds ? inColor : outColor;
            }

            SetColor(color);
        }

        void SetColor(Color color)
        {
            if (markerRenderer == null) return;
            markerRenderer.GetPropertyBlock(properties);
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_EmissionColor", color * 0.6f);
            markerRenderer.SetPropertyBlock(properties);
        }

        void Show(bool visible)
        {
            if (marker != null) marker.gameObject.SetActive(visible);
        }
    }
}
