using System;
using UnityEngine;

namespace VRKit
{
    /// <summary>
    /// Formas low poly por código (conos, cilindros, rocas, copas de árbol) con caras planas y color por vértice.
    /// El montador las usa para crear la vegetación de ejemplo. Vuestros modelos de Blender las sustituyen.
    /// </summary>
    public static class LowPolyShapes
    {
        public static void Cylinder(LowPolyMeshData m, Vector3 basePos, float radiusBottom, float radiusTop, float height, int sides, Color32 col, bool caps = true)
        {
            for (int k = 0; k < sides; k++)
            {
                float a0 = k / (float)sides * (float)Math.PI * 2f, a1 = (k + 1) / (float)sides * (float)Math.PI * 2f;
                var b0 = P(basePos, a0, radiusBottom, 0f); var b1 = P(basePos, a1, radiusBottom, 0f);
                var t0 = P(basePos, a0, radiusTop, height); var t1 = P(basePos, a1, radiusTop, height);
                m.FlatTriangle(b0, t0, t1, col);
                m.FlatTriangle(b0, t1, b1, col);
                if (caps)
                {
                    var top = new Vector3(basePos.x, basePos.y + height, basePos.z);
                    if (radiusTop > 0f) m.FlatTriangle(top, t1, t0, col);
                    m.FlatTriangle(basePos, b0, b1, col);
                }
            }
        }

        public static void Cone(LowPolyMeshData m, Vector3 basePos, float radius, float height, int sides, Color32 col)
        {
            Cylinder(m, basePos, radius, 0f, height, sides, col, true);
        }

        /// <summary>Icosaedro deformado: roca, arbusto o copa de árbol. jitter 0 = regular.</summary>
        public static void Blob(LowPolyMeshData m, Vector3 center, Vector3 size, float jitter, int seed, Color32 col, float colorVariation = 0.08f)
        {
            float t = (1f + (float)Math.Sqrt(5.0)) / 2f;
            var v = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            int[] f =
            {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1,
            };
            var p = new Vector3[12];
            for (int i = 0; i < 12; i++)
            {
                float len = (float)Math.Sqrt(v[i].x * v[i].x + v[i].y * v[i].y + v[i].z * v[i].z);
                float j = 1f + (WorldNoise.Hash01(i, seed, 1, 7) - 0.5f) * 2f * jitter;
                p[i] = new Vector3(center.x + v[i].x / len * size.x * j, center.y + v[i].y / len * size.y * j, center.z + v[i].z / len * size.z * j);
            }
            for (int i = 0; i < f.Length; i += 3)
            {
                float fv = 1f + (WorldNoise.Hash01(i, seed, 2, 7) - 0.5f) * 2f * colorVariation;
                var c = new Color32(B(col.r * fv), B(col.g * fv), B(col.b * fv), 255);
                m.FlatTriangle(p[f[i]], p[f[i + 1]], p[f[i + 2]], c);
            }
        }

        static Vector3 P(Vector3 b, float ang, float r, float h)
        {
            return new Vector3(b.x + (float)Math.Cos(ang) * r, b.y + h, b.z + (float)Math.Sin(ang) * r);
        }

        static byte B(float v) { return (byte)(v < 0f ? 0 : v > 255f ? 255 : v); }
    }
}
