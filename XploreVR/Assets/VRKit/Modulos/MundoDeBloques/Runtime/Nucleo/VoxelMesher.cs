using System.Collections.Generic;
using UnityEngine;

namespace VRKit
{
    /// <summary>Forma de un bloque: cubo normal, o dos planos en cruz (flores, hierba alta).</summary>
    public enum BlockShape { Cubo, Cruz }

    /// <summary>Lo que el mallado necesita saber de cada id de bloque. Lo prepara el BlockRegistry.</summary>
    public struct BlockMeshInfo
    {
        public bool exists;
        public BlockShape shape;
        /// <summary>Cubo sin transparencias: tapa las caras de al lado y oscurece las esquinas (oclusión ambiental).</summary>
        public bool opaque;
        /// <summary>Tiene colisión (se puede pisar).</summary>
        public bool solid;
        public int tileTop, tileSide, tileBottom;
    }

    /// <summary>Cómo está dividida la textura atlas: columnas × filas de casillas iguales, la casilla 0 arriba a la izquierda.</summary>
    public struct VoxelAtlasLayout
    {
        public int columns, rows;
        /// <summary>Margen dentro de cada casilla para que no se cuele el color de la vecina (en UV).</summary>
        public float inset;

        public VoxelAtlasLayout(int columns, int rows, int pixelWidth)
        {
            this.columns = columns < 1 ? 1 : columns;
            this.rows = rows < 1 ? 1 : rows;
            inset = pixelWidth > 0 ? 0.5f / pixelWidth : 0.0005f;
        }

        public void TileUV(int tile, out float u0, out float v0, out float u1, out float v1)
        {
            int col = tile % columns, row = tile / columns;
            u0 = (float)col / columns + inset;
            u1 = (float)(col + 1) / columns - inset;
            v1 = 1f - (float)row / rows - inset;
            v0 = 1f - (float)(row + 1) / rows + inset;
        }
    }

    /// <summary>Listas que se rellenan al mallar un chunk y luego se pasan a una Mesh.</summary>
    public sealed class VoxelMeshData
    {
        public readonly List<Vector3> vertices = new List<Vector3>(8192);
        public readonly List<Vector2> uvs = new List<Vector2>(8192);
        public readonly List<Color32> colors = new List<Color32>(8192);
        /// <summary>Todas las caras (lo que se dibuja).</summary>
        public readonly List<int> triangles = new List<int>(12288);
        /// <summary>Solo las caras de bloques sólidos (lo que choca). Sin flores ni hierba alta.</summary>
        public readonly List<int> colliderTriangles = new List<int>(12288);

        public void Clear() { vertices.Clear(); uvs.Clear(); colors.Clear(); triangles.Clear(); colliderTriangles.Clear(); }
        public int FaceCount { get { return triangles.Count / 6; } }
    }

    /// <summary>
    /// Convierte bloques en una malla: solo dibuja las caras que se ven (las que tocan aire o cristal),
    /// con un poco de sombra por cara y en las esquinas (oclusión ambiental) para que se lea el relieve sin luces.
    /// Código puro: se puede probar fuera de Unity.
    /// </summary>
    public static class VoxelMesher
    {
        // Caras: +X, -X, +Y, -Y, +Z, -Z. Cuatro vértices por cara, en el orden que Unity dibuja de frente.
        static readonly int[,] kDir = { { 1, 0, 0 }, { -1, 0, 0 }, { 0, 1, 0 }, { 0, -1, 0 }, { 0, 0, 1 }, { 0, 0, -1 } };
        static readonly int[,] kVerts =
        {
            { 1,0,0, 1,1,0, 1,1,1, 1,0,1 },   // +X
            { 0,0,1, 0,1,1, 0,1,0, 0,0,0 },   // -X
            { 0,1,0, 0,1,1, 1,1,1, 1,1,0 },   // +Y
            { 0,0,0, 1,0,0, 1,0,1, 0,0,1 },   // -Y
            { 1,0,1, 1,1,1, 0,1,1, 0,0,1 },   // +Z
            { 0,0,0, 0,1,0, 1,1,0, 1,0,0 },   // -Z
        };
        static readonly int[,] kUnit = { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };
        static readonly float[] kShade = { 0.82f, 0.82f, 1f, 0.55f, 0.68f, 0.68f };
        static readonly float[] kAO = { 0.5f, 0.68f, 0.84f, 1f };

        /// <summary>
        /// Malla un chunk a partir de un array con un bloque de margen alrededor (para saber qué hay en los chunks vecinos):
        /// tamaño (sx+2) × (h+2) × (sz+2), índice px + (sx+2)·(pz + (sz+2)·py).
        /// </summary>
        public static void BuildChunk(byte[] padded, int sx, int h, int sz, BlockMeshInfo[] info, VoxelAtlasLayout atlas, bool ambientOcclusion, VoxelMeshData mesh)
        {
            mesh.Clear();
            int px = sx + 2, pz = sz + 2;
            var corners = new int[4];

            for (int y = 0; y < h; y++)
            for (int z = 0; z < sz; z++)
            for (int x = 0; x < sx; x++)
            {
                int ci = (x + 1) + px * ((z + 1) + pz * (y + 1));
                byte id = padded[ci];
                if (id == 0) continue;
                var bi = info[id];
                if (!bi.exists) continue;

                if (bi.shape == BlockShape.Cruz)
                {
                    AddCross(mesh, x, y, z, bi.tileSide, atlas);
                    continue;
                }

                for (int f = 0; f < 6; f++)
                {
                    if (f == 3 && y == 0) continue; // la cara de abajo del todo nunca se ve
                    int nx = x + kDir[f, 0], ny = y + kDir[f, 1], nz = z + kDir[f, 2];
                    int ni = (nx + 1) + px * ((nz + 1) + pz * (ny + 1));
                    byte n = padded[ni];
                    if (n != 0 && (info[n].opaque || n == id)) continue;

                    int a = f / 2, b = (a + 1) % 3, c = (a + 2) % 3;
                    for (int v = 0; v < 4; v++)
                    {
                        if (!ambientOcclusion) { corners[v] = 3; continue; }
                        // Celda de delante de la cara y las tres que rodean esta esquina (sin crear arrays: esto corre miles de veces)
                        int du = kVerts[f, v * 3 + b] == 1 ? 1 : -1, dv = kVerts[f, v * 3 + c] == 1 ? 1 : -1;
                        int bx = kUnit[b, 0] * du, by = kUnit[b, 1] * du, bz = kUnit[b, 2] * du;
                        int cx = kUnit[c, 0] * dv, cy = kUnit[c, 1] * dv, cz = kUnit[c, 2] * dv;
                        bool o1 = Opaque(padded, info, nx + bx, ny + by, nz + bz, px, pz);
                        bool o2 = Opaque(padded, info, nx + cx, ny + cy, nz + cz, px, pz);
                        bool oc = Opaque(padded, info, nx + bx + cx, ny + by + cy, nz + bz + cz, px, pz);
                        corners[v] = o1 && o2 ? 0 : 3 - ((o1 ? 1 : 0) + (o2 ? 1 : 0) + (oc ? 1 : 0));
                    }
                    int tile = f == 2 ? bi.tileTop : f == 3 ? bi.tileBottom : bi.tileSide;
                    AddFace(mesh, f, x, y, z, tile, atlas, corners, bi.solid);
                }
            }
        }

        static bool Opaque(byte[] padded, BlockMeshInfo[] info, int x, int y, int z, int px, int pz)
        {
            byte id = padded[(x + 1) + px * ((z + 1) + pz * (y + 1))];
            return id != 0 && info[id].opaque;
        }

        static void AddFace(VoxelMeshData m, int f, float x, float y, float z, int tile, VoxelAtlasLayout atlas, int[] ao, bool solid)
        {
            int start = m.vertices.Count;
            float u0, v0, u1, v1;
            atlas.TileUV(tile, out u0, out v0, out u1, out v1);
            for (int v = 0; v < 4; v++)
            {
                m.vertices.Add(new Vector3(x + kVerts[f, v * 3], y + kVerts[f, v * 3 + 1], z + kVerts[f, v * 3 + 2]));
                byte g = (byte)(255f * kShade[f] * kAO[ao[v]]);
                m.colors.Add(new Color32(g, g, g, 255));
            }
            m.uvs.Add(new Vector2(u0, v0)); m.uvs.Add(new Vector2(u0, v1)); m.uvs.Add(new Vector2(u1, v1)); m.uvs.Add(new Vector2(u1, v0));

            // Si las esquinas están desiguales, se gira la diagonal para que la sombra no haga «pajarita»
            bool flip = ao[0] + ao[2] < ao[1] + ao[3];
            AddQuad(m.triangles, start, flip);
            if (solid) AddQuad(m.colliderTriangles, start, flip);
        }

        static void AddQuad(List<int> t, int s, bool flip)
        {
            if (!flip) { t.Add(s); t.Add(s + 1); t.Add(s + 2); t.Add(s); t.Add(s + 2); t.Add(s + 3); }
            else { t.Add(s + 1); t.Add(s + 2); t.Add(s + 3); t.Add(s + 1); t.Add(s + 3); t.Add(s); }
        }

        static void AddCross(VoxelMeshData m, float x, float y, float z, int tile, VoxelAtlasLayout atlas)
        {
            float u0, v0, u1, v1;
            atlas.TileUV(tile, out u0, out v0, out u1, out v1);
            const float a = 0.15f, b = 0.85f;
            AddPlane(m, new Vector3(x + a, y, z + a), new Vector3(x + b, y, z + b), u0, v0, u1, v1);
            AddPlane(m, new Vector3(x + b, y, z + a), new Vector3(x + a, y, z + b), u0, v0, u1, v1);
        }

        /// <summary>Un plano vertical de p0 a p1 visto por las dos caras.</summary>
        static void AddPlane(VoxelMeshData m, Vector3 p0, Vector3 p1, float u0, float v0, float u1, float v1)
        {
            for (int side = 0; side < 2; side++)
            {
                Vector3 a = side == 0 ? p0 : p1, b = side == 0 ? p1 : p0;
                int s = m.vertices.Count;
                m.vertices.Add(new Vector3(a.x, a.y, a.z));
                m.vertices.Add(new Vector3(a.x, a.y + 1f, a.z));
                m.vertices.Add(new Vector3(b.x, b.y + 1f, b.z));
                m.vertices.Add(new Vector3(b.x, b.y, b.z));
                m.uvs.Add(new Vector2(u0, v0)); m.uvs.Add(new Vector2(u0, v1)); m.uvs.Add(new Vector2(u1, v1)); m.uvs.Add(new Vector2(u1, v0));
                for (int i = 0; i < 4; i++) m.colors.Add(new Color32(230, 230, 230, 255));
                AddQuad(m.triangles, s, false);
            }
        }

        /// <summary>
        /// Un solo bloque centrado en el origen, de lado 1 (para el inventario, los objetos que caen y lo que llevas en la mano).
        /// </summary>
        public static void BuildSingle(BlockMeshInfo bi, VoxelAtlasLayout atlas, VoxelMeshData m)
        {
            m.Clear();
            if (!bi.exists) return;
            if (bi.shape == BlockShape.Cruz) { AddCross(m, -0.5f, -0.5f, -0.5f, bi.tileSide, atlas); return; }
            var full = new[] { 3, 3, 3, 3 };
            for (int f = 0; f < 6; f++)
            {
                int tile = f == 2 ? bi.tileTop : f == 3 ? bi.tileBottom : bi.tileSide;
                AddFace(m, f, -0.5f, -0.5f, -0.5f, tile, atlas, full, true);
            }
        }
    }
}
