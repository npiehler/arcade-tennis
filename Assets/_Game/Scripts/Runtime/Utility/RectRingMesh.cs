using UnityEngine;

namespace ArcadeTennis.Utility
{
    /// <summary>
    /// A rectangular outline lying flat on the XZ plane, for framing an area of
    /// the court. Used for the service box the serve has to land in: an ellipse
    /// would read as "somewhere around here", a rectangle reads as "this box",
    /// which is what a service box is.
    /// </summary>
    public static class RectRingMesh
    {
        public static Mesh Create(float halfWidth, float halfDepth, float thickness)
        {
            float half = Mathf.Max(thickness, 0.01f) * 0.5f;
            float ox = halfWidth + half, oz = halfDepth + half;
            float ix = Mathf.Max(halfWidth - half, 0.001f), iz = Mathf.Max(halfDepth - half, 0.001f);

            var outer = new Vector3[] {
                new Vector3(-ox, 0f, -oz), new Vector3(ox, 0f, -oz),
                new Vector3(ox, 0f, oz), new Vector3(-ox, 0f, oz) };
            var inner = new Vector3[] {
                new Vector3(-ix, 0f, -iz), new Vector3(ix, 0f, -iz),
                new Vector3(ix, 0f, iz), new Vector3(-ix, 0f, iz) };

            var vertices = new Vector3[8];
            var normals = new Vector3[8];
            for (int i = 0; i < 4; i++)
            {
                vertices[i * 2] = inner[i];
                vertices[i * 2 + 1] = outer[i];
                normals[i * 2] = Vector3.up;
                normals[i * 2 + 1] = Vector3.up;
            }

            var triangles = new int[24];
            for (int i = 0; i < 4; i++)
            {
                int next = (i + 1) % 4;
                int t = i * 6;
                triangles[t] = i * 2;
                triangles[t + 1] = next * 2 + 1;
                triangles[t + 2] = i * 2 + 1;
                triangles[t + 3] = i * 2;
                triangles[t + 4] = next * 2;
                triangles[t + 5] = next * 2 + 1;
            }

            var mesh = new Mesh { name = "RectRing" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
