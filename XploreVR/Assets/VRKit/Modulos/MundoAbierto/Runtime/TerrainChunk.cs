using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace VRKit
{
    /// <summary>
    /// Un trozo cuadrado de terreno ya convertido en malla, con su agua, su colisión y sus árboles y rocas.
    /// Lo crea y lo borra el OpenWorld según dónde esté el jugador.
    /// </summary>
    public class TerrainChunk : MonoBehaviour
    {
        public int CX { get; private set; }
        public int CZ { get; private set; }
        public long Key { get; private set; }
        /// <summary>0 = detalle completo (con colisión y vegetación), 1 = lejano, 2 = muy lejano.</summary>
        public int Lod { get; internal set; }
        public bool HasCollider { get { return m_Collider != null && m_Collider.enabled && m_Collider.sharedMesh != null; } }
        public TerrainChunkInfo Info { get; internal set; }

        MeshFilter m_Filter;
        MeshRenderer m_Renderer;
        MeshCollider m_Collider;
        Mesh m_Mesh, m_ColliderMesh, m_WaterMesh;
        GameObject m_Water;
        internal Transform propRoot;
        internal readonly List<GameObject> props = new List<GameObject>();
        internal readonly List<GameObject> caves = new List<GameObject>();
        internal bool propsSpawned;
        /// <summary>Árboles y rocas con colisión que faltan por colocar (se colocan unos pocos por frame).</summary>
        internal List<PropPlacement> pendingProps;
        internal int pendingIndex;
        bool m_Teleport;

        // La colisión se «cocina» en otro hilo (Physics.BakeMesh) y se pone cuando está lista: sin tirones
        Task m_Bake;
        Mesh m_BakingMesh;
        readonly List<KeyValuePair<Task, Mesh>> m_Obsolete = new List<KeyValuePair<Task, Mesh>>();
        public static bool BakeCollidersInBackground = true;
        internal void Setup(int cx, int cz, long key, Material material)
        {
            CX = cx; CZ = cz; Key = key;
            m_Filter = gameObject.AddComponent<MeshFilter>();
            m_Renderer = gameObject.AddComponent<MeshRenderer>();
            m_Renderer.sharedMaterial = material;
            m_Renderer.shadowCastingMode = ShadowCastingMode.Off;
            m_Mesh = new Mesh { name = "Terreno " + cx + "," + cz };
            m_Filter.sharedMesh = m_Mesh;
            propRoot = new GameObject("Vegetación").transform;
            propRoot.SetParent(transform, false);
        }

        internal void ApplyMesh(LowPolyMeshData d, bool collider, bool teleport)
        {
            m_Mesh.Clear();
            m_Mesh.indexFormat = d.vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            m_Mesh.SetVertices(d.vertices);
            m_Mesh.SetNormals(d.normals);
            m_Mesh.SetColors(d.colors);
            m_Mesh.SetTriangles(d.triangles, 0);
            m_Mesh.RecalculateBounds();

            if (collider)
            {
                if (m_Collider == null) m_Collider = gameObject.AddComponent<MeshCollider>();
                // Una malla nueva cada vez: la anterior puede estar aún cocinándose en otro hilo
                var cm = new Mesh { name = "Terreno (colisión) " + CX + "," + CZ };
                cm.indexFormat = m_Mesh.indexFormat;
                cm.SetVertices(d.vertices);
                cm.SetTriangles(d.colliderTriangles, 0);
                if (m_Bake != null) m_Obsolete.Add(new KeyValuePair<Task, Mesh>(m_Bake, m_BakingMesh));
                m_Bake = null; m_BakingMesh = null;
                if (BakeCollidersInBackground)
                {
                    int id = cm.GetInstanceID();
                    m_BakingMesh = cm;
                    m_Bake = Task.Run(() => Physics.BakeMesh(id, false));
                    enabled = true;     // Update mira cuándo termina
                }
                else SetColliderMesh(cm);
                if (teleport && !m_Teleport) { TeleportAreaUtil.Add(gameObject, 35f); m_Teleport = true; }
            }
            else
            {
                if (m_Collider != null) m_Collider.enabled = false;
                if (m_Bake != null) { m_Obsolete.Add(new KeyValuePair<Task, Mesh>(m_Bake, m_BakingMesh)); m_Bake = null; m_BakingMesh = null; enabled = true; }
            }
        }

        void SetColliderMesh(Mesh cm)
        {
            var old = m_ColliderMesh;
            m_ColliderMesh = cm;
            m_Collider.sharedMesh = cm;     // ya cocinada: esto es casi gratis
            m_Collider.enabled = true;
            if (old != null && old != cm) Destroy(old);
        }

        void Awake() { enabled = false; }

        void Update()
        {
            if (m_Bake != null && m_Bake.IsCompleted)
            {
                var cm = m_BakingMesh;
                m_Bake = null; m_BakingMesh = null;
                if (cm != null && m_Collider != null) SetColliderMesh(cm);
            }
            for (int i = m_Obsolete.Count - 1; i >= 0; i--)
                if (m_Obsolete[i].Key.IsCompleted) { if (m_Obsolete[i].Value != null) Destroy(m_Obsolete[i].Value); m_Obsolete.RemoveAt(i); }
            if (m_Bake == null && m_Obsolete.Count == 0) enabled = false;
        }

        internal void ApplyWater(LowPolyMeshData d, Material material)
        {
            if (d == null || material == null) { if (m_Water != null) m_Water.SetActive(false); return; }
            if (m_Water == null)
            {
                m_Water = new GameObject("Agua");
                m_Water.transform.SetParent(transform, false);
                m_Water.AddComponent<MeshFilter>().sharedMesh = m_WaterMesh = new Mesh { name = "Agua " + CX + "," + CZ };
                var r = m_Water.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            m_Water.SetActive(true);
            m_WaterMesh.Clear();
            m_WaterMesh.SetVertices(d.vertices);
            m_WaterMesh.SetNormals(d.normals);
            m_WaterMesh.SetColors(d.colors);
            m_WaterMesh.SetTriangles(d.triangles, 0);
            m_WaterMesh.RecalculateBounds();
        }

        void OnDestroy()
        {
            // No borrar una malla mientras otro hilo la cocina
            if (m_Bake != null) { try { m_Bake.Wait(); } catch { } if (m_BakingMesh != null) Destroy(m_BakingMesh); }
            foreach (var kv in m_Obsolete) { try { kv.Key.Wait(); } catch { } if (kv.Value != null) Destroy(kv.Value); }
            m_Obsolete.Clear();
            if (m_Mesh != null) Destroy(m_Mesh);
            if (m_ColliderMesh != null) Destroy(m_ColliderMesh);
            if (m_WaterMesh != null) Destroy(m_WaterMesh);
        }
    }
}
