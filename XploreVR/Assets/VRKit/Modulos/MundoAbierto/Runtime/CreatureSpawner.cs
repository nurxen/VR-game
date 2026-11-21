using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace VRKit
{
    /// <summary>
    /// Hace aparecer criaturas alrededor del jugador, sobre la NavMesh y fuera de su vista cercana:
    /// unas de día (animales) y otras de noche (bichos). Sirve con cualquier prefab que tenga NavMeshAgent
    /// (WorldCreature, o el SimpleEnemy del módulo Enemigos si lo tenéis).
    /// Para un juego tranquilo: Max Night bajo, o vacío para que no salga nada.
    /// </summary>
    public class CreatureSpawner : MonoBehaviour
    {
        [SerializeField] GameObject[] dayCreatures = new GameObject[0];
        [SerializeField] GameObject[] nightCreatures = new GameObject[0];
        [SerializeField, Range(0, 20)] int maxDay = 5;
        [SerializeField, Range(0, 20)] int maxNight = 3;
        [Tooltip("Segundos entre intentos.")]
        [SerializeField] float interval = 6f;
        [Tooltip("Distancia al jugador (mínima y máxima, metros).")]
        [SerializeField] Vector2 distance = new Vector2(22f, 45f);
        [Tooltip("Solo de noche detrás del jugador (no aparecen delante de sus ojos).")]
        [SerializeField] bool behindPlayerAtNight = true;
        [Tooltip("Biomas donde no salen bichos de noche (por ejemplo, un pueblo o un refugio).")]
        [SerializeField] Biome[] safeBiomes = new Biome[0];

        readonly List<GameObject> m_Day = new List<GameObject>();
        readonly List<GameObject> m_Night = new List<GameObject>();
        float m_Next;

        void Update()
        {
            var world = OpenWorld.Instance;
            var player = VRPlayer.Instance;
            if (world == null || player == null || !world.IsReady || Time.time < m_Next) return;
            m_Next = Time.time + interval;
            if (CaveSystem.Instance != null && CaveSystem.Instance.InCave) return;
            m_Day.RemoveAll(g => g == null);
            m_Night.RemoveAll(g => g == null);

            bool night = SkyCycle.Instance != null && SkyCycle.Instance.IsNight;
            if (night && m_Night.Count < maxNight && nightCreatures.Length > 0 && !IsSafe(world.BiomeAt(player.FeetPosition)))
                Spawn(nightCreatures[Random.Range(0, nightCreatures.Length)], m_Night, player, true);
            else if (!night && m_Day.Count < maxDay && dayCreatures.Length > 0)
                Spawn(dayCreatures[Random.Range(0, dayCreatures.Length)], m_Day, player, false);
        }

        bool IsSafe(Biome b) { foreach (var s in safeBiomes) if (s == b) return true; return false; }

        void Spawn(GameObject prefab, List<GameObject> list, VRPlayer player, bool night)
        {
            if (prefab == null) return;
            var feet = player.FeetPosition;
            Vector3 fwd = player.HeadForwardFlat;
            for (int i = 0; i < 6; i++)
            {
                Vector2 c = Random.insideUnitCircle.normalized;
                var dir = new Vector3(c.x, 0f, c.y);
                if (night && behindPlayerAtNight && Vector3.Dot(dir, fwd) > -0.2f) dir = -dir;
                var p = feet + dir * Random.Range(distance.x, distance.y);
                NavMeshHit hit;
                if (!NavMesh.SamplePosition(p, out hit, 6f, NavMesh.AllAreas)) continue;
                list.Add(Instantiate(prefab, hit.position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)));
                return;
            }
        }
    }
}
