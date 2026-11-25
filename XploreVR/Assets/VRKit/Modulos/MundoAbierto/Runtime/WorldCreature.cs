using UnityEngine;
using UnityEngine.AI;

namespace VRKit
{
    public enum CreatureMood { Pacifica, Hostil }

    /// <summary>
    /// Una criatura del mundo abierto que anda con NavMeshAgent (sobre la NavMesh de WorldNavMesh).
    ///   · Pacífica: pasea por su zona y se aparta si te acercas mucho.
    ///   · Hostil: si te ve, te sigue y te da un toque; al amanecer se desvanece.
    /// Para juegos tranquilos: pocas, lentas y que hagan poco daño (o ninguno: Damage = 0 y solo asustan).
    /// Con Health (Básico) se le puede pegar; sin él, es solo un bicho que molesta.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class WorldCreature : MonoBehaviour
    {
        [SerializeField] CreatureMood mood = CreatureMood.Hostil;
        [SerializeField] float wanderRadius = 12f;
        [SerializeField] float detectRange = 14f;
        [Tooltip("Pacíficas: distancia a la que se apartan.")]
        [SerializeField] float shyDistance = 4f;
        [SerializeField] float attackRange = 1.6f;
        [SerializeField] float damage = 8f;
        [SerializeField] float attackCooldown = 1.5f;
        [Tooltip("Hostiles: desaparecen al amanecer.")]
        [SerializeField] bool vanishAtDawn = true;
        [Tooltip("Desaparece si se aleja tanto del jugador (metros).")]
        [SerializeField] float despawnDistance = 90f;
        [SerializeField] AudioClip idleSound;

        NavMeshAgent m_Agent;
        Health m_Health;
        Vector3 m_Home;
        float m_NextThink, m_NextAttack, m_NextSound, m_VanishAt = -1f;

        public CreatureMood Mood { get { return mood; } }

        void Awake()
        {
            m_Agent = GetComponent<NavMeshAgent>();
            m_Health = GetComponent<Health>();
        }

        void OnEnable() { if (m_Health != null) m_Health.onDeath.AddListener(Die); }
        void OnDisable() { if (m_Health != null) m_Health.onDeath.RemoveListener(Die); }

        void Start()
        {
            m_Home = transform.position;
            m_NextSound = Time.time + Random.Range(5f, 15f);
        }

        void Update()
        {
            var player = VRPlayer.Instance;
            if (player == null || !m_Agent.isOnNavMesh) return;
            var feet = player.FeetPosition;
            float d = Vector3.Distance(transform.position, feet);
            if (d > despawnDistance) { Destroy(gameObject); return; }
            bool playerInCave = CaveSystem.Instance != null && CaveSystem.Instance.InCave;

            if (mood == CreatureMood.Hostil)
            {
                var sky = SkyCycle.Instance;
                if (vanishAtDawn && sky != null && !sky.IsNight)
                {
                    if (m_VanishAt < 0f) m_VanishAt = Time.time + Random.Range(1f, 12f);
                    else if (Time.time > m_VanishAt) { Vanish(); return; }
                }
                if (!playerInCave && d < detectRange)
                {
                    m_Agent.SetDestination(feet);
                    if (d < attackRange && Time.time >= m_NextAttack)
                    {
                        m_NextAttack = Time.time + attackCooldown;
                        if (damage > 0f && PlayerHealth.Instance != null)
                            PlayerHealth.Instance.TakeDamage(new DamageInfo(damage, transform.position, (feet - transform.position).normalized, gameObject));
                    }
                    return;
                }
            }
            else if (!playerInCave && d < shyDistance)
            {
                var away = transform.position + (transform.position - feet).normalized * 6f;
                NavMeshHit hit;
                if (NavMesh.SamplePosition(away, out hit, 4f, NavMesh.AllAreas)) m_Agent.SetDestination(hit.position);
                return;
            }

            if (Time.time >= m_NextThink)
            {
                m_NextThink = Time.time + Random.Range(3f, 8f);
                var target = m_Home + Random.insideUnitSphere * wanderRadius;
                NavMeshHit hit;
                if (NavMesh.SamplePosition(target, out hit, 5f, NavMesh.AllAreas)) m_Agent.SetDestination(hit.position);
            }
            if (idleSound != null && Time.time >= m_NextSound) { VRAudio.PlayAt(idleSound, transform.position, 0.6f); m_NextSound = Time.time + Random.Range(8f, 20f); }
        }

        public void Vanish()
        {
            Effects.Burst(transform.position + Vector3.up * 0.6f, new Color(0.7f, 0.7f, 0.85f), 0.8f, 0.4f);
            Destroy(gameObject);
        }

        void Die() { Vanish(); }
    }
}
