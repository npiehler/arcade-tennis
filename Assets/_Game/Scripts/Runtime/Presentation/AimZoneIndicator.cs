using UnityEngine;
using ArcadeTennis.Characters;
using ArcadeTennis.Court;
using ArcadeTennis.Utility;

namespace ArcadeTennis.Presentation
{
    /// <summary>
    /// Shows where the next shot is going: a ring around the chosen zone in the
    /// opponent's half, and a line from the player to it.
    ///
    /// The ring is not decoration. It is drawn from the very same numbers the
    /// solver aims with -- the lane's width from <see cref="AimZones"/>, its
    /// length from the config's weak and strong depth -- so it marks the ground
    /// the ball can actually land on rather than a rough idea of it. If the
    /// tuning changes, the ring changes with it and cannot start lying.
    ///
    /// It switches to the service box on its own while a serve is pending,
    /// because that is the only target that exists then.
    /// </summary>
    public class AimZoneIndicator : MonoBehaviour
    {
        [SerializeField] TennisCharacter character;
        [SerializeField] SwingController swing;
        [SerializeField] ServeController serve;
        [SerializeField] CourtDefinition court;

        [Header("Parts")]
        [SerializeField] Transform ring;
        [SerializeField] Transform line;

        [Header("Look")]
        [SerializeField] float groundOffset = 0.03f;
        [SerializeField] float ringThickness = 0.16f;
        [SerializeField] float lineWidth = 0.09f;
        [Tooltip("One colour for both strokes. A second colour for the serve only raised " +
                 "the question of what it meant -- where the ring sits already says whether " +
                 "this is a serve or a rally.")]
        [SerializeField] Color markerColor = new Color(0.45f, 0.85f, 1f, 1f);

        MeshFilter ringFilter;
        Renderer ringRenderer;
        Renderer lineRenderer;
        MaterialPropertyBlock properties;
        Mesh mesh;

        float builtRadiusX = -1f;
        float builtRadiusZ = -1f;

        void Awake()
        {
            properties = new MaterialPropertyBlock();
            if (ring != null)
            {
                ringFilter = ring.GetComponent<MeshFilter>();
                ringRenderer = ring.GetComponent<Renderer>();
            }
            if (line != null) lineRenderer = line.GetComponent<Renderer>();
        }

        void LateUpdate()
        {
            if (character == null || court == null || ring == null) return;

            // isActiveAndEnabled matters: a switched-off controller still answers
            // from its last state, and the marker would keep pointing somewhere.
            bool serveAiming = serve != null && serve.isActiveAndEnabled && serve.IsAiming;
            bool rallyAiming = swing != null && swing.isActiveAndEnabled && swing.IsAiming;

            // The marker is only up while the button is held. Showing it the rest
            // of the time would make it scenery rather than an answer to "where
            // am I about to put this".
            if (!serveAiming && !rallyAiming)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);

            int side = character.Side;
            AimZone zone = serveAiming ? serve.Zone : swing.Zone;

            Vector2 centre = serveAiming
                ? AimZones.ServeZoneCentre(court, side, serve.DeuceCourt, zone)
                : AimZones.GroundZoneCentre(court, side, zone);
            Vector2 extents = serveAiming
                ? AimZones.ServeZoneExtents(court, zone)
                : AimZones.GroundZoneExtents(court, zone);

            Vector3 spot = AimZones.ToWorld(side, centre.x, centre.y);
            spot.y = groundOffset;

            Rebuild(extents.x, extents.y);
            ring.position = spot;
            ring.rotation = Quaternion.identity;

            DrawLine(character.transform.position, spot);
            SetColor(markerColor);
        }

        void SetVisible(bool visible)
        {
            if (ring != null && ring.gameObject.activeSelf != visible)
                ring.gameObject.SetActive(visible);
            if (!visible && line != null && line.gameObject.activeSelf)
                line.gameObject.SetActive(false);
        }

        void Rebuild(float radiusX, float radiusZ)
        {
            if (ringFilter == null) return;
            if (Mathf.Approximately(builtRadiusX, radiusX) && Mathf.Approximately(builtRadiusZ, radiusZ))
                return;

            if (mesh != null)
            {
                if (Application.isPlaying) Destroy(mesh);
                else DestroyImmediate(mesh);
            }

            mesh = EllipseRingMesh.Create(radiusX, radiusZ, ringThickness);
            mesh.hideFlags = HideFlags.HideAndDontSave;
            ringFilter.sharedMesh = mesh;

            builtRadiusX = radiusX;
            builtRadiusZ = radiusZ;
        }

        void DrawLine(Vector3 from, Vector3 to)
        {
            if (line == null) return;

            var start = new Vector3(from.x, groundOffset, from.z);
            Vector3 delta = to - start;
            float length = delta.magnitude;

            if (length < 0.05f)
            {
                line.gameObject.SetActive(false);
                return;
            }

            if (!line.gameObject.activeSelf) line.gameObject.SetActive(true);

            line.position = start + delta * 0.5f;
            line.rotation = Quaternion.LookRotation(delta / length, Vector3.up);
            line.localScale = new Vector3(lineWidth, 0.01f, length);
        }

        void SetColor(Color color)
        {
            properties ??= new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_EmissionColor", color * 0.7f);

            if (ringRenderer != null) ringRenderer.SetPropertyBlock(properties);
            if (lineRenderer != null) lineRenderer.SetPropertyBlock(properties);
        }

        void OnDestroy()
        {
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
        }
    }
}
