using UnityEngine;
using UnityEngine.Events;

namespace VRKit
{
    /// <summary>
    /// Proyectil genérico: bala, bola de fuego, dardo, burbuja mágica...
    /// Lo dispara ProjectileLauncher. Detecta impactos con rayos (no atraviesa paredes aunque vaya rápido).
    /// Componentes: Rigidbody (sin gravedad si quieres que vaya recto) + este script.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : MonoBehaviour
    {
        [SerializeField] float damage = 20f;
        [SerializeField] float lifetime = 5f;
        [SerializeField] LayerMask hitLayers = ~0;
        [SerializeField] bool ignorePlayer = true;
        [Tooltip("Radio de explosión (0 = solo daña lo que toca).")]
        [SerializeField] float explosionRadius = 0f;
        [SerializeField] float impactForce = 2f;
        [SerializeField] GameObject impactEffectPrefab;
        [SerializeField] AudioClip impactSound;
        [Tooltip("Si no ignora al jugador: distancia a la cabeza que cuenta como impacto (el jugador VR no tiene collider en la cabeza).")]
        [SerializeField] float headHitRadius = 0.25f;

        public UnityEvent<GameObject> onHit = new UnityEvent<GameObject>();

        Rigidbody m_Rigidbody;
        Transform m_Shooter;
        Vector3 m_LastPos;
        bool m_Done;

        void Awake()
        {
            m_Rigidbody = GetComponent<Rigidbody>();
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        }

        /// <summary>
        /// Cosas que pueden parar un proyectil justo antes de la cabeza del jugador: (punto, dirección) → true si lo para.
        /// Lo usa el escudo (módulo Cuerpo a cuerpo).
        /// </summary>
        public static readonly System.Collections.Generic.List<System.Func<Vector3, Vector3, bool>> HeadHitBlockers =
            new System.Collections.Generic.List<System.Func<Vector3, Vector3, bool>>();

        public float DamageAmount { get { return damage; } set { damage = Mathf.Max(0f, value); } }

        /// <summary>Ajustes desde código (hechizos cargados, enemigos que disparan al jugador).</summary>
        public void Configure(float newDamage, bool newIgnorePlayer)
        {
            damage = newDamage;
            ignorePlayer = newIgnorePlayer;
        }

        public void Launch(Vector3 velocity, Transform shooter)
        {
            m_Shooter = shooter;
            m_Rigidbody.isKinematic = false;
            m_Rigidbody.linearVelocity = velocity;
            m_LastPos = transform.position;
            if (velocity.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(velocity);
            Destroy(gameObject, lifetime);
        }

        void FixedUpdate()
        {
            if (m_Done) return;
            Vector3 pos = transform.position;
            Vector3 seg = pos - m_LastPos;
            if (!ignorePlayer && PlayerHealth.Instance != null && seg.sqrMagnitude > 0f)
            {
                // ¿Ha pasado cerca de la cabeza del jugador?
                Vector3 head = PlayerHealth.Instance.HeadPosition;
                float t = Mathf.Clamp01(Vector3.Dot(head - m_LastPos, seg) / seg.sqrMagnitude);
                Vector3 closest = m_LastPos + seg * t;
                if (Vector3.Distance(closest, head) < headHitRadius && !Blocked(closest, seg.normalized))
                {
                    m_Done = true;
                    PlayerHealth.Instance.TakeDamage(new DamageInfo(damage, closest, seg.normalized, gameObject));
                    if (impactSound != null) VRAudio.PlayAt(impactSound, closest);
                    onHit.Invoke(PlayerHealth.Instance.gameObject);
                    Destroy(gameObject);
                    return;
                }
            }
            if (seg.sqrMagnitude > 0f)
            {
                var hits = Physics.RaycastAll(m_LastPos, seg.normalized, seg.magnitude, hitLayers, QueryTriggerInteraction.Ignore);
                float best = float.MaxValue;
                RaycastHit chosen = default(RaycastHit);
                bool found = false;
                foreach (var h in hits)
                {
                    if (Ignore(h.collider)) continue;
                    if (h.distance < best) { best = h.distance; chosen = h; found = true; }
                }
                if (found) { Hit(chosen.point, chosen.normal, chosen.collider); return; }
            }
            m_LastPos = pos;
        }

        static bool Blocked(Vector3 point, Vector3 dir)
        {
            foreach (var b in HeadHitBlockers) if (b(point, dir)) return true;
            return false;
        }

        bool Ignore(Collider c)
        {
            if (c.transform.IsChildOf(transform)) return true;
            if (m_Shooter != null && c.transform.IsChildOf(m_Shooter)) return true;
            if (ignorePlayer && PlayerHealth.Instance != null && c.transform.IsChildOf(PlayerHealth.Instance.transform)) return true;
            return false;
        }

        void Hit(Vector3 point, Vector3 normal, Collider hitCollider)
        {
            m_Done = true;
            Vector3 dir = m_Rigidbody.linearVelocity.normalized;

            if (explosionRadius > 0f)
            {
                foreach (var c in Physics.OverlapSphere(point, explosionRadius, hitLayers, QueryTriggerInteraction.Ignore))
                {
                    if (Ignore(c)) continue;
                    float falloff = 1f - Mathf.Clamp01(Vector3.Distance(point, c.ClosestPoint(point)) / explosionRadius);
                    Damage.TryApply(c, new DamageInfo(damage * falloff, point, (c.transform.position - point).normalized, gameObject));
                    if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic)
                        c.attachedRigidbody.AddExplosionForce(impactForce * 5f, point, explosionRadius, 0.3f, ForceMode.Impulse);
                }
            }
            else
            {
                Damage.TryApply(hitCollider, new DamageInfo(damage, point, dir, gameObject));
                if (hitCollider.attachedRigidbody != null && !hitCollider.attachedRigidbody.isKinematic)
                    hitCollider.attachedRigidbody.AddForceAtPosition(dir * impactForce, point, ForceMode.Impulse);
            }

            if (impactEffectPrefab != null) Instantiate(impactEffectPrefab, point, Quaternion.LookRotation(normal));
            if (impactSound != null) AudioSource.PlayClipAtPoint(impactSound, point);
            onHit.Invoke(hitCollider.gameObject);
            Destroy(gameObject);
        }
    }
}
