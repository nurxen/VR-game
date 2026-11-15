using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace VRKit
{
    /// <summary>
    /// Enemigo volador: dron, murciélago, hada malvada, nave pequeña, ojo flotante.
    /// No necesita NavMesh: flota a una altura, gira a tu alrededor a cierta distancia y te dispara
    /// proyectiles a la cabeza. Antes de disparar brilla (aviso) para que puedas esquivar o cubrirte con el escudo.
    /// Al morir cae con física. Necesita Health y un Collider.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class FlyingEnemy : MonoBehaviour
    {
        [Header("Vuelo")]
        [SerializeField] float preferredDistance = 6f;
        [SerializeField] float heightAboveHead = 1f;
        [SerializeField] float speed = 3f;
        [Tooltip("Grados por segundo que gira a tu alrededor (0 = no gira).")]
        [SerializeField] float orbitSpeed = 20f;
        [SerializeField] float bobAmount = 0.3f;
        [SerializeField] float detectRange = 20f;

        [Header("Disparo")]
        [SerializeField] Projectile projectile;
        [SerializeField] Transform muzzle;
        [SerializeField] float damage = 10f;
        [SerializeField] float projectileSpeed = 8f;
        [SerializeField] float fireCooldown = 2.5f;
        [Tooltip("Segundos de aviso (brillo) antes de disparar.")]
        [SerializeField] float warningTime = 0.6f;
        [SerializeField] Renderer warningRenderer;
        [SerializeField] Color warningColor = new Color(1f, 0.2f, 0.1f);

        [Header("Muerte")]
        [SerializeField] int scoreOnDeath = 15;
        [SerializeField] string objectiveId = "";
        [SerializeField] AudioClip fireSound;
        [SerializeField] AudioClip deathSound;

        public UnityEvent onFired = new UnityEvent();

        Health m_Health;
        float m_Angle, m_NextFire, m_Seed;
        bool m_Dead;
        MaterialPropertyBlock m_Block;
        Color m_BaseColor = Color.white;

        void Awake()
        {
            m_Health = GetComponent<Health>();
            m_Health.onDeath.AddListener(Die);
            if (muzzle == null) muzzle = transform;
            if (warningRenderer == null) warningRenderer = GetComponentInChildren<Renderer>();
            if (warningRenderer != null && warningRenderer.sharedMaterial != null) m_BaseColor = warningRenderer.sharedMaterial.color;
            m_Block = new MaterialPropertyBlock();
            m_Seed = Random.value * 10f;
            m_NextFire = Time.time + fireCooldown * Random.Range(0.5f, 1.2f);
        }

        void Start()
        {
            var p = VRPlayer.Instance;
            if (p != null) { Vector3 d = transform.position - p.FeetPosition; m_Angle = Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg; }
        }

        void Update()
        {
            if (m_Dead) return;
            var p = VRPlayer.Instance;
            if (p == null || p.Head == null) return;
            Vector3 head = p.Head.position;
            if (Vector3.Distance(head, transform.position) > detectRange) return;

            m_Angle += orbitSpeed * Time.deltaTime;
            float rad = m_Angle * Mathf.Deg2Rad;
            Vector3 goal = new Vector3(head.x + Mathf.Cos(rad) * preferredDistance, head.y + heightAboveHead + Mathf.Sin(Time.time * 1.3f + m_Seed) * bobAmount, head.z + Mathf.Sin(rad) * preferredDistance);
            transform.position = Vector3.MoveTowards(transform.position, goal, speed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(head - transform.position), 5f * Time.deltaTime);

            if (Time.time >= m_NextFire && projectile != null) { m_NextFire = Time.time + fireCooldown; StartCoroutine(Fire()); }
        }

        IEnumerator Fire()
        {
            for (float t = 0f; t < warningTime; t += Time.deltaTime)
            {
                Tint(Color.Lerp(m_BaseColor, warningColor, Mathf.PingPong(t * 8f, 1f)));
                yield return null;
            }
            Tint(m_BaseColor);
            if (m_Dead) yield break;
            var p = VRPlayer.Instance;
            if (p == null) yield break;
            Vector3 dir = (p.Head.position - muzzle.position).normalized;
            var proj = Instantiate(projectile, muzzle.position, Quaternion.LookRotation(dir));
            proj.Configure(damage, false); // este sí puede dar al jugador
            proj.Launch(dir * projectileSpeed, transform);
            if (fireSound != null) VRAudio.PlayAt(fireSound, muzzle.position);
            onFired.Invoke();
        }

        void Tint(Color c)
        {
            if (warningRenderer == null) return;
            warningRenderer.GetPropertyBlock(m_Block);
            m_Block.SetColor("_BaseColor", c); m_Block.SetColor("_Color", c);
            warningRenderer.SetPropertyBlock(m_Block);
        }

        void Die()
        {
            if (m_Dead) return;
            m_Dead = true;
            StopAllCoroutines();
            Tint(m_BaseColor * 0.4f);
            if (GameManager.Instance != null) GameManager.Instance.AddScore(scoreOnDeath);
            ObjectiveTracker.ReportStatic(objectiveId);
            if (scoreOnDeath > 0) FloatingText.Spawn("+" + scoreOnDeath, transform.position + Vector3.up * 0.3f, new Color(1f, 0.85f, 0.3f));
            if (deathSound != null) VRAudio.PlayAt(deathSound, transform.position);
            var rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = false; rb.useGravity = true;
            rb.angularVelocity = Random.insideUnitSphere * 6f;
            Destroy(gameObject, 3f);
        }
    }
}
