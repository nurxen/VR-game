using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace VRKit
{
    /// <summary>
    /// El mundo abierto: terreno infinito por chunks alrededor del jugador, con biomas, ríos, agua, vegetación y cuevas.
    ///
    /// Cómo funciona:
    ///   · El terreno sale de una SEMILLA: la misma semilla da siempre el mismo mundo. Por eso no se guarda el terreno,
    ///     se vuelve a generar igual al volver.
    ///   · Solo existe lo que está cerca (View Distance). Lo cercano con todo el detalle, colisión y vegetación;
    ///     lo lejano, más simple. Lo que se aleja se borra.
    ///   · Se genera en otros hilos (sin tirones) y se coloca un chunk por frame.
    ///   · Lo que CAMBIA (árboles quitados, objetos colocados) se guarda por chunk en archivos y se recarga al volver.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public class OpenWorld : MonoBehaviour
    {
        public static OpenWorld Instance { get; private set; }

        [Header("Mundo")]
        [Tooltip("0 = una al azar en cada mundo nuevo. Misma semilla = mismo mundo.")]
        [SerializeField] int seed = 0;
        [Tooltip("Nombre de la partida guardada (carpeta).")]
        [SerializeField] string worldName = "MundoAbierto";
        [SerializeField] bool loadSaved = true;
        [SerializeField] OpenTerrainSettings terrain = new OpenTerrainSettings();

        [Header("Chunks")]
        [Tooltip("Lado de cada chunk, en metros.")]
        [SerializeField] float chunkSize = 32f;
        [Tooltip("Cuadrados por lado en los chunks cercanos (16 = uno cada 2 m).")]
        [SerializeField, Range(4, 64)] int resolution = 16;
        [Tooltip("Cuadrados por lado en los lejanos.")]
        [SerializeField, Range(2, 32)] int farResolution = 8;
        [Tooltip("Hasta dónde se ve el terreno (metros). En Quest 2: 160–220.")]
        [SerializeField] float viewDistance = 200f;
        [Tooltip("Hasta dónde hay detalle, colisión y vegetación (metros).")]
        [SerializeField] float detailDistance = 80f;
        [SerializeField] bool flatShading = true;
        [Tooltip("Tiras bajo los bordes que tapan grietas entre chunks.")]
        [SerializeField] float skirtDepth = 4f;

        [Header("Aspecto")]
        [SerializeField] Material terrainMaterial;
        [SerializeField] Material waterMaterial;
        [Tooltip("Material de la vegetación combinada (colores por vértice). Vacío = el del terreno.")]
        [SerializeField] Material propMaterial;
        [SerializeField] BiomeProfile[] biomes = new BiomeProfile[0];
        [SerializeField, Range(0f, 0.2f)] float colorVariation = 0.06f;

        [Header("Vegetación")]
        [Tooltip("Lado de las casillas de reparto (metros). Como mucho un objeto por casilla.")]
        [SerializeField] float propCell = 3f;
        [Tooltip("Objetos que el jugador puede colocar y que se guardan (por nombre).")]
        [SerializeField] List<GameObject> placeablePrefabs = new List<GameObject>();

        [Header("Cuevas")]
        [SerializeField] GameObject caveEntrancePrefab;
        [Tooltip("Una posible cueva cada tantos metros.")]
        [SerializeField] float caveSpacing = 220f;
        [SerializeField, Range(0f, 1f)] float caveChance = 0.3f;

        [Header("Jugador")]
        [Tooltip("Quién decide qué se carga. Vacío = la cámara.")]
        [SerializeField] Transform viewer;
        [SerializeField] bool placePlayer = true;
        [Tooltip("Dónde se empieza a buscar tierra firme para aparecer (x, z).")]
        [SerializeField] Vector2 spawnPoint = Vector2.zero;
        [Tooltip("Si el jugador acaba bajo el suelo (por un tirón), se le sube.")]
        [SerializeField] bool keepPlayerAboveGround = true;
        [Tooltip("Se puede usar el teletransporte de XRI sobre el terreno (no en cuestas muy empinadas).")]
        [SerializeField] bool teleportOnTerrain = true;

        [Header("Rendimiento")]
        [Tooltip("Chunks generándose a la vez en otros hilos.")]
        [SerializeField, Range(1, 4)] int workerThreads = 2;
        [Tooltip("Chunks que se colocan por frame.")]
        [SerializeField, Range(1, 4)] int appliesPerFrame = 1;
        [Tooltip("Ajustar la niebla y la cámara a la distancia de vista.")]
        [SerializeField] bool setupFogAndCamera = true;
        [Tooltip("Minutos entre guardados automáticos.")]
        [SerializeField] float autosaveMinutes = 1.5f;

        public UnityEvent onReady = new UnityEvent();
        public event Action<TerrainChunk> ChunkLoaded;
        public event Action<TerrainChunk> ChunkUnloaded;

        // ------------------------------------------------------------------ Estado
        class Entry
        {
            public int cx, cz;
            public TerrainChunk view;
            public int lod = -1, pendingLod = -1;
            public float dist;
        }

        class Job
        {
            public long key; public int cx, cz, lod;
            public int[] removed;
            public LowPolyMeshData mesh, water, decor;
            public TerrainChunkInfo info;
            public List<PropPlacement> props;
            public List<CaveSite> caves;
            public Exception error;
        }

        [Serializable] class PlacedSave { public string key; public float px, py, pz, rx, ry, rz, rw, s = 1f; }
        [Serializable] class ChunkSave { public List<int> removed = new List<int>(); public List<PlacedSave> placed = new List<PlacedSave>(); }
        class Record { public ChunkSave data = new ChunkSave(); public HashSet<int> removed = new HashSet<int>(); public bool dirty; }

        [Serializable]
        class Meta
        {
            public int version = 1, seed;
            public bool hasPlayer;
            public float px, py, pz, yaw;
            public float timeOfDay = -1f;
            public List<string> discovered = new List<string>();
        }

        readonly Dictionary<long, Entry> m_Entries = new Dictionary<long, Entry>();
        readonly Dictionary<long, Record> m_Records = new Dictionary<long, Record>();
        readonly List<Entry> m_Queue = new List<Entry>();
        readonly ConcurrentQueue<Job> m_Done = new ConcurrentQueue<Job>();
        readonly Dictionary<GameObject, Stack<GameObject>> m_Pool = new Dictionary<GameObject, Stack<GameObject>>();
        readonly Dictionary<string, GameObject> m_Placeable = new Dictionary<string, GameObject>();
        readonly HashSet<PlacedObject> m_Placed = new HashSet<PlacedObject>();
        readonly HashSet<string> m_Discovered = new HashSet<string>();
        readonly List<Entry> m_Scratch = new List<Entry>();

        OpenTerrainGenerator m_Gen;
        TerrainPalette m_Palette;
        PropRuleData[] m_RuleData;
        BiomePropRule[] m_Rules;
        PropGeometry[] m_RuleGeometry;
        LowPolyMeshData m_WaterTemplate;
        Transform m_PoolRoot;
        Meta m_Meta;
        int m_Running, m_CenterX = int.MinValue, m_CenterZ = int.MinValue;
        bool m_Initialized, m_Ready, m_Despawning, m_LocomotionLocked;
        float m_NextRefresh, m_NextAutosave;

        // ------------------------------------------------------------------ Acceso
        public OpenTerrainGenerator Generator { get { return m_Gen; } }
        public OpenTerrainSettings Terrain { get { return terrain; } }
        public int Seed { get { return m_Gen != null ? m_Gen.Seed : seed; } }
        public float ChunkSize { get { return chunkSize; } }
        public float ViewDistance { get { return viewDistance; } }
        public float WaterLevel { get { return terrain.waterLevel; } }
        public bool IsReady { get { return m_Ready; } }
        public string SaveFolder { get { return Path.Combine(Application.persistentDataPath, "vrkit_mundo_abierto", worldName); } }
        /// <summary>Lo pone CaveSystem: dentro de una cueva no se corrige la altura del jugador.</summary>
        public bool PlayerUnderground { get; set; }

        static long Key(int cx, int cz) { return ((long)cx << 32) ^ (uint)cz; }
        int ChunkOf(float v) { return Mathf.FloorToInt(v / chunkSize); }

        /// <summary>Altura del suelo que se pisa (la de la malla cercana) en un punto del mundo.</summary>
        public float GroundHeight(float x, float z) { return m_Gen == null ? 0f : TerrainMesher.SurfaceHeight(m_Gen, x, z, chunkSize, resolution); }
        public TerrainSample SampleAt(float x, float z) { return m_Gen.Sample(x, z); }
        public Biome BiomeAt(Vector3 p) { return m_Gen.BiomeAt(p.x, p.z); }
        public BiomeProfile ProfileOf(Biome b) { foreach (var p in biomes) if (p != null && p.biome == b) return p; return null; }
        public bool IsUnderwater(Vector3 p) { return p.y < terrain.waterLevel; }

        // ================================================================== Arranque

        void Awake() { Instance = this; }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            m_Meta = loadSaved ? ReadJson<Meta>(Path.Combine(SaveFolder, "mundo.json")) : null;
            Initialize(m_Meta != null ? m_Meta.seed : seed);
        }

        /// <summary>Empieza el mundo con esa semilla (0 = al azar). Borra lo que hubiera cargado.</summary>
        public void Initialize(int newSeed)
        {
            Instance = this;
            foreach (var e in m_Entries.Values) if (e.view != null) Destroy(e.view.gameObject);
            m_Entries.Clear(); m_Queue.Clear(); m_Records.Clear();
            Job dummy; while (m_Done.TryDequeue(out dummy)) { }

            int s = newSeed != 0 ? newSeed : UnityEngine.Random.Range(1, int.MaxValue);
            m_Gen = new OpenTerrainGenerator(terrain, s);
            seed = s;
            if (m_Meta == null) m_Meta = new Meta();
            m_Meta.seed = s;
            m_Discovered.Clear();
            foreach (var d in m_Meta.discovered) m_Discovered.Add(d);

            m_Palette = new TerrainPalette { variation = colorVariation };
            foreach (var b in biomes)
                if (b != null) m_Palette.ground[(int)b.biome] = b.groundColor;

            BuildRules();
            m_Placeable.Clear();
            foreach (var p in placeablePrefabs) if (p != null) m_Placeable[p.name] = p;
            if (m_PoolRoot == null) { m_PoolRoot = new GameObject("Reserva de objetos").transform; m_PoolRoot.SetParent(transform, false); m_PoolRoot.gameObject.SetActive(false); }
            m_WaterTemplate = new LowPolyMeshData();
            TerrainMesher.BuildWater(chunkSize, terrain.waterLevel, 4, m_WaterTemplate);

            m_Initialized = true;
            m_Ready = false;
            m_CenterX = m_CenterZ = int.MinValue;
            if (setupFogAndCamera) SetupFogAndCamera();

            var player = VRPlayer.Instance;
            if (player != null && placePlayer)
            {
                Vector3 target;
                float? yaw = null;
                if (m_Meta.hasPlayer) { target = new Vector3(m_Meta.px, m_Meta.py, m_Meta.pz); yaw = m_Meta.yaw; }
                else target = FindSpawn(spawnPoint.x, spawnPoint.y);
                target.y = Mathf.Max(target.y, GroundHeight(target.x, target.z)) + 0.05f;
                player.TeleportTo(target, yaw);
                player.SetLocomotionEnabled(false);
                m_LocomotionLocked = true;
            }
            var sky = SkyCycle.Instance;
            if (sky != null && m_Meta.timeOfDay >= 0f) sky.TimeOfDay = m_Meta.timeOfDay;
            m_NextAutosave = Time.time + autosaveMinutes * 60f;
        }

        void BuildRules()
        {
            var data = new List<PropRuleData>();
            var rules = new List<BiomePropRule>();
            var geo = new List<PropGeometry>();
            var cache = new Dictionary<GameObject, PropGeometry>();
            foreach (var b in biomes)
            {
                if (b == null) continue;
                foreach (var r in b.props)
                {
                    if (r == null || r.prefab == null || r.density <= 0f) continue;
                    PropGeometry g;
                    if (!cache.TryGetValue(r.prefab, out g)) { g = ReadGeometry(r.prefab); cache[r.prefab] = g; }
                    data.Add(new PropRuleData
                    {
                        biomeMask = 1 << (int)b.biome, density = r.density,
                        minSlope = r.slope.x, maxSlope = r.slope.y, minAltitude = r.altitude.x, maxAltitude = r.altitude.y,
                        minScale = r.scale.x, maxScale = r.scale.y, riverBoost = r.riverBoost,
                    });
                    rules.Add(r);
                    geo.Add(g);
                }
            }
            m_RuleData = data.ToArray();
            m_Rules = rules.ToArray();
            m_RuleGeometry = geo.ToArray();
        }

        /// <summary>Junta las mallas de un prefab (relativas a su raíz). La malla tiene que ser legible (Read/Write).</summary>
        static PropGeometry ReadGeometry(GameObject prefab)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var c = new List<Color32>(); var t = new List<int>();
            var root = prefab.transform.worldToLocalMatrix;
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;
                if (!mesh.isReadable) { Debug.LogWarning("[VRKit] La malla de «" + prefab.name + "» no es legible: en su FBX marcad Read/Write. Se dibuja como objeto suelto."); return null; }
                var m = root * mf.transform.localToWorldMatrix;
                int start = v.Count;
                var mv = mesh.vertices; var mn = mesh.normals; var mc = mesh.colors32;
                var tint = mf.GetComponent<MeshRenderer>() != null && mf.GetComponent<MeshRenderer>().sharedMaterial != null && mf.GetComponent<MeshRenderer>().sharedMaterial.HasProperty("_BaseColor")
                    ? (Color32)mf.GetComponent<MeshRenderer>().sharedMaterial.GetColor("_BaseColor") : new Color32(255, 255, 255, 255);
                for (int i = 0; i < mv.Length; i++)
                {
                    v.Add(m.MultiplyPoint3x4(mv[i]));
                    n.Add(mn != null && i < mn.Length ? m.MultiplyVector(mn[i]).normalized : Vector3.up);
                    c.Add(mc != null && i < mc.Length ? mc[i] : tint);
                }
                foreach (int idx in mesh.triangles) t.Add(start + idx);
            }
            if (v.Count == 0) return null;
            return new PropGeometry { vertices = v.ToArray(), normals = n.ToArray(), colors = c.ToArray(), triangles = t.ToArray() };
        }

        void SetupFogAndCamera()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = viewDistance * 0.45f;
            RenderSettings.fogEndDistance = viewDistance * 0.95f;
            var cam = Camera.main;
            if (cam != null) cam.farClipPlane = Mathf.Max(cam.farClipPlane, viewDistance + 50f);
        }

        /// <summary>Busca tierra firme y llana cerca de (x, z), en espiral.</summary>
        public Vector3 FindSpawn(float x, float z)
        {
            for (int i = 0; i < 400; i++)
            {
                float a = i * 2.4f, r = i * 6f;
                float px = x + Mathf.Cos(a) * r, pz = z + Mathf.Sin(a) * r;
                var s = m_Gen.Sample(px, pz);
                if (s.height > terrain.waterLevel + 1.5f && m_Gen.SlopeAt(px, pz, 2f) < 18f && s.mountain < 0.4f)
                    return new Vector3(px, GroundHeight(px, pz), pz);
            }
            return new Vector3(x, Mathf.Max(terrain.waterLevel, GroundHeight(x, z)), z);
        }

        // ================================================================== Cada frame

        Vector3 ViewerPos()
        {
            Transform v = viewer;
            if (v == null && Camera.main != null) v = Camera.main.transform;
            return v != null ? v.position : Vector3.zero;
        }

        void Update()
        {
            if (!m_Initialized) return;

            // 1. Colocar lo que han terminado los hilos
            Job job;
            for (int i = 0; i < appliesPerFrame && m_Done.TryDequeue(out job); i++) Apply(job);

            // 2. ¿Qué chunks hacen falta?
            var p = ViewerPos();
            int ccx = ChunkOf(p.x), ccz = ChunkOf(p.z);
            if (ccx != m_CenterX || ccz != m_CenterZ || Time.time >= m_NextRefresh)
            {
                m_CenterX = ccx; m_CenterZ = ccz;
                m_NextRefresh = Time.time + 0.5f;
                Refresh(p);
            }

            // 3. Lanzar trabajos
            while (m_Running < workerThreads && m_Queue.Count > 0)
            {
                var e = m_Queue[0];
                m_Queue.RemoveAt(0);
                if (!m_Entries.ContainsKey(Key(e.cx, e.cz)) || e.pendingLod < 0) continue;
                Launch(e);
            }

            GuardPlayer();

            if (autosaveMinutes > 0f && Time.time >= m_NextAutosave) { m_NextAutosave = Time.time + autosaveMinutes * 60f; Save(); }
        }

        void Refresh(Vector3 p)
        {
            int r = Mathf.CeilToInt(viewDistance / chunkSize) + 1;
            for (int dz = -r; dz <= r; dz++)
            for (int dx = -r; dx <= r; dx++)
            {
                int cx = m_CenterX + dx, cz = m_CenterZ + dz;
                float d = DistanceToChunk(p, cx, cz);
                if (d > viewDistance) continue;
                int lod = d < detailDistance ? 0 : 1;
                long k = Key(cx, cz);
                Entry e;
                if (!m_Entries.TryGetValue(k, out e)) { e = new Entry { cx = cx, cz = cz }; m_Entries.Add(k, e); }
                e.dist = d;
                if (e.lod != lod && e.pendingLod != lod)
                {
                    bool queued = e.pendingLod >= 0;
                    e.pendingLod = lod;
                    if (!queued) m_Queue.Add(e);
                }
            }
            m_Queue.Sort((a, b) => a.dist.CompareTo(b.dist));

            // Borrar lo lejano
            m_Scratch.Clear();
            foreach (var e in m_Entries.Values)
                if (DistanceToChunk(p, e.cx, e.cz) > viewDistance + chunkSize) m_Scratch.Add(e);
            foreach (var e in m_Scratch) Unload(e);
        }

        float DistanceToChunk(Vector3 p, int cx, int cz)
        {
            float x0 = cx * chunkSize, z0 = cz * chunkSize;
            float dx = Mathf.Max(x0 - p.x, 0f, p.x - (x0 + chunkSize));
            float dz = Mathf.Max(z0 - p.z, 0f, p.z - (z0 + chunkSize));
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        // ================================================================== Generar (otro hilo) y colocar (aquí)

        void Launch(Entry e)
        {
            var job = new Job { key = Key(e.cx, e.cz), cx = e.cx, cz = e.cz, lod = e.pendingLod };
            if (job.lod == 0)
            {
                var rec = GetRecord(job.key);
                job.removed = new int[rec.removed.Count];
                rec.removed.CopyTo(job.removed);
            }
            m_Running++;
            var gen = m_Gen; var pal = m_Palette; var rules = m_RuleData; var geo = m_RuleGeometry;
            float size = chunkSize, cell = propCell, skirt = skirtDepth, spacing = caveSpacing, chance = caveChance;
            int res = resolution, far = farResolution; bool flat = flatShading;
            var water = m_WaterTemplate;
            var ruleInfo = m_Rules;
            Task.Run(() =>
            {
                try
                {
                    job.mesh = new LowPolyMeshData();
                    job.info = TerrainMesher.Build(gen, job.cx, job.cz, size, job.lod == 0 ? res : far, flat, pal, skirt, job.mesh);
                    if (job.info.hasWater) job.water = water;
                    if (job.lod == 0)
                    {
                        job.props = new List<PropPlacement>();
                        PropScatter.Scatter(gen, job.cx, job.cz, size, res, cell, rules, job.props);
                        if (job.removed.Length > 0)
                        {
                            var removed = new HashSet<int>(job.removed);
                            job.props.RemoveAll(pp => removed.Contains(pp.id));
                        }
                        // Toda la vegetación que se puede combinar, en una malla
                        job.decor = new LowPolyMeshData();
                        foreach (var pp in job.props)
                        {
                            var g = geo[pp.rule];
                            if (g == null) continue;
                            var r = ruleInfo[pp.rule];
                            Vector3 up = new Vector3(0f, 1f, 0f);
                            if (r.alignToSlope)
                            {
                                float wx = job.cx * size + pp.x, wz = job.cz * size + pp.z;
                                float dx = gen.Height(wx + 1f, wz) - gen.Height(wx - 1f, wz);
                                float dz = gen.Height(wx, wz + 1f) - gen.Height(wx, wz - 1f);
                                up = new Vector3(-dx * 0.5f, 1f, -dz * 0.5f);
                            }
                            PropCombiner.Append(job.decor, g, new Vector3(pp.x, pp.y - r.sink * pp.scale, pp.z), pp.yaw, up, pp.scale);
                        }
                        job.caves = new List<CaveSite>();
                        CaveGen.Sites(gen, job.cx, job.cz, size, spacing, chance, job.caves);
                    }
                }
                catch (Exception ex) { job.error = ex; }
                m_Done.Enqueue(job);
            });
        }

        void Apply(Job job)
        {
            m_Running--;
            if (job.error != null) { Debug.LogException(job.error); }
            Entry e;
            if (!m_Entries.TryGetValue(job.key, out e)) return;          // se alejó mientras se generaba
            if (e.pendingLod == job.lod) e.pendingLod = -1;
            if (job.error != null) return;

            bool created = false;
            if (e.view == null)
            {
                var go = new GameObject("Chunk " + job.cx + "," + job.cz);
                go.layer = gameObject.layer;
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(job.cx * chunkSize, 0f, job.cz * chunkSize);
                e.view = go.AddComponent<TerrainChunk>();
                e.view.Setup(job.cx, job.cz, job.key, terrainMaterial);
                created = true;
            }
            e.lod = job.lod;
            e.view.Lod = job.lod;
            e.view.Info = job.info;
            // Cada parte por separado: si una falla, el resto del chunk se coloca igual
            try { e.view.ApplyMesh(job.mesh, job.lod == 0, teleportOnTerrain); } catch (Exception ex) { Debug.LogException(ex); }
            try { e.view.ApplyWater(job.water, waterMaterial); } catch (Exception ex) { Debug.LogException(ex); }
            try { if (job.lod == 0) SpawnDetail(e, job); else DespawnDetail(e); } catch (Exception ex) { Debug.LogException(ex); }

            if (created && ChunkLoaded != null) ChunkLoaded(e.view);
        }

        // ================================================================== Vegetación, cuevas y objetos colocados

        void SpawnDetail(Entry e, Job job)
        {
            var view = e.view;
            DespawnDetail(e);
            view.propsSpawned = true;
            var rec = GetRecord(job.key);

            // 1. Malla combinada
            if (job.decor != null && job.decor.vertices.Count > 0)
            {
                var go = new GameObject("Vegetación combinada");
                go.transform.SetParent(view.propRoot, false);
                var mesh = new Mesh { name = "Vegetación " + job.cx + "," + job.cz };
                mesh.indexFormat = job.decor.vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
                mesh.SetVertices(job.decor.vertices);
                mesh.SetNormals(job.decor.normals);
                mesh.SetColors(job.decor.colors);
                mesh.SetTriangles(job.decor.triangles, 0);
                mesh.RecalculateBounds();
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = propMaterial != null ? propMaterial : terrainMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.AddComponent<DestroyMeshOnDestroy>();
                view.props.Add(go);
            }

            // 2. Por cada objeto: su colisión (si la tiene) o el objeto entero (si no se pudo combinar)
            foreach (var pp in job.props)
            {
                var rule = m_Rules[pp.rule];
                bool combined = m_RuleGeometry[pp.rule] != null;
                if (combined && !rule.collide) continue;
                var inst = Take(rule.prefab, view.propRoot);
                inst.transform.localPosition = new Vector3(pp.x, pp.y - rule.sink * pp.scale, pp.z);
                inst.transform.localRotation = Quaternion.Euler(0f, pp.yaw, 0f);
                inst.transform.localScale = rule.prefab.transform.localScale * pp.scale;
                foreach (var rend in inst.GetComponentsInChildren<Renderer>(true)) rend.enabled = !combined;
                var wp = inst.GetComponent<WorldProp>();
                if (wp == null) wp = inst.AddComponent<WorldProp>();
                wp.ChunkKey = job.key; wp.Id = pp.id; wp.Rule = pp.rule; wp.SourcePrefab = rule.prefab;
                view.props.Add(inst);
            }

            // 3. Cuevas
            if (caveEntrancePrefab != null && job.caves != null)
                foreach (var c in job.caves)
                {
                    var go = Instantiate(caveEntrancePrefab, new Vector3(c.x, GroundHeight(c.x, c.z), c.z), Quaternion.Euler(0f, c.yaw, 0f), view.transform);
                    var ce = go.GetComponent<CaveEntrance>();
                    if (ce == null) ce = go.AddComponent<CaveEntrance>();
                    ce.Site = c;
                    view.caves.Add(go);
                }

            // 4. Objetos colocados por el jugador
            foreach (var ps in rec.data.placed)
            {
                GameObject prefab;
                if (ps.key == null || !m_Placeable.TryGetValue(ps.key, out prefab)) continue;
                var go = Instantiate(prefab, new Vector3(ps.px, ps.py, ps.pz), new Quaternion(ps.rx, ps.ry, ps.rz, ps.rw));
                go.transform.localScale = prefab.transform.localScale * ps.s;
                var po = go.GetComponent<PlacedObject>();
                if (po == null) po = go.AddComponent<PlacedObject>();
                po.prefabKey = ps.key;
            }
        }

        void DespawnDetail(Entry e)
        {
            var view = e.view;
            if (view == null || !view.propsSpawned) return;
            SavePlacedOf(e);
            m_Despawning = true;
            foreach (var po in PlacedIn(e.cx, e.cz)) Destroy(po.gameObject);
            m_Despawning = false;
            foreach (var go in view.props)
            {
                if (go == null) continue;
                var wp = go.GetComponent<WorldProp>();
                if (wp != null && wp.SourcePrefab != null) Give(wp.SourcePrefab, go);
                else Destroy(go);
            }
            view.props.Clear();
            foreach (var c in view.caves) if (c != null) Destroy(c);
            view.caves.Clear();
            view.propsSpawned = false;
        }

        void Unload(Entry e)
        {
            m_Entries.Remove(Key(e.cx, e.cz));
            m_Queue.Remove(e);
            if (e.view != null)
            {
                DespawnDetail(e);
                if (ChunkUnloaded != null) ChunkUnloaded(e.view);
                Destroy(e.view.gameObject);
            }
            Record rec;
            if (m_Records.TryGetValue(Key(e.cx, e.cz), out rec))
            {
                if (rec.dirty) WriteRecord(Key(e.cx, e.cz), rec);
                m_Records.Remove(Key(e.cx, e.cz));
            }
        }

        GameObject Take(GameObject prefab, Transform parent)
        {
            Stack<GameObject> s;
            if (m_Pool.TryGetValue(prefab, out s) && s.Count > 0)
            {
                var go = s.Pop();
                if (go != null) { go.transform.SetParent(parent, false); go.SetActive(true); return go; }
            }
            return Instantiate(prefab, parent, false);
        }

        void Give(GameObject prefab, GameObject go)
        {
            Stack<GameObject> s;
            if (!m_Pool.TryGetValue(prefab, out s)) { s = new Stack<GameObject>(); m_Pool[prefab] = s; }
            go.transform.SetParent(m_PoolRoot, false);
            s.Push(go);
        }

        /// <summary>Quita un árbol, roca… del mundo y lo guarda (no vuelve a salir).</summary>
        public void RemoveProp(WorldProp prop)
        {
            if (prop == null) return;
            var rec = GetRecord(prop.ChunkKey);
            if (rec.removed.Add(prop.Id)) { rec.data.removed.Add(prop.Id); rec.dirty = true; }
            Entry e;
            if (m_Entries.TryGetValue(prop.ChunkKey, out e) && e.view != null)
            {
                e.view.props.Remove(prop.gameObject);
                if (prop.SourcePrefab != null) Give(prop.SourcePrefab, prop.gameObject); else Destroy(prop.gameObject);
                // La vegetación combinada se rehace sin él
                if (m_RuleGeometry[prop.Rule] != null && e.pendingLod < 0) { e.pendingLod = 0; e.lod = -1; m_Queue.Insert(0, e); }
            }
            else Destroy(prop.gameObject);
        }

        /// <summary>Pone un objeto que se guarda con la partida. El prefab tiene que estar en Placeable Prefabs.</summary>
        public GameObject Place(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;
            if (!m_Placeable.ContainsKey(prefab.name)) { m_Placeable[prefab.name] = prefab; Debug.LogWarning("[VRKit] «" + prefab.name + "» no está en Placeable Prefabs: añadidlo para que se cargue en otra partida."); }
            var go = Instantiate(prefab, position, rotation);
            var po = go.GetComponent<PlacedObject>();
            if (po == null) po = go.AddComponent<PlacedObject>();
            po.prefabKey = prefab.name;
            return go;
        }

        internal void RegisterPlaced(PlacedObject p) { m_Placed.Add(p); MarkDirtyAt(p.transform.position); }
        internal void UnregisterPlaced(PlacedObject p) { m_Placed.Remove(p); if (!m_Despawning) MarkDirtyAt(p.transform.position); }

        void MarkDirtyAt(Vector3 pos) { GetRecord(Key(ChunkOf(pos.x), ChunkOf(pos.z))).dirty = true; }

        IEnumerable<PlacedObject> PlacedIn(int cx, int cz)
        {
            var list = new List<PlacedObject>();
            foreach (var p in m_Placed)
                if (p != null && ChunkOf(p.transform.position.x) == cx && ChunkOf(p.transform.position.z) == cz) list.Add(p);
            return list;
        }

        void SavePlacedOf(Entry e)
        {
            var rec = GetRecord(Key(e.cx, e.cz));
            var list = new List<PlacedSave>();
            foreach (var p in PlacedIn(e.cx, e.cz))
            {
                var t = p.transform;
                list.Add(new PlacedSave { key = p.prefabKey, px = t.position.x, py = t.position.y, pz = t.position.z, rx = t.rotation.x, ry = t.rotation.y, rz = t.rotation.z, rw = t.rotation.w, s = t.localScale.x });
            }
            rec.data.placed = list;
        }

        // ================================================================== El jugador, siempre sobre el suelo

        void GuardPlayer()
        {
            var player = VRPlayer.Instance;
            if (player == null || PlayerUnderground) return;
            var feet = player.FeetPosition;
            Entry e;
            bool groundReady = m_Entries.TryGetValue(Key(ChunkOf(feet.x), ChunkOf(feet.z)), out e) && e.view != null && e.view.HasCollider;

            if (!m_Ready && groundReady)
            {
                m_Ready = true;
                onReady.Invoke();
            }
            if (!placePlayer) return;

            // Sin suelo debajo todavía: quieto
            if (!groundReady && !m_LocomotionLocked) { player.SetLocomotionEnabled(false); m_LocomotionLocked = true; }
            else if (groundReady && m_LocomotionLocked) { player.SetLocomotionEnabled(true); m_LocomotionLocked = false; }

            if (keepPlayerAboveGround)
            {
                float ground = GroundHeight(feet.x, feet.z);
                if (feet.y < ground - 0.5f || (!groundReady && feet.y < ground))
                    player.TeleportTo(new Vector3(feet.x, ground + 0.05f, feet.z));
            }
        }

        // ================================================================== Descubrir

        public bool IsDiscovered(string id) { return m_Discovered.Contains(id); }
        /// <summary>Marca algo como descubierto. Devuelve true si es la primera vez.</summary>
        public bool Discover(string id) { return id != null && m_Discovered.Add(id); }
        public IEnumerable<string> Discovered { get { return m_Discovered; } }

        // ================================================================== Guardar

        Record GetRecord(long key)
        {
            Record r;
            if (m_Records.TryGetValue(key, out r)) return r;
            r = new Record();
            var saved = loadSaved ? ReadJson<ChunkSave>(ChunkPath(key)) : null;
            if (saved != null)
            {
                r.data = saved;
                if (r.data.removed == null) r.data.removed = new List<int>();
                if (r.data.placed == null) r.data.placed = new List<PlacedSave>();
                foreach (int id in r.data.removed) r.removed.Add(id);
            }
            m_Records[key] = r;
            return r;
        }

        string ChunkPath(long key)
        {
            int cx = (int)(key >> 32), cz = (int)(key & 0xffffffffL);
            return Path.Combine(SaveFolder, "chunks", cx + "_" + cz + ".json");
        }

        void WriteRecord(long key, Record r)
        {
            WriteJson(ChunkPath(key), r.data);
            r.dirty = false;
        }

        /// <summary>Guarda todo: dónde está el jugador, la hora, lo descubierto y los chunks cambiados.</summary>
        public void Save()
        {
            if (!m_Initialized) return;
            foreach (var e in m_Entries.Values) if (e.view != null && e.view.propsSpawned) SavePlacedOf(e);
            foreach (var kv in m_Records) if (kv.Value.dirty) WriteRecord(kv.Key, kv.Value);

            var player = VRPlayer.Instance;
            if (player != null && placePlayer && !PlayerUnderground)
            {
                var f = player.FeetPosition;
                m_Meta.hasPlayer = true;
                m_Meta.px = f.x; m_Meta.py = f.y + 0.1f; m_Meta.pz = f.z;
                m_Meta.yaw = player.Head != null ? player.Head.eulerAngles.y : 0f;
            }
            if (SkyCycle.Instance != null) m_Meta.timeOfDay = SkyCycle.Instance.TimeOfDay;
            m_Meta.discovered = new List<string>(m_Discovered);
            WriteJson(Path.Combine(SaveFolder, "mundo.json"), m_Meta);
        }

        /// <summary>Borra la partida y empieza otro mundo (semilla 0 = al azar).</summary>
        public void NewWorld(int newSeed = 0)
        {
            try { if (Directory.Exists(SaveFolder)) Directory.Delete(SaveFolder, true); } catch (Exception ex) { Debug.LogWarning("[VRKit] " + ex.Message); }
            m_Meta = new Meta();
            Initialize(newSeed);
        }

        void OnApplicationPause(bool paused) { if (paused) Save(); }
        void OnApplicationQuit() { Save(); }

        static T ReadJson<T>(string path) where T : class
        {
            try { return File.Exists(path) ? JsonUtility.FromJson<T>(File.ReadAllText(path)) : null; }
            catch (Exception ex) { Debug.LogWarning("[VRKit] No se pudo leer " + path + ": " + ex.Message); return null; }
        }

        static void WriteJson(string path, object data)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(data));
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception ex) { Debug.LogWarning("[VRKit] No se pudo guardar " + path + ": " + ex.Message); }
        }
    }
}
