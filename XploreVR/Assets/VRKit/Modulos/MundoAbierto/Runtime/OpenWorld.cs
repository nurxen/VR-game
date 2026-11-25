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
        [Tooltip("Cuadrados por lado en los muy lejanos (más allá de Mid Distance).")]
        [SerializeField, Range(2, 16)] int veryFarResolution = 4;
        [Tooltip("Hasta dónde se ve el terreno (metros). En Quest 2: 160–220.")]
        [SerializeField] float viewDistance = 200f;
        [Tooltip("Hasta dónde hay detalle, colisión y vegetación (metros).")]
        [SerializeField] float detailDistance = 80f;
        [Tooltip("Más allá de esto, el terreno se hace con aún menos triángulos (metros).")]
        [SerializeField] float midDistance = 130f;
        [SerializeField] bool flatShading = true;
        [Tooltip("Tiras bajo los bordes que tapan grietas entre chunks.")]
        [SerializeField] float skirtDepth = 4f;

        [Header("Aspecto")]
        [SerializeField] Material terrainMaterial;
        [SerializeField] Material waterMaterial;
        [Tooltip("Material de la vegetación combinada (colores por vértice). Vacío = el del terreno.")]
        [SerializeField] Material propMaterial;
        [Tooltip("Plantilla para la vegetación CON TEXTURA (shader VRKit/PropTextura). Por cada material de los prefabs se hace " +
                 "una copia con su textura y su color: no se usan los shaders del paquete original.")]
        [SerializeField] Material texturedPropMaterial;
        [Tooltip("Triángulos máximos del LOD automático de cada prop combinado.")]
        [SerializeField] int autoLodTriangles = 1500;
        [Tooltip("Para materiales de otros paquetes que salen con el color cambiado: original → copia corregida (shader VRKit/PropTextura). " +
                 "Lo rellena Tools › VRKit › Mundo abierto › Copia editable de materiales.")]
        [SerializeField] List<MaterialOverride> materialOverrides = new List<MaterialOverride>();
        [Tooltip("Escribir en la consola qué textura y color se ha leído de cada material de vegetación.")]
        [SerializeField] bool logMaterials = true;

        [Serializable]
        public class MaterialOverride
        {
            public Material original;
            public Material replacement;
        }
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
        [Tooltip("Árboles y rocas con colisión que se colocan por frame (repartidos para que no haya tirones).")]
        [SerializeField, Range(4, 200)] int propSpawnsPerFrame = 24;
        [Tooltip("Una sola superficie de agua que sigue al jugador, en vez de una por chunk (muchas menos llamadas de dibujo).")]
        [SerializeField] bool singleWaterPlane = true;
        [Tooltip("Baja la distancia de vista sola si los FPS no llegan, y la vuelve a subir cuando sobran.")]
        [SerializeField] bool autoQuality = true;
        [Tooltip("La distancia de vista no baja de esto con la calidad automática (metros).")]
        [SerializeField] float minViewDistance = 112f;
        [Tooltip("FPS que se quieren. 0 = los de las gafas (72 en Quest 2, si no se cambia).")]
        [SerializeField] float targetFps = 0f;
        [Tooltip("Ajustar la niebla y la cámara a la distancia de vista.")]
        [SerializeField] bool setupFogAndCamera = true;
        [Tooltip("Minutos entre guardados automáticos.")]
        [SerializeField] float autosaveMinutes = 1.5f;

        public UnityEvent onReady = new UnityEvent();
        public event Action<TerrainChunk> ChunkLoaded;
        public event Action<TerrainChunk> ChunkUnloaded;
        /// <summary>Cada vez que se coloca o cambia la malla de un chunk (también al pasar a tener detalle).</summary>
        public event Action<TerrainChunk> ChunkUpdated;
        /// <summary>Al guardar la partida (para que otros guarden lo suyo: mapa…).</summary>
        public event Action Saving;
        /// <summary>Al empezar un mundo (también uno nuevo): lo de la partida anterior ya no vale.</summary>
        public event Action WorldStarted;

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
            public LowPolyMeshData mesh, water;
            public LowPolyMeshData[] decor;     // una malla por material
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
        readonly List<TerrainChunk> m_SpawnQueue = new List<TerrainChunk>();

        OpenTerrainGenerator m_Gen;
        TerrainPalette m_Palette;
        PropRuleData[] m_RuleData;
        BiomePropRule[] m_Rules;
        PropGeometry[] m_RuleGeometry;
        // Materiales de la vegetación combinada: 0 = color por vértice; 1… = uno por material con textura de los prefabs
        readonly List<Material> m_PropMaterials = new List<Material>();
        readonly Dictionary<Material, int> m_PropMaterialIndex = new Dictionary<Material, int>();
        readonly Dictionary<string, int> m_PropLookIndex = new Dictionary<string, int>();
        readonly List<Material> m_CreatedPropMaterials = new List<Material>();
        // Materiales originales de prefabs (regla con «Original Material»): índice en m_PropMaterials. No se destruyen.
        readonly Dictionary<Material, int> m_OriginalIndex = new Dictionary<Material, int>();
        LowPolyMeshData m_WaterTemplate;
        Transform m_PoolRoot;
        Meta m_Meta;
        int m_Running, m_CenterX = int.MinValue, m_CenterZ = int.MinValue;
        bool m_Initialized, m_Ready, m_Despawning, m_LocomotionLocked;
        float m_NextRefresh, m_NextAutosave;
        float m_MaxViewDistance, m_QualityTime, m_QualityFrames, m_QualitySum;
        int m_SlowWindows, m_FastWindows;
        Transform m_WaterPlane;
        Mesh m_WaterPlaneMesh;

        // ------------------------------------------------------------------ Acceso
        public OpenTerrainGenerator Generator { get { return m_Gen; } }
        public OpenTerrainSettings Terrain { get { return terrain; } }
        public int Seed { get { return m_Gen != null ? m_Gen.Seed : seed; } }
        public float ChunkSize { get { return chunkSize; } }
        /// <summary>Hasta dónde se ve (metros). Se puede cambiar en marcha (opciones de calidad).</summary>
        public float ViewDistance
        {
            get { return viewDistance; }
            set
            {
                viewDistance = Mathf.Max(detailDistance + chunkSize, value);
                if (setupFogAndCamera) SetupFogAndCamera();
                m_NextRefresh = 0f;
            }
        }
        public float DetailDistance { get { return detailDistance; } }
        public float CaveSpacing { get { return caveSpacing; } }
        public float CaveChance { get { return caveChance; } }
        /// <summary>Color de suelo de cada bioma (para el mapa).</summary>
        public Color32 GroundColor(Biome b) { return m_Palette != null ? m_Palette.ground[(int)b] : new Color32(128, 128, 128, 255); }
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

        void Awake() { Instance = this; m_MaxViewDistance = viewDistance; }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (m_WaterPlaneMesh != null) Destroy(m_WaterPlaneMesh);
            foreach (var m in m_CreatedPropMaterials) if (m != null) Destroy(m);
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
            m_Entries.Clear(); m_Queue.Clear(); m_Records.Clear(); m_SpawnQueue.Clear();
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
                if (b != null)
                {
                    Color32 c = b.groundColor;
                    c.a = (byte)Mathf.RoundToInt(Mathf.Clamp01(b.groundTexture) * 255f);   // el shader lo usa como «cuánta textura»
                    m_Palette.ground[(int)b.biome] = c;
                }

            BuildRules();
            m_Placeable.Clear();
            foreach (var p in placeablePrefabs) if (p != null) m_Placeable[p.name] = p;
            if (m_PoolRoot == null) { m_PoolRoot = new GameObject("Reserva de objetos").transform; m_PoolRoot.SetParent(transform, false); m_PoolRoot.gameObject.SetActive(false); }
            m_WaterTemplate = new LowPolyMeshData();
            TerrainMesher.BuildWater(chunkSize, terrain.waterLevel, 4, m_WaterTemplate);
            if (singleWaterPlane) BuildWaterPlane();

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
            if (WorldStarted != null) WorldStarted();
        }

        /// <summary>Una sola malla de agua grande (cuadrícula de 8 m) que sigue al jugador a saltos de 8 m.</summary>
        void BuildWaterPlane()
        {
            if (waterMaterial == null) return;
            if (m_WaterPlane == null)
            {
                var go = new GameObject("Agua");
                go.layer = gameObject.layer;
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>();
                var r = go.AddComponent<MeshRenderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                m_WaterPlane = go.transform;
                m_WaterPlaneMesh = new Mesh { name = "Agua" };
                go.GetComponent<MeshFilter>().sharedMesh = m_WaterPlaneMesh;
            }
            m_WaterPlane.GetComponent<MeshRenderer>().sharedMaterial = waterMaterial;
            int cells = Mathf.CeilToInt((m_MaxViewDistance + chunkSize) * 2f / 8f);
            var d = new LowPolyMeshData();
            TerrainMesher.BuildWater(cells * 8f, terrain.waterLevel, cells, d);
            m_WaterPlaneMesh.Clear();
            m_WaterPlaneMesh.indexFormat = d.vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            m_WaterPlaneMesh.SetVertices(d.vertices);
            m_WaterPlaneMesh.SetNormals(d.normals);
            m_WaterPlaneMesh.SetColors(d.colors);
            m_WaterPlaneMesh.SetTriangles(d.triangles, 0);
            m_WaterPlaneMesh.RecalculateBounds();
            m_WaterPlane.localScale = Vector3.one;
            m_WaterPlane.gameObject.SetActive(true);
            m_WaterPlane.position = new Vector3(-cells * 4f, 0f, -cells * 4f);
        }

        void MoveWaterPlane(Vector3 p)
        {
            if (m_WaterPlane == null) return;
            var mesh = m_WaterPlaneMesh;
            float half = mesh.bounds.size.x * 0.5f;
            // A saltos de 8 m, para que las olas (que van por posición) no «salten»
            m_WaterPlane.position = new Vector3(Mathf.Round(p.x / 8f) * 8f - half, 0f, Mathf.Round(p.z / 8f) * 8f - half);
            bool under = PlayerUnderground;
            if (m_WaterPlane.gameObject.activeSelf == under) m_WaterPlane.gameObject.SetActive(!under);
        }

        void BuildRules()
        {
            var data = new List<PropRuleData>();
            var rules = new List<BiomePropRule>();
            var geo = new List<PropGeometry>();
            var cache = new Dictionary<string, PropGeometry>();
            foreach (var m in m_CreatedPropMaterials) if (m != null) Destroy(m);
            m_CreatedPropMaterials.Clear();
            m_PropMaterials.Clear(); m_PropMaterialIndex.Clear(); m_PropLookIndex.Clear(); m_OriginalIndex.Clear();
            m_PropMaterials.Add(propMaterial != null ? propMaterial : terrainMaterial);
            foreach (var b in biomes)
            {
                if (b == null) continue;
                foreach (var r in b.props)
                {
                    if (r == null || r.prefab == null || r.density <= 0f) continue;
                    PropGeometry g;
                    string ck = r.prefab.GetInstanceID() + ":" + r.lod + ":" + r.originalMaterial;
                    if (!cache.TryGetValue(ck, out g)) { g = ReadGeometry(r.prefab, r.lod, r.originalMaterial); cache[ck] = g; }
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

        /// <summary>
        /// Lee la geometría de un prefab (relativa a su raíz) para combinarla. Si tiene LOD Group, solo la del LOD elegido
        /// (-1 = automático: el primero con autoLodTriangles o menos). Las mallas tienen que ser legibles (Read/Write).
        /// Cada material con textura se convierte en un material propio (shader VRKit/PropTextura) con su textura y color;
        /// los que no tienen textura pasan su color a los vértices y van todos juntos con el material de vegetación.
        /// </summary>
        PropGeometry ReadGeometry(GameObject prefab, int lod, bool original)
        {
            var renderers = PickRenderers(prefab, lod);
            var parts = new List<PropPart>();
            var root = prefab.transform.worldToLocalMatrix;
            foreach (var mr in renderers)
            {
                var mf = mr != null ? mr.GetComponent<MeshFilter>() : null;
                var mesh = mf != null ? mf.sharedMesh : null;
                if (mesh == null) continue;
                if (!mesh.isReadable)
                {
                    Debug.LogWarning("[VRKit] La malla «" + mesh.name + "» de «" + prefab.name + "» no es legible: en su FBX marcad Read/Write " +
                                     "(o usad Tools › VRKit › Mundo abierto › Preparar modelos seleccionados). Mientras, se dibuja como objeto suelto.");
                    return null;
                }
                var m = root * mf.transform.localToWorldMatrix;
                var mv = mesh.vertices; var mn = mesh.normals; var mc = mesh.colors32;
                var muv = mesh.uv;
                var mats = mr.sharedMaterials;
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    var src = Resolve(mats != null && mats.Length > 0 ? mats[Mathf.Min(sub, mats.Length - 1)] : null);
                    bool hasUv = muv != null && muv.Length == mv.Length;
                    // «Original Material»: su propio material y shader, sin traducir (para texturas que son máscaras, estaciones…)
                    bool keep = original && src != null && !PropMaterialReader.IsReady(src);
                    bool textured = keep ? hasUv : hasUv && src != null && (PropMaterialReader.IsReady(src) || (texturedPropMaterial != null && PropMaterialReader.Read(src).texture != null));
                    Color color = keep || textured ? Color.white : PropMaterialReader.Read(src).color;
                    int matIndex = keep ? OriginalMaterialFor(src) : textured ? PropMaterialFor(src) : 0;

                    // Solo los vértices que usa este submesh, renumerados
                    var tris = mesh.GetTriangles(sub);
                    var remap = new Dictionary<int, int>();
                    var pv = new List<Vector3>(); var pn = new List<Vector3>(); var pc = new List<Color32>(); var pu = new List<Vector2>();
                    var pt = new int[tris.Length];
                    for (int k = 0; k < tris.Length; k++)
                    {
                        int i = tris[k], j;
                        if (!remap.TryGetValue(i, out j))
                        {
                            j = pv.Count; remap[i] = j;
                            pv.Add(m.MultiplyPoint3x4(mv[i]));
                            pn.Add(mn != null && i < mn.Length ? m.MultiplyVector(mn[i]).normalized : Vector3.up);
                            // Con textura: el color va en el material (los colores de vértice de otros paquetes suelen ser datos de viento)
                            // Con su material original, sus colores de vértice tal cual: su shader los usa (viento, sombreado…)
                            pc.Add(keep ? (mc != null && i < mc.Length ? mc[i] : new Color32(255, 255, 255, 255))
                                 : textured ? new Color32(255, 255, 255, 255) : (mc != null && i < mc.Length ? mc[i] : (Color32)color));
                            if (textured) pu.Add(muv[i]);
                        }
                        pt[k] = j;
                    }
                    if (pv.Count == 0) continue;
                    parts.Add(new PropPart { material = matIndex, vertices = pv.ToArray(), normals = pn.ToArray(), colors = pc.ToArray(), uvs = textured ? pu.ToArray() : null, triangles = pt });
                }
            }
            if (parts.Count == 0) return null;
            var g = new PropGeometry { parts = parts.ToArray() };
            if (g.TriangleCount > 6000)
                Debug.LogWarning("[VRKit] «" + prefab.name + "» tiene " + g.TriangleCount + " triángulos en el LOD usado: en la vegetación es mucho para Quest. Probad un LOD más bajo en su regla del bioma.");
            return g;
        }

        /// <summary>Los renderers del LOD elegido, o todos los del prefab (sin los nombrados _LOD1, _LOD2…) si no tiene LOD Group.</summary>
        List<MeshRenderer> PickRenderers(GameObject prefab, int lod)
        {
            var list = new List<MeshRenderer>();
            var group = prefab.GetComponentInChildren<LODGroup>(true);
            if (group != null)
            {
                var lods = group.GetLODs();
                if (lods.Length > 0)
                {
                    int pick = lod;
                    if (pick < 0)
                    {
                        pick = lods.Length - 1;
                        for (int i = 0; i < lods.Length; i++)
                            if (CountTriangles(lods[i].renderers) <= autoLodTriangles) { pick = i; break; }
                    }
                    pick = Mathf.Clamp(pick, 0, lods.Length - 1);
                    foreach (var r in lods[pick].renderers) { var mr = r as MeshRenderer; if (mr != null) list.Add(mr); }
                    return list;
                }
            }
            foreach (var mr in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = mr.gameObject.name;
                int at = n.LastIndexOf("_LOD", StringComparison.OrdinalIgnoreCase);
                if (at >= 0 && at + 4 < n.Length && n[at + 4] != '0') continue;
                list.Add(mr);
            }
            return list;
        }

        static int CountTriangles(Renderer[] renderers)
        {
            int n = 0;
            foreach (var r in renderers)
            {
                var mf = r != null ? r.GetComponent<MeshFilter>() : null;
                if (mf == null || mf.sharedMesh == null) continue;
                for (int s = 0; s < mf.sharedMesh.subMeshCount; s++) n += (int)mf.sharedMesh.GetIndexCount(s) / 3;
            }
            return n;
        }

        /// <summary>El material del propio prefab, sin copiar ni cambiar de shader.</summary>
        int OriginalMaterialFor(Material src)
        {
            int idx;
            if (m_OriginalIndex.TryGetValue(src, out idx)) return idx;
            idx = m_PropMaterials.Count;
            m_PropMaterials.Add(src);
            m_OriginalIndex[src] = idx;
            if (logMaterials) Debug.Log("[VRKit] Vegetación: «" + src.name + "» con su material original (shader " + (src.shader != null ? src.shader.name : "?") + ").");
            return idx;
        }

        /// <summary>Índice del material combinado para src (ya resuelto con Material Overrides). Crea uno si hace falta.</summary>
        int PropMaterialFor(Material src)
        {
            int idx;
            if (m_PropMaterialIndex.TryGetValue(src, out idx)) return idx;
            if (PropMaterialReader.IsReady(src))
            {
                // Una copia ya corregida (shader VRKit/PropTextura): se usa tal cual
                idx = m_PropMaterials.Count;
                m_PropMaterials.Add(src);
                m_PropMaterialIndex[src] = idx;
                return idx;
            }
            var look = PropMaterialReader.Read(src);
            // Materiales distintos que se ven igual comparten uno: menos llamadas de dibujo
            string key = look.texture.GetInstanceID() + "|" + look.scaleOffset + "|" + ((Color32)look.color).ToString() + "|" + look.cutoff.ToString("0.00") + "|" + look.twoSided;
            if (m_PropLookIndex.TryGetValue(key, out idx)) { m_PropMaterialIndex[src] = idx; return idx; }
            var mat = new Material(texturedPropMaterial) { name = "Vegetación · " + src.name };
            PropMaterialReader.Apply(mat, look);
            m_CreatedPropMaterials.Add(mat);
            if (logMaterials) Debug.Log("[VRKit] Vegetación: «" + src.name + "» (shader " + (src.shader != null ? src.shader.name : "?") + ") → " + look +
                                        ". Si el color sale mal: Tools › VRKit › Mundo abierto › Copia editable de materiales.");
            idx = m_PropMaterials.Count;
            m_PropMaterials.Add(mat);
            m_PropLookIndex[key] = idx;
            m_PropMaterialIndex[src] = idx;
            return idx;
        }

        /// <summary>El material que manda para src: su sustituto de Material Overrides, si lo tiene.</summary>
        Material Resolve(Material src)
        {
            if (src == null || materialOverrides == null) return src;
            foreach (var o in materialOverrides) if (o != null && o.original == src && o.replacement != null) return o.replacement;
            return src;
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
            SpawnPendingProps();

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
            MoveWaterPlane(p);
            if (autoQuality) AutoQuality();

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
                int lod = d < detailDistance ? 0 : d < midDistance ? 1 : 2;
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
            int res = resolution, far = farResolution, veryFar = Mathf.Min(veryFarResolution, farResolution); bool flat = flatShading;
            var water = singleWaterPlane ? null : m_WaterTemplate;
            var ruleInfo = m_Rules;
            int matCount = m_PropMaterials.Count;
            Task.Run(() =>
            {
                try
                {
                    job.mesh = new LowPolyMeshData();
                    job.info = TerrainMesher.Build(gen, job.cx, job.cz, size, job.lod == 0 ? res : job.lod == 1 ? far : veryFar, flat, pal, skirt, job.mesh);
                    if (job.info.hasWater && water != null) job.water = water;
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
                        job.decor = new LowPolyMeshData[matCount];
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
            if (ChunkUpdated != null) ChunkUpdated(e.view);
        }

        // ================================================================== Calidad automática

        void AutoQuality()
        {
            if (!m_Ready || PlayerUnderground) return;
            m_QualitySum += Time.unscaledDeltaTime;
            m_QualityFrames++;
            if (Time.unscaledTime < m_QualityTime) return;
            m_QualityTime = Time.unscaledTime + 1f;
            if (m_QualityFrames < 5) { m_QualitySum = 0f; m_QualityFrames = 0; return; }
            float avg = m_QualitySum / m_QualityFrames;
            m_QualitySum = 0f; m_QualityFrames = 0;
            float target = 1f / (targetFps > 0f ? targetFps : VRPerformance.DisplayFrameRate);

            if (avg > target * 1.12f) { m_SlowWindows++; m_FastWindows = 0; }
            else if (avg < target * 1.03f) { m_FastWindows++; m_SlowWindows = 0; }
            else { m_SlowWindows = 0; m_FastWindows = 0; }

            float min = Mathf.Max(minViewDistance, detailDistance + chunkSize);
            if (m_SlowWindows >= 2 && viewDistance > min)
            {
                ViewDistance = Mathf.Max(min, viewDistance - chunkSize * 0.5f);
                m_SlowWindows = 0;
                if (VRDevMode.Active) Debug.Log("[VRKit] Calidad automática: distancia de vista " + viewDistance.ToString("0") + " m");
            }
            else if (m_FastWindows >= 6 && viewDistance < m_MaxViewDistance)
            {
                ViewDistance = Mathf.Min(m_MaxViewDistance, viewDistance + chunkSize * 0.5f);
                m_FastWindows = 0;
            }
        }

        /// <summary>Cambia la distancia de vista máxima (la calidad automática no pasará de aquí).</summary>
        public void SetMaxViewDistance(float meters)
        {
            m_MaxViewDistance = Mathf.Max(detailDistance + chunkSize, meters);
            if (singleWaterPlane && m_Initialized) BuildWaterPlane();
            ViewDistance = Mathf.Min(viewDistance, m_MaxViewDistance);
            if (!autoQuality) ViewDistance = m_MaxViewDistance;
        }

        // ================================================================== Vegetación, cuevas y objetos colocados

        void SpawnDetail(Entry e, Job job)
        {
            var view = e.view;
            DespawnDetail(e);
            view.propsSpawned = true;
            var rec = GetRecord(job.key);

            // 1. Mallas combinadas: una por material (casi siempre una o pocas)
            if (job.decor != null)
                for (int mi = 0; mi < job.decor.Length; mi++)
                {
                    var d = job.decor[mi];
                    if (d == null || d.vertices.Count == 0) continue;
                    var go = new GameObject(mi == 0 ? "Vegetación combinada" : "Vegetación combinada · " + mi);
                    go.transform.SetParent(view.propRoot, false);
                    var mesh = new Mesh { name = "Vegetación " + job.cx + "," + job.cz + " · " + mi };
                    mesh.indexFormat = d.vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
                    mesh.SetVertices(d.vertices);
                    mesh.SetNormals(d.normals);
                    mesh.SetColors(d.colors);
                    if (d.uvs.Count == d.vertices.Count) mesh.SetUVs(0, d.uvs);
                    mesh.SetTriangles(d.triangles, 0);
                    mesh.RecalculateBounds();
                    // Material original: puede usar normal map, que necesita tangentes
                    if (mi < m_PropMaterials.Count && m_OriginalIndex.ContainsKey(m_PropMaterials[mi]) && d.uvs.Count == d.vertices.Count) mesh.RecalculateTangents();
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = mi < m_PropMaterials.Count ? m_PropMaterials[mi] : m_PropMaterials[0];
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    go.AddComponent<DestroyMeshOnDestroy>();
                    view.props.Add(go);
                }

            // 2. Árboles y rocas con colisión (o que no se pudieron combinar): unos pocos por frame
            view.pendingProps = job.props;
            view.pendingIndex = 0;
            if (job.props != null && job.props.Count > 0 && !m_SpawnQueue.Contains(view)) m_SpawnQueue.Add(view);

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

        void SpawnPendingProps()
        {
            int budget = propSpawnsPerFrame;
            while (budget > 0 && m_SpawnQueue.Count > 0)
            {
                var view = m_SpawnQueue[0];
                if (view == null || view.pendingProps == null || view.pendingIndex >= view.pendingProps.Count)
                {
                    if (view != null) view.pendingProps = null;
                    m_SpawnQueue.RemoveAt(0);
                    continue;
                }
                var pp = view.pendingProps[view.pendingIndex++];
                var rule = m_Rules[pp.rule];
                bool combined = m_RuleGeometry[pp.rule] != null;
                if (combined && !rule.collide) continue;      // ya está en la malla combinada y no choca
                budget--;
                var inst = Take(rule.prefab, view.propRoot);
                inst.transform.localPosition = new Vector3(pp.x, pp.y - rule.sink * pp.scale, pp.z);
                inst.transform.localRotation = Quaternion.Euler(0f, pp.yaw, 0f);
                inst.transform.localScale = rule.prefab.transform.localScale * pp.scale;
                foreach (var rend in inst.GetComponentsInChildren<Renderer>(true)) rend.enabled = !combined;
                foreach (var lg in inst.GetComponentsInChildren<LODGroup>(true)) lg.enabled = !combined;
                var wp = inst.GetComponent<WorldProp>();
                if (wp == null) wp = inst.AddComponent<WorldProp>();
                wp.ChunkKey = view.Key; wp.Id = pp.id; wp.Rule = pp.rule; wp.SourcePrefab = rule.prefab;
                view.props.Add(inst);
            }
        }

        void DespawnDetail(Entry e)
        {
            var view = e.view;
            if (view == null) return;
            view.pendingProps = null;
            m_SpawnQueue.Remove(view);
            if (!view.propsSpawned) return;
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
            if (e.view != null) m_SpawnQueue.Remove(e.view);
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
            if (Saving != null) { try { Saving(); } catch (Exception ex) { Debug.LogException(ex); } }
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

        internal static T ReadJson<T>(string path) where T : class
        {
            try { return File.Exists(path) ? JsonUtility.FromJson<T>(File.ReadAllText(path)) : null; }
            catch (Exception ex) { Debug.LogWarning("[VRKit] No se pudo leer " + path + ": " + ex.Message); return null; }
        }

        internal static void WriteJson(string path, object data)
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
