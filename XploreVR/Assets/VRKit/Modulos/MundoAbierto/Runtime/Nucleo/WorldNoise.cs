using System;

namespace VRKit
{
    /// <summary>
    /// Ruido de Perlin con semilla para el mundo abierto: la misma semilla da siempre el mismo terreno. (Copia del ruido del Mundo de bloques: los módulos no dependen unos de otros.)
    /// Código puro, sin Unity: se puede probar fuera del editor.
    /// </summary>
    public sealed class WorldNoise
    {
        readonly int[] m_P = new int[512];

        public WorldNoise(int seed)
        {
            var rnd = new Random(seed);
            var perm = new int[256];
            for (int i = 0; i < 256; i++) perm[i] = i;
            for (int i = 255; i > 0; i--) { int j = rnd.Next(i + 1); int t = perm[i]; perm[i] = perm[j]; perm[j] = t; }
            for (int i = 0; i < 512; i++) m_P[i] = perm[i & 255];
        }

        static int FastFloor(float f) { int i = (int)f; return f < i ? i - 1 : i; }
        static float Fade(float t) { return t * t * t * (t * (t * 6f - 15f) + 10f); }
        static float Lerp(float a, float b, float t) { return a + (b - a) * t; }

        static float Grad2(int h, float x, float y)
        {
            switch (h & 7)
            {
                case 0: return x + y;
                case 1: return -x + y;
                case 2: return x - y;
                case 3: return -x - y;
                case 4: return x;
                case 5: return -x;
                case 6: return y;
                default: return -y;
            }
        }

        static float Grad3(int h, float x, float y, float z)
        {
            int hh = h & 15;
            float u = hh < 8 ? x : y;
            float v = hh < 4 ? y : (hh == 12 || hh == 14 ? x : z);
            return ((hh & 1) == 0 ? u : -u) + ((hh & 2) == 0 ? v : -v);
        }

        /// <summary>Ruido 2D, aproximadamente entre -1 y 1.</summary>
        public float Noise(float x, float y)
        {
            int xi = FastFloor(x), yi = FastFloor(y);
            float xf = x - xi, yf = y - yi;
            xi &= 255; yi &= 255;
            float u = Fade(xf), v = Fade(yf);
            int aa = m_P[m_P[xi] + yi], ab = m_P[m_P[xi] + yi + 1];
            int ba = m_P[m_P[xi + 1] + yi], bb = m_P[m_P[xi + 1] + yi + 1];
            float x1 = Lerp(Grad2(aa, xf, yf), Grad2(ba, xf - 1f, yf), u);
            float x2 = Lerp(Grad2(ab, xf, yf - 1f), Grad2(bb, xf - 1f, yf - 1f), u);
            return Lerp(x1, x2, v) * 0.7071f;
        }

        /// <summary>Ruido 3D, aproximadamente entre -1 y 1 (cuevas).</summary>
        public float Noise(float x, float y, float z)
        {
            int xi = FastFloor(x), yi = FastFloor(y), zi = FastFloor(z);
            float xf = x - xi, yf = y - yi, zf = z - zi;
            xi &= 255; yi &= 255; zi &= 255;
            float u = Fade(xf), v = Fade(yf), w = Fade(zf);
            int a = m_P[xi] + yi, aa = m_P[a] + zi, ab = m_P[a + 1] + zi;
            int b = m_P[xi + 1] + yi, ba = m_P[b] + zi, bb = m_P[b + 1] + zi;
            return Lerp(
                Lerp(Lerp(Grad3(m_P[aa], xf, yf, zf), Grad3(m_P[ba], xf - 1f, yf, zf), u),
                     Lerp(Grad3(m_P[ab], xf, yf - 1f, zf), Grad3(m_P[bb], xf - 1f, yf - 1f, zf), u), v),
                Lerp(Lerp(Grad3(m_P[aa + 1], xf, yf, zf - 1f), Grad3(m_P[ba + 1], xf - 1f, yf, zf - 1f), u),
                     Lerp(Grad3(m_P[ab + 1], xf, yf - 1f, zf - 1f), Grad3(m_P[bb + 1], xf - 1f, yf - 1f, zf - 1f), u), v), w);
        }

        /// <summary>Varias capas de ruido (octavas): formas grandes con detalle pequeño encima. Entre -1 y 1.</summary>
        public float Fbm(float x, float y, int octaves, float lacunarity = 2f, float gain = 0.5f)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Noise(x, y) * amp;
                norm += amp;
                amp *= gain;
                x *= lacunarity; y *= lacunarity;
            }
            return norm > 0f ? sum / norm : 0f;
        }

        /// <summary>Ruido «de crestas»: picos afilados y valles anchos (cordilleras). Entre 0 y 1.</summary>
        public float Ridged(float x, float y, int octaves)
        {
            float sum = 0f, amp = 0.5f, norm = 0f, freq = 1f, prev = 1f;
            for (int i = 0; i < octaves; i++)
            {
                float n = 1f - Math.Abs(Noise(x * freq, y * freq));
                n *= n;
                sum += n * amp * prev;
                norm += amp;
                prev = n;
                amp *= 0.5f; freq *= 2f;
            }
            return norm > 0f ? sum / norm : 0f;
        }

        /// <summary>Número «aleatorio» fijo para una posición (árboles, menas, flores). Entre 0 y 1.</summary>
        public static float Hash01(int x, int y, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u;
                h ^= (uint)x * 0x85EBCA77u; h = (h << 13) | (h >> 19);
                h ^= (uint)y * 0xC2B2AE3Du; h = (h << 17) | (h >> 15);
                h ^= (uint)z * 0x27D4EB2Fu;
                h ^= h >> 16; h *= 0x7FEB352Du; h ^= h >> 15; h *= 0x846CA68Bu; h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }
    }
}
