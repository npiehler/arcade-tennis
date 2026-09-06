using System.Collections.Generic;
using UnityEngine;

namespace ArcadeTennis.Court
{
    /// <summary>
    /// Generates the court geometry from a <see cref="CourtDefinition"/> so the
    /// visuals can never drift from the numbers the rules are evaluated against.
    ///
    /// Rebuilding is idempotent: everything lands under a single "_Generated"
    /// child that is cleared first. Call <see cref="Rebuild"/> from the inspector
    /// context menu, or from a script.
    ///
    /// The generated parts carry no colliders. Ball flight, bounces and net
    /// contact are all evaluated analytically against the CourtDefinition, and
    /// player movement is clamped rather than simulated, so physics colliders
    /// would only add cost and surprises.
    /// </summary>
    public class CourtBuilder : MonoBehaviour
    {
        const string GeneratedRootName = "_Generated";
        const float LineHeight = 0.005f;   // lifted off the surface to avoid z-fighting
        const int NetSegments = 28;        // slices used to approximate the sag

        [SerializeField] CourtDefinition court;

        [Header("Materials")]
        [SerializeField] Material surfaceMaterial;
        [SerializeField] Material apronMaterial;
        [SerializeField] Material lineMaterial;
        [SerializeField] Material netMaterial;
        [SerializeField] Material netTapeMaterial;
        [SerializeField] Material postMaterial;
        [SerializeField] Material outfieldMaterial;

        [Header("Surroundings")]
        [Tooltip("Edge length of the flat ground the court sits on, so the horizon is not the skybox.")]
        [SerializeField] float outfieldSize = 160f;

        [Header("Gizmos")]
        [SerializeField] bool drawGizmos = true;

        public CourtDefinition Court => court;

        [ContextMenu("Rebuild Court")]
        public void Rebuild()
        {
            if (court == null)
            {
                Debug.LogError("CourtBuilder: no CourtDefinition assigned.", this);
                return;
            }

            Transform root = ResetGeneratedRoot();

            BuildSurfaces(root);
            BuildLines(root);
            BuildNet(root);
        }

        Transform ResetGeneratedRoot()
        {
            Transform existing = transform.Find(GeneratedRootName);
            if (existing != null) DestroyObject(existing.gameObject);

            var root = new GameObject(GeneratedRootName).transform;
            root.SetParent(transform, false);
            return root;
        }

        void BuildSurfaces(Transform root)
        {
            Vector2 ground = court.GroundSize;

            // Flat ground well past the apron so the camera never sees the
            // skybox horizon meeting nothing.
            AddBox(root, "Outfield", new Vector3(0f, -0.06f, 0f),
                new Vector3(outfieldSize, 0.04f, outfieldSize), outfieldMaterial);

            // Apron first, then the live court slightly above it so the colour
            // change reads cleanly from the camera behind the baseline.
            AddBox(root, "Apron", new Vector3(0f, -0.02f, 0f),
                new Vector3(ground.x, 0.02f, ground.y), apronMaterial);

            AddBox(root, "Surface", new Vector3(0f, -0.005f, 0f),
                new Vector3(court.DoublesHalfWidth * 2f + 1.2f, 0.01f, court.Length + 1.2f),
                surfaceMaterial);
        }

        void BuildLines(Transform root)
        {
            var lines = new GameObject("Lines").transform;
            lines.SetParent(root, false);

            float w = court.LineWidth;
            float halfL = court.HalfLength;
            float singles = court.SinglesHalfWidth;
            float doubles = court.DoublesHalfWidth;
            float service = court.ServiceLineDistance;

            // Baselines
            AddLine(lines, "Baseline+", new Vector3(0f, 0f, halfL), doubles * 2f + w, w);
            AddLine(lines, "Baseline-", new Vector3(0f, 0f, -halfL), doubles * 2f + w, w);

            // Sidelines
            AddLine(lines, "DoublesSideline+", new Vector3(doubles, 0f, 0f), w, halfL * 2f + w);
            AddLine(lines, "DoublesSideline-", new Vector3(-doubles, 0f, 0f), w, halfL * 2f + w);
            AddLine(lines, "SinglesSideline+", new Vector3(singles, 0f, 0f), w, halfL * 2f + w);
            AddLine(lines, "SinglesSideline-", new Vector3(-singles, 0f, 0f), w, halfL * 2f + w);

            // Service lines run between the singles sidelines only
            AddLine(lines, "ServiceLine+", new Vector3(0f, 0f, service), singles * 2f, w);
            AddLine(lines, "ServiceLine-", new Vector3(0f, 0f, -service), singles * 2f, w);

            // Centre service line splits both service boxes
            AddLine(lines, "CentreServiceLine", Vector3.zero, w, service * 2f);

            // Centre marks on the baselines
            float mark = court.CentreMarkLength;
            AddLine(lines, "CentreMark+", new Vector3(0f, 0f, halfL - mark * 0.5f), w, mark);
            AddLine(lines, "CentreMark-", new Vector3(0f, 0f, -halfL + mark * 0.5f), w, mark);
        }

        void BuildNet(Transform root)
        {
            var net = new GameObject("Net").transform;
            net.SetParent(root, false);

            float halfWidth = court.NetHalfWidth;
            float segmentWidth = halfWidth * 2f / NetSegments;

            for (int i = 0; i < NetSegments; i++)
            {
                float x = -halfWidth + segmentWidth * (i + 0.5f);
                float height = court.NetHeightAt(x);

                float tape = 0.06f;

                AddBox(net, $"Segment{i:00}",
                    new Vector3(x, (height - tape) * 0.5f, 0f),
                    new Vector3(segmentWidth, height - tape, court.NetThickness),
                    netMaterial);

                AddBox(net, $"Tape{i:00}",
                    new Vector3(x, height - tape * 0.5f, 0f),
                    new Vector3(segmentWidth, tape, court.NetThickness * 1.6f),
                    netTapeMaterial);
            }

            float postHeight = court.NetHeightPost;
            AddBox(net, "Post+", new Vector3(halfWidth, postHeight * 0.5f, 0f),
                new Vector3(0.1f, postHeight, 0.1f), postMaterial);
            AddBox(net, "Post-", new Vector3(-halfWidth, postHeight * 0.5f, 0f),
                new Vector3(0.1f, postHeight, 0.1f), postMaterial);
        }

        void AddLine(Transform parent, string name, Vector3 centre, float width, float length)
        {
            AddBox(parent, name, new Vector3(centre.x, LineHeight * 0.5f, centre.z),
                new Vector3(width, LineHeight, length), lineMaterial);
        }

        GameObject AddBox(Transform parent, string name, Vector3 localPosition, Vector3 size, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = size;

            DestroyObject(go.GetComponent<Collider>());

            if (material != null)
                go.GetComponent<MeshRenderer>().sharedMaterial = material;

            return go;
        }

        static void DestroyObject(Object target)
        {
            if (target == null) return;

            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        void OnDrawGizmos()
        {
            if (!drawGizmos || court == null) return;

            Gizmos.matrix = transform.localToWorldMatrix;

            // Live singles court
            Gizmos.color = Color.yellow;
            DrawRect(-court.SinglesHalfWidth, court.SinglesHalfWidth, -court.HalfLength, court.HalfLength);

            // Doubles alleys
            Gizmos.color = new Color(1f, 1f, 0f, 0.35f);
            DrawRect(-court.DoublesHalfWidth, court.DoublesHalfWidth, -court.HalfLength, court.HalfLength);

            // The four service boxes, so the serve targets are visible while tuning
            Gizmos.color = Color.cyan;
            foreach (int side in new[] { 1, -1 })
            {
                foreach (bool deuce in new[] { true, false })
                {
                    Rect box = court.GetServiceBox(side, deuce);
                    DrawRect(box.xMin, box.xMax, box.yMin, box.yMax);
                }
            }

            // Net silhouette including the sag
            Gizmos.color = Color.white;
            Vector3 previous = Vector3.zero;
            for (int i = 0; i <= NetSegments; i++)
            {
                float x = Mathf.Lerp(-court.NetHalfWidth, court.NetHalfWidth, i / (float)NetSegments);
                var point = new Vector3(x, court.NetHeightAt(x), 0f);
                if (i > 0) Gizmos.DrawLine(previous, point);
                previous = point;
            }
        }

        void DrawRect(float minX, float maxX, float minZ, float maxZ)
        {
            var corners = new List<Vector3>
            {
                new Vector3(minX, 0f, minZ),
                new Vector3(maxX, 0f, minZ),
                new Vector3(maxX, 0f, maxZ),
                new Vector3(minX, 0f, maxZ),
            };

            for (int i = 0; i < corners.Count; i++)
                Gizmos.DrawLine(corners[i], corners[(i + 1) % corners.Count]);
        }
    }
}
