using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace VRKit
{
    /// <summary>
    /// El mapa del mundo abierto.
    ///   · Se va descubriendo lo que exploras (el resto, en niebla). Desde lo alto se ve más lejos. Se guarda con la partida.
    ///   · Minimapa en la muñeca izquierda: gira la muñeca y míralo como un reloj. El norte del minimapa apunta al norte de verdad.
    ///   · Mapa grande: botón Y (o M en el teclado). Joystick izquierdo = moverlo, joystick derecho arriba/abajo = zoom,
    ///     X = poner o quitar una marca donde estás, Y = cerrar. Mientras está abierto no te mueves.
    ///   · Salen: las cuevas (con pista «?» si hay una cerca que no has visto), el punto de inicio, tus marcas
    ///     y todo lo que lleve un MapMarker (pueblos, ruinas…). Para cosas que se generan con la semilla: AddSource.
    /// Todo el dibujo se hace en otro hilo: en las gafas solo cuesta subir la textura cuando cambia.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class WorldMap : MonoBehaviour
    {
        public enum OpenButton { Y_o_B, X_o_A, Ninguno }

        public static WorldMap Instance { get; private set; }

        [Header("Explorar")]
        [Tooltip("Radio que se descubre alrededor del jugador (metros).")]
        [SerializeField] float revealRadius = 48f;
        [Tooltip("Metros de más por cada metro de altura sobre el agua (desde lo alto se ve más).")]
        [SerializeField] float heightBonus = 0.6f;
        [SerializeField] float maxRevealRadius = 140f;

        [Header("Pistas")]
        [Tooltip("Si hay una cueva (o algo con pista) cerca que no has visto, sale un «?» por la zona.")]
        [SerializeField] bool hints = true;
        [SerializeField] float hintRadius = 380f;
        [SerializeField, Range(1, 5)] int hintCount = 2;
        [Tooltip("Lo impreciso que es la pista (metros).")]
        [SerializeField] float hintBlur = 35f;

        [Header("Mapa grande")]
        [SerializeField] Hand buttonHand = Hand.Left;
        [SerializeField] OpenButton openButton = OpenButton.Y_o_B;
        [Tooltip("Tecla para probar en el editor.")]
        [SerializeField] Key keyboardKey = Key.M;
        [Tooltip("Lado del mapa (metros).")]
        [SerializeField] float mapSize = 0.8f;
        [Tooltip("A qué distancia de la cara se abre (metros).")]
        [SerializeField] float mapDistance = 0.7f;
        [SerializeField] int mapResolution = 512;
        [Tooltip("Metros por píxel de cada zoom.")]
        [SerializeField] float[] zoomLevels = { 1f, 2f, 4f, 8f, 16f };
        [SerializeField] int startZoom = 1;

        [Header("Minimapa (muñeca)")]
        [SerializeField] bool showMinimap = true;
        [SerializeField] Hand minimapHand = Hand.Left;
        [Tooltip("Diámetro (metros).")]
        [SerializeField] float minimapDiameter = 0.085f;
        [SerializeField] float minimapMetersPerPixel = 1.5f;
        [SerializeField] int minimapResolution = 128;
        [Tooltip("Dónde va respecto al mando (metros, ejes del mando).")]
        [SerializeField] Vector3 minimapOffset = new Vector3(0f, 0.035f, -0.075f);
        [Tooltip("Inclinación hacia ti (grados).")]
        [SerializeField] float minimapTilt = 25f;
        [Tooltip("Solo se ve al girar la muñeca para mirarlo (como un reloj).")]
        [SerializeField] bool minimapOnlyWhenLooking = true;
        [SerializeField, Range(10f, 80f)] float lookAngle = 50f;

        [Header("Aspecto y sonido")]
        [Tooltip("Material con el shader VRKit/Mapa (lo crea el builder).")]
        [SerializeField] Material mapMaterial;
        [SerializeField] Color textColor = new Color(0.96f, 0.93f, 0.84f);
        [SerializeField] AudioClip openSound;
        [SerializeField] AudioClip markSound;
        [SerializeField] AudioClip discoverSound;

        public UnityEvent<string> onDiscover = new UnityEvent<string>();
        public UnityEvent onOpen = new UnityEvent();
        public UnityEvent onClose = new UnityEvent();

        // ------------------------------------------------------------------ Estado

        [Serializable] class KnownSave { public string id, name; public float x, z; public int icon; }
        [Serializable] class MapSave
        {
            public int version = 1, seed, pins;
            public List<long> keys = new List<long>();
            public List<int> masks = new List<int>();
            public List<KnownSave> known = new List<KnownSave>();
        }

        class Label { public string text; public float px, py; }

        class ComposeJob
        {
            public MapRaster.View view;
            public Color32[] pixels;
            public float playerX, playerZ, yaw;
            public bool big;
            public HashSet<string> discovered;
            public List<MapFeature> extra;
            public readonly List<Label> labels = new List<Label>();
            public bool needsMore;
            public Task task;
        }

        static readonly List<IMapFeatureSource> s_Sources = new List<IMapFeatureSource>();

        OpenWorld m_World;
        ExploredMap m_Explored;
        readonly Dictionary<long, Color32[]> m_Tiles = new Dictionary<long, Color32[]>();
        readonly Dictionary<long, List<MapFeature>> m_Regions = new Dictionary<long, List<MapFeature>>();
        readonly object m_TileLock = new object(), m_RegionLock = new object();
        readonly List<KnownSave> m_Known = new List<KnownSave>();
        readonly HashSet<string> m_Announced = new HashSet<string>();
        CaveMapSource m_Caves;
        OpenTerrainGenerator m_Gen;
        Color32[] m_BiomeColors;
        int m_Pins, m_Seed;
        bool m_Loaded;
        const float RegionSize = 256f;

        // Vista
        HandInput m_Left, m_Right, m_Button;
        InputAction m_Key;
        GameObject m_Big, m_Mini;
        Transform m_MiniQuad;
        Texture2D m_BigTex, m_MiniTex;
        Material m_BigMat, m_MiniMat, m_FrameMat;
        Mesh m_Quad;
        TextMeshPro m_Info, m_Fps;
        readonly List<TextMeshPro> m_Labels = new List<TextMeshPro>();
        ComposeJob m_BigJob, m_MiniJob, m_ScanJob;
        Color32[] m_BigBuf, m_MiniBuf;    // se reutilizan (no se crea basura en cada vuelta)
        float m_NextBig, m_NextMini, m_NextReveal, m_NextScan, m_NextMarkers, m_MiniShown;
        Vector3 m_LastReveal = new Vector3(float.MaxValue, 0f, 0f);
        Vector2 m_Pan;
        int m_Zoom;
        bool m_Open, m_Dirty = true, m_ZoomHeld;

        public bool IsOpen { get { return m_Open; } }
        /// <summary>Metros cuadrados explorados.</summary>
        public float ExploredArea { get { return m_Explored != null ? m_Explored.Area : 0f; } }

        /// <summary>Añade algo que pone cosas en el mapa a partir de la semilla (pueblos generados…).</summary>
        public static void AddSource(IMapFeatureSource source)
        {
            lock (s_Sources) if (source != null && !s_Sources.Contains(source)) s_Sources.Add(source);
            if (Instance != null) Instance.ClearRegions();
        }
        public static void RemoveSource(IMapFeatureSource source)
        {
            lock (s_Sources) s_Sources.Remove(source);
            if (Instance != null) Instance.ClearRegions();
        }

        // ================================================================== Arranque

        void Awake() { Instance = this; m_Zoom = Mathf.Clamp(startZoom, 0, zoomLevels.Length - 1); }

        void OnEnable()
        {
            m_Left = new HandInput(Hand.Left);
            m_Right = new HandInput(Hand.Right);
            m_Button = buttonHand == Hand.Left ? m_Left : m_Right;
            m_Key = new InputAction("Mapa", InputActionType.Button);
            m_Key.AddBinding("<Keyboard>/" + keyboardKey.ToString().ToLowerInvariant());
            m_Key.Enable();
        }

        void OnDisable()
        {
            if (m_Open) Close();
            if (m_Left != null) m_Left.Dispose();
            if (m_Right != null) m_Right.Dispose();
            m_Left = m_Right = m_Button = null;
            if (m_Key != null) { m_Key.Dispose(); m_Key = null; }
            Unhook();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            foreach (var o in new UnityEngine.Object[] { m_BigTex, m_MiniTex, m_BigMat, m_MiniMat, m_FrameMat, m_Quad }) if (o != null) Destroy(o);
        }

        void Hook(OpenWorld w)
        {
            Unhook();
            m_World = w;
            if (w == null) return;
            w.WorldStarted += OnWorldStarted;
            w.Saving += Save;
            if (w.Generator != null) OnWorldStarted();
        }

        void Unhook()
        {
            if (m_World == null) return;
            m_World.WorldStarted -= OnWorldStarted;
            m_World.Saving -= Save;
            m_World = null;
        }

        void OnWorldStarted()
        {
            m_Gen = m_World.Generator;
            m_Seed = m_World.Seed;
            m_Explored = new ExploredMap(m_World.ChunkSize);
            m_BiomeColors = new Color32[8];
            for (int i = 0; i < 8; i++) m_BiomeColors[i] = m_World.GroundColor((Biome)i);
            m_Caves = new CaveMapSource(m_World.CaveSpacing, m_World.CaveChance);
            lock (m_TileLock) m_Tiles.Clear();
            ClearRegions();
            m_Known.Clear(); m_Pins = 0; m_Announced.Clear();
            m_LastReveal = new Vector3(float.MaxValue, 0f, 0f);
            m_BigJob = m_MiniJob = m_ScanJob = null;   // lo que estuviera en marcha ya no vale

            var save = OpenWorld.ReadJson<MapSave>(Path.Combine(m_World.SaveFolder, "mapa.json"));
            if (save != null && save.seed == m_Seed)
            {
                m_Explored.Import(save.keys, save.masks);
                if (save.known != null) m_Known.AddRange(save.known);
                m_Pins = save.pins;
            }
            m_Loaded = true;
            m_Dirty = true;
        }

        void ClearRegions() { lock (m_RegionLock) m_Regions.Clear(); m_Dirty = true; }

        /// <summary>Guarda lo explorado y lo conocido (lo llama OpenWorld al guardar).</summary>
        public void Save()
        {
            if (!m_Loaded || m_World == null) return;
            var s = new MapSave { seed = m_Seed, pins = m_Pins, known = new List<KnownSave>(m_Known) };
            m_Explored.Export(s.keys, s.masks);
            OpenWorld.WriteJson(Path.Combine(m_World.SaveFolder, "mapa.json"), s);
        }

        // ================================================================== Cada frame

        void Update()
        {
            var w = OpenWorld.Instance;
            if (w != m_World) Hook(w);
            if (!m_Loaded || m_World == null || !m_World.IsReady) return;
            var player = VRPlayer.Instance;
            if (player == null) return;
            bool underground = m_World.PlayerUnderground;
            var feet = player.FeetPosition;

            if (!underground) Reveal(feet);
            if (!underground && Time.time >= m_NextMarkers) { m_NextMarkers = Time.time + 0.5f; CheckMarkers(feet); }
            if (!underground && Time.time >= m_NextScan && m_ScanJob == null) { m_NextScan = Time.time + 1.5f; StartScan(feet); }
            if (m_ScanJob != null && m_ScanJob.task.IsCompleted) FinishScan();

            // Abrir / cerrar
            bool pressed = m_Key != null && m_Key.WasPressedThisFrame();
            if (m_Button != null && openButton != OpenButton.Ninguno)
                pressed |= (openButton == OpenButton.Y_o_B ? m_Button.Secondary : m_Button.Primary).WasPressedThisFrame();
            if (pressed) { if (m_Open) Close(); else if (!underground) Open(); }

            if (m_Open) UpdateBig(player);
            else if (showMinimap) UpdateMini(player);
        }

        void Reveal(Vector3 feet)
        {
            if (Time.time < m_NextReveal) return;
            m_NextReveal = Time.time + 0.2f;
            if ((feet - m_LastReveal).sqrMagnitude < 4f) return;
            m_LastReveal = feet;
            float r = Mathf.Min(maxRevealRadius, revealRadius + Mathf.Max(0f, feet.y - m_World.WaterLevel - 12f) * heightBonus);
            if (m_Explored.Reveal(feet.x, feet.z, r) > 0) m_Dirty = true;
            if (!HasKnown("inicio"))
                m_Known.Add(new KnownSave { id = "inicio", name = "Inicio", x = feet.x, z = feet.z, icon = (int)MapIcon.Casa });
        }

        bool HasKnown(string id) { foreach (var k in m_Known) if (k.id == id) return true; return false; }

        // ------------------------------------------------------------------ Descubrir (MapMarker y fuentes con autoDiscover)

        void CheckMarkers(Vector3 feet)
        {
            for (int i = MapMarker.All.Count - 1; i >= 0; i--)
            {
                var m = MapMarker.All[i];
                if (m == null || m.AlwaysKnown) continue;
                var p = m.transform.position;
                float dx = p.x - feet.x, dz = p.z - feet.z;
                if (dx * dx + dz * dz > m.DiscoverRadius * m.DiscoverRadius) continue;
                string id = m.Id;
                if (!m_World.Discover(id)) continue;
                if (!HasKnown(id)) m_Known.Add(new KnownSave { id = id, name = m.DisplayName, x = p.x, z = p.z, icon = (int)m.Icon });
                Announce(m.Announce ? m.DisplayName : null);
            }
        }

        void Announce(string name)
        {
            m_Dirty = true;
            if (!string.IsNullOrEmpty(name)) SubtitleSystem.SayStatic("", "Descubres: " + name);
            if (discoverSound != null) VRAudio.Play2D(discoverSound, 0.6f);
            onDiscover.Invoke(name ?? "");
        }

        void StartScan(Vector3 feet)
        {
            var job = new ComposeJob { playerX = feet.x, playerZ = feet.z };
            job.extra = new List<MapFeature>();
            m_ScanJob = job;
            job.task = Task.Run(() =>
            {
                var near = new List<MapFeature>();
                CollectRegions(job.playerX - 80f, job.playerZ - 80f, job.playerX + 80f, job.playerZ + 80f, near);
                foreach (var f in near)
                {
                    if (!f.autoDiscover) continue;
                    float dx = f.x - job.playerX, dz = f.z - job.playerZ, r = f.discoverRadius > 0f ? f.discoverRadius : 18f;
                    if (dx * dx + dz * dz <= r * r) job.extra.Add(f);
                }
            });
        }

        void FinishScan()
        {
            var job = m_ScanJob;
            m_ScanJob = null;
            if (job.task.IsFaulted) { Debug.LogException(job.task.Exception); return; }
            foreach (var f in job.extra)
                if (m_World.Discover(f.id)) Announce(f.name);
        }

        // ------------------------------------------------------------------ Mapa grande

        void Open()
        {
            var player = VRPlayer.Instance;
            if (player == null || player.Head == null) return;
            EnsureBig();
            var head = player.Head;
            Vector3 fwd = player.HeadForwardFlat;
            m_Big.transform.SetParent(player.transform, true);
            m_Big.transform.position = head.position + fwd * mapDistance - Vector3.up * 0.12f;
            m_Big.transform.rotation = Quaternion.LookRotation(m_Big.transform.position - head.position, Vector3.up);
            m_Big.SetActive(true);
            if (m_Mini != null) m_Mini.SetActive(false);
            m_Pan = Vector2.zero;
            m_Open = true;
            m_Dirty = true;
            m_NextBig = 0f;
            player.SetLocomotionEnabled(false);
            if (openSound != null) VRAudio.Play2D(openSound, 0.5f);
            onOpen.Invoke();
        }

        public void Close()
        {
            if (!m_Open) return;
            m_Open = false;
            if (m_Big != null) m_Big.SetActive(false);
            var player = VRPlayer.Instance;
            if (player != null) player.SetLocomotionEnabled(true);
            if (openSound != null) VRAudio.Play2D(openSound, 0.35f);
            onClose.Invoke();
        }

        void UpdateBig(VRPlayer player)
        {
            if (m_World.PlayerUnderground) { Close(); return; }
            float mpp = zoomLevels[m_Zoom];

            // Mover con el joystick izquierdo; zoom con el derecho
            Vector2 pan = m_Left.Stick.ReadValue<Vector2>();
            if (pan.sqrMagnitude > 0.04f) { m_Pan += pan * (mapResolution * mpp * 0.45f * Time.deltaTime); m_Dirty = true; }
            float zy = m_Right.Stick.ReadValue<Vector2>().y;
            if (Mathf.Abs(zy) > 0.7f)
            {
                if (!m_ZoomHeld)
                {
                    m_ZoomHeld = true;
                    int z = Mathf.Clamp(m_Zoom + (zy > 0f ? -1 : 1), 0, zoomLevels.Length - 1);
                    if (z != m_Zoom) { m_Zoom = z; m_Dirty = true; m_NextBig = 0f; if (markSound != null) VRAudio.Play2D(markSound, 0.25f); }
                }
            }
            else if (Mathf.Abs(zy) < 0.3f) m_ZoomHeld = false;

            // Marca
            var markButton = openButton == OpenButton.X_o_A ? m_Button.Secondary : m_Button.Primary;
            if (markButton.WasPressedThisFrame()) TogglePin(player.FeetPosition);

            if (m_BigJob != null && m_BigJob.task.IsCompleted) FinishCompose(m_BigJob, m_BigTex);
            if (m_BigJob == null && Time.time >= m_NextBig)
            {
                var feet = player.FeetPosition;
                m_NextBig = Time.time + (m_Dirty ? 0.15f : 0.5f);
                m_Dirty = false;
                var v = new MapRaster.View { centerX = feet.x + m_Pan.x, centerZ = feet.z + m_Pan.y, mpp = mpp, width = mapResolution, height = mapResolution };
                m_BigJob = StartCompose(v, true, feet, Yaw(player));
            }
            if (m_Info != null && Time.frameCount % 30 == 0)
                m_Info.text = "Explorado: " + (ExploredArea / 1e6f).ToString("0.00") + " km²   ·   Zoom " + (m_Zoom + 1) + "/" + zoomLevels.Length;
        }

        void TogglePin(Vector3 feet)
        {
            for (int i = m_Known.Count - 1; i >= 0; i--)
            {
                var k = m_Known[i];
                if (k.icon != (int)MapIcon.Marca) continue;
                float dx = k.x - feet.x, dz = k.z - feet.z;
                if (dx * dx + dz * dz < 12f * 12f) { m_Known.RemoveAt(i); m_Dirty = true; m_NextBig = 0f; if (markSound != null) VRAudio.Play2D(markSound, 0.4f); return; }
            }
            m_Pins++;
            m_Known.Add(new KnownSave { id = "pin:" + m_Pins, name = "", x = feet.x, z = feet.z, icon = (int)MapIcon.Marca });
            m_Dirty = true; m_NextBig = 0f;
            if (markSound != null) VRAudio.Play2D(markSound, 0.6f);
        }

        static float Yaw(VRPlayer player) { var f = player.HeadForwardFlat; return Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg; }

        // ------------------------------------------------------------------ Minimapa

        void UpdateMini(VRPlayer player)
        {
            var hand = player.GetHand(minimapHand);
            if (hand == null || player.Head == null) return;
            EnsureMini(hand);

            // ¿Lo está mirando? (como un reloj)
            Vector3 normal = hand.TransformDirection(Quaternion.Euler(-minimapTilt, 0f, 0f) * Vector3.up);
            Vector3 pos = m_Mini.transform.position;
            Vector3 toHead = player.Head.position - pos;
            bool look = !minimapOnlyWhenLooking || (Vector3.Dot(normal, toHead.normalized) > Mathf.Cos(lookAngle * Mathf.Deg2Rad) && toHead.magnitude < 0.75f);
            bool underground = m_World.PlayerUnderground;
            m_MiniShown = Mathf.MoveTowards(m_MiniShown, look && !underground ? 1f : 0f, Time.deltaTime * 6f);
            bool visible = m_MiniShown > 0.01f;
            if (m_Mini.activeSelf != visible) m_Mini.SetActive(visible);
            if (!visible) return;

            // El norte del minimapa apunta al norte de verdad
            Vector3 north = Vector3.ProjectOnPlane(Vector3.forward, normal);
            if (north.sqrMagnitude < 1e-4f) north = Vector3.ProjectOnPlane(hand.forward, normal);
            m_MiniQuad.rotation = Quaternion.LookRotation(-normal, north.normalized);
            m_MiniQuad.localScale = Vector3.one * (minimapDiameter * Mathf.SmoothStep(0.3f, 1f, m_MiniShown));

            if (m_Fps != null)
            {
                bool dev = VRDevMode.Active;
                if (m_Fps.gameObject.activeSelf != dev) m_Fps.gameObject.SetActive(dev);
                if (dev)
                {
                    m_Fps.transform.position = pos + normal * 0.005f + Vector3.up * (minimapDiameter * 0.75f);
                    m_Fps.transform.rotation = Quaternion.LookRotation(m_Fps.transform.position - player.Head.position, Vector3.up);
                    if (Time.frameCount % 15 == 0)
                    {
                        float fps = VRPerformance.Fps > 0f ? VRPerformance.Fps : 1f / Mathf.Max(0.001f, Time.smoothDeltaTime);
                        m_Fps.text = fps.ToString("0") + " FPS · " + m_World.ViewDistance.ToString("0") + " m";
                        m_Fps.color = fps >= VRPerformance.DisplayFrameRate * 0.95f ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 0.6f, 0.4f);
                    }
                }
            }

            if (m_MiniJob != null && m_MiniJob.task.IsCompleted) FinishCompose(m_MiniJob, m_MiniTex);
            if (m_MiniJob == null && Time.time >= m_NextMini)
            {
                m_NextMini = Time.time + 0.2f;
                var feet = player.FeetPosition;
                var v = new MapRaster.View { centerX = feet.x, centerZ = feet.z, mpp = minimapMetersPerPixel, width = minimapResolution, height = minimapResolution, circle = true };
                m_MiniJob = StartCompose(v, false, feet, Yaw(player));
            }
        }

        // ------------------------------------------------------------------ Componer (otro hilo)

        ComposeJob StartCompose(MapRaster.View v, bool big, Vector3 feet, float yaw)
        {
            var job = new ComposeJob { view = v, big = big, playerX = feet.x, playerZ = feet.z, yaw = yaw };
            int count = v.width * v.height;
            if (big) { if (m_BigBuf == null || m_BigBuf.Length != count) m_BigBuf = new Color32[count]; job.pixels = m_BigBuf; }
            else { if (m_MiniBuf == null || m_MiniBuf.Length != count) m_MiniBuf = new Color32[count]; job.pixels = m_MiniBuf; }
            job.discovered = new HashSet<string>(m_World.Discovered);
            job.extra = new List<MapFeature>();
            foreach (var k in m_Known)
            {
                job.extra.Add(new MapFeature { id = k.id, name = k.name, x = k.x, z = k.z, icon = (MapIcon)k.icon });
                job.discovered.Add(k.id);
            }
            foreach (var m in MapMarker.All)
            {
                if (m == null) continue;
                var f = m.ToFeature();
                if (m.AlwaysKnown) job.discovered.Add(f.id);
                job.extra.Add(f);
            }
            var gen = m_Gen; var explored = m_Explored;
            job.task = Task.Run(() => Compose(job, gen, explored));
            return job;
        }

        void Compose(ComposeJob job, OpenTerrainGenerator gen, ExploredMap explored)
        {
            var v = job.view;
            var missing = new HashSet<long>();
            MapRaster.Compose(job.pixels, v, explored, GetTile, missing);

            // Lo que sale en el mapa
            float halfW = v.width * 0.5f * v.mpp, halfH = v.height * 0.5f * v.mpp;
            float reach = hints ? hintRadius : 0f;
            var features = new List<MapFeature>();
            CollectRegions(Math.Min(v.centerX - halfW, job.playerX - reach), Math.Min(v.centerZ - halfH, job.playerZ - reach),
                           Math.Max(v.centerX + halfW, job.playerX + reach), Math.Max(v.centerZ + halfH, job.playerZ + reach), features);
            features.AddRange(job.extra);

            float s = job.big ? Math.Max(5f, v.width / 80f) : Math.Max(3f, v.width / 34f);
            var hintList = new List<KeyValuePair<float, MapFeature>>();
            var seenIds = new HashSet<string>();
            foreach (var f in features)
            {
                if (f.id != null && !seenIds.Add(f.id)) continue;
                MapFeatureState st;
                if (f.id != null && job.discovered.Contains(f.id)) st = MapFeatureState.Descubierto;
                else if (explored.IsExplored(f.x, f.z)) st = MapFeatureState.Visto;
                else
                {
                    if (hints && f.hint)
                    {
                        float dx = f.x - job.playerX, dz = f.z - job.playerZ, d2 = dx * dx + dz * dz;
                        if (d2 < hintRadius * hintRadius) hintList.Add(new KeyValuePair<float, MapFeature>(d2, f));
                    }
                    continue;
                }
                float px, py;
                MapRaster.ToPixel(v, f.x, f.z, out px, out py);
                if (px < -s * 2 || py < -s * 2 || px > v.width + s * 2 || py > v.height + s * 2) continue;
                MapRaster.DrawFeature(job.pixels, v.width, v.height, px, py, f.icon, st, s);
                if (job.big && st == MapFeatureState.Descubierto && !string.IsNullOrEmpty(f.name))
                    job.labels.Add(new Label { text = f.name, px = px, py = py + s * 1.6f });
            }
            hintList.Sort((a, b) => a.Key.CompareTo(b.Key));
            for (int i = 0; i < hintList.Count && i < hintCount; i++)
            {
                var f = hintList[i].Value;
                // Movida un poco (siempre igual para la misma cosa): la pista dice la zona, no el sitio exacto
                int hsh = f.id != null ? f.id.GetHashCode() : 0;
                float ox = ((hsh & 0xff) / 255f - 0.5f) * 2f * hintBlur * 0.7f, oz = (((hsh >> 8) & 0xff) / 255f - 0.5f) * 2f * hintBlur * 0.7f;
                float px, py;
                MapRaster.ToPixel(v, f.x + ox, f.z + oz, out px, out py);
                MapRaster.DrawHint(job.pixels, v.width, v.height, px, py, Math.Max(s * 1.6f, hintBlur * 1.3f / v.mpp), s);
            }

            float ppx, ppy;
            MapRaster.ToPixel(v, job.playerX, job.playerZ, out ppx, out ppy);
            MapRaster.DrawPlayer(job.pixels, v.width, v.height, ppx, ppy, job.yaw, job.big ? s * 1.1f : s * 1.2f);
            int ts = job.big ? Math.Max(2, v.width / 256) : 1;
            MapRaster.DrawNorth(job.pixels, v.width, v.height, ts, v.circle);
            if (job.big) MapRaster.DrawScaleBar(job.pixels, v.width, v.height, v.mpp, ts);

            // Azulejos que faltan: unos cuantos ahora (los más cerca del centro); el resto en la siguiente vuelta
            if (missing.Count > 0)
            {
                var list = new List<long>(missing);
                float size = explored.chunkSize;
                list.Sort((a, b) => Dist(a, v, size).CompareTo(Dist(b, v, size)));
                var sw = System.Diagnostics.Stopwatch.StartNew();
                foreach (var key in list)
                {
                    int cx = (int)(key >> 32), cz = (int)(key & 0xffffffffL);
                    var tile = MapRaster.BuildTile(gen, cx, cz, size, m_BiomeColors);
                    lock (m_TileLock)
                    {
                        if (m_Tiles.Count > 6000) m_Tiles.Clear();
                        m_Tiles[key] = tile;
                    }
                    if (sw.ElapsedMilliseconds > 30) break;
                }
                job.needsMore = true;
            }
        }

        static float Dist(long key, MapRaster.View v, float size)
        {
            float x = ((int)(key >> 32) + 0.5f) * size - v.centerX, z = ((int)(key & 0xffffffffL) + 0.5f) * size - v.centerZ;
            return x * x + z * z;
        }

        Color32[] GetTile(int cx, int cz)
        {
            Color32[] t;
            lock (m_TileLock) m_Tiles.TryGetValue(ExploredMap.Key(cx, cz), out t);
            return t;
        }

        void CollectRegions(float x0, float z0, float x1, float z1, List<MapFeature> output)
        {
            var gen = m_Gen;
            if (gen == null) return;
            int rx0 = (int)Math.Floor(x0 / RegionSize), rx1 = (int)Math.Floor(x1 / RegionSize);
            int rz0 = (int)Math.Floor(z0 / RegionSize), rz1 = (int)Math.Floor(z1 / RegionSize);
            IMapFeatureSource[] sources;
            lock (s_Sources) sources = s_Sources.ToArray();
            for (int rz = rz0; rz <= rz1; rz++)
            for (int rx = rx0; rx <= rx1; rx++)
            {
                long key = ExploredMap.Key(rx, rz);
                List<MapFeature> list;
                lock (m_RegionLock) m_Regions.TryGetValue(key, out list);
                if (list == null)
                {
                    list = new List<MapFeature>();
                    try
                    {
                        if (m_Caves != null) m_Caves.FeaturesIn(gen, rx, rz, RegionSize, list);
                        foreach (var s in sources) s.FeaturesIn(gen, rx, rz, RegionSize, list);
                    }
                    catch (Exception ex) { Debug.LogException(ex); }
                    lock (m_RegionLock) { if (m_Regions.Count > 4000) m_Regions.Clear(); m_Regions[key] = list; }
                }
                output.AddRange(list);
            }
        }

        void FinishCompose(ComposeJob job, Texture2D tex)
        {
            if (job == m_BigJob) m_BigJob = null;
            if (job == m_MiniJob) m_MiniJob = null;
            if (job.task.IsFaulted) { Debug.LogException(job.task.Exception); return; }
            if (tex == null || tex.width != job.view.width) return;
            tex.SetPixels32(job.pixels);
            tex.Apply(false);
            if (job.needsMore) { m_Dirty = true; if (job.big) m_NextBig = 0f; else m_NextMini = 0f; }
            if (job.big) PlaceLabels(job);
        }

        void PlaceLabels(ComposeJob job)
        {
            // Los nombres más cerca del centro, como mucho 14
            job.labels.Sort((a, b) => Sq(a, job.view).CompareTo(Sq(b, job.view)));
            int n = Mathf.Min(14, job.labels.Count);
            while (m_Labels.Count < n) m_Labels.Add(MakeText(m_Big.transform, 0.22f, textColor, TextAlignmentOptions.Bottom));
            for (int i = 0; i < m_Labels.Count; i++)
            {
                var t = m_Labels[i];
                if (i >= n) { if (t.gameObject.activeSelf) t.gameObject.SetActive(false); continue; }
                var l = job.labels[i];
                t.gameObject.SetActive(true);
                t.text = l.text;
                t.transform.localPosition = new Vector3((l.px / job.view.width - 0.5f) * mapSize, (l.py / job.view.height - 0.5f) * mapSize, -0.004f);
            }
        }

        static float Sq(Label l, MapRaster.View v) { float x = l.px - v.width * 0.5f, y = l.py - v.height * 0.5f; return x * x + y * y; }

        // ------------------------------------------------------------------ Objetos

        Material NewMaterial(Texture tex)
        {
            Material m;
            if (mapMaterial != null) m = new Material(mapMaterial);
            else
            {
                var sh = Shader.Find("VRKit/Mapa");
                if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
                m = new Material(sh);
            }
            m.mainTexture = tex;
            return m;
        }

        Mesh Quad()
        {
            if (m_Quad != null) return m_Quad;
            m_Quad = new Mesh { name = "Mapa" };
            m_Quad.SetVertices(new List<Vector3> { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) });
            m_Quad.SetUVs(0, new List<Vector2> { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
            m_Quad.SetTriangles(new List<int> { 0, 2, 1, 2, 3, 1 }, 0);
            m_Quad.RecalculateNormals();
            m_Quad.RecalculateBounds();
            return m_Quad;
        }

        GameObject QuadObject(string name, Transform parent, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = Quad();
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        static Texture2D NewTexture(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Mapa", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = MapRaster.FogColor;
            t.SetPixels32(px);
            t.Apply(false);
            return t;
        }

        void EnsureBig()
        {
            if (m_Big != null) return;
            m_Big = new GameObject("Mapa grande");
            m_BigTex = NewTexture(mapResolution);
            m_BigMat = NewMaterial(m_BigTex);
            var frameTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            frameTex.SetPixel(0, 0, new Color(0.13f, 0.11f, 0.1f, 1f)); frameTex.Apply(false);
            m_FrameMat = NewMaterial(frameTex);
            var map = QuadObject("Mapa", m_Big.transform, m_BigMat);
            map.transform.localScale = new Vector3(mapSize, mapSize, 1f);
            var frame = QuadObject("Marco", m_Big.transform, m_FrameMat);
            frame.transform.localPosition = new Vector3(0f, 0.025f, 0.004f);
            frame.transform.localScale = new Vector3(mapSize + 0.04f, mapSize + 0.11f, 1f);
            var title = MakeText(m_Big.transform, 0.42f, textColor, TextAlignmentOptions.Left);
            title.text = "<b>Mapa</b>";
            title.transform.localPosition = new Vector3(-mapSize * 0.5f + 0.5f * 0.6f, mapSize * 0.5f + 0.035f, -0.002f);
            m_Info = MakeText(m_Big.transform, 0.2f, textColor, TextAlignmentOptions.Right);
            m_Info.transform.localPosition = new Vector3(mapSize * 0.5f - 0.5f * 0.6f, mapSize * 0.5f + 0.03f, -0.002f);
            var help = MakeText(m_Big.transform, 0.17f, new Color(textColor.r, textColor.g, textColor.b, 0.8f), TextAlignmentOptions.Center);
            string mark = openButton == OpenButton.X_o_A ? "Y" : "X";
            string close = openButton == OpenButton.X_o_A ? "X" : "Y";
            help.text = "Joystick izq.: mover   ·   Joystick der.: zoom   ·   " + mark + ": marca aquí   ·   " + close + ": cerrar";
            help.transform.localPosition = new Vector3(0f, -mapSize * 0.5f - 0.0f + 0.0f, -0.002f);
            help.rectTransform.pivot = new Vector2(0.5f, 1f);
            help.transform.localPosition = new Vector3(0f, -mapSize * 0.5f - 0.004f, -0.002f);
            m_Big.SetActive(false);
        }

        void EnsureMini(Transform hand)
        {
            if (m_Mini != null)
            {
                if (m_Mini.transform.parent != hand) m_Mini.transform.SetParent(hand, false);
                return;
            }
            m_Mini = new GameObject("Minimapa");
            m_Mini.transform.SetParent(hand, false);
            m_Mini.transform.localPosition = minimapOffset;
            m_MiniTex = NewTexture(minimapResolution);
            m_MiniMat = NewMaterial(m_MiniTex);
            m_MiniQuad = QuadObject("Mapa", m_Mini.transform, m_MiniMat).transform;
            m_Fps = MakeText(m_Mini.transform, 0.11f, Color.white, TextAlignmentOptions.Center);
            m_Fps.gameObject.SetActive(false);
            m_Mini.SetActive(false);
        }

        static TextMeshPro MakeText(Transform parent, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject("Texto");
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshPro>();
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.enableWordWrapping = false;
            t.rectTransform.sizeDelta = new Vector2(0.6f, 0.05f);
            t.outlineWidth = 0.2f;
            t.outlineColor = new Color32(20, 16, 14, 255);
            return t;
        }
    }
}
