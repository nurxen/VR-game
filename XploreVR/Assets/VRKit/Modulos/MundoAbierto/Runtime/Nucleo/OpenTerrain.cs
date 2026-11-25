using System;

namespace VRKit
{
    /// <summary>Las zonas del mundo. Cada una tiene su color de suelo, su vegetación y su música.</summary>
    public enum Biome { Pradera, Bosque, Ribera, Desierto, Montana, Nevado, Playa, Fondo }

    /// <summary>Cómo es el terreno. Se edita en el Inspector del OpenWorld. Medidas en metros.</summary>
    [Serializable]
    public class OpenTerrainSettings
    {
        [UnityEngine.Header("Alturas (metros)")]
        [UnityEngine.Tooltip("Altura del agua: mar, lagos y ríos.")]
        public float waterLevel = 0f;
        [UnityEngine.Tooltip("Altura media de las tierras bajas sobre el agua.")]
        public float baseHeight = 14f;
        [UnityEngine.Tooltip("Cuánto suben y bajan las colinas.")]
        public float hillHeight = 18f;
        [UnityEngine.Tooltip("Altura máxima de las montañas sobre el resto.")]
        public float mountainHeight = 110f;
        [UnityEngine.Tooltip("Por encima, nieve.")]
        public float snowHeight = 75f;

        [UnityEngine.Header("Tamaños (metros: más grande = formas más anchas)")]
        public float continentSize = 1400f;
        public float hillSize = 260f;
        public float mountainSize = 700f;
        [UnityEngine.Tooltip("Tamaño de las zonas de clima (desierto, bosque…).")]
        public float climateSize = 900f;

        [UnityEngine.Header("Ríos")]
        public bool rivers = true;
        [UnityEngine.Tooltip("Separación media entre ríos.")]
        public float riverSpacing = 900f;
        [UnityEngine.Tooltip("Anchura del cauce (relativa). 0,011 ≈ 10–20 m.")]
        [UnityEngine.Range(0.005f, 0.06f)] public float riverWidth = 0.011f;
        [UnityEngine.Tooltip("Anchura de las orillas que bajan hacia el río.")]
        [UnityEngine.Range(0.01f, 0.2f)] public float riverBanks = 0.045f;
        [UnityEngine.Tooltip("Profundidad del cauce bajo el agua.")]
        public float riverDepth = 2.5f;

        [UnityEngine.Header("Clima")]
        [UnityEngine.Tooltip("-1 = todo frío, 1 = todo caluroso.")]
        [UnityEngine.Range(-1f, 1f)] public float temperatureBias = 0f;
        [UnityEngine.Tooltip("-1 = todo seco, 1 = todo húmedo.")]
        [UnityEngine.Range(-1f, 1f)] public float humidityBias = 0f;
        [UnityEngine.Tooltip("Pendiente (grados) a partir de la cual se ve roca.")]
        [UnityEngine.Range(15f, 60f)] public float rockSlope = 34f;
    }

    /// <summary>Todo lo que se sabe de un punto del mundo.</summary>
    public struct TerrainSample
    {
        public float height;
        /// <summary>0 = lejos de un río, 1 = en el cauce.</summary>
        public float river;
        /// <summary>De 0 a 1.</summary>
        public float temperature, humidity, mountain;
    }

    /// <summary>
    /// El terreno del mundo abierto: altura, ríos y bioma de cualquier punto, a partir de una semilla.
    /// Código puro y sin estado: se puede usar desde varios hilos a la vez (así se genera sin tirones) y
    /// la misma semilla da siempre el mismo mundo. Por eso no hace falta guardar el terreno, solo lo que cambia.
    /// </summary>
    public sealed class OpenTerrainGenerator
    {
        readonly OpenTerrainSettings m_S;
        readonly WorldNoise m_Noise;
        readonly int m_Seed;
        // Desplazamientos para que cada capa de ruido sea independiente
        const float kC = 1031.7f, kH = -517.3f, kM = 233.9f, kR = 771.1f, kT = -1291.5f, kW = 619.4f, kD = 87.1f;

        public OpenTerrainGenerator(OpenTerrainSettings settings, int seed)
        {
            m_S = settings ?? new OpenTerrainSettings();
            m_Seed = seed;
            m_Noise = new WorldNoise(seed);
        }

        public OpenTerrainSettings Settings { get { return m_S; } }
        public int Seed { get { return m_Seed; } }

        static float Smooth(float a, float b, float x)
        {
            float t = (x - a) / (b - a);
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return t * t * (3f - 2f * t);
        }

        static float Lerp(float a, float b, float t) { return a + (b - a) * t; }

        public TerrainSample Sample(float x, float z)
        {
            var s = new TerrainSample();

            // Continentes: tierras altas, llanuras y alguna zona baja con lagos
            float continent = m_Noise.Fbm(x / m_S.continentSize + kC, z / m_S.continentSize, 3);
            // Colinas
            float hills = m_Noise.Fbm(x / m_S.hillSize + kH, z / m_S.hillSize, 4);
            // Montañas: una máscara grande dice dónde hay cordillera; dentro, crestas
            float mask = Smooth(-0.15f, 0.35f, m_Noise.Fbm(x / (m_S.mountainSize * 2.2f) + kM, z / (m_S.mountainSize * 2.2f), 2));
            float ridges = m_Noise.Ridged(x / m_S.mountainSize - kM, z / m_S.mountainSize, 5);
            s.mountain = mask * ridges;

            // Las zonas bajas solo bajan del agua de vez en cuando (lagos), no a menudo
            float low = continent < -0.25f ? (continent + 0.25f) * 40f : 0f;
            float h = m_S.baseHeight + continent * 10f + low + hills * m_S.hillHeight * (0.35f + 0.65f * Smooth(-0.2f, 0.4f, continent))
                      + s.mountain * s.mountain * m_S.mountainHeight * 1.4f;

            // Detalle pequeño (irregularidad del suelo)
            h += m_Noise.Noise(x / 23f + kD, z / 23f) * 0.8f;

            // Ríos: donde un ruido pasa por cero, un cauce serpenteante que baja hasta el agua
            if (m_S.rivers)
            {
                float warp = m_Noise.Noise(x / 180f - kR, z / 180f) * 0.35f;
                float r = Math.Abs(m_Noise.Noise(x / m_S.riverSpacing + kR + warp, z / m_S.riverSpacing - kR)
                                   + 0.12f * m_Noise.Noise(x / (m_S.riverSpacing * 0.25f), z / (m_S.riverSpacing * 0.25f) + kR));
                float bankEnd = m_S.riverWidth + m_S.riverBanks;
                s.river = 1f - Smooth(m_S.riverWidth * 0.5f, bankEnd * 1.6f, r);
                // En las cumbres los ríos no excavan (serían cañones de 100 m): nacen más abajo
                float carve = (1f - Smooth(m_S.riverWidth, bankEnd, r)) * (1f - Smooth(0.35f, 0.7f, s.mountain));
                float bed = m_S.waterLevel - m_S.riverDepth;
                float t = Smooth(0f, 1f, carve);
                h = Lerp(h, Math.Min(h, bed + (1f - t) * 3f), t);
            }

            // Clima: temperatura (baja con la altura) y humedad (sube junto a los ríos)
            float temp = m_Noise.Fbm(x / m_S.climateSize + kT, z / m_S.climateSize, 2) * 1.6f + m_S.temperatureBias;
            temp -= Math.Max(0f, h - 30f) / 120f;
            float hum = m_Noise.Fbm(x / m_S.climateSize - kW, z / m_S.climateSize + kW, 2) * 1.6f + m_S.humidityBias + s.river * 0.5f;
            // Un poco de ruido fino: los bordes entre biomas quedan irregulares, no en línea recta
            float edge = m_Noise.Noise(x / 17f - kD, z / 17f + kD) * 0.08f;
            temp += edge; hum -= edge;
            s.temperature = Clamp01(temp * 0.5f + 0.5f);
            s.humidity = Clamp01(hum * 0.5f + 0.5f);
            s.height = h;
            return s;
        }

        public float Height(float x, float z) { return Sample(x, z).height; }

        /// <summary>Bioma de un punto. slopeDeg = pendiente en grados (la da la malla).</summary>
        public Biome BiomeOf(TerrainSample s, float slopeDeg)
        {
            float above = s.height - m_S.waterLevel;
            if (above < -0.3f) return Biome.Fondo;
            if (above > m_S.snowHeight || (above > m_S.snowHeight * 0.75f && s.temperature < 0.35f)) return Biome.Nevado;
            if (s.mountain > 0.45f || slopeDeg > m_S.rockSlope || above > m_S.snowHeight * 0.6f) return Biome.Montana;
            if (above < 1.6f) return s.river > 0.6f ? Biome.Ribera : Biome.Playa;
            if (s.river > 0.45f && s.temperature < 0.8f) return Biome.Ribera;
            if (s.temperature > 0.57f && s.humidity < 0.45f) return Biome.Desierto;
            if (s.humidity > 0.56f) return Biome.Bosque;
            return Biome.Pradera;
        }

        /// <summary>Pendiente en grados, mirando la altura a ±step metros.</summary>
        public float SlopeAt(float x, float z, float step = 1f)
        {
            float dx = Height(x + step, z) - Height(x - step, z);
            float dz = Height(x, z + step) - Height(x, z - step);
            double g = Math.Sqrt(dx * dx + dz * dz) / (2f * step);
            return (float)(Math.Atan(g) * 180.0 / Math.PI);
        }

        public Biome BiomeAt(float x, float z) { return BiomeOf(Sample(x, z), SlopeAt(x, z)); }

        static float Clamp01(float v) { return v < 0f ? 0f : v > 1f ? 1f : v; }
    }
}
