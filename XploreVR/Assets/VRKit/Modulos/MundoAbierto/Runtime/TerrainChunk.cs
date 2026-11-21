using System.Collections.Generic;
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
        /// <summary>0 = detalle completo (con colisión y vegetación), 1 = lejano.</summary>
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
        bool m_Teleport;
        static System.Type s_TeleportArea;

        /// <summary>Permite teletransportarse sobre el terreno (solo en las zonas poco empinadas, si la versión de XRI lo permite).</summary>
        static void AddTeleportArea(GameObject go)
        {
            if (s_TeleportArea == null)
                s_TeleportArea = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea, Unity.XR.Interaction.Toolkit")
                              ?? System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.TeleportationArea, Unity.XR.Interaction.Toolkit");
            if (s_TeleportArea == null) return;
            var area = go.AddComponent(s_TeleportArea);
            var f = s_TeleportArea.GetProperty("filterSelectionByHitNormal");
            if (f != null && f.CanWrite) f.SetValue(area, true);
            var tol = s_TeleportArea.GetProperty("upNormalToleranceDegrees");
            if (tol != null && tol.CanWrite) tol.SetValue(area, 35f);
            // Por código, XRI no le pone la capa «Teleport» (desde el editor, sí)
            var maskType = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.InteractionLayerMask, Unity.XR.Interaction.Toolkit");
            var layers = s_TeleportArea.GetProperty("interactionLayers");
            var getMask = maskType != null ? maskType.GetMethod("GetMask", new[] { typeof(string[]) }) : null;
            if (layers != null && layers.CanWrite && getMask != null)
            {
                var mask = getMask.Invoke(null, new object[] { new[] { "Teleport" } });
                var value = maskType.GetProperty("value");
                if (value != null && System.Convert.ToInt32(value.GetValue(mask)) != 0) layers.SetValue(area, mask);
            }
        }

        internal void Setup(int cx, int cz, long key, Material material)
        {
            CX = cx; CZ = cz; Key = key;
            m_Filter = gameObject.AddComponent<MeshFilter>();
            m_Renderer = gameObject.AddComponent<MeshRenderer>();
            m_Renderer.sharedMaterial = material;
            m_Renderer.shadowCastingMode = ShadowCastingMode.Off;
            m_Mesh = new Mesh { name = "Terreno " + cx + "," + cz };
            m_Mesh.MarkDynamic();
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
                if (m_ColliderMesh == null) m_ColliderMesh = new Mesh { name = "Terreno (colisión) " + CX + "," + CZ };
                m_ColliderMesh.Clear();
                m_ColliderMesh.indexFormat = m_Mesh.indexFormat;
                m_ColliderMesh.SetVertices(d.vertices);
                m_ColliderMesh.SetTriangles(d.colliderTriangles, 0);
                m_Collider.sharedMesh = null;
                m_Collider.sharedMesh = m_ColliderMesh;
                m_Collider.enabled = true;
                if (teleport && !m_Teleport) { AddTeleportArea(gameObject); m_Teleport = true; }
            }
            else if (m_Collider != null) m_Collider.enabled = false;
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
            if (m_Mesh != null) Destroy(m_Mesh);
            if (m_ColliderMesh != null) Destroy(m_ColliderMesh);
            if (m_WaterMesh != null) Destroy(m_WaterMesh);
        }
    }
}
