using System;
using UnityEngine;

namespace VRKit
{
    /// <summary>
    /// Un trozo de un prefab de vegetación con un solo material. Material 0 = sin textura (color por vértice);
    /// 1, 2… = un material con textura que crea el OpenWorld (todos los trozos con el mismo material se juntan).
    /// </summary>
    public sealed class PropPart
    {
        public int material;
        public Vector3[] vertices;
        public Vector3[] normals;
        public Color32[] colors;
        /// <summary>Solo si el material tiene textura.</summary>
        public Vector2[] uvs;
        public int[] triangles;
    }

    /// <summary>La geometría de un prefab de vegetación (un solo LOD), leída una vez, para copiarla muchas veces en un chunk.</summary>
    public sealed class PropGeometry
    {
        public PropPart[] parts;
        public int VertexCount { get { int n = 0; if (parts != null) foreach (var p in parts) n += p.vertices.Length; return n; } }
        public int TriangleCount { get { int n = 0; if (parts != null) foreach (var p in parts) n += p.triangles.Length / 3; return n; } }
    }

    /// <summary>
    /// Junta todos los árboles, rocas y hierba de un chunk en UNA malla por material (casi siempre una o pocas). Así, en vez de cientos de objetos,
    /// la Quest dibuja uno por chunk: es lo que permite tanta vegetación a 72 FPS. Código puro (va en otro hilo).
    /// </summary>
    public static class PropCombiner
    {
        /// <summary>
        /// Copia g, girada yaw grados, inclinada hacia «up», escalada y movida a pos. Cada trozo va a la malla de su
        /// material: byMaterial[material] (se crea si falta).
        /// </summary>
        public static void Append(LowPolyMeshData[] byMaterial, PropGeometry g, Vector3 pos, float yawDeg, Vector3 up, float scale)
        {
            if (g == null || g.parts == null) return;
            foreach (var part in g.parts)
            {
                if (part.material < 0 || part.material >= byMaterial.Length) continue;
                if (byMaterial[part.material] == null) byMaterial[part.material] = new LowPolyMeshData();
                Append(byMaterial[part.material], part, pos, yawDeg, up, scale);
            }
        }

        /// <summary>Copia un trozo en m.</summary>
        public static void Append(LowPolyMeshData m, PropPart g, Vector3 pos, float yawDeg, Vector3 up, float scale)
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
            bool uv = g.uvs != null && g.uvs.Length == g.vertices.Length;
            // Si esta malla mezcla trozos con y sin UV, rellenar para que cuadren
            if (uv) while (m.uvs.Count < start) m.uvs.Add(new Vector2(0f, 0f));
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
                if (uv) m.uvs.Add(g.uvs[i]);
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
