using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRKit
{
    /// <summary>Icono de algo en el mapa.</summary>
    public enum MapIcon { Cueva, Lugar, Pueblo, Casa, Tesoro, Peligro, Personaje, Marca }

    /// <summary>Qué sabe el jugador de algo del mapa.</summary>
    public enum MapFeatureState { Oculto, Pista, Visto, Descubierto }

    /// <summary>Algo que sale en el mapa: una cueva, un pueblo, una marca del jugador…</summary>
    public struct MapFeature
    {
        /// <summary>Identificador único y estable (el mismo con la misma semilla). Es el que se guarda como «descubierto».</summary>
        public string id;
        public string name;
        public float x, z;
        public MapIcon icon;
        /// <summary>Si no lo has visto aún, el mapa da una pista («?») cuando estás cerca.</summary>
        public bool hint;
        /// <summary>Se descubre solo al acercarse a menos de discoverRadius (las cuevas no: las descubre CaveSystem).</summary>
        public bool autoDiscover;
        public float discoverRadius;
    }

    /// <summary>
    /// Pone cosas en el mapa a partir de la semilla, sin que estén cargadas (como las cuevas).
    /// Para que salgan en el mapa los pueblos, ruinas… que generéis: implementad esto y llamad a WorldMap.AddSource.
    /// OJO: se llama desde otro hilo. Solo podéis usar el generador y cálculos (nada de GameObjects ni Random de Unity).
    /// </summary>
    public interface IMapFeatureSource
    {
        /// <summary>Añade a output lo que hay en la región [rx·size, (rx+1)·size) × [rz·size, (rz+1)·size).</summary>
        void FeaturesIn(OpenTerrainGenerator gen, int rx, int rz, float size, List<MapFeature> output);
    }

    /// <summary>Las cuevas del OpenWorld, como cosas del mapa (con pista cuando estás cerca).</summary>
    public sealed class CaveMapSource : IMapFeatureSource
    {
        readonly float m_Spacing, m_Chance;
        public CaveMapSource(float spacing, float chance) { m_Spacing = spacing; m_Chance = chance; }

        public void FeaturesIn(OpenTerrainGenerator gen, int rx, int rz, float size, List<MapFeature> output)
        {
            var sites = new List<CaveSite>();
            CaveGen.Sites(gen, rx, rz, size, m_Spacing, m_Chance, sites);
            foreach (var s in sites)
                output.Add(new MapFeature { id = "cueva:" + s.id, name = s.name, x = s.x, z = s.z, icon = MapIcon.Cueva, hint = true });
        }
    }

    /// <summary>
    /// Lo explorado: cada chunk se divide en 4×4 casillas y se guarda un bit por casilla (16 bits por chunk).
    /// Se puede leer desde otros hilos.
    /// </summary>
    public sealed class ExploredMap
    {
        public const int Cells = 4;
        public readonly float chunkSize;
        readonly Dictionary<long, ushort> m_Masks = new Dictionary<long, ushort>();
        readonly object m_Lock = new object();
        int m_Bits;

        public ExploredMap(float chunkSize) { this.chunkSize = chunkSize; }

        public static long Key(int cx, int cz) { return ((long)cx << 32) ^ (uint)cz; }
        public float CellSize { get { return chunkSize / Cells; } }
        /// <summary>Metros cuadrados explorados.</summary>
        public float Area { get { lock (m_Lock) return m_Bits * CellSize * CellSize; } }

        /// <summary>Marca como explorado un círculo. Devuelve cuántas casillas nuevas.</summary>
        public int Reveal(float x, float z, float radius)
        {
            float cell = CellSize;
            int i0 = (int)Math.Floor((x - radius) / cell), i1 = (int)Math.Floor((x + radius) / cell);
            int j0 = (int)Math.Floor((z - radius) / cell), j1 = (int)Math.Floor((z + radius) / cell);
            float r2 = radius * radius;
            int added = 0;
            lock (m_Lock)
            {
                for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                {
                    float dx = (i + 0.5f) * cell - x, dz = (j + 0.5f) * cell - z;
                    if (dx * dx + dz * dz > r2) continue;
                    int cx = FloorDiv(i, Cells), cz = FloorDiv(j, Cells);
                    int bit = 1 << ((j - cz * Cells) * Cells + (i - cx * Cells));
                    long k = Key(cx, cz);
                    ushort m;
                    m_Masks.TryGetValue(k, out m);
                    if ((m & bit) != 0) continue;
                    m_Masks[k] = (ushort)(m | bit);
                    added++;
                }
                m_Bits += added;
            }
            return added;
        }

        public ushort Mask(int cx, int cz)
        {
            ushort m;
            lock (m_Lock) m_Masks.TryGetValue(Key(cx, cz), out m);
            return m;
        }

        public bool IsExplored(float x, float z)
        {
            float cell = CellSize;
            int i = (int)Math.Floor(x / cell), j = (int)Math.Floor(z / cell);
            int cx = FloorDiv(i, Cells), cz = FloorDiv(j, Cells);
            return (Mask(cx, cz) & (1 << ((j - cz * Cells) * Cells + (i - cx * Cells)))) != 0;
        }

        /// <summary>Copia las máscaras de un rectángulo de chunks (para leerlas luego sin bloquear).</summary>
        public void CopyMasks(int cx0, int cz0, int w, int h, ushort[] output)
        {
            lock (m_Lock)
                for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                {
                    ushort m;
                    m_Masks.TryGetValue(Key(cx0 + i, cz0 + j), out m);
                    output[j * w + i] = m;
                }
        }

        public void Export(List<long> keys, List<int> masks)
        {
            keys.Clear(); masks.Clear();
            lock (m_Lock) foreach (var kv in m_Masks) { keys.Add(kv.Key); masks.Add(kv.Value); }
        }

        public void Import(List<long> keys, List<int> masks)
        {
            lock (m_Lock)
            {
                m_Masks.Clear(); m_Bits = 0;
                if (keys == null || masks == null) return;
                for (int i = 0; i < keys.Count && i < masks.Count; i++)
                {
                    ushort m = (ushort)masks[i];
                    m_Masks[keys[i]] = m;
                    for (int b = m; b != 0; b &= b - 1) m_Bits++;
                }
            }
        }

        public void Clear() { lock (m_Lock) { m_Masks.Clear(); m_Bits = 0; } }

        static int FloorDiv(int a, int b) { return a >= 0 ? a / b : -((-a + b - 1) / b); }
    }

    /// <summary>
    /// Dibuja el mapa en un array de píxeles (Color32), sin Unity: se puede hacer en otro hilo.
    /// Fila 0 = sur; columna 0 = oeste. Cada chunk es un «azulejo» de 16×16 píxeles con relieve, agua y curvas de nivel.
    /// </summary>
    public static class MapRaster
    {
        public const int TilePx = 16;

        public static readonly Color32 FogColor = new Color32(36, 41, 54, 255);
        public static readonly Color32 PendingColor = new Color32(92, 100, 96, 255);
        public static readonly Color32 RimColor = new Color32(222, 206, 160, 255);
        static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        static readonly Color32 Ink = new Color32(28, 24, 22, 255);
        static readonly Color32 Paper = new Color32(245, 238, 215, 255);

        // ------------------------------------------------------------------ Azulejos

        /// <summary>Un chunk del mapa: relieve sombreado, color del bioma, agua por profundidad y curvas de nivel.</summary>
        public static Color32[] BuildTile(OpenTerrainGenerator gen, int cx, int cz, float size, Color32[] biomeColors, float contour = 8f)
        {
            const int n = TilePx;
            int m = n + 2;
            float step = size / n;
            float wl = gen.Settings.waterLevel;
            var h = new float[m * m];
            var samples = new TerrainSample[n * n];
            float x0 = cx * size, z0 = cz * size;
            for (int j = -1; j <= n; j++)
            for (int i = -1; i <= n; i++)
            {
                var s = gen.Sample(x0 + (i + 0.5f) * step, z0 + (j + 0.5f) * step);
                h[(j + 1) * m + i + 1] = s.height;
                if (i >= 0 && j >= 0 && i < n && j < n) samples[j * n + i] = s;
            }

            // Luz del noroeste, como en los mapas de papel
            float lx = -0.55f, ly = 0.70f, lz = 0.45f;
            float ll = (float)Math.Sqrt(lx * lx + ly * ly + lz * lz); lx /= ll; ly /= ll; lz /= ll;

            var px = new Color32[n * n];
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                int c = (j + 1) * m + i + 1;
                float hc = h[c];
                float dx = (h[c + 1] - h[c - 1]) / (2f * step);
                float dz = (h[c + m] - h[c - m]) / (2f * step);
                Color32 col;
                if (hc < wl - 0.05f)
                {
                    float depth = Clamp01((wl - hc) / 7f);
                    col = Lerp(new Color32(118, 178, 212, 255), new Color32(38, 88, 150, 255), depth);
                    // Orilla: un borde claro
                    if (h[c + 1] >= wl || h[c - 1] >= wl || h[c + m] >= wl || h[c - m] >= wl) col = Lerp(col, new Color32(214, 232, 238, 255), 0.55f);
                }
                else
                {
                    float slope = (float)(Math.Atan(Math.Sqrt(dx * dx + dz * dz)) * 180.0 / Math.PI);
                    var biome = gen.BiomeOf(samples[j * n + i], slope);
                    col = biomeColors[(int)biome];
                    float nl = 1f / (float)Math.Sqrt(dx * dx + 1f + dz * dz);
                    float lam = (-dx * lx + ly - dz * lz) * nl;
                    float shade = 0.5f + 0.62f * lam;
                    if (contour > 0f)
                    {
                        int k = (int)Math.Floor((hc - wl) / contour);
                        if (k != (int)Math.Floor((h[c + 1] - wl) / contour) || k != (int)Math.Floor((h[c + m] - wl) / contour)) shade *= 0.84f;
                    }
                    col = Scale(col, Clamp(shade, 0.42f, 1.25f));
                }
                px[j * n + i] = col;
            }
            return px;
        }

        // ------------------------------------------------------------------ Componer

        public struct View
        {
            public float centerX, centerZ;
            /// <summary>Metros por píxel.</summary>
            public float mpp;
            public int width, height;
            /// <summary>Redondo (minimapa): fuera del círculo, transparente.</summary>
            public bool circle;
        }

        /// <summary>Devuelve el azulejo de un chunk, o null si aún no está hecho.</summary>
        public delegate Color32[] TileGetter(int cx, int cz);

        /// <summary>
        /// Dibuja lo explorado (con su azulejo) y la niebla en lo que no. Los chunks explorados sin azulejo
        /// se añaden a missing (para hacerlos y volver a componer).
        /// </summary>
        public static void Compose(Color32[] dst, View v, ExploredMap explored, TileGetter tiles, HashSet<long> missing)
        {
            int w = v.width, hgt = v.height;
            float size = explored.chunkSize, cell = explored.CellSize, mpp = v.mpp;
            float left = v.centerX - w * 0.5f * mpp, bottom = v.centerZ - hgt * 0.5f * mpp;

            int cx0 = (int)Math.Floor((left - cell) / size), cz0 = (int)Math.Floor((bottom - cell) / size);
            int cx1 = (int)Math.Floor((left + w * mpp + cell) / size), cz1 = (int)Math.Floor((bottom + hgt * mpp + cell) / size);
            int gw = cx1 - cx0 + 1, gh = cz1 - cz0 + 1;
            var masks = new ushort[gw * gh];
            explored.CopyMasks(cx0, cz0, gw, gh, masks);
            var tileGrid = new Color32[gw * gh][];
            for (int j = 0; j < gh; j++)
            for (int i = 0; i < gw; i++)
                if (masks[j * gw + i] != 0)
                {
                    var t = tiles(cx0 + i, cz0 + j);
                    tileGrid[j * gw + i] = t;
                    if (t == null && missing != null) missing.Add(ExploredMap.Key(cx0 + i, cz0 + j));
                }

            float cxPix = w * 0.5f, czPix = hgt * 0.5f, radius = Math.Min(w, hgt) * 0.5f;
            float rim = Math.Max(1.5f, radius * 0.045f);
            float tpx = TilePx / size;
            float jit = cell * 0.45f;

            for (int j = 0; j < hgt; j++)
            {
                float z = bottom + (j + 0.5f) * mpp;
                for (int i = 0; i < w; i++)
                {
                    int o = j * w + i;
                    if (v.circle)
                    {
                        float ddx = i + 0.5f - cxPix, ddz = j + 0.5f - czPix;
                        float d = (float)Math.Sqrt(ddx * ddx + ddz * ddz);
                        if (d > radius) { dst[o] = Clear; continue; }
                        if (d > radius - rim) { dst[o] = RimColor; continue; }
                    }
                    float x = left + (i + 0.5f) * mpp;
                    // Borde de la niebla irregular: se mira lo explorado un poco movido al azar
                    long wi = (long)Math.Floor(x / Math.Max(mpp, 1f)), wj = (long)Math.Floor(z / Math.Max(mpp, 1f));
                    float ex = x + (Hash(wi, wj, 1) - 0.5f) * 2f * jit;
                    float ez = z + (Hash(wi, wj, 2) - 0.5f) * 2f * jit;
                    if (IsSet(masks, gw, gh, cx0, cz0, size, cell, ex, ez))
                    {
                        int tcx = (int)Math.Floor(x / size), tcz = (int)Math.Floor(z / size);
                        int gi = tcx - cx0, gj = tcz - cz0;
                        Color32[] tile = gi >= 0 && gj >= 0 && gi < gw && gj < gh ? tileGrid[gj * gw + gi] : null;
                        if (tile == null) { dst[o] = PendingColor; continue; }
                        int pi = Math.Min(TilePx - 1, (int)((x - tcx * size) * tpx));
                        int pj = Math.Min(TilePx - 1, (int)((z - tcz * size) * tpx));
                        dst[o] = tile[pj * TilePx + pi];
                    }
                    else
                    {
                        float nz = Hash(wi >> 2, wj >> 2, 3);
                        dst[o] = Scale(FogColor, 0.9f + 0.2f * nz);
                    }
                }
            }
        }

        static bool IsSet(ushort[] masks, int gw, int gh, int cx0, int cz0, float size, float cell, float x, float z)
        {
            int ci = (int)Math.Floor(x / cell), cj = (int)Math.Floor(z / cell);
            int cx = (int)Math.Floor(x / size), cz = (int)Math.Floor(z / size);
            int gi = cx - cx0, gj = cz - cz0;
            if (gi < 0 || gj < 0 || gi >= gw || gj >= gh) return false;
            int li = ci - cx * ExploredMap.Cells, lj = cj - cz * ExploredMap.Cells;
            if (li < 0) li = 0; if (li > 3) li = 3; if (lj < 0) lj = 0; if (lj > 3) lj = 3;
            return (masks[gj * gw + gi] & (1 << (lj * ExploredMap.Cells + li))) != 0;
        }

        /// <summary>De coordenadas del mundo a píxel de la vista.</summary>
        public static void ToPixel(View v, float x, float z, out float px, out float pz)
        {
            px = (x - v.centerX) / v.mpp + v.width * 0.5f;
            pz = (z - v.centerZ) / v.mpp + v.height * 0.5f;
        }

        // ------------------------------------------------------------------ Iconos

        public static void DrawFeature(Color32[] dst, int w, int h, float x, float y, MapIcon icon, MapFeatureState state, float s)
        {
            if (state == MapFeatureState.Oculto) return;
            bool seen = state == MapFeatureState.Visto;
            switch (icon)
            {
                case MapIcon.Cueva:
                    Disc(dst, w, h, x, y, s * 1.05f, Paper);
                    HalfDisc(dst, w, h, x, y - s * 0.5f, s * 0.92f, seen ? new Color32(110, 100, 92, 255) : new Color32(48, 34, 28, 255));
                    break;
                case MapIcon.Lugar:
                    Star(dst, w, h, x, y, s * 1.25f, Ink);
                    Star(dst, w, h, x, y, s * 0.95f, seen ? new Color32(190, 180, 150, 255) : new Color32(250, 204, 60, 255));
                    break;
                case MapIcon.Pueblo:
                case MapIcon.Casa:
                    var roof = icon == MapIcon.Pueblo ? new Color32(196, 74, 52, 255) : new Color32(60, 110, 200, 255);
                    if (seen) roof = new Color32(150, 140, 130, 255);
                    Disc(dst, w, h, x, y, s * 1.1f, Paper);
                    Rect(dst, w, h, x - s * 0.5f, y - s * 0.65f, s, s * 0.6f, Ink);
                    Tri(dst, w, h, x - s * 0.8f, y - 0.05f * s, x + s * 0.8f, y - 0.05f * s, x, y + s * 0.75f, roof);
                    break;
                case MapIcon.Tesoro:
                    Diamond(dst, w, h, x, y, s * 1.1f, Ink);
                    Diamond(dst, w, h, x, y, s * 0.8f, seen ? new Color32(190, 180, 150, 255) : new Color32(255, 214, 70, 255));
                    break;
                case MapIcon.Peligro:
                    Tri(dst, w, h, x - s, y - s * 0.8f, x + s, y - s * 0.8f, x, y + s, Ink);
                    Tri(dst, w, h, x - s * 0.7f, y - s * 0.6f, x + s * 0.7f, y - s * 0.6f, x, y + s * 0.65f, seen ? new Color32(170, 120, 110, 255) : new Color32(220, 50, 40, 255));
                    break;
                case MapIcon.Personaje:
                    Disc(dst, w, h, x, y, s, Ink);
                    Disc(dst, w, h, x, y, s * 0.75f, seen ? new Color32(190, 170, 150, 255) : new Color32(245, 150, 50, 255));
                    break;
                default: // Marca
                    Disc(dst, w, h, x, y + s * 0.4f, s * 0.95f, Ink);
                    Disc(dst, w, h, x, y + s * 0.4f, s * 0.75f, new Color32(226, 54, 64, 255));
                    Disc(dst, w, h, x, y + s * 0.4f, s * 0.3f, Paper);
                    Tri(dst, w, h, x - s * 0.45f, y + s * 0.1f, x + s * 0.45f, y + s * 0.1f, x, y - s * 0.9f, new Color32(226, 54, 64, 255));
                    break;
            }
        }

        /// <summary>Pista: un círculo de rayas con «?» donde más o menos está.</summary>
        public static void DrawHint(Color32[] dst, int w, int h, float x, float y, float radius, float s)
        {
            var c = new Color32(255, 216, 90, 255);
            int seg = Math.Max(10, (int)(radius * 0.9f));
            for (int k = 0; k < seg; k++)
            {
                if ((k & 1) == 1) continue;
                float a0 = (float)(k * 2.0 * Math.PI / seg), a1 = (float)((k + 1) * 2.0 * Math.PI / seg);
                Line(dst, w, h, x + (float)Math.Cos(a0) * radius, y + (float)Math.Sin(a0) * radius,
                                x + (float)Math.Cos(a1) * radius, y + (float)Math.Sin(a1) * radius, Math.Max(1f, s * 0.22f), c);
            }
            int scale = Math.Max(1, (int)Math.Round(s / 3.5f));
            Text(dst, w, h, (int)(x - 2.5f * scale), (int)(y - 3.5f * scale), "?", c, scale, true);
        }

        /// <summary>El jugador: una flecha que apunta hacia donde mira (yaw 0 = norte).</summary>
        public static void DrawPlayer(Color32[] dst, int w, int h, float x, float y, float yawDeg, float s)
        {
            float a = (float)(yawDeg * Math.PI / 180.0);
            float fx = (float)Math.Sin(a), fy = (float)Math.Cos(a);   // adelante
            float rx = fy, ry = -fx;                                   // derecha
            Tri(dst, w, h, x + fx * s * 1.5f, y + fy * s * 1.5f, x - fx * s + rx * s * 1.15f, y - fy * s + ry * s * 1.15f, x - fx * s - rx * s * 1.15f, y - fy * s - ry * s * 1.15f, Ink);
            Tri(dst, w, h, x + fx * s * 1.05f, y + fy * s * 1.05f, x - fx * s * 0.65f + rx * s * 0.8f, y - fy * s * 0.65f + ry * s * 0.8f, x - fx * s * 0.65f - rx * s * 0.8f, y - fy * s * 0.65f - ry * s * 0.8f, new Color32(255, 255, 255, 255));
        }

        /// <summary>Barra de escala abajo a la izquierda (50 m, 100 m, 200 m…).</summary>
        public static void DrawScaleBar(Color32[] dst, int w, int h, float mpp, int scale)
        {
            float[] nice = { 10, 20, 50, 100, 200, 500, 1000, 2000, 5000 };
            float target = w * 0.22f * mpp, meters = nice[0];
            foreach (var n in nice) if (n <= target) meters = n;
            int len = (int)(meters / mpp);
            int x0 = 10 * scale, y0 = 10 * scale;
            Rect(dst, w, h, x0 - scale, y0 - scale, len + 2 * scale, 4 * scale, Ink);
            Rect(dst, w, h, x0, y0, len / 2, 2 * scale, Paper);
            string label = meters >= 1000 ? (meters / 1000f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " km" : meters.ToString("0") + " m";
            Text(dst, w, h, x0, y0 + 6 * scale, label, Paper, scale, true);
        }

        /// <summary>Una «N» en el borde norte.</summary>
        public static void DrawNorth(Color32[] dst, int w, int h, int scale, bool circle)
        {
            int x = w / 2 - (5 * scale) / 2, y = h - (circle ? 4 : 6) * scale - 7 * scale;
            Disc(dst, w, h, w * 0.5f, y + 3.5f * scale, 5.5f * scale, Ink);
            Text(dst, w, h, x, y, "N", Paper, scale, false);
        }

        // ------------------------------------------------------------------ Texto (fuente de 5×7)

        static readonly Dictionary<char, string[]> Font = new Dictionary<char, string[]>
        {
            { '0', new[] { ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###." } },
            { '1', new[] { "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###." } },
            { '2', new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####" } },
            { '3', new[] { "####.", "....#", "....#", ".###.", "....#", "....#", "####." } },
            { '4', new[] { "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#." } },
            { '5', new[] { "#####", "#....", "####.", "....#", "....#", "#...#", ".###." } },
            { '6', new[] { "..##.", ".#...", "#....", "####.", "#...#", "#...#", ".###." } },
            { '7', new[] { "#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..." } },
            { '8', new[] { ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###." } },
            { '9', new[] { ".###.", "#...#", "#...#", ".####", "....#", "...#.", ".##.." } },
            { '?', new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".....", "..#.." } },
            { 'N', new[] { "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#" } },
            { 'm', new[] { ".....", ".....", "##.#.", "#.#.#", "#.#.#", "#.#.#", "#.#.#" } },
            { 'k', new[] { "#....", "#....", "#..#.", "#.#..", "##...", "#.#..", "#..#." } },
            { 'f', new[] { "..##.", ".#...", "###..", ".#...", ".#...", ".#...", ".#..." } },
            { 'p', new[] { ".....", ".....", "####.", "#...#", "####.", "#....", "#...." } },
            { 's', new[] { ".....", ".....", ".####", "#....", ".###.", "....#", "####." } },
            { '.', new[] { ".....", ".....", ".....", ".....", ".....", ".##..", ".##.." } },
            { ' ', new[] { ".....", ".....", ".....", ".....", ".....", ".....", "....." } },
        };

        /// <summary>Escribe texto (solo números, N, m, k, ?, «fps»). (x, y) = esquina de abajo a la izquierda.</summary>
        public static void Text(Color32[] dst, int w, int h, int x, int y, string text, Color32 color, int scale, bool outline)
        {
            if (outline)
                for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                    if (ox != 0 || oy != 0) Text(dst, w, h, x + ox * scale, y + oy * scale, text, Ink, scale, false);
            int cx = x;
            foreach (char ch in text)
            {
                string[] g;
                if (Font.TryGetValue(ch, out g))
                    for (int r = 0; r < 7; r++)
                    for (int c = 0; c < 5; c++)
                        if (g[r][c] == '#') Rect(dst, w, h, cx + c * scale, y + (6 - r) * scale, scale, scale, color);
                cx += 6 * scale;
            }
        }

        // ------------------------------------------------------------------ Formas

        static void Put(Color32[] d, int w, int h, int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            int o = y * w + x;
            if (d[o].a == 0) return;    // fuera del círculo del minimapa
            d[o] = c;
        }

        public static void Rect(Color32[] d, int w, int h, float x, float y, float rw, float rh, Color32 c)
        {
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y), x1 = (int)Math.Ceiling(x + rw), y1 = (int)Math.Ceiling(y + rh);
            for (int j = y0; j < y1; j++) for (int i = x0; i < x1; i++) Put(d, w, h, i, j, c);
        }

        public static void Disc(Color32[] d, int w, int h, float x, float y, float r, Color32 c)
        {
            int x0 = (int)Math.Floor(x - r), x1 = (int)Math.Ceiling(x + r), y0 = (int)Math.Floor(y - r), y1 = (int)Math.Ceiling(y + r);
            float r2 = r * r;
            for (int j = y0; j <= y1; j++)
            for (int i = x0; i <= x1; i++)
            {
                float dx = i + 0.5f - x, dy = j + 0.5f - y;
                if (dx * dx + dy * dy <= r2) Put(d, w, h, i, j, c);
            }
        }

        static void HalfDisc(Color32[] d, int w, int h, float x, float y, float r, Color32 c)
        {
            int x0 = (int)Math.Floor(x - r), x1 = (int)Math.Ceiling(x + r), y1 = (int)Math.Ceiling(y + r);
            float r2 = r * r;
            for (int j = (int)Math.Floor(y); j <= y1; j++)
            for (int i = x0; i <= x1; i++)
            {
                float dx = i + 0.5f - x, dy = j + 0.5f - y;
                if (dy >= 0f && dx * dx + dy * dy <= r2) Put(d, w, h, i, j, c);
            }
        }

        static void Diamond(Color32[] d, int w, int h, float x, float y, float r, Color32 c)
        {
            int x0 = (int)Math.Floor(x - r), x1 = (int)Math.Ceiling(x + r), y0 = (int)Math.Floor(y - r), y1 = (int)Math.Ceiling(y + r);
            for (int j = y0; j <= y1; j++)
            for (int i = x0; i <= x1; i++)
                if (Math.Abs(i + 0.5f - x) + Math.Abs(j + 0.5f - y) <= r) Put(d, w, h, i, j, c);
        }

        static void Star(Color32[] d, int w, int h, float x, float y, float r, Color32 c)
        {
            for (int k = 0; k < 5; k++)
            {
                double a = Math.PI / 2 + k * 2 * Math.PI / 5;
                double a1 = a + Math.PI / 5, a0 = a - Math.PI / 5;
                Tri(d, w, h, x, y, x + (float)Math.Cos(a) * r, y + (float)Math.Sin(a) * r, x + (float)Math.Cos(a1) * r * 0.42f, y + (float)Math.Sin(a1) * r * 0.42f, c);
                Tri(d, w, h, x, y, x + (float)Math.Cos(a0) * r * 0.42f, y + (float)Math.Sin(a0) * r * 0.42f, x + (float)Math.Cos(a) * r, y + (float)Math.Sin(a) * r, c);
            }
        }

        public static void Tri(Color32[] d, int w, int h, float ax, float ay, float bx, float by, float cx, float cy, Color32 col)
        {
            int x0 = (int)Math.Floor(Math.Min(ax, Math.Min(bx, cx))), x1 = (int)Math.Ceiling(Math.Max(ax, Math.Max(bx, cx)));
            int y0 = (int)Math.Floor(Math.Min(ay, Math.Min(by, cy))), y1 = (int)Math.Ceiling(Math.Max(ay, Math.Max(by, cy)));
            float area = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
            if (Math.Abs(area) < 1e-5f) return;
            for (int j = y0; j <= y1; j++)
            for (int i = x0; i <= x1; i++)
            {
                float px = i + 0.5f, py = j + 0.5f;
                float w0 = ((bx - px) * (cy - py) - (by - py) * (cx - px)) / area;
                float w1 = ((cx - px) * (ay - py) - (cy - py) * (ax - px)) / area;
                float w2 = 1f - w0 - w1;
                if (w0 >= 0f && w1 >= 0f && w2 >= 0f) Put(d, w, h, i, j, col);
            }
        }

        static void Line(Color32[] d, int w, int h, float x0, float y0, float x1, float y1, float r, Color32 c)
        {
            float len = (float)Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
            int steps = Math.Max(1, (int)(len * 2f));
            for (int k = 0; k <= steps; k++)
            {
                float t = k / (float)steps;
                Disc(d, w, h, x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, r, c);
            }
        }

        // ------------------------------------------------------------------ Útiles

        static float Hash(long x, long z, int salt)
        {
            unchecked
            {
                ulong h = (ulong)(x * 374761393L + z * 668265263L + salt * 2147483647L);
                h = (h ^ (h >> 13)) * 1274126177UL;
                h ^= h >> 16;
                return (h & 0xffffff) / 16777216f;
            }
        }

        static float Clamp01(float v) { return v < 0f ? 0f : v > 1f ? 1f : v; }
        static float Clamp(float v, float a, float b) { return v < a ? a : v > b ? b : v; }
        static Color32 Lerp(Color32 a, Color32 b, float t)
        {
            return new Color32((byte)(a.r + (b.r - a.r) * t), (byte)(a.g + (b.g - a.g) * t), (byte)(a.b + (b.b - a.b) * t), 255);
        }
        static Color32 Scale(Color32 c, float k)
        {
            return new Color32((byte)Math.Min(255f, c.r * k), (byte)Math.Min(255f, c.g * k), (byte)Math.Min(255f, c.b * k), 255);
        }
    }
}
