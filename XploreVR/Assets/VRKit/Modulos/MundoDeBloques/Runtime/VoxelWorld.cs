using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRKit
{
    /// <summary>Lo que devuelve un rayo contra los bloques.</summary>
    public struct VoxelHit
    {
        /// <summary>Bloque golpeado (coordenadas de bloque).</summary>
        public Vector3Int block;
        /// <summary>Cara golpeada: hacia dónde «sale» del bloque.</summary>
        public Vector3Int normal;
        public byte id;
        /// <summary>Distancia en metros desde el origen del rayo.</summary>
        public float distance;
        /// <summary>Punto del mundo donde entró el rayo.</summary>
        public Vector3 point;
        /// <summary>Hueco de al lado de la cara golpeada: donde se pone un bloque nuevo.</summary>
        public Vector3Int Adjacent { get { return block + normal; } }
    }

    /// <summary>Qué bloque usa el generador para cada capa del terreno.</summary>
    [Serializable]
    public class VoxelTerrainBlocks
    {
        public BlockType grass, dirt, stone, sand, snow, log, leaves, coalOre, ironOre, bedrock, flower, tallGrass;

        public VoxelTerrainIds ToIds(BlockRegistry r)
        {
            return new VoxelTerrainIds
            {
                grass = r.IdOf(grass), dirt = r.IdOf(dirt), stone = r.IdOf(stone), sand = r.IdOf(sand), snow = r.IdOf(snow),
                log = r.IdOf(log), leaves = r.IdOf(leaves), coal = r.IdOf(coalOre), iron = r.IdOf(ironOre),
                bedrock = r.IdOf(bedrock), flower = r.IdOf(flower), tallGrass = r.IdOf(tallGrass),
            };
        }
    }

    /// <summary>
    /// El mundo de bloques. Guarda qué bloque hay en cada sitio, genera el terreno con una semilla, crea las mallas
    /// de los chunks cercanos al jugador y avisa cuando algo cambia.
    ///
    ///   VoxelWorld.Instance.SetBlock(new Vector3Int(10, 25, 4), id);
    ///   if (VoxelWorld.Instance.Raycast(ray, 5f, out var hit)) …
    ///
    /// El tamaño de cada bloque es la escala del objeto: escala 1 = bloques de 1 metro (como Minecraft);
    /// escala 0,05 = una maqueta sobre la mesa. Todo funciona igual.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class VoxelWorld : MonoBehaviour
    {
        public const int ChunkSize = 16;

        public static VoxelWorld Instance { get; private set; }

        [Header("Bloques")]
        [SerializeField] BlockRegistry registry;
        [SerializeField] VoxelTerrainBlocks terrainBlocks = new VoxelTerrainBlocks();

        [Header("Tamaño")]
        [Tooltip("Altura del mundo en bloques. Más alto = más memoria y más tiempo al crear cada chunk.")]
        [SerializeField, Range(16, 128)] int worldHeight = 48;
        [Tooltip("Infinito: se genera según andas. Desmarcado: un mundo de Size In Chunks × 16 bloques, con bordes.")]
        [SerializeField] bool infinite = false;
        [SerializeField] Vector2Int sizeInChunks = new Vector2Int(8, 8);
        [Tooltip("Cuántos chunks se dibujan alrededor del jugador. En Quest 2: 3 o 4.")]
        [SerializeField, Range(1, 12)] int viewDistance = 4;

        [Header("Generación")]
        [Tooltip("0 = una al azar cada mundo nuevo. La misma semilla da siempre el mismo mundo.")]
        [SerializeField] int seed = 0;
        [SerializeField] VoxelTerrainSettings terrain = new VoxelTerrainSettings();

        [Header("Rendimiento")]
        [Tooltip("Chunks nuevos que se mallan por frame. Más = carga antes pero puede dar tirones.")]
        [SerializeField, Range(1, 8)] int chunksPerFrame = 2;
        [SerializeField] bool ambientOcclusion = true;
        [SerializeField] bool colliders = true;

        [Header("Jugador")]
        [Tooltip("Se puede teletransportar sobre los bloques (solo encima, no a las paredes).")]
        [SerializeField] bool teleportOnBlocks = true;
        [Tooltip("Quién decide qué chunks se cargan. Vacío = la cámara.")]
        [SerializeField] Transform viewer;
        [Tooltip("Niebla que esconde el borde de lo que se ha cargado.")]
        [SerializeField] bool fogHidesEdges = true;
        [SerializeField] Color fogColor = new Color(0.62f, 0.78f, 0.95f);
        [Tooltip("Desmarcado si otro script (VoxelGame) decide la semilla y carga la partida.")]
        [SerializeField] bool initializeOnStart = true;

        /// <summary>(posición, id anterior, id nuevo)</summary>
        public event Action<Vector3Int, byte, byte> BlockChanged;
        /// <summary>Cuando ya están dibujados los chunks de alrededor del jugador.</summary>
        public event Action Ready;

        class ChunkData
        {
            public byte[] blocks;
            public bool edited;
            public VoxelChunk view;
        }

        readonly Dictionary<long, ChunkData> m_Chunks = new Dictionary<long, ChunkData>();
        readonly HashSet<long> m_Urgent = new HashSet<long>();
        readonly List<long> m_Pending = new List<long>();
        readonly List<long> m_Scratch = new List<long>();
        readonly Dictionary<byte, Mesh> m_ItemMeshes = new Dictionary<byte, Mesh>();
        VoxelTerrainGenerator m_Generator;
        VoxelMeshData m_MeshData;
        byte[] m_Padded;
        Material m_Material;
        bool m_Initialized, m_Ready;
        int m_CenterX = int.MinValue, m_CenterZ = int.MinValue;

        public BlockRegistry Registry { get { return registry; } }
        public Material Material { get { return m_Material; } }
        public int Height { get { return worldHeight; } }
        public int Seed { get { return seed; } }
        public bool IsReady { get { return m_Ready; } }
        public bool Infinite { get { return infinite; } }
        public Vector2Int SizeInChunks { get { return sizeInChunks; } }
        public int ViewDistance { get { return viewDistance; } set { viewDistance = Mathf.Clamp(value, 1, 12); m_CenterX = int.MinValue; ApplyFog(); } }
        /// <summary>Lo que mide un bloque en metros.</summary>
        public float BlockSize { get { return transform.lossyScale.x; } }
        public Color FogColor { get { return fogColor; } set { fogColor = value; if (fogHidesEdges) RenderSettings.fogColor = value; } }

        // ================================================================== Arranque

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            foreach (var m in m_ItemMeshes.Values) if (m != null) Destroy(m);
        }

        void Start()
        {
            if (initializeOnStart && !m_Initialized && FindFirstObjectByType<VoxelGame>() == null) Initialize(seed);
        }

        /// <summary>Empieza un mundo con esa semilla (0 = al azar). Borra lo que hubiera.</summary>
        public void Initialize(int newSeed)
        {
            Instance = this;
            if (registry == null) { Debug.LogError("[VRKit] VoxelWorld sin BlockRegistry. Usad GameObject › VRKit › Mundo de bloques para montarlo."); enabled = false; return; }
            registry.Init(true);
            seed = newSeed != 0 ? newSeed : UnityEngine.Random.Range(1, int.MaxValue);
            m_Generator = new VoxelTerrainGenerator(terrain, terrainBlocks.ToIds(registry), seed);
            if (m_MeshData == null) m_MeshData = new VoxelMeshData();
            m_Padded = new byte[(ChunkSize + 2) * (worldHeight + 2) * (ChunkSize + 2)];
            if (m_Material == null) m_Material = registry.material != null ? registry.material : CreateMaterial();

            foreach (var c in m_Chunks.Values) if (c.view != null) Destroy(c.view.gameObject);
            m_Chunks.Clear(); m_Urgent.Clear(); m_Pending.Clear();
            m_CenterX = m_CenterZ = int.MinValue;
            m_Ready = false;
            m_Initialized = true;
            Shader.SetGlobalFloat("_VRKitVoxelDarkness", 0f);
            ApplyFog();
        }

        Material CreateMaterial()
        {
            var sh = Shader.Find("VRKit/Voxel");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
            var m = new Material(sh) { name = "Bloques (creado al empezar)" };
            Texture2D tex = registry.atlas;
            if (tex == null)
            {
                int w, h;
                var px = VoxelAtlasPainter.Paint(16, out w, out h);
                tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Atlas (creado al empezar)" };
                tex.SetPixels32(px);
                tex.Apply();
                registry.atlas = tex;
            }
            m.mainTexture = tex;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            return m;
        }

        void ApplyFog()
        {
            if (!fogHidesEdges || !m_Initialized) return;
            float chunk = ChunkSize * BlockSize;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = Mathf.Max(chunk, (viewDistance - 1.5f) * chunk);
            RenderSettings.fogEndDistance = (viewDistance - 0.25f) * chunk;
            var cam = Camera.main;
            if (cam != null && FindFirstObjectByType<VoxelDayNight>() == null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = fogColor;
            }
        }

        // ================================================================== Coordenadas

        public Vector3 ToLocal(Vector3 world) { return transform.InverseTransformPoint(world); }
        public Vector3 ToWorld(Vector3 local) { return transform.TransformPoint(local); }

        /// <summary>Bloque que contiene un punto del mundo.</summary>
        public Vector3Int WorldToBlock(Vector3 world)
        {
            var l = ToLocal(world);
            return new Vector3Int(Mathf.FloorToInt(l.x), Mathf.FloorToInt(l.y), Mathf.FloorToInt(l.z));
        }

        /// <summary>Centro de un bloque, en el mundo.</summary>
        public Vector3 BlockCenter(Vector3Int b) { return ToWorld(new Vector3(b.x + 0.5f, b.y + 0.5f, b.z + 0.5f)); }

        static int FloorDiv(int a, int b) { return a >= 0 ? a / b : (a - b + 1) / b; }
        static long Key(int cx, int cz) { return ((long)cx << 32) ^ (uint)cz; }

        public bool ChunkInBounds(int cx, int cz)
        {
            return infinite || (cx >= 0 && cz >= 0 && cx < sizeInChunks.x && cz < sizeInChunks.y);
        }

        public bool InBounds(Vector3Int b)
        {
            return b.y >= 0 && b.y < worldHeight && ChunkInBounds(FloorDiv(b.x, ChunkSize), FloorDiv(b.z, ChunkSize));
        }

        /// <summary>Columna central del mundo (donde aparece el jugador por defecto).</summary>
        public Vector2Int CenterColumn
        {
            get { return infinite ? Vector2Int.zero : new Vector2Int(sizeInChunks.x * ChunkSize / 2, sizeInChunks.y * ChunkSize / 2); }
        }

        // ================================================================== Leer y cambiar bloques

        ChunkData GetChunk(int cx, int cz, bool create)
        {
            ChunkData c;
            long k = Key(cx, cz);
            if (m_Chunks.TryGetValue(k, out c)) return c;
            if (!create || !m_Initialized || !ChunkInBounds(cx, cz)) return null;
            c = new ChunkData { blocks = new byte[ChunkSize * ChunkSize * worldHeight] };
            m_Generator.Generate(c.blocks, ChunkSize, worldHeight, ChunkSize, cx, cz);
            m_Chunks.Add(k, c);
            return c;
        }

        public byte GetBlock(Vector3Int b) { return GetBlock(b.x, b.y, b.z); }

        public byte GetBlock(int x, int y, int z)
        {
            if (y < 0 || y >= worldHeight) return 0;
            int cx = FloorDiv(x, ChunkSize), cz = FloorDiv(z, ChunkSize);
            var c = GetChunk(cx, cz, true);
            if (c == null) return 0;
            return c.blocks[(x - cx * ChunkSize) + ChunkSize * ((z - cz * ChunkSize) + ChunkSize * y)];
        }

        public BlockType GetBlockType(Vector3Int b) { byte id = GetBlock(b); return id == 0 ? null : registry.Get(id); }

        public bool IsSolid(Vector3Int b) { return IsSolid(b.x, b.y, b.z); }

        public bool IsSolid(int x, int y, int z)
        {
            byte id = GetBlock(x, y, z);
            return id != 0 && registry.MeshInfo[id].solid;
        }

        /// <summary>Altura del bloque sólido más alto de una columna (-1 si no hay ninguno).</summary>
        public int SurfaceHeight(int x, int z)
        {
            for (int y = worldHeight - 1; y >= 0; y--)
                if (IsSolid(x, y, z)) return y;
            return -1;
        }

        /// <summary>Cambia un bloque (0 = aire). Devuelve false si está fuera del mundo o ya era ese.</summary>
        public bool SetBlock(Vector3Int b, byte id, bool byPlayer = true)
        {
            if (!m_Initialized || !InBounds(b)) return false;
            int cx = FloorDiv(b.x, ChunkSize), cz = FloorDiv(b.z, ChunkSize);
            var c = GetChunk(cx, cz, true);
            if (c == null) return false;
            int lx = b.x - cx * ChunkSize, lz = b.z - cz * ChunkSize;
            int i = lx + ChunkSize * (lz + ChunkSize * b.y);
            byte old = c.blocks[i];
            if (old == id) return false;
            c.blocks[i] = id;
            if (byPlayer) c.edited = true;

            // Este chunk, y los vecinos si el bloque está en el borde (caras y sombras de las esquinas)
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                bool nx = dx == 0 || (dx < 0 && lx == 0) || (dx > 0 && lx == ChunkSize - 1);
                bool nz = dz == 0 || (dz < 0 && lz == 0) || (dz > 0 && lz == ChunkSize - 1);
                if (nx && nz) MarkDirty(cx + dx, cz + dz);
            }
            if (BlockChanged != null) BlockChanged(b, old, id);
            return true;
        }

        public bool SetBlock(Vector3Int b, BlockType type, bool byPlayer = true) { return SetBlock(b, registry.IdOf(type), byPlayer); }

        void MarkDirty(int cx, int cz)
        {
            ChunkData c;
            if (m_Chunks.TryGetValue(Key(cx, cz), out c) && c.view != null) m_Urgent.Add(Key(cx, cz));
        }

        // ================================================================== Rayos

        /// <summary>
        /// Rayo contra los bloques (sin física: recorre la rejilla casilla a casilla, así acierta siempre el bloque exacto).
        /// maxDistance en metros. Con solidOnly, atraviesa flores y hierba alta.
        /// </summary>
        public bool Raycast(Ray ray, float maxDistance, out VoxelHit hit, bool solidOnly = false)
        {
            hit = default(VoxelHit);
            if (!m_Initialized) return false;
            Vector3 o = ToLocal(ray.origin);
            Vector3 dl = transform.InverseTransformVector(ray.direction);
            float scale = dl.magnitude;          // bloques por metro
            if (scale < 1e-6f) return false;
            Vector3 d = dl / scale;
            float maxT = maxDistance * scale;

            int x = Mathf.FloorToInt(o.x), y = Mathf.FloorToInt(o.y), z = Mathf.FloorToInt(o.z);
            int sx = d.x > 0 ? 1 : d.x < 0 ? -1 : 0, sy = d.y > 0 ? 1 : d.y < 0 ? -1 : 0, sz = d.z > 0 ? 1 : d.z < 0 ? -1 : 0;
            float tdx = sx != 0 ? Mathf.Abs(1f / d.x) : float.PositiveInfinity;
            float tdy = sy != 0 ? Mathf.Abs(1f / d.y) : float.PositiveInfinity;
            float tdz = sz != 0 ? Mathf.Abs(1f / d.z) : float.PositiveInfinity;
            float tmx = sx > 0 ? (x + 1 - o.x) * tdx : sx < 0 ? (o.x - x) * tdx : float.PositiveInfinity;
            float tmy = sy > 0 ? (y + 1 - o.y) * tdy : sy < 0 ? (o.y - y) * tdy : float.PositiveInfinity;
            float tmz = sz > 0 ? (z + 1 - o.z) * tdz : sz < 0 ? (o.z - z) * tdz : float.PositiveInfinity;
            float t = 0f;
            var normal = Vector3Int.zero;

            for (int guard = 0; guard < 512 && t <= maxT; guard++)
            {
                byte id = GetBlock(x, y, z);
                if (id != 0 && (!solidOnly || registry.MeshInfo[id].solid))
                {
                    hit.block = new Vector3Int(x, y, z);
                    hit.normal = normal;
                    hit.id = id;
                    hit.distance = t / scale;
                    hit.point = ToWorld(o + d * t);
                    return true;
                }
                if (tmx < tmy && tmx < tmz) { x += sx; t = tmx; tmx += tdx; normal = new Vector3Int(-sx, 0, 0); }
                else if (tmy < tmz) { y += sy; t = tmy; tmy += tdy; normal = new Vector3Int(0, -sy, 0); }
                else { z += sz; t = tmz; tmz += tdz; normal = new Vector3Int(0, 0, -sz); }
            }
            return false;
        }

        // ================================================================== Carga de chunks

        Vector3 ViewerLocal()
        {
            Transform v = viewer;
            if (v == null && Camera.main != null) v = Camera.main.transform;
            if (v != null) return ToLocal(v.position);
            var c = CenterColumn;
            return new Vector3(c.x, 0f, c.y);
        }

        void Update()
        {
            if (!m_Initialized) return;

            // 1. Cambios hechos por el jugador: se rehacen ya (se nota si tardan)
            if (m_Urgent.Count > 0)
            {
                m_Scratch.Clear();
                m_Scratch.AddRange(m_Urgent);
                m_Urgent.Clear();
                foreach (long k in m_Scratch) RebuildView(k);
            }

            // 2. ¿El jugador ha cambiado de chunk? Lista de chunks a cargar, del más cercano al más lejano
            var p = ViewerLocal();
            int ccx = FloorDiv(Mathf.FloorToInt(p.x), ChunkSize), ccz = FloorDiv(Mathf.FloorToInt(p.z), ChunkSize);
            if (ccx != m_CenterX || ccz != m_CenterZ)
            {
                m_CenterX = ccx; m_CenterZ = ccz;
                RefreshPending();
                UnloadFar();
            }

            // 3. Mallar unos pocos por frame
            int budget = chunksPerFrame;
            while (budget > 0 && m_Pending.Count > 0)
            {
                long k = m_Pending[0];
                m_Pending.RemoveAt(0);
                int cx = (int)(k >> 32), cz = (int)(k & 0xffffffffL);
                if (CreateView(cx, cz)) budget--;
            }

            if (!m_Ready && m_Pending.Count == 0)
            {
                m_Ready = true;
                if (Ready != null) Ready();
            }
        }

        void RefreshPending()
        {
            m_Pending.Clear();
            var list = new List<KeyValuePair<long, int>>();
            for (int dz = -viewDistance; dz <= viewDistance; dz++)
            for (int dx = -viewDistance; dx <= viewDistance; dx++)
            {
                int cx = m_CenterX + dx, cz = m_CenterZ + dz;
                if (dx * dx + dz * dz > (viewDistance + 0.5f) * (viewDistance + 0.5f)) continue;
                if (!ChunkInBounds(cx, cz)) continue;
                ChunkData c;
                if (m_Chunks.TryGetValue(Key(cx, cz), out c) && c.view != null) continue;
                list.Add(new KeyValuePair<long, int>(Key(cx, cz), dx * dx + dz * dz));
            }
            list.Sort((a, b) => a.Value.CompareTo(b.Value));
            foreach (var kv in list) m_Pending.Add(kv.Key);
        }

        void UnloadFar()
        {
            int r = viewDistance + 1;
            m_Scratch.Clear();
            foreach (var kv in m_Chunks)
            {
                int cx = (int)(kv.Key >> 32), cz = (int)(kv.Key & 0xffffffffL);
                int dx = cx - m_CenterX, dz = cz - m_CenterZ;
                int d2 = dx * dx + dz * dz;
                if (kv.Value.view != null && d2 > r * r)
                {
                    Destroy(kv.Value.view.gameObject);
                    kv.Value.view = null;
                }
                // En mundos infinitos se olvidan los chunks lejanos que nadie ha tocado (se pueden volver a generar)
                if (infinite && !kv.Value.edited && kv.Value.view == null && d2 > (r + 3) * (r + 3)) m_Scratch.Add(kv.Key);
            }
            foreach (long k in m_Scratch) m_Chunks.Remove(k);
        }

        bool CreateView(int cx, int cz)
        {
            var c = GetChunk(cx, cz, true);
            if (c == null || c.view != null) return false;
            var go = new GameObject("Chunk " + cx + "," + cz);
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(cx * ChunkSize, 0f, cz * ChunkSize);
            var view = go.AddComponent<VoxelChunk>();
            view.Setup(cx, cz, m_Material, colliders);
            if (colliders && teleportOnBlocks) AddTeleportArea(go);
            c.view = view;
            RebuildView(Key(cx, cz));
            return true;
        }

        void RebuildView(long key)
        {
            ChunkData c;
            if (!m_Chunks.TryGetValue(key, out c) || c.view == null) return;
            int cx = (int)(key >> 32), cz = (int)(key & 0xffffffffL);
            FillPadded(cx, cz);
            VoxelMesher.BuildChunk(m_Padded, ChunkSize, worldHeight, ChunkSize, registry.MeshInfo, registry.Layout, ambientOcclusion, m_MeshData);
            c.view.Apply(m_MeshData);
        }

        /// <summary>Copia el chunk y un bloque de margen de sus 8 vecinos (para saber qué caras se ven en el borde).</summary>
        void FillPadded(int cx, int cz)
        {
            const int S = ChunkSize;
            int px = S + 2, pz = S + 2;
            Array.Clear(m_Padded, 0, m_Padded.Length);
            for (int z = -1; z <= S; z++)
            for (int x = -1; x <= S; x++)
            {
                int dx = x < 0 ? -1 : x >= S ? 1 : 0, dz = z < 0 ? -1 : z >= S ? 1 : 0;
                var n = GetChunk(cx + dx, cz + dz, true);
                if (n == null) continue;
                int lx = x - dx * S, lz = z - dz * S;
                int src = lx + S * lz, dst = (x + 1) + px * (z + 1);
                for (int y = 0; y < worldHeight; y++)
                    m_Padded[dst + px * pz * (y + 1)] = n.blocks[src + S * S * y];
            }
        }

        static void AddTeleportArea(GameObject go) { TeleportAreaUtil.Add(go, 20f); }

        /// <summary>Vuelve a mallar todo lo visible (después de cambiar el atlas o la oclusión ambiental).</summary>
        public void RebuildAll()
        {
            foreach (var kv in m_Chunks) if (kv.Value.view != null) m_Urgent.Add(kv.Key);
        }

        // ================================================================== Objetos sueltos

        /// <summary>Malla de un solo bloque de lado 1, centrada (inventario, objetos que caen, bloque en la mano).</summary>
        public Mesh GetItemMesh(byte id)
        {
            Mesh m;
            if (m_ItemMeshes.TryGetValue(id, out m) && m != null) return m;
            var data = new VoxelMeshData();
            VoxelMesher.BuildSingle(registry.MeshInfo[id], registry.Layout, data);
            m = new Mesh { name = "Bloque " + id };
            m.SetVertices(data.vertices);
            m.SetUVs(0, data.uvs);
            m.SetColors(data.colors);
            m.SetTriangles(data.triangles, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            m_ItemMeshes[id] = m;
            return m;
        }

        // ================================================================== Guardar

        /// <summary>Chunks que el jugador ha cambiado, comprimidos (los demás se vuelven a generar con la semilla).</summary>
        public List<VoxelChunkSave> GetEditedChunks()
        {
            var list = new List<VoxelChunkSave>();
            foreach (var kv in m_Chunks)
                if (kv.Value.edited)
                    list.Add(new VoxelChunkSave { x = (int)(kv.Key >> 32), z = (int)(kv.Key & 0xffffffffL), data = VoxelRle.Encode(kv.Value.blocks) });
            return list;
        }

        /// <summary>Pone un chunk guardado. remap traduce ids antiguos (por si la lista de bloques cambió).</summary>
        public void LoadEditedChunk(VoxelChunkSave s, byte[] remap)
        {
            if (!m_Initialized || !ChunkInBounds(s.x, s.z)) return;
            var c = GetChunk(s.x, s.z, true);
            if (c == null) return;
            if (!VoxelRle.Decode(s.data, c.blocks, remap))
            {
                Debug.LogWarning("[VRKit] Chunk guardado " + s.x + "," + s.z + " dañado o de otra altura de mundo: se genera de nuevo.");
                m_Generator.Generate(c.blocks, ChunkSize, worldHeight, ChunkSize, s.x, s.z);
                return;
            }
            c.edited = true;
            if (c.view != null) m_Urgent.Add(Key(s.x, s.z));
        }

        void OnDrawGizmosSelected()
        {
            if (infinite) return;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.3f, 0.9f, 0.5f, 0.6f);
            var size = new Vector3(sizeInChunks.x * ChunkSize, worldHeight, sizeInChunks.y * ChunkSize);
            Gizmos.DrawWireCube(size * 0.5f, size);
        }
    }

    [Serializable]
    public class VoxelChunkSave
    {
        public int x, z;
        public string data;
    }
}
