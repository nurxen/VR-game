using System;
using System.Collections.Generic;

namespace VRKit
{
    /// <summary>
    /// Comprime los bloques de un chunk para guardarlos: «200 de aire, 3 de tierra…» (run-length).
    /// Un chunk de 12.288 bloques suele quedar en unos cientos de bytes.
    /// </summary>
    public static class VoxelRle
    {
        public static string Encode(byte[] data)
        {
            var o = new List<byte>(512);
            int i = 0;
            while (i < data.Length)
            {
                byte v = data[i];
                int run = 1;
                while (i + run < data.Length && data[i + run] == v && run < 255) run++;
                o.Add((byte)run); o.Add(v);
                i += run;
            }
            return Convert.ToBase64String(o.ToArray());
        }

        /// <summary>Descomprime en data. remap (opcional) traduce ids antiguos a nuevos si cambió la lista de bloques.</summary>
        public static bool Decode(string text, byte[] data, byte[] remap = null)
        {
            if (string.IsNullOrEmpty(text)) return false;
            byte[] raw;
            try { raw = Convert.FromBase64String(text); } catch (FormatException) { return false; }
            int p = 0;
            for (int i = 0; i + 1 < raw.Length; i += 2)
            {
                int run = raw[i];
                byte v = raw[i + 1];
                if (remap != null) v = v < remap.Length ? remap[v] : (byte)0;
                for (int k = 0; k < run && p < data.Length; k++) data[p++] = v;
            }
            return p == data.Length;
        }
    }
}
