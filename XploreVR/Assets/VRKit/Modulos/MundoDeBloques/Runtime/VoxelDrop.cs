using UnityEngine;

namespace VRKit
{
    /// <summary>
    /// Un bloquecito que cae al romper un bloque: gira, flota y, cuando te acercas, vuela hacia ti y entra en el inventario.
    /// No usa física (es más barato): cae hasta el bloque sólido de debajo.
    /// </summary>
    public class VoxelDrop : MonoBehaviour
    {
        /// <summary>Distancia (en metros) a la que viene hacia ti.</summary>
        public static float PickupRadius = 1.6f;
        /// <summary>Segundos antes de desaparecer si nadie lo coge.</summary>
        public static float Lifetime = 300f;
        public static AudioClip PickupSound;

        byte m_Id;
        int m_Count;
        float m_Born, m_VelY;
        bool m_Flying;
        Vector3 m_Spin;

        public static VoxelDrop Spawn(VoxelWorld world, byte id, int count, Vector3 position)
        {
            if (world == null || id == 0 || count <= 0) return null;
            var go = new GameObject("Bloque suelto");
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            go.transform.localScale = Vector3.one * (world.BlockSize * 0.25f);
            go.AddComponent<MeshFilter>().sharedMesh = world.GetItemMesh(id);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = world.Material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var d = go.AddComponent<VoxelDrop>();
            d.m_Id = id;
            d.m_Count = count;
            d.m_Born = Time.time;
            d.m_VelY = 2f * world.BlockSize;
            d.m_Spin = new Vector3(0f, 90f, 0f);
            return d;
        }

        void Update()
        {
            var world = VoxelWorld.Instance;
            var player = VRPlayer.Instance;
            if (world == null) { Destroy(gameObject); return; }
            float bs = world.BlockSize;

            if (Time.time - m_Born > Lifetime) { Destroy(gameObject); return; }
            transform.Rotate(m_Spin * Time.deltaTime, Space.World);

            // ¿Viene hacia el jugador?
            if (player != null && player.Head != null && Time.time - m_Born > 0.4f)
            {
                Vector3 chest = player.Head.position - Vector3.up * 0.45f;
                float d = Vector3.Distance(transform.position, chest);
                if (m_Flying || d < PickupRadius)
                {
                    m_Flying = true;
                    transform.position = Vector3.MoveTowards(transform.position, chest, Time.deltaTime * Mathf.Max(3f, d * 8f));
                    if (d < 0.25f)
                    {
                        var inv = BlockInventory.Instance;
                        if (inv != null) inv.Add(m_Id, m_Count);
                        if (PickupSound != null) VRAudio.Play2D(PickupSound, 0.4f);
                        Destroy(gameObject);
                    }
                    return;
                }
            }

            // Caer hasta el suelo de bloques
            m_VelY -= 20f * bs * Time.deltaTime;
            Vector3 p = transform.position + Vector3.up * (m_VelY * Time.deltaTime);
            var below = world.WorldToBlock(p - Vector3.up * (0.13f * bs));
            if (m_VelY < 0f && world.IsSolid(below))
            {
                var top = world.ToWorld(new Vector3(below.x + 0.5f, below.y + 1f, below.z + 0.5f));
                p.y = Mathf.Max(p.y, top.y + 0.13f * bs + Mathf.Sin(Time.time * 3f) * 0.03f * bs);
                m_VelY = 0f;
            }
            transform.position = p;
        }
    }

    /// <summary>Efectos del mundo de bloques: trocitos del bloque que salen volando al romperlo.</summary>
    public static class VoxelFx
    {
        public static int DebrisPieces = 6;

        public static void Debris(VoxelWorld world, byte id, Vector3 center)
        {
            if (world == null || id == 0 || DebrisPieces <= 0) return;
            var mesh = world.GetItemMesh(id);
            float bs = world.BlockSize;
            for (int i = 0; i < DebrisPieces; i++)
            {
                var go = new GameObject("Trozo");
                go.transform.position = center + Random.insideUnitSphere * 0.3f * bs;
                go.transform.rotation = Random.rotation;
                go.transform.localScale = Vector3.one * (bs * Random.Range(0.08f, 0.14f));
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = world.Material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var p = go.AddComponent<VoxelDebrisPiece>();
                p.velocity = (Random.insideUnitSphere + Vector3.up * 1.2f) * (2.2f * bs);
                p.gravity = 14f * bs;
            }
        }
    }
}
