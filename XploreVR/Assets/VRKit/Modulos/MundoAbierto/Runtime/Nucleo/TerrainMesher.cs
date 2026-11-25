using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRKit
{
    /// <summary>Listas de una malla (terreno, agua, cueva, roca…) antes de pasarlas a una Mesh de Unity.</summary>
    public sealed class LowPolyMeshData
    {
        public readonly List<Vector3> vertices = new List<Vector3>(4096);
        public readonly List<Vector3> normals = new List<Vector3>(4096);
        public readonly List<Color32> colors = new List<Color32>(4096);
        public readonly List<int> triangles = new List<int>(6144);
        /// <summary>Solo lo que choca (sin los faldones de los bordes).</summary>
        public readonly List<int> colliderTriangles = new List<int>(6144);
        /// <summary>Solo la vegetación con textura (el terreno no lleva UV).</summary>
        public readonly List<Vector2> uvs = new List<Vector2>();

        public void Clear() { vertices.Clear(); normals.Clear(); colors.Clear(); triangles.Clear(); colliderTriangles.Clear(); uvs.Clear(); }

        /// <summary>Añade un triángulo con su propia normal y color (aspecto low poly de caras planas).</summary>
        public void FlatTriangle(Vector3 a, Vector3 b, Vector3 c, Color32 col, bool collide = true)
        {
            Vector3 n = Normal(a, b, c);
            int s = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            normals.Add(n); normals.Add(n); normals.Add(n);
            colors.Add(col); colors.Add(col); colors.Add(col);
            triangles.Add(s); triangles.Add(s + 1); triangles.Add(s + 2);
            if (collide) { colliderTriangles.Add(s); colliderTriangles.Add(s + 1); colliderTriangles.Add(s + 2); }
        }

        public static Vector3 Normal(Vector3 a, Vector3 b, Vector3 c)
        {
            float ux = b.x - a.x, uy = b.y - a.y, uz = b.z - a.z;
            float vx = c.x - a.x, vy = c.y - a.y, vz = c.z - a.z;
            float nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
            float m = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
            return m > 1e-8f ? new Vector3(nx / m, ny / m, nz / m) : new Vector3(0f, 1f, 0f);
        }
    }

    /// <summary>Colores del suelo de cada bioma. Lo rellena el OpenWorld desde sus BiomeProfile.</summary>
    public sealed class TerrainPalette
    {
        public readonly Color32[] ground = new Color32[8];
        /// <summary>Variación aleatoria de brillo por cara (0 = ninguna).</summary>
        public float variation = 0.06f;

        public TerrainPalette()
        {
            ground[(int)Biome.Pradera] = new Color32(118, 170, 78, 255);
            ground[(int)Biome.Bosque] = new Color32(70, 128, 58, 255);
            ground[(int)Biome.Ribera] = new Color32(86, 150, 74, 255);
            ground[(int)Biome.Desierto] = new Color32(222, 190, 125, 255);
            ground[(int)Biome.Montana] = new Color32(128, 120, 112, 255);
            ground[(int)Biome.Nevado] = new Color32(236, 240, 247, 255);
            ground[(int)Biome.Playa] = new Color32(228, 210, 152, 255);
            ground[(int)Biome.Fondo] = new Color32(118, 108, 84, 255);
        }
    }

    /// <summary>Resumen de un chunk generado.</summary>
    public struct TerrainChunkInfo
    {
        public float minHeight, maxHeight;
        public bool hasWater;
    }

    /// <summary>
    /// Crea la malla de un trozo cuadrado de terreno: una rejilla de alturas, cada cuadrado partido en dos triángulos.
    /// Con caras planas (low poly) cada triángulo lleva su color según el bioma y la pendiente.
    /// Los bordes llevan un «faldón» hacia abajo que tapa las grietas entre chunks de distinto detalle.
    /// Código puro: se ejecuta en otro hilo para no dar tirones.
    /// </summary>
    public static class TerrainMesher
    {
        public static TerrainChunkInfo Build(OpenTerrainGenerator gen, int cx, int cz, float size, int cells, bool flatShading,
                                             TerrainPalette palette, float skirtDepth, LowPolyMeshData mesh)
        {
            mesh.Clear();
            int n = cells + 1;
            float step = size / cells;
            float ox = cx * size, oz = cz * size;
            var samples = new TerrainSample[n * n];
            var info = new TerrainChunkInfo { minHeight = float.MaxValue, maxHeight = float.MinValue };

            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                var s = gen.Sample(ox + i * step, oz + j * step);
                samples[i + j * n] = s;
                if (s.height < info.minHeight) info.minHeight = s.height;
                if (s.height > info.maxHeight) info.maxHeight = s.height;
            }
            info.hasWater = info.minHeight < gen.Settings.waterLevel;

            if (flatShading) BuildFlat(gen, samples, n, step, cx, cz, palette, mesh);
            else BuildSmooth(gen, samples, n, step, ox, oz, palette, mesh);
            if (skirtDepth > 0f) Skirts(gen, samples, n, step, skirtDepth, palette, mesh);
            return info;
        }

        static void BuildFlat(OpenTerrainGenerator gen, TerrainSample[] s, int n, float step, int cx, int cz, TerrainPalette pal, LowPolyMeshData m)
        {
            for (int j = 0; j < n - 1; j++)
            for (int i = 0; i < n - 1; i++)
            {
                var a = s[i + j * n]; var b = s[i + 1 + j * n]; var c = s[i + (j + 1) * n]; var d = s[i + 1 + (j + 1) * n];
                Vector3 pa = new Vector3(i * step, a.height, j * step);
                Vector3 pb = new Vector3((i + 1) * step, b.height, j * step);
                Vector3 pc = new Vector3(i * step, c.height, (j + 1) * step);
                Vector3 pd = new Vector3((i + 1) * step, d.height, (j + 1) * step);
                // Diagonal alternada: el relieve no se ve «rayado» siempre en la misma dirección
                bool alt = ((i + j) & 1) == 0;
                int gi = cx * 100000 + i, gj = cz * 100000 + j;
                if (alt)
                {
                    Tri(gen, pal, m, pa, pc, pd, a, c, d, gi, gj, 0);
                    Tri(gen, pal, m, pa, pd, pb, a, d, b, gi, gj, 1);
                }
                else
                {
                    Tri(gen, pal, m, pa, pc, pb, a, c, b, gi, gj, 0);
                    Tri(gen, pal, m, pb, pc, pd, b, c, d, gi, gj, 1);
                }
            }
        }

        static void Tri(OpenTerrainGenerator gen, TerrainPalette pal, LowPolyMeshData m, Vector3 p0, Vector3 p1, Vector3 p2,
                        TerrainSample s0, TerrainSample s1, TerrainSample s2, int gi, int gj, int k)
        {
            var avg = new TerrainSample
            {
                height = (s0.height + s1.height + s2.height) / 3f,
                river = (s0.river + s1.river + s2.river) / 3f,
                temperature = (s0.temperature + s1.temperature + s2.temperature) / 3f,
                humidity = (s0.humidity + s1.humidity + s2.humidity) / 3f,
                mountain = (s0.mountain + s1.mountain + s2.mountain) / 3f,
            };
            Vector3 nrm = LowPolyMeshData.Normal(p0, p1, p2);
            float slope = (float)(Math.Acos(Math.Max(-1f, Math.Min(1f, nrm.y))) * 180.0 / Math.PI);
            var biome = gen.BiomeOf(avg, slope);
            m.FlatTriangle(p0, p1, p2, Vary(pal.ground[(int)biome], pal.variation, gi, gj, k));
        }

        static void BuildSmooth(OpenTerrainGenerator gen, TerrainSample[] s, int n, float step, float ox, float oz, TerrainPalette pal, LowPolyMeshData m)
        {
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                var t = s[i + j * n];
                float hl = i > 0 ? s[i - 1 + j * n].height : gen.Height(ox + (i - 1) * step, oz + j * step);
                float hr = i < n - 1 ? s[i + 1 + j * n].height : gen.Height(ox + (i + 1) * step, oz + j * step);
                float hd = j > 0 ? s[i + (j - 1) * n].height : gen.Height(ox + i * step, oz + (j - 1) * step);
                float hu = j < n - 1 ? s[i + (j + 1) * n].height : gen.Height(ox + i * step, oz + (j + 1) * step);
                var nrm = new Vector3(hl - hr, 2f * step, hd - hu);
                float len = (float)Math.Sqrt(nrm.x * nrm.x + nrm.y * nrm.y + nrm.z * nrm.z);
                nrm = new Vector3(nrm.x / len, nrm.y / len, nrm.z / len);
                float slope = (float)(Math.Acos(nrm.y) * 180.0 / Math.PI);
                m.vertices.Add(new Vector3(i * step, t.height, j * step));
                m.normals.Add(nrm);
                m.colors.Add(Vary(pal.ground[(int)gen.BiomeOf(t, slope)], pal.variation * 0.5f, i, j, 7));
            }
            for (int j = 0; j < n - 1; j++)
            for (int i = 0; i < n - 1; i++)
            {
                int a = i + j * n, b = a + 1, c = a + n, d = c + 1;
                int[] q = ((i + j) & 1) == 0 ? new[] { a, c, d, a, d, b } : new[] { a, c, b, b, c, d };
                foreach (int v in q) { m.triangles.Add(v); m.colliderTriangles.Add(v); }
            }
        }

        /// <summary>Tiras verticales bajo los cuatro bordes: tapan huecos si el vecino tiene otro nivel de detalle.</summary>
        static void Skirts(OpenTerrainGenerator gen, TerrainSample[] s, int n, float step, float depth, TerrainPalette pal, LowPolyMeshData m)
        {
            for (int k = 0; k < n - 1; k++)
            {
                // Del mismo color que el suelo de ese borde: si asoma por una grieta, no se nota
                Color32 cs = Edge(gen, pal, s[k]), cn = Edge(gen, pal, s[(n - 1) * n + k]), cw = Edge(gen, pal, s[k * n]), ce = Edge(gen, pal, s[k * n + n - 1]);
                // Borde sur (z = 0), mirando a -Z
                Skirt(m, new Vector3(k * step, s[k].height, 0f), new Vector3((k + 1) * step, s[k + 1].height, 0f), depth, cs, false);
                // Borde norte (z = máx), mirando a +Z
                int r = (n - 1) * n;
                Skirt(m, new Vector3(k * step, s[r + k].height, (n - 1) * step), new Vector3((k + 1) * step, s[r + k + 1].height, (n - 1) * step), depth, cn, true);
                // Borde oeste (x = 0), mirando a -X
                Skirt(m, new Vector3(0f, s[k * n].height, k * step), new Vector3(0f, s[(k + 1) * n].height, (k + 1) * step), depth, cw, true);
                // Borde este (x = máx), mirando a +X
                Skirt(m, new Vector3((n - 1) * step, s[k * n + n - 1].height, k * step), new Vector3((n - 1) * step, s[(k + 1) * n + n - 1].height, (k + 1) * step), depth, ce, false);
            }
        }

        static Color32 Edge(OpenTerrainGenerator gen, TerrainPalette pal, TerrainSample t)
        {
            var c = pal.ground[(int)gen.BiomeOf(t, 0f)];
            return new Color32((byte)(c.r * 0.85f), (byte)(c.g * 0.85f), (byte)(c.b * 0.85f), c.a);
        }

        static void Skirt(LowPolyMeshData m, Vector3 a, Vector3 b, float depth, Color32 col, bool flip)
        {
            var a2 = new Vector3(a.x, a.y - depth, a.z);
            var b2 = new Vector3(b.x, b.y - depth, b.z);
            if (!flip) { m.FlatTriangle(a, b, b2, col, false); m.FlatTriangle(a, b2, a2, col, false); }
            else { m.FlatTriangle(a, b2, b, col, false); m.FlatTriangle(a, a2, b2, col, false); }
        }

        static Color32 Vary(Color32 c, float amount, int x, int y, int k)
        {
            float f = 1f + (WorldNoise.Hash01(x, y, k, 99) - 0.5f) * 2f * amount;
            return new Color32(Clamp(c.r * f), Clamp(c.g * f), Clamp(c.b * f), c.a);   // alfa = cuánta textura de suelo
        }

        static byte Clamp(float v) { return (byte)(v < 0f ? 0 : v > 255f ? 255 : v); }

        /// <summary>
        /// Altura exacta de la MALLA en (x, z): la del ruido varía un poco entre los vértices, y lo que pisa el jugador
        /// es la malla. Usa la misma partición en triángulos que Build.
        /// </summary>
        public static float SurfaceHeight(OpenTerrainGenerator gen, float x, float z, float size, int cells)
        {
            float step = size / cells;
            int i = (int)Math.Floor(x / step), j = (int)Math.Floor(z / step);
            float fx = x / step - i, fz = z / step - j;
            float ha = gen.Height(i * step, j * step), hb = gen.Height((i + 1) * step, j * step);
            float hc = gen.Height(i * step, (j + 1) * step), hd = gen.Height((i + 1) * step, (j + 1) * step);
            // Índices locales del chunk (para saber qué diagonal tiene esta casilla)
            int li = ((i % cells) + cells) % cells, lj = ((j % cells) + cells) % cells;
            if (((li + lj) & 1) == 0)
            {
                // diagonal a-d: triángulo (a, c, d) si fz > fx, si no (a, d, b)
                return fz > fx ? ha + (hd - hc) * fx + (hc - ha) * fz : ha + (hb - ha) * fx + (hd - hb) * fz;
            }
            // diagonal b-c: triángulo (a, c, b) si fx + fz < 1, si no (b, c, d)
            return fx + fz < 1f ? ha + (hb - ha) * fx + (hc - ha) * fz : hd + (hc - hd) * (1f - fx) + (hb - hd) * (1f - fz);
        }

        /// <summary>Un cuadrado de agua a la altura del agua, del tamaño del chunk.</summary>
        public static void BuildWater(float size, float waterLevel, int cells, LowPolyMeshData m)
        {
            m.Clear();
            float step = size / cells;
            var col = new Color32(255, 255, 255, 255);
            var up = new Vector3(0f, 1f, 0f);
            for (int j = 0; j <= cells; j++)
            for (int i = 0; i <= cells; i++)
            {
                m.vertices.Add(new Vector3(i * step, waterLevel, j * step));
                m.normals.Add(up);
                m.colors.Add(col);
            }
            int n = cells + 1;
            for (int j = 0; j < cells; j++)
            for (int i = 0; i < cells; i++)
            {
                int a = i + j * n, b = a + 1, c = a + n, d = c + 1;
                m.triangles.Add(a); m.triangles.Add(c); m.triangles.Add(d);
                m.triangles.Add(a); m.triangles.Add(d); m.triangles.Add(b);
            }
        }
    }
}
