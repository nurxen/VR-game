using UnityEngine;

namespace VRKit
{
    /// <summary>
    /// Pinta por código una textura atlas de ejemplo (8 × 8 casillas de 16 píxeles, estilo pixel art)
    /// para que el mundo se vea desde el primer minuto. Cambiadla por la vuestra cuando la tengáis:
    /// mismas casillas, mismo orden (o cambiad los números de casilla de cada BlockType).
    /// Código puro: devuelve los píxeles; el montador los guarda como PNG.
    /// </summary>
    public static class VoxelAtlasPainter
    {
        public const int Columns = 8, Rows = 8;

        // Número de casilla de cada textura (fila a fila, empezando arriba a la izquierda)
        public const int GrassTop = 0, GrassSide = 1, Dirt = 2, Stone = 3, Cobble = 4, LogSide = 5, LogTop = 6, Planks = 7,
                         Leaves = 8, Sand = 9, Glass = 10, Bricks = 11, CoalOre = 12, IronOre = 13, Bedrock = 14, TableTop = 15,
                         TableSide = 16, Flower = 17, TallGrass = 18, Wool = 19, Snow = 20, SnowSide = 21, Gravel = 22, StoneBricks = 23;

        public static readonly string[] TileNames =
        {
            "Hierba (arriba)", "Hierba (lado)", "Tierra", "Piedra", "Roca", "Tronco (lado)", "Tronco (arriba)", "Tablones",
            "Hojas", "Arena", "Cristal", "Ladrillos", "Carbón", "Hierro", "Lecho de roca", "Mesa (arriba)",
            "Mesa (lado)", "Flor", "Hierba alta", "Lana", "Nieve", "Nieve (lado)", "Grava", "Ladrillos de piedra",
        };

        static Color32[] s_Px;
        static int s_W, s_T;

        /// <summary>Devuelve los píxeles en el orden de Texture2D.SetPixels32 (fila 0 = abajo).</summary>
        public static Color32[] Paint(int tilePx, out int width, out int height)
        {
            s_T = tilePx < 4 ? 4 : tilePx;
            width = s_W = Columns * s_T;
            height = Rows * s_T;
            s_Px = new Color32[width * height];

            for (int t = 0; t < TileNames.Length; t++)
                for (int y = 0; y < s_T; y++)
                    for (int x = 0; x < s_T; x++)
                        Set(t, x, y, PaintPixel(t, x, y));
            return s_Px;
        }

        // x a la derecha, y hacia abajo dentro de la casilla (como se dibuja pixel art)
        static void Set(int tile, int x, int y, Color32 c)
        {
            int col = tile % Columns, row = tile / Columns;
            int px = col * s_T + x;
            int py = (Rows - 1 - row) * s_T + (s_T - 1 - y);
            s_Px[px + py * s_W] = c;
        }

        static float H(int x, int y, int salt) { return VoxelNoise.Hash01(x, y, salt, 1234); }

        static Color32 C(int r, int g, int b, int a = 255)
        {
            return new Color32((byte)Clamp(r), (byte)Clamp(g), (byte)Clamp(b), (byte)Clamp(a));
        }
        static int Clamp(int v) { return v < 0 ? 0 : v > 255 ? 255 : v; }

        static Color32 Vary(int r, int g, int b, int x, int y, int salt, int amount)
        {
            int d = (int)((H(x, y, salt) - 0.5f) * 2f * amount);
            return C(r + d, g + d, b + d);
        }

        static Color32 PaintPixel(int t, int x, int y)
        {
            int T = s_T, s = T / 16 < 1 ? 1 : T / 16; // escala si se pide más resolución
            int gx = x / s, gy = y / s;               // coordenadas en una rejilla de 16
            switch (t)
            {
                case GrassTop: return GrassPx(gx, gy, 1);
                case GrassSide:
                {
                    int edge = 3 + (int)(H(gx, 0, 21) * 3f);
                    return gy < edge ? GrassPx(gx, gy, 2) : DirtPx(gx, gy, 3);
                }
                case Dirt: return DirtPx(gx, gy, 4);
                case Stone: return StonePx(gx, gy, 5);
                case Cobble:
                {
                    int cx = (gx + (gy / 4 % 2) * 2) % 5, cy = gy % 4;
                    bool mortar = cx == 0 || cy == 0;
                    return mortar ? Vary(82, 82, 82, gx, gy, 6, 8) : Vary(130, 130, 128, gx / 5, gy / 4, 7, 22);
                }
                case LogSide:
                {
                    bool groove = H(gx, 0, 8) < 0.3f && H(gx, gy / 3, 9) < 0.8f;
                    return groove ? Vary(70, 54, 32, gx, gy, 10, 6) : Vary(104, 82, 50, gx, gy / 2, 11, 12);
                }
                case LogTop:
                {
                    float dx = gx - 7.5f, dy = gy - 7.5f;
                    float d = (float)System.Math.Sqrt(dx * dx + dy * dy);
                    if (gx == 0 || gy == 0 || gx == 15 || gy == 15) return Vary(104, 82, 50, gx, gy, 12, 10);
                    return ((int)d % 3 == 0) ? Vary(150, 115, 68, gx, gy, 13, 6) : Vary(178, 142, 88, gx, gy, 14, 8);
                }
                case Planks: return PlanksPx(gx, gy);
                case Leaves:
                {
                    if (H(gx, gy, 15) < 0.18f) return C(0, 0, 0, 0);
                    return Vary(58, 122, 38, gx, gy, 16, 26);
                }
                case Sand: return Vary(222, 210, 160, gx, gy, 17, 10);
                case Glass:
                {
                    bool border = gx == 0 || gy == 0 || gx == 15 || gy == 15;
                    bool glint = (gx + gy == 6 || gx + gy == 7) && gx > 2 && gx < 6;
                    if (border) return C(205, 230, 238);
                    if (glint) return C(240, 250, 255);
                    return C(0, 0, 0, 0);
                }
                case Bricks:
                {
                    int row = gy / 4;
                    int off = row % 2 == 0 ? 0 : 4;
                    bool mortar = gy % 4 == 3 || (gx + off) % 8 == 7;
                    return mortar ? Vary(186, 180, 168, gx, gy, 18, 8) : Vary(152, 72, 56, (gx + off) / 8, row, 19, 18);
                }
                case CoalOre: return Ore(gx, gy, 20, C(34, 34, 36));
                case IronOre: return Ore(gx, gy, 22, C(216, 176, 146));
                case Bedrock:
                {
                    float r = H(gx, gy, 23);
                    int v = r < 0.33f ? 40 : r < 0.66f ? 85 : 120;
                    return C(v, v, v);
                }
                case TableTop:
                {
                    bool grid = (gx == 2 || gx == 13 || gy == 2 || gy == 13 || gx == 6 || gx == 9 || gy == 6 || gy == 9) && gx >= 2 && gx <= 13 && gy >= 2 && gy <= 13;
                    return grid ? Vary(96, 70, 40, gx, gy, 24, 6) : PlanksPx(gx, gy);
                }
                case TableSide:
                {
                    if (gy < 3) return PlanksPx(gx, gy);
                    bool frame = gx < 2 || gx > 13 || gy > 13;
                    bool saw = gy >= 6 && gy <= 8 && gx >= 3 && gx <= 7;
                    bool hammer = (gx == 10 && gy >= 5 && gy <= 12) || (gy == 5 && gx >= 9 && gx <= 12);
                    if (saw) return C(170, 170, 176);
                    if (hammer) return C(80, 60, 40);
                    return frame ? Vary(96, 70, 40, gx, gy, 25, 6) : Vary(140, 108, 64, gx, gy, 26, 10);
                }
                case Flower:
                {
                    bool stem = gx == 7 && gy >= 7;
                    bool leaf = (gy == 11 && (gx == 6 || gx == 5)) || (gy == 10 && gx == 8);
                    bool petal = (System.Math.Abs(gx - 7) + System.Math.Abs(gy - 4) <= 3) && !(gx == 7 && gy == 4);
                    if (gx == 7 && gy == 4) return C(250, 214, 60);
                    if (petal) return Vary(220, 50, 60, gx, gy, 27, 14);
                    if (stem || leaf) return C(60, 140, 50);
                    return C(0, 0, 0, 0);
                }
                case TallGrass:
                {
                    int bladeTop = 15 - (int)(H(gx, 0, 28) * 12f);
                    bool blade = (gx % 3 != 2 || H(gx, 1, 29) < 0.3f) && gy >= bladeTop;
                    return blade ? Vary(74, 150, 52, gx, gy, 30, 18) : C(0, 0, 0, 0);
                }
                case Wool:
                {
                    bool weave = (gx + gy) % 4 == 0;
                    return weave ? Vary(214, 214, 214, gx, gy, 31, 6) : Vary(236, 236, 236, gx, gy, 32, 6);
                }
                case Snow: return Vary(240, 246, 252, gx, gy, 33, 6);
                case SnowSide:
                {
                    int edge = 3 + (int)(H(gx, 0, 34) * 2f);
                    return gy < edge ? Vary(240, 246, 252, gx, gy, 35, 6) : DirtPx(gx, gy, 36);
                }
                case Gravel:
                {
                    float r = H(gx / 2, gy / 2, 37);
                    if (r < 0.3f) return Vary(110, 104, 100, gx, gy, 38, 10);
                    if (r < 0.6f) return Vary(150, 144, 140, gx, gy, 39, 10);
                    return Vary(126, 112, 98, gx, gy, 40, 10);
                }
                case StoneBricks:
                {
                    int row = gy / 8;
                    int off = row % 2 == 0 ? 0 : 8;
                    bool mortar = gy % 8 == 7 || (gx + off) % 16 == 15;
                    return mortar ? Vary(84, 84, 84, gx, gy, 41, 6) : Vary(128, 128, 128, gx, gy, 42, 12);
                }
            }
            return C(255, 0, 255);
        }

        static Color32 GrassPx(int x, int y, int salt) { return Vary(98, 160, 56, x, y, salt * 7, 24); }
        static Color32 DirtPx(int x, int y, int salt)
        {
            if (H(x, y, salt * 11) < 0.08f) return C(96, 66, 44);
            return Vary(136, 98, 68, x, y, salt * 13, 16);
        }
        static Color32 StonePx(int x, int y, int salt)
        {
            if (H(x / 3, y / 2, salt * 17) < 0.15f) return Vary(108, 108, 108, x, y, salt, 6);
            return Vary(128, 128, 128, x, y, salt * 19, 14);
        }
        static Color32 PlanksPx(int x, int y)
        {
            int board = y / 4;
            int seam = (int)(H(board, 0, 43) * 12f) + 2;
            bool line = y % 4 == 3 || x == seam;
            return line ? Vary(110, 84, 48, x, y, 44, 6) : Vary(166, 132, 80, x / 3, y, 45 + board, 12);
        }
        static Color32 Ore(int x, int y, int salt, Color32 ore)
        {
            float r = H(x / 2, y / 2, salt);
            bool spot = r < 0.22f && H(x, y, salt + 1) < 0.85f;
            return spot ? ore : StonePx(x, y, salt + 2);
        }
    }
}
