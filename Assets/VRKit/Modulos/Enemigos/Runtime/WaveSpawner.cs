using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace VRKit
{
    /// <summary>
    /// Oleadas de enemigos (modo horda, defensa de torre, arena). Cada oleada es una lista de «qué y cuántos».
    /// Cuando no queda ninguno vivo, pasa a la siguiente tras un descanso. Al terminar todas, victoria (opcional).
    /// Los enemigos aparecen en los Spawn Points o, si no hay, en un círculo alrededor del jugador
    /// (sobre el NavMesh si existe). Funciona con cualquier prefab que tenga Health.
    /// </summary>
    public class WaveSpawner : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            public GameObject prefab;
            [Min(1)] public int count = 3;
        }

        [Serializable]
        public class Wave
        {
            public string name = "Oleada";
            public Entry[] enemies = new Entry[0];
            [Tooltip("Segundos entre un enemigo y el siguiente.")]
            public float spawnInterval = 1f;
        }

        [SerializeField] Wave[] waves = new Wave[0];
        [SerializeField] Transform[] spawnPoints = new Transform[0];
        [Tooltip("Sin Spawn Points: distancia al jugador a la que aparecen.")]
        [SerializeField] float spawnRadius = 10f;
        [SerializeField] float timeBetweenWaves = 5f;
        [SerializeField] bool startOnPlay = false;
        [Tooltip("Al terminar la última oleada, empezar otra vez con más enemigos (modo infinito).")]
        [SerializeField] bool endless = false;
        [SerializeField] float endlessMultiplier = 1.3f;
        [SerializeField] bool victoryWhenDone = true;
        [Tooltip("Texto opcional: «Oleada 2/5 · Quedan 4».")]
        [SerializeField] TMP_Text statusLabel;
        [SerializeField] AudioClip waveStartSound;

        public UnityEvent<int> onWaveStarted = new UnityEvent<int>();
        public UnityEvent<int> onWaveCleared = new UnityEvent<int>();
        public UnityEvent onAllWavesCleared = new UnityEvent();

        public int CurrentWave { get; private set; }
        public int Alive { get { return m_Alive.Count; } }
        public bool Running { get; private set; }

        readonly List<Health> m_Alive = new List<Health>();
        int m_Loop;

        void Start() { if (startOnPlay) Begin(); UpdateLabel(); }

        /// <summary>Configura las oleadas por código (lo usa el montador del editor).</summary>
        public void SetWaves(Wave[] newWaves) { waves = newWaves; }

        public void Begin()
        {
            if (Running || waves.Length == 0) return;
            Running = true;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            do
            {
                for (int w = 0; w < waves.Length; w++)
                {
                    CurrentWave = w + 1 + m_Loop * waves.Length;
                    FloatingText.SpawnInFront(waves[w].name + " " + CurrentWave, new Color(1f, 0.6f, 0.4f), 1.2f);
                    VRAudio.Play2D(waveStartSound);
                    onWaveStarted.Invoke(CurrentWave);
                    float mult = Mathf.Pow(endlessMultiplier, m_Loop);
                    foreach (var e in waves[w].enemies)
                    {
                        int n = Mathf.CeilToInt(e.count * mult);
                        for (int i = 0; i < n; i++)
                        {
                            Spawn(e.prefab);
                            yield return new WaitForSeconds(waves[w].spawnInterval);
                        }
                    }
                    while (m_Alive.Count > 0) { m_Alive.RemoveAll(h => h == null || h.IsDead); UpdateLabel(); yield return new WaitForSeconds(0.25f); }
                    onWaveCleared.Invoke(CurrentWave);
                    UpdateLabel();
                    if (w < waves.Length - 1 || endless) yield return new WaitForSeconds(timeBetweenWaves);
                }
                m_Loop++;
            } while (endless);

            Running = false;
            onAllWavesCleared.Invoke();
            if (victoryWhenDone && GameManager.Instance != null) GameManager.Instance.Victory();
        }

        void Spawn(GameObject prefab)
        {
            if (prefab == null) return;
            Vector3 pos;
            Quaternion rot = Quaternion.identity;
            if (spawnPoints.Length > 0)
            {
                var sp = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
                pos = sp.position; rot = sp.rotation;
            }
            else
            {
                Vector3 center = VRPlayer.Instance != null ? VRPlayer.Instance.FeetPosition : transform.position;
                Vector2 c = UnityEngine.Random.insideUnitCircle.normalized * spawnRadius;
                pos = center + new Vector3(c.x, 0f, c.y);
                rot = Quaternion.LookRotation(center - pos);
            }
            NavMeshHit hit;
            if (NavMesh.SamplePosition(pos, out hit, 3f, NavMesh.AllAreas)) pos = hit.position;
            var go = Instantiate(prefab, pos, rot);
            var h = go.GetComponentInChildren<Health>();
            if (h != null) m_Alive.Add(h);
            Effects.Burst(pos + Vector3.up, new Color(0.6f, 0.3f, 1f), 1.5f, 0.3f);
            UpdateLabel();
        }

        void UpdateLabel()
        {
            if (statusLabel == null) return;
            statusLabel.text = Running ? "Oleada " + CurrentWave + (endless ? "" : "/" + waves.Length) + " · Quedan " + m_Alive.Count : "";
        }
    }
}
