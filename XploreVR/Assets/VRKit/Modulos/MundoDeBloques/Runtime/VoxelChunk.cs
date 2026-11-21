using UnityEngine;
using UnityEngine.Rendering;

namespace VRKit
{
    /// <summary>
    /// Una columna de 16 × 16 bloques del mundo, ya convertida en malla. La crea y la actualiza el VoxelWorld:
    /// no hace falta ponerla a mano. Sirve también para saber si un rayo ha dado contra el mundo.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class VoxelChunk : MonoBehaviour
    {
        public int CX { get; private set; }
        public int CZ { get; private set; }

        MeshFilter m_Filter;
        MeshRenderer m_Renderer;
        MeshCollider m_Collider;
        Mesh m_Mesh, m_ColliderMesh;

        internal void Setup(int cx, int cz, Material material, bool collider)
        {
            CX = cx; CZ = cz;
            m_Filter = GetComponent<MeshFilter>();
            m_Renderer = GetComponent<MeshRenderer>();
            m_Renderer.sharedMaterial = material;
            m_Renderer.shadowCastingMode = ShadowCastingMode.Off;
            m_Renderer.receiveShadows = false;
            m_Mesh = new Mesh { name = "Chunk " + cx + "," + cz };
            m_Mesh.MarkDynamic();
            m_Filter.sharedMesh = m_Mesh;
            if (collider)
            {
                m_ColliderMesh = new Mesh { name = "Chunk (colisión) " + cx + "," + cz };
                m_Collider = gameObject.AddComponent<MeshCollider>();
            }
        }

        internal void Apply(VoxelMeshData d)
        {
            var format = d.vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            m_Mesh.Clear();
            m_Mesh.indexFormat = format;
            m_Mesh.SetVertices(d.vertices);
            m_Mesh.SetUVs(0, d.uvs);
            m_Mesh.SetColors(d.colors);
            m_Mesh.SetTriangles(d.triangles, 0);
            m_Mesh.RecalculateNormals();
            m_Mesh.RecalculateBounds();
            m_Renderer.enabled = d.triangles.Count > 0;

            if (m_Collider != null)
            {
                bool any = d.colliderTriangles.Count > 0;
                m_ColliderMesh.Clear();
                m_ColliderMesh.indexFormat = format;
                if (any)
                {
                    m_ColliderMesh.SetVertices(d.vertices);
                    m_ColliderMesh.SetTriangles(d.colliderTriangles, 0);
                }
                m_Collider.sharedMesh = null;           // obliga a Unity a recalcular la colisión
                if (any) m_Collider.sharedMesh = m_ColliderMesh;
                m_Collider.enabled = any;
            }
        }

        void OnDestroy()
        {
            if (m_Mesh != null) Destroy(m_Mesh);
            if (m_ColliderMesh != null) Destroy(m_ColliderMesh);
        }
    }
}
