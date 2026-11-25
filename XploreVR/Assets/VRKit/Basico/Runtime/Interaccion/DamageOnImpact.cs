using UnityEngine;
using UnityEngine.Events;

namespace VRKit
{
    /// <summary>
    /// Hace daño a lo que golpea según la velocidad del choque.
    /// Una piedra lanzada a un enemigo, una espada, un martillo, un coche...
    /// El objetivo necesita un componente que implemente IDamageable (Health, Target...).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class DamageOnImpact : MonoBehaviour
    {
        [Tooltip("Velocidad mínima del choque (m/s) para hacer daño.")]
        [SerializeField] float minSpeed = 2f;
        [Tooltip("Daño por cada m/s de velocidad.")]
        [SerializeField] float damagePerSpeed = 5f;
        [SerializeField] float maxDamage = 100f;
        [Tooltip("Si tiene Throwable, solo hace daño mientras se sujeta o va lanzado, no cuando está tirado en el suelo y algo lo empuja.")]
        [SerializeField] bool ignoreWhenResting = true;
        [Tooltip("Tiempo mínimo entre dos golpes al mismo objetivo.")]
        [SerializeField] float hitCooldown = 0.3f;
        [SerializeField] bool destroyOnHit = false;
        [SerializeField] GameObject hitEffectPrefab;

        public UnityEvent<float> onHit = new UnityEvent<float>();

        Throwable m_Throwable;
        float m_LastHitTime = -999f;

        void Awake()
        {
            m_Throwable = GetComponent<Throwable>();
        }

        void OnCollisionEnter(Collision collision)
        {
            float speed = collision.relativeVelocity.magnitude;
            if (speed < minSpeed) return;
            if (Time.time - m_LastHitTime < hitCooldown) return;
            if (ignoreWhenResting && m_Throwable != null && !m_Throwable.IsThrown && !m_Throwable.IsHeld) return;

            var contact = collision.GetContact(0);
            float dmg = Mathf.Min(maxDamage, speed * damagePerSpeed);
            var info = new DamageInfo(dmg, contact.point, collision.relativeVelocity.normalized * -1f, gameObject);

            if (Damage.TryApply(collision.collider, info))
            {
                m_LastHitTime = Time.time;
                if (hitEffectPrefab != null)
                    Instantiate(hitEffectPrefab, contact.point, Quaternion.LookRotation(contact.normal));
                onHit.Invoke(dmg);
                if (destroyOnHit) Destroy(gameObject);
            }
        }
    }
}
