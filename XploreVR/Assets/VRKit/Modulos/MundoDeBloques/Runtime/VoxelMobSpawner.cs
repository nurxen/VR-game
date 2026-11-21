using System.Collections.Generic;
using UnityEngine;

namespace VRKit
{
    /// <summary>
    /// Hace aparecer criaturas alrededor del jugador, fuera de su vista cercana: pacíficas de día, hostiles de noche.
    /// Sin VoxelDayNight, las hostiles salen siempre. En modo creativo no salen hostiles (salvo que lo marquéis).
    /// </summary>
    public class VoxelMobSpawner : MonoBehaviour
    {
        [SerializeField] VoxelMob[] passivePrefabs = new VoxelMob[0];
        [SerializeField] VoxelMob[] hostilePrefabs = new VoxelMob[0];
        [SerializeField, Range(0, 30)] int maxPassive = 6;
        [SerializeField, Range(0, 30)] int maxHostile = 5;
        [Tooltip("Segundos entre intentos.")]
        [SerializeField] float interval = 3f;
        [Tooltip("Distancia al jugador, en bloques (mínima y máxima).")]
        [SerializeField] Vector2 distance = new Vector2(14f, 28f);
        [SerializeField] bool hostilesInCreative = false;

        readonly List<VoxelMob> m_Alive = new List<VoxelMob>();
        float m_Next;

        void Update()
        {
            var world = VoxelWorld.Instance;
            if (world == null || !world.IsReady || Time.time < m_Next) return;
            m_Next = Time.time + interval;
            m_Alive.RemoveAll(m => m == null);

            int passive = 0, hostile = 0;
            foreach (var m in m_Alive) if (m.Kind == VoxelMobKind.Hostil) hostile++; else passive++;

            var dn = VoxelDayNight.Instance;
            bool night = dn == null || dn.IsNight;
            var game = VoxelGame.Instance;
            bool creative = game != null && game.IsCreative;

            if (night && (!creative || hostilesInCreative) && hostile < maxHostile && hostilePrefabs.Length > 0)
                TrySpawn(world, hostilePrefabs[Random.Range(0, hostilePrefabs.Length)]);
            else if (!night && passive < maxPassive && passivePrefabs.Length > 0)
                TrySpawn(world, passivePrefabs[Random.Range(0, passivePrefabs.Length)]);
        }

        void TrySpawn(VoxelWorld world, VoxelMob prefab)
        {
            var player = VRPlayer.Instance;
            if (prefab == null || player == null) return;
            Vector3 p = world.ToLocal(player.FeetPosition);
            for (int attempt = 0; attempt < 6; attempt++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f), d = Random.Range(distance.x, distance.y);
                int x = Mathf.FloorToInt(p.x + Mathf.Cos(a) * d), z = Mathf.FloorToInt(p.z + Mathf.Sin(a) * d);
                int y = world.SurfaceHeight(x, z);
                if (y < 0 || y >= world.Height - 3) continue;
                var ground = new Vector3Int(x, y, z);
                if (!world.InBounds(ground)) continue;
                var info = world.Registry.MeshInfo[world.GetBlock(ground)];
                if (!info.opaque) continue;                                  // ni encima de hojas ni de cristal
                if (world.IsSolid(x, y + 1, z) || world.IsSolid(x, y + 2, z)) continue;

                var mob = Instantiate(prefab, world.transform);
                mob.transform.localPosition = new Vector3(x + 0.5f, y + 1f, z + 0.5f);
                mob.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                mob.transform.localScale = Vector3.one;
                m_Alive.Add(mob);
                return;
            }
        }
    }
}
