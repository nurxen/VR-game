using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;

namespace VRKit
{
    public enum VoxelGameMode { Creativo, Supervivencia }

    /// <summary>
    /// La partida del mundo de bloques: modo (creativo o supervivencia), aparecer en el mundo, guardar y cargar,
    /// y volver al punto de aparición al morir. Uno por escena, junto al VoxelWorld.
    ///
    /// Guarda en Application.persistentDataPath/vrkit_mundos/(nombre).json: la semilla, los chunks que habéis cambiado,
    /// dónde estabas, el inventario y la hora del día. Se guarda solo cada pocos minutos, al pausar y al salir.
    /// Para los botones del menú: Save(), NewWorld(), SetCreative(true/false), ToggleMode().
    /// </summary>
    public class VoxelGame : MonoBehaviour
    {
        public static VoxelGame Instance { get; private set; }

        [SerializeField] VoxelWorld world;
        [SerializeField] BlockInventory inventory;
        [SerializeField] VoxelGameMode mode = VoxelGameMode.Supervivencia;

        [Header("Partida guardada")]
        [Tooltip("Nombre del archivo. Cambiadlo para tener varios mundos.")]
        [SerializeField] string worldName = "Mundo1";
        [SerializeField] bool loadSaved = true;
        [Tooltip("Minutos entre guardados automáticos (0 = solo al pausar y salir).")]
        [SerializeField] float autosaveMinutes = 2f;

        [Header("Jugador")]
        [Tooltip("Llevar al jugador a la superficie al empezar. Desmarcado en maquetas.")]
        [SerializeField] bool placePlayer = true;
        [Tooltip("Al morir en supervivencia: vuelve al punto de aparición con la vida llena (en vez de Game Over).")]
        [SerializeField] bool respawnOnDeath = true;
        [Tooltip("Negro mientras se carga el mundo (recomendado: evita ver cómo aparece).")]
        [SerializeField] bool fadeWhileLoading = true;

        public UnityEvent onWorldReady = new UnityEvent();
        public UnityEvent onRespawn = new UnityEvent();
        public UnityEvent<bool> onModeChanged = new UnityEvent<bool>();

        [Serializable]
        class SaveData
        {
            public int version = 1;
            public int seed;
            public int height;
            public string mode;
            public float px, py, pz, yaw;
            public bool hasPlayer;
            public float spawnX, spawnY, spawnZ;
            public float timeOfDay = -1f;
            public string selected;
            public List<string> palette = new List<string>();
            public List<VoxelItemSave> items = new List<VoxelItemSave>();
            public List<VoxelChunkSave> chunks = new List<VoxelChunkSave>();
        }

        Vector3 m_SpawnLocal;
        bool m_Loading, m_PendingRespawn;
        float m_NextAutosave;
        SaveData m_Loaded;
        Func<DamageInfo, float, float> m_DeathFilter;

        public VoxelGameMode Mode { get { return mode; } }
        public bool IsCreative { get { return mode == VoxelGameMode.Creativo; } }
        public string SavePath { get { return Path.Combine(Application.persistentDataPath, "vrkit_mundos", worldName + ".json"); } }
        public bool HasSave { get { return File.Exists(SavePath); } }

        // ================================================================== Arranque

        void Awake()
        {
            Instance = this;
            if (world == null) world = FindFirstObjectByType<VoxelWorld>();
            if (inventory == null) inventory = FindFirstObjectByType<BlockInventory>();
        }

        void OnEnable()
        {
            m_DeathFilter = InterceptDeath;
            PlayerHealth.DamageFilters.Add(m_DeathFilter);
        }

        void OnDisable() { PlayerHealth.DamageFilters.Remove(m_DeathFilter); }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            if (world == null) { Debug.LogError("[VRKit] VoxelGame sin VoxelWorld."); return; }
            m_Loaded = loadSaved ? ReadSave() : null;
            if (m_Loaded != null && m_Loaded.height != world.Height)
            {
                Debug.LogWarning("[VRKit] La partida guardada es de un mundo de otra altura (" + m_Loaded.height + "). Se empieza uno nuevo.");
                m_Loaded = null;
            }
            if (m_Loaded != null && !string.IsNullOrEmpty(m_Loaded.mode))
                mode = m_Loaded.mode == VoxelGameMode.Creativo.ToString() ? VoxelGameMode.Creativo : VoxelGameMode.Supervivencia;
            StartCoroutine(Begin(m_Loaded != null ? m_Loaded.seed : world.Seed, m_Loaded));
        }

        IEnumerator Begin(int seed, SaveData save)
        {
            m_Loading = true;
            var player = VRPlayer.Instance;
            var fader = ScreenFader.Instance;
            if (fadeWhileLoading && fader != null) yield return fader.FadeTo(1f, 0.05f);
            if (player != null && placePlayer) player.SetLocomotionEnabled(false);

            world.Initialize(seed);
            if (save != null)
            {
                var remap = Remap(save.palette);
                foreach (var c in save.chunks) world.LoadEditedChunk(c, remap);
            }
            ApplyMode(false);
            if (inventory != null)
            {
                if (save != null) inventory.FromSave(save.items, save.selected);
                else { inventory.Clear(); if (!IsCreative) inventory.GiveStartingItems(); }
            }
            var dayNight = VoxelDayNight.Instance;
            if (dayNight != null && save != null && save.timeOfDay >= 0f) dayNight.TimeOfDay = save.timeOfDay;

            // Punto de aparición: el guardado, o encima del centro del mundo
            if (save != null && (save.spawnX != 0f || save.spawnZ != 0f)) m_SpawnLocal = new Vector3(save.spawnX, save.spawnY, save.spawnZ);
            else m_SpawnLocal = SurfacePoint(world.CenterColumn.x, world.CenterColumn.y);

            // Primero se mueve al jugador (así el mundo carga alrededor de él), luego se espera a que haya suelo
            if (player != null && placePlayer)
            {
                Vector3 target = save != null && save.hasPlayer ? new Vector3(save.px, save.py, save.pz) : m_SpawnLocal;
                player.TeleportTo(world.ToWorld(target), save != null && save.hasPlayer ? save.yaw : (float?)null);
            }
            while (!world.IsReady) yield return null;
            yield return null;

            if (player != null && placePlayer)
            {
                // Por si se guardó dentro de un bloque que ya no está igual
                var feet = world.WorldToBlock(player.FeetPosition);
                if (world.IsSolid(feet) || world.IsSolid(feet + Vector3Int.up))
                    player.TeleportTo(world.ToWorld(SurfacePoint(feet.x, feet.z)));
                var vp = player.GetComponent<VoxelPlayer>();
                if (vp != null) vp.ResetFall();
                player.SetLocomotionEnabled(true);
            }
            if (fadeWhileLoading && fader != null) yield return fader.FadeTo(0f, 0.6f);

            m_Loading = false;
            m_NextAutosave = Time.time + autosaveMinutes * 60f;
            onWorldReady.Invoke();
        }

        /// <summary>Pies sobre el bloque más alto de esa columna (en coordenadas de bloque).</summary>
        Vector3 SurfacePoint(int x, int z)
        {
            int y = world.SurfaceHeight(x, z);
            return new Vector3(x + 0.5f, y + 1.02f, z + 0.5f);
        }

        byte[] Remap(List<string> palette)
        {
            if (palette == null || palette.Count == 0) return null;
            var r = new byte[256];
            for (int i = 1; i < palette.Count && i < 256; i++)
            {
                var b = world.Registry.Find(palette[i]);
                r[i] = b != null ? b.Id : (byte)0;
                if (b == null && !string.IsNullOrEmpty(palette[i])) Debug.LogWarning("[VRKit] El bloque guardado «" + palette[i] + "» ya no existe: se cambia por aire.");
            }
            return r;
        }

        // ================================================================== Modo

        public void SetCreative(bool creative)
        {
            var m = creative ? VoxelGameMode.Creativo : VoxelGameMode.Supervivencia;
            if (m == mode) return;
            mode = m;
            ApplyMode(true);
        }

        public void ToggleMode() { SetCreative(!IsCreative); }

        void ApplyMode(bool notify)
        {
            if (inventory != null) inventory.Creative = IsCreative;
            if (PlayerHealth.Instance != null)
            {
                PlayerHealth.Instance.Invincible = IsCreative;
                if (IsCreative) PlayerHealth.Instance.ResetHealth();
            }
            if (notify) onModeChanged.Invoke(IsCreative);
        }

        // ================================================================== Morir y volver

        float InterceptDeath(DamageInfo info, float amount)
        {
            var ph = PlayerHealth.Instance;
            if (!respawnOnDeath || ph == null || IsCreative) return amount;
            if (amount >= ph.Current && !m_PendingRespawn)
            {
                m_PendingRespawn = true;
                StartCoroutine(DieAndRespawn());
                return 0f;
            }
            return m_PendingRespawn ? 0f : amount;
        }

        IEnumerator DieAndRespawn()
        {
            var fader = ScreenFader.Instance;
            SubtitleSystem.SayStatic("", "Has muerto. Vuelves al punto de aparición.");
            if (fader != null) yield return fader.FadeTo(1f, 0.5f);
            Respawn(false);
            if (PlayerHealth.Instance != null) PlayerHealth.Instance.ResetHealth();
            yield return new WaitForSeconds(0.3f);
            if (fader != null) yield return fader.FadeTo(0f, 0.5f);
            m_PendingRespawn = false;
        }

        /// <summary>Lleva al jugador a su punto de aparición (al caer al vacío, al morir o desde un botón).</summary>
        public void Respawn() { Respawn(true); }

        void Respawn(bool fade)
        {
            var player = VRPlayer.Instance;
            if (player == null || world == null) return;
            var col = new Vector3Int(Mathf.FloorToInt(m_SpawnLocal.x), 0, Mathf.FloorToInt(m_SpawnLocal.z));
            var target = world.ToWorld(SurfacePoint(col.x, col.z));
            if (fade) player.TeleportWithFade(target);
            else player.TeleportTo(target);
            var vp = player.GetComponent<VoxelPlayer>();
            if (vp != null) vp.ResetFall();
            onRespawn.Invoke();
        }

        /// <summary>El punto de aparición pasa a ser donde está el jugador (una cama, un botón…).</summary>
        public void SetSpawnHere()
        {
            var player = VRPlayer.Instance;
            if (player != null) m_SpawnLocal = world.ToLocal(player.FeetPosition);
        }

        // ================================================================== Guardar

        void Update()
        {
            if (!m_Loading && autosaveMinutes > 0f && Time.time >= m_NextAutosave)
            {
                m_NextAutosave = Time.time + autosaveMinutes * 60f;
                Save();
            }
        }

        void OnApplicationPause(bool paused) { if (paused) Save(); }   // en Quest: quitarse las gafas o ir al menú
        void OnApplicationQuit() { Save(); }

        public void Save()
        {
            if (m_Loading || world == null || !world.IsReady) return;
            var d = new SaveData
            {
                seed = world.Seed,
                height = world.Height,
                mode = mode.ToString(),
                spawnX = m_SpawnLocal.x, spawnY = m_SpawnLocal.y, spawnZ = m_SpawnLocal.z,
                chunks = world.GetEditedChunks(),
            };
            d.palette.AddRange(world.Registry.Palette());
            var player = VRPlayer.Instance;
            if (player != null && placePlayer)
            {
                var p = world.ToLocal(player.FeetPosition);
                d.px = p.x; d.py = p.y + 0.05f; d.pz = p.z;
                d.yaw = player.Head != null ? player.Head.eulerAngles.y : 0f;
                d.hasPlayer = true;
            }
            if (inventory != null)
            {
                d.items = inventory.ToSave();
                var sel = inventory.SelectedType;
                d.selected = sel != null ? sel.displayName : null;
            }
            if (VoxelDayNight.Instance != null) d.timeOfDay = VoxelDayNight.Instance.TimeOfDay;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
                string tmp = SavePath + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(d));
                if (File.Exists(SavePath)) File.Delete(SavePath);
                File.Move(tmp, SavePath);   // así un corte a mitad no deja el guardado roto
            }
            catch (Exception e) { Debug.LogWarning("[VRKit] No se pudo guardar el mundo: " + e.Message); }
        }

        SaveData ReadSave()
        {
            try
            {
                if (!File.Exists(SavePath)) return null;
                return JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VRKit] Partida guardada ilegible, se empieza un mundo nuevo: " + e.Message);
                return null;
            }
        }

        /// <summary>Borra la partida guardada y empieza un mundo nuevo (semilla 0 = al azar).</summary>
        public void NewWorld(int seed = 0)
        {
            try { if (File.Exists(SavePath)) File.Delete(SavePath); } catch (Exception) { }
            foreach (var mob in FindObjectsByType<VoxelMob>(FindObjectsSortMode.None)) Destroy(mob.gameObject);
            StopAllCoroutines();
            StartCoroutine(Begin(seed, null));
        }

        public void NewWorldRandom() { NewWorld(0); }
    }
}
