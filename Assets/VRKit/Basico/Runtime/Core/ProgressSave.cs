using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace VRKit
{
    /// <summary>
    /// Progreso guardado entre partidas en un archivo JSON: niveles desbloqueados, mejor puntuación
    /// de cada nivel, banderas («ha visto la intro», «tiene la llave») y números sueltos (monedas…).
    ///
    ///   ProgressSave.SetFlag("intro_vista");
    ///   if (ProgressSave.HasFlag("intro_vista")) …
    ///   ProgressSave.RecordScore("Nivel1", 350);   // devuelve true si es récord
    ///   ProgressSave.Unlock("Nivel2");
    ///   ProgressSave.SetInt("monedas", 120);
    ///
    /// Se guarda solo en cada cambio. ProgressSave.ResetAll() lo borra todo.
    /// </summary>
    public static class ProgressSave
    {
        [Serializable] class Entry { public string key; public int value; }
        [Serializable] class Data
        {
            public List<string> flags = new List<string>();
            public List<string> unlocked = new List<string>();
            public List<Entry> scores = new List<Entry>();
            public List<Entry> ints = new List<Entry>();
        }

        static Data s_Data;
        static string FilePath { get { return Path.Combine(Application.persistentDataPath, "vrkit_progreso.json"); } }

        public static event Action Changed;

        static Data D
        {
            get
            {
                if (s_Data != null) return s_Data;
                try { s_Data = File.Exists(FilePath) ? JsonUtility.FromJson<Data>(File.ReadAllText(FilePath)) : null; }
                catch (Exception e) { Debug.LogWarning("[VRKit] No se pudo leer el progreso: " + e.Message); }
                if (s_Data == null) s_Data = new Data();
                return s_Data;
            }
        }

        static void Save()
        {
            try { File.WriteAllText(FilePath, JsonUtility.ToJson(D, true)); }
            catch (Exception e) { Debug.LogWarning("[VRKit] No se pudo guardar el progreso: " + e.Message); }
            if (Changed != null) Changed();
        }

        // ---------------------------------------------------------------- Banderas
        public static bool HasFlag(string flag) { return D.flags.Contains(flag); }
        public static void SetFlag(string flag) { if (!string.IsNullOrEmpty(flag) && !D.flags.Contains(flag)) { D.flags.Add(flag); Save(); } }
        public static void ClearFlag(string flag) { if (D.flags.Remove(flag)) Save(); }

        // ---------------------------------------------------------------- Niveles
        public static bool IsUnlocked(string level) { return D.unlocked.Contains(level); }
        public static void Unlock(string level) { if (!string.IsNullOrEmpty(level) && !D.unlocked.Contains(level)) { D.unlocked.Add(level); Save(); } }

        public static int GetBestScore(string level) { var e = Find(D.scores, level); return e != null ? e.value : 0; }

        /// <summary>Guarda la puntuación si es mejor que la anterior. Devuelve true si es un récord nuevo.</summary>
        public static bool RecordScore(string level, int score)
        {
            var e = Find(D.scores, level);
            if (e != null && e.value >= score) return false;
            if (e == null) D.scores.Add(new Entry { key = level, value = score }); else e.value = score;
            Save();
            return true;
        }

        // ---------------------------------------------------------------- Números
        public static int GetInt(string key, int fallback = 0) { var e = Find(D.ints, key); return e != null ? e.value : fallback; }
        public static void SetInt(string key, int value)
        {
            var e = Find(D.ints, key);
            if (e == null) D.ints.Add(new Entry { key = key, value = value }); else e.value = value;
            Save();
        }
        public static void AddInt(string key, int amount) { SetInt(key, GetInt(key) + amount); }

        public static void ResetAll() { s_Data = new Data(); Save(); }

        static Entry Find(List<Entry> list, string key)
        {
            foreach (var e in list) if (e.key == key) return e;
            return null;
        }
    }
}
