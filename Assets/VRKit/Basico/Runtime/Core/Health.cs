using UnityEngine;
using UnityEngine.Events;

namespace VRKit
{
    /// <summary>Datos de un golpe: cuánto daño, dónde y quién lo causó.</summary>
    public struct DamageInfo
    {
        public float amount;
        public Vector3 point;
        public Vector3 direction;
        public GameObject source;

        public DamageInfo(float amount, Vector3 point, Vector3 direction, GameObject source)
        {
            this.amount = amount;
            this.point = point;
            this.direction = direction;
            this.source = source;
        }
    }

    /// <summary>Cualquier cosa que pueda recibir daño (enemigos, dianas, cajas, el jugador).</summary>
    public interface IDamageable
    {
        void TakeDamage(DamageInfo info);
    }

    public static class Damage
    {
        /// <summary>Busca un IDamageable en el objeto golpeado o en sus padres y le aplica el daño.</summary>
        public static bool TryApply(Component hit, DamageInfo info)
        {
            if (hit == null) return false;
            var target = hit.GetComponentInParent<IDamageable>();
            if (target == null) return false;
            target.TakeDamage(info);
            return true;
        }

        static readonly Collider[] s_Buffer = new Collider[64];

        /// <summary>
        /// Explosión: daño en un radio (menos cuanto más lejos, si falloff) y empuje a los Rigidbody.
        /// Cada IDamageable recibe el golpe una sola vez aunque tenga varios colliders.
        /// Devuelve a cuántos ha hecho daño.
        /// </summary>
        public static int Explode(Vector3 center, float radius, float damage, float force, GameObject source, int layerMask = ~0, bool falloff = true, Transform ignoreRoot = null)
        {
            int n = Physics.OverlapSphereNonAlloc(center, radius, s_Buffer, layerMask, QueryTriggerInteraction.Ignore);
            var damaged = new System.Collections.Generic.HashSet<IDamageable>();
            var pushed = new System.Collections.Generic.HashSet<Rigidbody>();
            int count = 0;
            for (int i = 0; i < n; i++)
            {
                var c = s_Buffer[i];
                if (ignoreRoot != null && c.transform.IsChildOf(ignoreRoot)) continue;
                Vector3 closest = c.ClosestPoint(center);
                float dist = Vector3.Distance(center, closest);
                float k = falloff ? Mathf.Clamp01(1f - dist / radius) : 1f;

                var rb = c.attachedRigidbody;
                if (rb != null && force > 0f && pushed.Add(rb))
                    rb.AddExplosionForce(force, center, radius, 0.4f, ForceMode.Impulse);

                var d = c.GetComponentInParent<IDamageable>();
                if (d == null || !damaged.Add(d) || damage <= 0f) continue;
                Vector3 dir = (closest - center).sqrMagnitude > 0.0001f ? (closest - center).normalized : Vector3.up;
                d.TakeDamage(new DamageInfo(damage * Mathf.Max(0.2f, k), closest, dir, source));
                count++;
            }
            return count;
        }
    }

    /// <summary>
    /// Vida genérica. Ponla en enemigos, objetos rompibles o cualquier cosa que deba morir.
    /// Engancha efectos, sonidos o animaciones desde los eventos del Inspector.
    /// </summary>
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] float maxHealth = 100f;
        [Tooltip("Segundos de invulnerabilidad tras recibir un golpe (0 = ninguno).")]
        [SerializeField] float invulnerableTime = 0f;
        [SerializeField] bool destroyOnDeath = false;
        [SerializeField] float destroyDelay = 0f;

        [Header("Eventos")]
        [Tooltip("Se lanza con la cantidad de daño recibida.")]
        public UnityEvent<float> onDamaged = new UnityEvent<float>();
        [Tooltip("Se lanza con la vida normalizada (0 a 1). Útil para barras de vida.")]
        public UnityEvent<float> onHealthChanged = new UnityEvent<float>();
        public UnityEvent onDeath = new UnityEvent();

        public float Current { get; private set; }
        public float Max { get { return maxHealth; } }
        public float Normalized { get { return maxHealth > 0f ? Current / maxHealth : 0f; } }
        public bool IsDead { get; private set; }
        public DamageInfo LastDamage { get; private set; }

        float m_LastHitTime = -999f;

        protected virtual void Awake()
        {
            Current = maxHealth;
        }

        public void TakeDamage(DamageInfo info)
        {
            if (IsDead || info.amount <= 0f) return;
            if (Time.time - m_LastHitTime < invulnerableTime) return;

            info.amount = ModifyDamage(info);
            if (info.amount <= 0f) return;

            m_LastHitTime = Time.time;
            LastDamage = info;
            Current = Mathf.Max(0f, Current - info.amount);

            onDamaged.Invoke(info.amount);
            onHealthChanged.Invoke(Normalized);
            OnDamaged(info);

            if (Current <= 0f) Die();
        }

        /// <summary>Atajo para llamar desde eventos del Inspector.</summary>
        public void TakeDamage(float amount)
        {
            TakeDamage(new DamageInfo(amount, transform.position, Vector3.zero, null));
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            Current = Mathf.Min(maxHealth, Current + amount);
            onHealthChanged.Invoke(Normalized);
        }

        public void ResetHealth()
        {
            IsDead = false;
            Current = maxHealth;
            onHealthChanged.Invoke(Normalized);
        }

        public void Kill()
        {
            if (IsDead) return;
            Current = 0f;
            onHealthChanged.Invoke(0f);
            Die();
        }

        void Die()
        {
            IsDead = true;
            onDeath.Invoke();
            OnDied();
            if (destroyOnDeath) Destroy(gameObject, destroyDelay);
        }

        /// <summary>Para clases hijas (por ejemplo PlayerHealth).</summary>
        protected virtual void OnDamaged(DamageInfo info) { }
        /// <summary>Para clases hijas: cambiar el daño antes de aplicarlo (escudos, armadura, dificultad).</summary>
        protected virtual float ModifyDamage(DamageInfo info) { return info.amount; }
        protected virtual void OnDied() { }
    }
}
