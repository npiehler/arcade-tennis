using UnityEngine;

namespace ArcadeTennis.Utility
{
    /// <summary>Builds flat rings laid out on the XZ plane, used for ground indicators.</summary>
    public static class RingMesh
    {
        public static Mesh Create(float innerRadius, float outerRadius, int segments = 64)
        {
            segments = Mathf.Max(segments, 8);

            var vertices = new Vector3[segments * 2];
            var normals = new Vector3[segments * 2];
            var triangles = new int[segments * 6];

            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                vertices[i * 2] = new Vector3(cos * innerRadius, 0f, sin * innerRadius);
                vertices[i * 2 + 1] = new Vector3(cos * outerRadius, 0f, sin * outerRadius);
                normals[i * 2] = Vector3.up;
                normals[i * 2 + 1] = Vector3.up;

                int next = (i + 1) % segments;
                int t = i * 6;

                triangles[t] = i * 2;
                triangles[t + 1] = next * 2 + 1;
                triangles[t + 2] = i * 2 + 1;

                triangles[t + 3] = i * 2;
                triangles[t + 4] = next * 2;
                triangles[t + 5] = next * 2 + 1;
            }

            var mesh = new Mesh { name = "Ring" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
