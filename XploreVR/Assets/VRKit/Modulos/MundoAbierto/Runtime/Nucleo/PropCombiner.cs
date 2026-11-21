using System;
using UnityEngine;

namespace VRKit
{
    /// <summary>La geometría de un prefab de vegetación, leída una vez, para copiarla muchas veces en un chunk.</summary>
    public sealed class PropGeometry
    {
        public Vector3[] vertices;
        public Vector3[] normals;
        public Color32[] colors;
        public int[] triangles;
        public int VertexCount { get { return vertices != null ? vertices.Length : 0; } }
    }

    /// <summary>
    /// Junta todos los árboles, rocas y hierba de un chunk en UNA sola malla. Así, en vez de cientos de objetos,
    /// la Quest dibuja uno por chunk: es lo que permite tanta vegetación a 72 FPS. Código puro (va en otro hilo).
    /// </summary>
    public static class PropCombiner
    {
        /// <summary>Copia g en m, girada yaw grados, inclinada hacia «up», escalada y movida a pos.</summary>
        public static void Append(LowPolyMeshData m, PropGeometry g, Vector3 pos, float yawDeg, Vector3 up, float scale)
        {
            if (g == null || g.vertices == null) return;
            // Base: «arriba» = up; «adelante» = el yaw proyectado sobre el plano perpendicular a up
            double yaw = yawDeg * Math.PI / 180.0;
            Vector3 f0 = new Vector3((float)Math.Sin(yaw), 0f, (float)Math.Cos(yaw));
            Vector3 u = Norm(up);
            float d = Dot(f0, u);
            Vector3 fwd = Norm(new Vector3(f0.x - u.x * d, f0.y - u.y * d, f0.z - u.z * d));
            Vector3 right = Cross(u, fwd);

            int start = m.vertices.Count;
            for (int i = 0; i < g.vertices.Length; i++)
            {
                var v = g.vertices[i];
                m.vertices.Add(new Vector3(
                    pos.x + (right.x * v.x + u.x * v.y + fwd.x * v.z) * scale,
                    pos.y + (right.y * v.x + u.y * v.y + fwd.y * v.z) * scale,
                    pos.z + (right.z * v.x + u.z * v.y + fwd.z * v.z) * scale));
                var n = g.normals != null && i < g.normals.Length ? g.normals[i] : new Vector3(0f, 1f, 0f);
                m.normals.Add(new Vector3(right.x * n.x + u.x * n.y + fwd.x * n.z, right.y * n.x + u.y * n.y + fwd.y * n.z, right.z * n.x + u.z * n.y + fwd.z * n.z));
                m.colors.Add(g.colors != null && i < g.colors.Length ? g.colors[i] : new Color32(255, 255, 255, 255));
            }
            for (int i = 0; i < g.triangles.Length; i++) m.triangles.Add(start + g.triangles[i]);
        }

        static float Dot(Vector3 a, Vector3 b) { return a.x * b.x + a.y * b.y + a.z * b.z; }
        static Vector3 Cross(Vector3 a, Vector3 b) { return new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x); }
        static Vector3 Norm(Vector3 v)
        {
            float m = (float)Math.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
            return m > 1e-6f ? new Vector3(v.x / m, v.y / m, v.z / m) : new Vector3(0f, 1f, 0f);
        }
    }
}
