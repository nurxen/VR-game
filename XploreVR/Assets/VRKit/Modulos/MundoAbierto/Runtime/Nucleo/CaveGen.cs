using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRKit
{
    /// <summary>Una entrada de cueva en el mundo: dónde está, hacia dónde mira y su nombre.</summary>
    public struct CaveSite
    {
        /// <summary>Identificador estable (sale de la casilla del mundo donde está).</summary>
        public long id;
        public float x, y, z;
        /// <summary>Hacia dónde mira la boca (cuesta abajo), en grados.</summary>
        public float yaw;
        public string name;
    }

    /// <summary>
    /// Cuevas: dónde aparecen sus entradas y cómo es su interior. Las entradas salen en laderas, nunca en el agua ni en
    /// llano. El interior es un túnel con salas, generado con la misma semilla: siempre igual para la misma cueva.
    /// </summary>
    public static class CaveGen
    {
        static readonly string[] kNoun = { "Cueva", "Gruta", "Sima", "Caverna", "Galería" };
        static readonly string[] kAdj =
        {
            "del Eco", "de los Cristales", "Musgosa", "del Viento", "Profunda", "de la Luna", "Escondida", "del Murmullo",
            "de las Raíces", "Azul", "del Ermitaño", "de los Susurros", "Dormida", "del Agua Quieta", "de las Luciérnagas",
        };

        /// <summary>Entradas de cueva dentro del chunk (cx, cz). spacing = una posible cueva cada tantos metros.</summary>
        public static void Sites(OpenTerrainGenerator gen, int cx, int cz, float size, float spacing, float chance, List<CaveSite> output)
        {
            output.Clear();
            if (chance <= 0f || spacing <= 0f) return;
            float x0 = cx * size, z0 = cz * size, x1 = x0 + size, z1 = z0 + size;
            int gx0 = (int)Math.Floor(x0 / spacing), gx1 = (int)Math.Floor(x1 / spacing);
            int gz0 = (int)Math.Floor(z0 / spacing), gz1 = (int)Math.Floor(z1 / spacing);
            for (int gz = gz0; gz <= gz1; gz++)
            for (int gx = gx0; gx <= gx1; gx++)
            {
                if (WorldNoise.Hash01(gx, gz, 11, gen.Seed) >= chance) continue;
                // Se prueban unos pocos puntos de la casilla; el primero en una buena ladera se queda.
                // Se elige mirando toda la casilla (no solo este trozo): así cada casilla tiene UNA cueva
                // aunque toque varios chunks, y solo la añade el chunk donde cae.
                for (int t = 0; t < 6; t++)
                {
                    float px = (gx + 0.1f + 0.8f * WorldNoise.Hash01(gx, gz, 20 + t, gen.Seed)) * spacing;
                    float pz = (gz + 0.1f + 0.8f * WorldNoise.Hash01(gx, gz, 40 + t, gen.Seed)) * spacing;
                    float h = gen.Height(px, pz);
                    float slope = gen.SlopeAt(px, pz, 2f);
                    if (h < gen.Settings.waterLevel + 4f || slope < 14f || slope > 48f) continue;
                    float dx = gen.Height(px + 2f, pz) - gen.Height(px - 2f, pz);
                    float dz = gen.Height(px, pz + 2f) - gen.Height(px, pz - 2f);
                    float yaw = (float)(Math.Atan2(-dx, -dz) * 180.0 / Math.PI);   // cuesta abajo
                    if (px < x0 || px >= x1 || pz < z0 || pz >= z1) break;     // es de otro chunk
                    long id = ((long)gx << 32) ^ (uint)gz;
                    output.Add(new CaveSite { id = id, x = px, y = h, z = pz, yaw = yaw, name = Name(id, gen.Seed) });
                    break;
                }
            }
        }

        public static string Name(long id, int seed)
        {
            int a = (int)(id >> 32), b = (int)(id & 0xffffffffL);
            var n = kNoun[(int)(WorldNoise.Hash01(a, b, 70, seed) * kNoun.Length) % kNoun.Length];
            var j = kAdj[(int)(WorldNoise.Hash01(a, b, 71, seed) * kAdj.Length) % kAdj.Length];
            return n + " " + j;
        }

        /// <summary>Lo que necesita el juego del interior: la malla, dónde empieza y dónde están las salas.</summary>
        public sealed class Interior
        {
            public readonly LowPolyMeshData mesh = new LowPolyMeshData();
            public Vector3 start, startForward;
            public readonly List<Vector3> chambers = new List<Vector3>();
            public readonly List<Vector3> floorPoints = new List<Vector3>();
        }

        /// <summary>
        /// Túnel con salas visto desde dentro: anillos de piedra a lo largo de un camino que serpentea, con el suelo
        /// aplanado para poder andar. Coordenadas locales (el juego lo coloca bajo tierra).
        /// </summary>
        public static Interior BuildInterior(long id, int seed, int segments = 9, int sides = 10)
        {
            var it = new Interior();
            int a = (int)(id >> 32), b = (int)(id & 0xffffffffL);
            Func<int, float> R = k => WorldNoise.Hash01(a, b, 200 + k, seed);

            // Camino: puntos cada ~8 m, girando poco a poco y subiendo/bajando un poco
            var path = new List<Vector3>();
            var radius = new List<float>();
            float yaw = 0f, y = 0f;
            var p = new Vector3(0f, 0f, 0f);
            int total = segments * 3;
            for (int i = 0; i <= total; i++)
            {
                path.Add(p);
                bool chamber = i > 2 && i % 6 == 0;
                float r = chamber ? 6.5f + R(i) * 3f : 2.6f + R(i + 300) * 1.2f;
                if (i == 0 || i == total) r = 2.8f;
                radius.Add(r);
                if (chamber) it.chambers.Add(new Vector3(p.x, p.y - r * 0.55f, p.z));
                yaw += (R(i + 100) - 0.5f) * 50f;
                y = Math.Max(-6f, Math.Min(6f, y + (R(i + 200) - 0.5f) * 2.4f));
                float rad = yaw * (float)Math.PI / 180f;
                p = new Vector3(p.x + (float)Math.Sin(rad) * 8f, y, p.z + (float)Math.Cos(rad) * 8f);
            }

            // Anillos
            var rings = new List<Vector3[]>();
            var floors = new List<float>();
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 fwd = i < path.Count - 1 ? Sub(path[i + 1], path[i]) : Sub(path[i], path[i - 1]);
                fwd = Norm(new Vector3(fwd.x, 0f, fwd.z));
                var right = new Vector3(fwd.z, 0f, -fwd.x);
                var ring = new Vector3[sides];
                float floorY = path[i].y - radius[i] * 0.55f;
                floors.Add(floorY);
                for (int k = 0; k < sides; k++)
                {
                    float ang = (k / (float)sides) * (float)Math.PI * 2f;
                    float jit = 1f + (WorldNoise.Hash01(a + i, b + k, 90, seed) - 0.5f) * 0.35f;
                    float rx = (float)Math.Cos(ang) * radius[i] * jit, ry = (float)Math.Sin(ang) * radius[i] * jit;
                    var v = new Vector3(path[i].x + right.x * rx, path[i].y + ry, path[i].z + right.z * rx);
                    if (v.y < floorY) v = new Vector3(v.x, floorY + (WorldNoise.Hash01(a + i, b + k, 91, seed) - 0.5f) * 0.15f, v.z); // suelo plano
                    ring[k] = v;
                }
                rings.Add(ring);
                it.floorPoints.Add(new Vector3(path[i].x, floorY, path[i].z));
            }

            // Caras hacia dentro (se ve desde el interior)
            for (int i = 0; i < rings.Count - 1; i++)
            for (int k = 0; k < sides; k++)
            {
                int k2 = (k + 1) % sides;
                Vector3 p0 = rings[i][k], p1 = rings[i][k2], p2 = rings[i + 1][k], p3 = rings[i + 1][k2];
                var col = WallColor(p0, floors[i], a, b, i, k, seed);
                it.mesh.FlatTriangle(p0, p2, p1, col);
                it.mesh.FlatTriangle(p1, p2, p3, col);
            }
            // Tapas de los extremos
            Cap(it.mesh, rings[0], path[0], true, a, b, seed);
            Cap(it.mesh, rings[rings.Count - 1], path[path.Count - 1], false, a, b, seed);

            it.start = it.floorPoints[1];
            it.startForward = Norm(Sub(path[2], path[1]));
            return it;
        }

        static Color32 WallColor(Vector3 v, float floorY, int a, int b, int i, int k, int seed)
        {
            float f = 0.85f + WorldNoise.Hash01(a + i, b + k, 92, seed) * 0.3f;
            bool floor = v.y < floorY + 0.3f;
            int r = floor ? 92 : 84, g = floor ? 84 : 80, bl = floor ? 72 : 88;
            return new Color32((byte)Math.Min(255, r * f), (byte)Math.Min(255, g * f), (byte)Math.Min(255, bl * f), 255);
        }

        static void Cap(LowPolyMeshData m, Vector3[] ring, Vector3 center, bool first, int a, int b, int seed)
        {
            var col = new Color32(70, 66, 74, 255);
            for (int k = 0; k < ring.Length; k++)
            {
                int k2 = (k + 1) % ring.Length;
                if (first) m.FlatTriangle(center, ring[k], ring[k2], col);
                else m.FlatTriangle(center, ring[k2], ring[k], col);
            }
        }

        static Vector3 Sub(Vector3 p, Vector3 q) { return new Vector3(p.x - q.x, p.y - q.y, p.z - q.z); }
        static Vector3 Norm(Vector3 v)
        {
            float m = (float)Math.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
            return m > 1e-6f ? new Vector3(v.x / m, v.y / m, v.z / m) : new Vector3(0f, 0f, 1f);
        }
    }
}
