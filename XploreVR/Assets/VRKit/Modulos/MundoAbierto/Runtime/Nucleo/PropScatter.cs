using System;
using System.Collections.Generic;

namespace VRKit
{
    /// <summary>Regla de vegetación u objeto en versión pura (la rellena el OpenWorld desde los BiomeProfile).</summary>
    public struct PropRuleData
    {
        /// <summary>Biomas donde sale: un bit por Biome (1 &lt;&lt; (int)Biome.Bosque).</summary>
        public int biomeMask;
        /// <summary>Cuántos por cada 100 m² (10 × 10 m).</summary>
        public float density;
        public float minSlope, maxSlope;
        /// <summary>Metros sobre el agua.</summary>
        public float minAltitude, maxAltitude;
        public float minScale, maxScale;
        /// <summary>Multiplica la densidad junto a los ríos (2 = el doble en las orillas).</summary>
        public float riverBoost;
    }

    /// <summary>Dónde va un objeto: posición dentro del chunk, giro, tamaño y un número estable para guardarlo.</summary>
    public struct PropPlacement
    {
        public int rule;
        public float x, y, z, yaw, scale;
        /// <summary>Siempre el mismo para el mismo sitio y semilla: sirve para guardar «este árbol se taló».</summary>
        public int id;
    }

    /// <summary>
    /// Reparte árboles, rocas, hierba… por un chunk. El chunk se divide en casillas; cada casilla tira un dado fijo
    /// (según la semilla y su posición) para decidir si lleva algo y qué. Como mucho un objeto por casilla: nunca se
    /// montan unos encima de otros. Código puro, se ejecuta en otro hilo.
    /// </summary>
    public static class PropScatter
    {
        public static void Scatter(OpenTerrainGenerator gen, int cx, int cz, float size, int meshCells, float cell, PropRuleData[] rules, List<PropPlacement> output)
        {
            output.Clear();
            if (rules == null || rules.Length == 0 || cell <= 0f) return;
            int cells = Math.Max(1, (int)(size / cell));
            float c = size / cells;
            float area = c * c / 100f;
            float water = gen.Settings.waterLevel;
            var weights = new float[rules.Length];
            int seed = gen.Seed;

            for (int j = 0; j < cells; j++)
            for (int i = 0; i < cells; i++)
            {
                int gx = cx * cells + i, gz = cz * cells + j;          // casilla en todo el mundo
                float lx = (i + 0.15f + 0.7f * WorldNoise.Hash01(gx, gz, 1, seed)) * c;
                float lz = (j + 0.15f + 0.7f * WorldNoise.Hash01(gx, gz, 2, seed)) * c;
                float wx = cx * size + lx, wz = cz * size + lz;
                var s = gen.Sample(wx, wz);
                float above = s.height - water;
                if (above < -0.2f) continue;                            // bajo el agua, nada
                float slope = gen.SlopeAt(wx, wz, c * 0.5f);
                var biome = gen.BiomeOf(s, slope);
                int bit = 1 << (int)biome;

                float total = 0f;
                for (int r = 0; r < rules.Length; r++)
                {
                    var rule = rules[r];
                    float w = 0f;
                    if ((rule.biomeMask & bit) != 0 && slope >= rule.minSlope && slope <= rule.maxSlope && above >= rule.minAltitude && above <= rule.maxAltitude)
                        w = rule.density * area * (1f + (rule.riverBoost - 1f) * s.river);
                    weights[r] = w;
                    total += w;
                }
                if (total <= 0f) continue;

                float roll = WorldNoise.Hash01(gx, gz, 3, seed);
                if (roll >= Math.Min(1f, total)) continue;              // la casilla se queda vacía
                float pick = WorldNoise.Hash01(gx, gz, 4, seed) * total;
                int chosen = rules.Length - 1;
                for (int r = 0; r < rules.Length; r++) { pick -= weights[r]; if (pick <= 0f && weights[r] > 0f) { chosen = r; break; } }

                var cr = rules[chosen];
                output.Add(new PropPlacement
                {
                    rule = chosen,
                    x = lx, y = TerrainMesher.SurfaceHeight(gen, wx, wz, size, meshCells), z = lz,
                    yaw = WorldNoise.Hash01(gx, gz, 5, seed) * 360f,
                    scale = cr.minScale + (cr.maxScale - cr.minScale) * WorldNoise.Hash01(gx, gz, 6, seed),
                    id = i + j * cells,
                });
            }
        }
    }
}
