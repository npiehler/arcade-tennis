using UnityEngine;
using ArcadeTennis.Characters;
using ArcadeTennis.Utility;

namespace ArcadeTennis.Presentation
{
    /// <summary>
    /// Draws the ring on the ground that shows how far a character can reach.
    /// The mesh is generated at runtime and never saved into the scene, so the
    /// project keeps no generated geometry lying around.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class ReachIndicator : MonoBehaviour
    {
        [SerializeField] TennisCharacter character;
        [SerializeField] float thickness = 0.09f;
        [SerializeField] float groundOffset = 0.02f;

        Mesh mesh;
        float builtRadius = -1f;

        void Awake() => Rebuild();

        void LateUpdate()
        {
            if (character == null) return;

            if (!Mathf.Approximately(builtRadius, character.ReachRadius)) Rebuild();

            Vector3 position = character.transform.position;
            transform.position = new Vector3(position.x, groundOffset, position.z);
        }

        void Rebuild()
        {
            if (character == null) return;

            float radius = character.ReachRadius;
            mesh = RingMesh.Create(Mathf.Max(radius - thickness, 0.01f), radius);
            mesh.hideFlags = HideFlags.HideAndDontSave;

            GetComponent<MeshFilter>().sharedMesh = mesh;
            builtRadius = radius;
        }

        void OnDestroy()
        {
            if (mesh == null) return;

            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
        }
    }
}
