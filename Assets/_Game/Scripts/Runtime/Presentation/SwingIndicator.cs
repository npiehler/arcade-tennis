using UnityEngine;
using ArcadeTennis.Characters;

namespace ArcadeTennis.Presentation
{
    /// <summary>
    /// Reports what the racket made of the ball: a short bar over the character
    /// that flashes the colour of the contact grade, and a different colour again
    /// when a serve turns out to be a fault.
    ///
    /// It used to be a charge meter. With the charge gone there is nothing to
    /// fill, but the feedback it carried is worth keeping -- contact quality is
    /// now the only thing that separates a deep ball from a short one, so the
    /// player has to be told about it.
    ///
    /// It sits in the world rather than in a HUD because the player is watching
    /// the ball, not the screen corner; a real scoreboard arrives in milestone 8.
    /// </summary>
    public class SwingIndicator : MonoBehaviour
    {
        [SerializeField] SwingController swing;

        [Tooltip("Optional. Serve contacts and faults flash here too.")]
        [SerializeField] ServeController serve;

        [SerializeField] Transform character;
        [SerializeField] Transform fill;
        [SerializeField] Transform background;

        [Header("Placement")]
        [SerializeField] float heightAboveGround = 2.25f;
        [SerializeField] float width = 1.10f;
        [SerializeField] float thickness = 0.13f;

        [Header("Grade colours")]
        [SerializeField] Color perfectColor = new Color(0.45f, 0.98f, 1f, 1f);
        [SerializeField] Color goodColor = new Color(0.35f, 0.92f, 0.45f, 1f);
        [SerializeField] Color earlyColor = new Color(1f, 0.76f, 0.30f, 1f);
        [SerializeField] Color lateColor = new Color(1f, 0.52f, 0.22f, 1f);
        [SerializeField] Color missColor = new Color(1f, 0.28f, 0.28f, 1f);

        [Tooltip("A serve can be struck flawlessly and still be a fault, so the fault " +
                 "colour has to override the contact grade rather than sit beside it.")]
        [SerializeField] Color faultColor = new Color(1f, 0.35f, 0.45f, 1f);

        [Header("Flash")]
        [SerializeField] float flashDuration = 0.55f;

        MaterialPropertyBlock properties;
        Renderer fillRenderer;
        Camera view;

        float flashRemaining;
        Color flashColor;

        void Awake()
        {
            properties = new MaterialPropertyBlock();
            if (fill != null) fillRenderer = fill.GetComponent<Renderer>();
        }

        void OnEnable()
        {
            if (swing != null) swing.Contacted += OnContacted;
            if (serve != null)
            {
                serve.Contacted += OnContacted;
                serve.Resolved += OnServeResolved;
            }
        }

        void OnDisable()
        {
            if (swing != null) swing.Contacted -= OnContacted;
            if (serve != null)
            {
                serve.Contacted -= OnContacted;
                serve.Resolved -= OnServeResolved;
            }
        }

        void OnContacted(SwingContact contact)
        {
            flashRemaining = flashDuration;
            flashColor = ColorFor(contact.Grade);
        }

        void OnServeResolved(ServeOutcome outcome, bool doubleFault)
        {
            if (outcome == ServeOutcome.In) return;

            flashRemaining = flashDuration * (doubleFault ? 2f : 1f);
            flashColor = faultColor;
        }

        Color ColorFor(SwingGrade grade)
        {
            switch (grade)
            {
                case SwingGrade.Perfect: return perfectColor;
                case SwingGrade.Good: return goodColor;
                case SwingGrade.Early: return earlyColor;
                case SwingGrade.Late: return lateColor;
                default: return missColor;
            }
        }

        void LateUpdate()
        {
            if (character == null || fill == null) return;

            if (flashRemaining > 0f) flashRemaining -= Time.deltaTime;

            bool visible = flashRemaining > 0f;
            SetVisible(visible);
            if (!visible) return;

            Vector3 spot = character.position;
            transform.position = new Vector3(spot.x, heightAboveGround, spot.z);

            // Face whatever is looking. The match camera holds a fixed rotation,
            // so copying it keeps the bar perfectly steady instead of swimming
            // as the player runs.
            if (view == null) view = Camera.main;
            if (view != null) transform.rotation = view.transform.rotation;

            fill.localScale = new Vector3(width, thickness, 0.02f);
            fill.localPosition = new Vector3(0f, 0f, -0.01f);

            if (background != null)
                background.localScale = new Vector3(width * 1.06f, thickness * 1.35f, 0.02f);

            SetColor(flashColor);
        }

        void SetColor(Color color)
        {
            if (fillRenderer == null) return;

            // Same reason as BallLandingMarker: a domain reload in play mode
            // drops this field but keeps the renderer beside it.
            properties ??= new MaterialPropertyBlock();

            fillRenderer.GetPropertyBlock(properties);
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_EmissionColor", color * 0.8f);
            fillRenderer.SetPropertyBlock(properties);
        }

        void SetVisible(bool visible)
        {
            if (fill != null && fill.gameObject.activeSelf != visible) fill.gameObject.SetActive(visible);
            if (background != null && background.gameObject.activeSelf != visible)
                background.gameObject.SetActive(visible);
        }
    }
}
