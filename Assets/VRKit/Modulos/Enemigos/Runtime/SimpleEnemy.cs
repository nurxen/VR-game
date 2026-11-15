using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace VRKit
{
    /// <summary>
    /// Enemigo básico: está quieto (o patrulla) hasta que te ve, te persigue y te ataca cuerpo a cuerpo.
    /// Sirve de base para zombis, monstruos, duendes traviesos, titanes pequeños...
    ///
    /// Necesita: NavMeshAgent + Health + Collider. El suelo debe tener un NavMesh horneado
    /// (componente NavMeshSurface del paquete AI Navigation).
    /// Animator opcional con parámetros: Speed (float), Attack, Hit, Die (triggers).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Health))]
    public class SimpleEnemy : MonoBehaviour
    {
        public enum State { Idle, Patrol, Chase, Attack, Dead }

        [Header("Detección")]
        [SerializeField] float detectRange = 12f;
        [Tooltip("Distancia a la que deja de perseguirte.")]
        [SerializeField] float loseRange = 20f;
        [Tooltip("Necesita verte (sin paredes en medio) para detectarte.")]
        [SerializeField] bool requireLineOfSight = true;
        [SerializeField] LayerMask sightBlockers = ~0;

        [Header("Ataque")]
        [SerializeField] float attackRange = 1.6f;
        [SerializeField] float attackDamage = 10f;
        [SerializeField] float attackCooldown = 1.5f;
        [Tooltip("Retraso entre empezar la animación y que el golpe haga daño.")]
        [SerializeField] float attackHitDelay = 0.4f;

        [Header("Patrulla (opcional)")]
        [SerializeField] Transform[] patrolPoints = new Transform[0];

        [Header("Muerte")]
        [SerializeField] int scoreOnDeath = 10;
        [Tooltip("Objetivo que avanza al morir (ObjectiveTracker). Vacío = ninguno.")]
        [SerializeField] string objectiveId = "";
        [SerializeField] float destroyDelay = 3f;

        [Header("Presentación")]
        [SerializeField] Animator animator;
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip alertSound;
        [SerializeField] AudioClip attackSound;
        [SerializeField] AudioClip hurtSound;
        [SerializeField] AudioClip deathSound;

        public UnityEvent<State> onStateChanged = new UnityEvent<State>();

        NavMeshAgent m_Agent;
        Health m_Health;
        float m_NextAttackTime;
        int m_PatrolIndex;
        readonly HashSet<string> m_AnimParams = new HashSet<string>();

        public State Current { get; private set; }

        void Awake()
        {
            m_Agent = GetComponent<NavMeshAgent>();
            m_Health = GetComponent<Health>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null)
                foreach (var p in animator.parameters) m_AnimParams.Add(p.name);

            m_Health.onDamaged.AddListener(OnHurt);
            m_Health.onDeath.AddListener(OnDeath);
            SetState(patrolPoints.Length > 0 ? State.Patrol : State.Idle);
        }

        void Update()
        {
            if (Current == State.Dead) return;
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) { Stop(); return; }

            var player = PlayerHealth.Instance;
            if (player == null || player.IsDead) { Patrol(); return; }

            Vector3 target = player.FeetPosition;
            float dist = Vector3.Distance(Flat(transform.position), Flat(target));

            switch (Current)
            {
                case State.Idle:
                case State.Patrol:
                    if (dist <= detectRange && CanSee(player)) { Alert(); SetState(State.Chase); }
                    else Patrol();
                    break;

                case State.Chase:
                    if (dist > loseRange) { SetState(patrolPoints.Length > 0 ? State.Patrol : State.Idle); break; }
                    if (dist <= attackRange) { SetState(State.Attack); break; }
                    MoveTo(target);
                    break;

                case State.Attack:
                    Stop();
                    FaceTowards(target);
                    if (dist > attackRange * 1.2f) { SetState(State.Chase); break; }
                    if (Time.time >= m_NextAttackTime)
                    {
                        m_NextAttackTime = Time.time + attackCooldown;
                        Trigger("Attack");
                        if (audioSource != null && attackSound != null) audioSource.PlayOneShot(attackSound);
                        Invoke(nameof(DealAttackDamage), attackHitDelay);
                    }
                    break;
            }

            if (animator != null && m_AnimParams.Contains("Speed"))
                animator.SetFloat("Speed", m_Agent.enabled ? m_Agent.velocity.magnitude : 0f);
        }

        void DealAttackDamage()
        {
            var player = PlayerHealth.Instance;
            if (Current == State.Dead || player == null) return;
            if (Vector3.Distance(Flat(transform.position), Flat(player.FeetPosition)) <= attackRange * 1.3f)
                player.TakeDamage(new DamageInfo(attackDamage, player.HeadPosition, transform.forward, gameObject));
        }

        void Patrol()
        {
            if (patrolPoints.Length == 0) { Stop(); return; }
            var p = patrolPoints[m_PatrolIndex];
            if (p == null) return;
            MoveTo(p.position);
            if (!m_Agent.pathPending && m_Agent.remainingDistance < 0.5f)
                m_PatrolIndex = (m_PatrolIndex + 1) % patrolPoints.Length;
        }

        bool CanSee(PlayerHealth player)
        {
            if (!requireLineOfSight) return true;
            Vector3 eye = transform.position + Vector3.up * 1.5f;
            Vector3 head = player.HeadPosition;
            RaycastHit hit;
            if (Physics.Linecast(eye, head, out hit, sightBlockers, QueryTriggerInteraction.Ignore))
                return hit.collider.transform.IsChildOf(player.transform) || hit.collider.transform.IsChildOf(transform);
            return true;
        }

        void MoveTo(Vector3 pos)
        {
            if (!m_Agent.enabled || !m_Agent.isOnNavMesh) return;
            NavMeshHit nh;
            if (NavMesh.SamplePosition(pos, out nh, 2f, NavMesh.AllAreas)) pos = nh.position;
            m_Agent.isStopped = false;
            m_Agent.SetDestination(pos);
        }

        void Stop()
        {
            if (m_Agent.enabled && m_Agent.isOnNavMesh) m_Agent.isStopped = true;
        }

        void FaceTowards(Vector3 target)
        {
            Vector3 dir = Flat(target) - Flat(transform.position);
            if (dir.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 8f);
        }

        void Alert()
        {
            if (audioSource != null && alertSound != null) audioSource.PlayOneShot(alertSound);
        }

        void OnHurt(float amount)
        {
            if (Current == State.Dead) return;
            Trigger("Hit");
            if (audioSource != null && hurtSound != null) audioSource.PlayOneShot(hurtSound);
            if (Current == State.Idle || Current == State.Patrol) SetState(State.Chase); // si le disparas, te busca
        }

        void OnDeath()
        {
            SetState(State.Dead);
            CancelInvoke();
            Stop();
            m_Agent.enabled = false;
            Trigger("Die");
            if (audioSource != null && deathSound != null) audioSource.PlayOneShot(deathSound);
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            if (GameManager.Instance != null) GameManager.Instance.AddScore(scoreOnDeath);
            ObjectiveTracker.ReportStatic(objectiveId);
            if (scoreOnDeath > 0) FloatingText.Spawn("+" + scoreOnDeath, transform.position + Vector3.up * 2.2f, new Color(1f, 0.85f, 0.3f));
            Destroy(gameObject, destroyDelay);
        }

        void SetState(State s)
        {
            if (Current == s) return;
            Current = s;
            onStateChanged.Invoke(s);
        }

        void Trigger(string param)
        {
            if (animator != null && m_AnimParams.Contains(param)) animator.SetTrigger(param);
        }

        static Vector3 Flat(Vector3 v) { return new Vector3(v.x, 0f, v.z); }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, detectRange);
            Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
