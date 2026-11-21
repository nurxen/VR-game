using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace VRKit
{
    /// <summary>
    /// NavMesh que se rehace sola alrededor del jugador, para que los enemigos y animales (NavMeshAgent) puedan andar
    /// por un terreno que se genera mientras juegas. Recoge las colisiones del terreno y de los objetos en una caja
    /// alrededor del jugador y la reconstruye en segundo plano cuando te mueves o se carga un chunk.
    /// No necesita el paquete AI Navigation (usa la API de Unity directamente).
    /// </summary>
    public class WorldNavMesh : MonoBehaviour
    {
        [Tooltip("Tamaño de la zona con NavMesh alrededor del jugador (metros).")]
        [SerializeField] Vector3 size = new Vector3(120f, 400f, 120f);
        [Tooltip("Se rehace cuando el jugador se aleja esto del centro (metros).")]
        [SerializeField] float rebuildDistance = 25f;
        [Tooltip("Capas que cuentan como suelo u obstáculo.")]
        [SerializeField] LayerMask layers = ~0;
        [Tooltip("Tipo de agente (0 = Humanoid, el de por defecto).")]
        [SerializeField] int agentTypeId = 0;
        [Tooltip("No crear NavMesh bajo el agua.")]
        [SerializeField] bool excludeWater = true;

        NavMeshData m_Data;
        OpenWorld m_World;
        NavMeshDataInstance m_Instance;
        AsyncOperation m_Op;
        Vector3 m_Center = new Vector3(float.MaxValue, 0f, 0f);
        bool m_Dirty = true;
        float m_NextAllowed;
        readonly List<NavMeshBuildSource> m_Sources = new List<NavMeshBuildSource>();
        readonly List<NavMeshBuildMarkup> m_Markups = new List<NavMeshBuildMarkup>();

        public bool IsBuilding { get { return m_Op != null && !m_Op.isDone; } }

        void OnEnable()
        {
            m_Data = new NavMeshData(agentTypeId);
            m_Instance = NavMesh.AddNavMeshData(m_Data);
        }

        void OnDisable()
        {
            if (m_World != null) { m_World.ChunkLoaded -= OnChunk; m_World.ChunkUnloaded -= OnChunk; m_World = null; }
            m_Instance.Remove();
        }

        void OnChunk(TerrainChunk c) { m_Dirty = true; }

        void Update()
        {
            var world = OpenWorld.Instance;
            if (world != null && m_World != world) { m_World = world; world.ChunkLoaded += OnChunk; world.ChunkUnloaded += OnChunk; }
            var player = VRPlayer.Instance;
            if (world == null || player == null || !world.IsReady || IsBuilding || Time.time < m_NextAllowed) return;
            if (CaveSystem.Instance != null && CaveSystem.Instance.InCave) return;
            var p = player.FeetPosition;
            var flat = new Vector3(p.x, 0f, p.z);
            if (!m_Dirty && Vector3.Distance(flat, new Vector3(m_Center.x, 0f, m_Center.z)) < rebuildDistance) return;
            m_Dirty = false;
            m_NextAllowed = Time.time + 3f;   // como mucho una vez cada 3 s
            m_Center = new Vector3(Mathf.Round(p.x / 4f) * 4f, p.y, Mathf.Round(p.z / 4f) * 4f);
            Rebuild();
        }

        void Rebuild()
        {
            var bounds = new Bounds(m_Center, size);
            m_Sources.Clear();
            NavMeshBuilder.CollectSources(bounds, layers, NavMeshCollectGeometry.PhysicsColliders, 0, m_Markups, m_Sources);
            if (excludeWater && OpenWorld.Instance != null)
            {
                // Una caja «no caminable» bajo el nivel del agua
                float water = OpenWorld.Instance.WaterLevel;
                var src = new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.ModifierBox,
                    transform = Matrix4x4.Translate(new Vector3(m_Center.x, water - 20f, m_Center.z)),
                    size = new Vector3(size.x, 40f - 0.3f, size.z),
                    area = 1,   // Not Walkable
                };
                m_Sources.Add(src);
            }
            var settings = NavMesh.GetSettingsByID(agentTypeId);
            m_Op = NavMeshBuilder.UpdateNavMeshDataAsync(m_Data, settings, m_Sources, bounds);
        }
    }
}
