using System;

namespace VRKit
{
    /// <summary>Cómo es el terreno. Se edita en el Inspector del VoxelWorld.</summary>
    [Serializable]
    public class VoxelTerrainSettings
    {
        [UnityEngine.Tooltip("Mundo plano (modo creativo, construir sin colinas).")]
        public bool flat = false;
        [UnityEngine.Tooltip("Altura media del suelo, en bloques.")]
        public int baseHeight = 20;
        [UnityEngine.Tooltip("Cuánto suben y bajan las colinas.")]
        public float hillHeight = 16f;
        [UnityEngine.Tooltip("Tamaño de las colinas: más pequeño = colinas más anchas.")]
        public float hillScale = 0.025f;
        [UnityEngine.Tooltip("Altura extra de las montañas (0 = sin montañas).")]
        public float mountainHeight = 22f;
        public float mountainScale = 0.008f;
        [UnityEngine.Range(1, 6)] public int octaves = 4;

        [UnityEngine.Header("Capas")]
        [UnityEngine.Tooltip("Por debajo de esta altura, la superficie es arena (playas y desiertos bajos).")]
        public int sandLevel = 17;
        [UnityEngine.Tooltip("Por encima de esta altura, la superficie es nieve.")]
        public int snowLevel = 34;
        [UnityEngine.Tooltip("Bloques de tierra bajo la hierba antes de la piedra.")]
        public int dirtDepth = 3;

        [UnityEngine.Header("Decoración")]
        [UnityEngine.Range(0f, 0.1f)] public float treeChance = 0.007f;
        [UnityEngine.Range(0f, 0.3f)] public float tallGrassChance = 0.08f;
        [UnityEngine.Range(0f, 0.1f)] public float flowerChance = 0.02f;
        [UnityEngine.Range(0f, 0.05f)] public float coalChance = 0.012f;
        [UnityEngine.Range(0f, 0.05f)] public float ironChance = 0.006f;

        [UnityEngine.Header("Cuevas (más bloques que dibujar: probad el rendimiento)")]
        public bool caves = false;
        [UnityEngine.Range(0.3f, 0.9f)] public float caveThreshold = 0.62f;
        public float caveScale = 0.07f;
    }

    /// <summary>Qué id tiene cada bloque que usa el generador (0 = no usarlo). Lo rellena el VoxelWorld.</summary>
    public struct VoxelTerrainIds
    {
        public byte grass, dirt, stone, sand, snow, log, leaves, coal, iron, bedrock, flower, tallGrass;
    }

    /// <summary>
    /// Genera los bloques de una columna de chunk. Código puro: la misma semilla da siempre el mismo terreno,
    /// por eso al guardar solo hace falta guardar lo que el jugador ha cambiado.
    /// </summary>
    public sealed class VoxelTerrainGenerator
    {
        readonly VoxelTerrainSettings m_S;
        readonly VoxelTerrainIds m_Ids;
        readonly VoxelNoise m_Noise;
        readonly int m_Seed;

        public VoxelTerrainGenerator(VoxelTerrainSettings settings, VoxelTerrainIds ids, int seed)
        {
            m_S = settings ?? new VoxelTerrainSettings();
            m_Ids = ids;
            m_Seed = seed;
            m_Noise = new VoxelNoise(seed);
        }

        /// <summary>Altura de la superficie (el bloque más alto) en una columna del mundo.</summary>
        public int SurfaceHeight(int wx, int wz, int worldHeight)
        {
            int maxH = worldHeight - 9; // hueco para los árboles
            if (m_S.flat) return Clamp(m_S.baseHeight, 1, maxH);
            float hills = m_Noise.Fbm(wx * m_S.hillScale, wz * m_S.hillScale, m_S.octaves) * m_S.hillHeight;
            float m = m_Noise.Fbm(wx * m_S.mountainScale + 311.7f, wz * m_S.mountainScale - 127.3f, 3);
            float mountains = m > 0.05f ? (m - 0.05f) * (m - 0.05f) * 8f * m_S.mountainHeight : 0f;
            return Clamp((int)Math.Round(m_S.baseHeight + hills + mountains), 1, maxH);
        }

        byte SurfaceBlock(int h)
        {
            if (h <= m_S.sandLevel && m_Ids.sand != 0) return m_Ids.sand;
            if (h >= m_S.snowLevel && m_Ids.snow != 0) return m_Ids.snow;
            return m_Ids.grass;
        }

        /// <summary>Rellena data (sx × h × sz, índice x + sx·(z + sz·y)) para el chunk (cx, cz).</summary>
        public void Generate(byte[] data, int sx, int h, int sz, int cx, int cz)
        {
            Array.Clear(data, 0, data.Length);
            int ox = cx * sx, oz = cz * sz;

            for (int z = 0; z < sz; z++)
            for (int x = 0; x < sx; x++)
            {
                int wx = ox + x, wz = oz + z;
                int top = SurfaceHeight(wx, wz, h);
                byte surface = SurfaceBlock(top);
                for (int y = 0; y <= top; y++)
                {
                    byte id;
                    if (y == 0 && m_Ids.bedrock != 0) id = m_Ids.bedrock;
                    else if (y == top) id = surface;
                    else if (y > top - m_S.dirtDepth) id = surface == m_Ids.sand ? m_Ids.sand : m_Ids.dirt;
                    else
                    {
                        id = m_Ids.stone;
                        float r = VoxelNoise.Hash01(wx, y, wz, m_Seed + 17);
                        if (m_Ids.iron != 0 && y < 24 && r < m_S.ironChance) id = m_Ids.iron;
                        else if (m_Ids.coal != 0 && r > 1f - m_S.coalChance) id = m_Ids.coal;
                    }
                    if (m_S.caves && y > 1 && y < top - 2)
                    {
                        float c = m_Noise.Noise(wx * m_S.caveScale, y * m_S.caveScale * 1.6f, wz * m_S.caveScale);
                        if (c * 0.5f + 0.5f > m_S.caveThreshold) id = 0;
                    }
                    data[x + sx * (z + sz * y)] = id;
                }

                // Hierba alta y flores encima de la hierba
                if (surface == m_Ids.grass && top + 1 < h)
                {
                    float r = VoxelNoise.Hash01(wx, 0, wz, m_Seed + 5);
                    byte deco = 0;
                    if (m_Ids.flower != 0 && r < m_S.flowerChance) deco = m_Ids.flower;
                    else if (m_Ids.tallGrass != 0 && r < m_S.flowerChance + m_S.tallGrassChance) deco = m_Ids.tallGrass;
                    if (deco != 0) data[x + sx * (z + sz * (top + 1))] = deco;
                }
            }

            if (m_Ids.log == 0 || m_S.treeChance <= 0f) return;

            // Árboles: se miran también las columnas de alrededor, porque las hojas de un árbol vecino pueden entrar en este chunk
            for (int z = -2; z < sz + 2; z++)
            for (int x = -2; x < sx + 2; x++)
            {
                int wx = ox + x, wz = oz + z;
                if (VoxelNoise.Hash01(wx, 1, wz, m_Seed + 9) >= m_S.treeChance) continue;
                int top = SurfaceHeight(wx, wz, h);
                if (SurfaceBlock(top) != m_Ids.grass) continue;
                int trunk = 4 + (int)(VoxelNoise.Hash01(wx, 2, wz, m_Seed + 9) * 3f); // 4 a 6
                int crown = top + trunk;

                for (int y = crown - 2; y <= crown + 1; y++)
                {
                    int r = y >= crown ? 1 : 2;
                    for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        bool corner = Math.Abs(dx) == r && Math.Abs(dz) == r;
                        if (corner && (y == crown + 1 || VoxelNoise.Hash01(wx + dx, y, wz + dz, m_Seed + 3) < 0.5f)) continue;
                        if (y == crown + 1 && corner) continue;
                        Put(data, sx, h, sz, x + dx, y, z + dz, m_Ids.leaves, false);
                    }
                }
                for (int y = top + 1; y <= crown; y++) Put(data, sx, h, sz, x, y, z, m_Ids.log, true);
                Put(data, sx, h, sz, x, top, z, m_Ids.dirt, true); // bajo el tronco, tierra
            }
        }

        void Put(byte[] data, int sx, int h, int sz, int x, int y, int z, byte id, bool overwrite)
        {
            if (id == 0 || x < 0 || x >= sx || z < 0 || z >= sz || y < 0 || y >= h) return;
            int i = x + sx * (z + sz * y);
            byte cur = data[i];
            if (cur == 0 || cur == m_Ids.tallGrass || cur == m_Ids.flower || (overwrite && cur == m_Ids.leaves) || (overwrite && id == m_Ids.dirt && cur == m_Ids.grass))
                data[i] = id;
        }

        static int Clamp(int v, int a, int b) { return v < a ? a : v > b ? b : v; }
    }
}
