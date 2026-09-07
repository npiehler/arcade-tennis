using UnityEngine;

namespace ArcadeTennis.Utility
{
    /// <summary>
    /// An elliptical outline lying flat on the XZ plane, used for the target zone
    /// marker.
    ///
    /// Built as a real ellipse rather than a circle squashed by the transform,
    /// because squashing a ring makes its outline thick at the ends and thin at
    /// the sides -- on a zone that is twice as long as it is wide, that reads as
    /// a mistake.
    /// </summary>
    public static class EllipseRingMesh
    {
        public static Mesh Create(float radiusX, float radiusZ, float thickness, int segments = 96)
        {
            segments = Mathf.Max(segments, 12);
            float half = Mathf.Max(thickness, 0.01f) * 0.5f;

            var vertices = new Vector3[segments * 2];
            var normals = new Vector3[segments * 2];
            var triangles = new int[segments * 6];

            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                var point = new Vector3(cos * radiusX, 0f, sin * radiusZ);

                // Outward normal of an ellipse is not the radius direction, so it
                // is taken from the tangent instead. That is what keeps the band
                // an even width the whole way round.
                var tangent = new Vector3(-sin * radiusX, 0f, cos * radiusZ);
                var outward = new Vector3(tangent.z, 0f, -tangent.x).normalized;

                vertices[i * 2] = point - outward * half;
                vertices[i * 2 + 1] = point + outward * half;
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

            var mesh = new Mesh { name = "EllipseRing" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
