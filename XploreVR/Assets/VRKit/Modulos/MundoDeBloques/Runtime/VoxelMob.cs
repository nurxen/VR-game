using UnityEngine;

namespace VRKit
{
    public enum VoxelMobKind { Pacifico, Hostil }

    /// <summary>
    /// Una criatura que anda por los bloques sin NavMesh (el mundo cambia demasiado para hornearlo):
    /// choca con los bloques, sube escalones de uno saltando y no se tira por los barrancos al pasear.
    ///   · Pacífica: pasea; si le pegas, huye.
    ///   · Hostil: te persigue si te ve y te pega. Con luz de día desaparece (si Despawn In Daylight).
    /// Necesita Health (Básico) y un Collider para que la mano pueda pegarle. Trabaja en unidades de bloque:
    /// vive dentro del objeto del VoxelWorld, así también funciona en las maquetas.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class VoxelMob : MonoBehaviour
    {
        [SerializeField] VoxelMobKind kind = VoxelMobKind.Hostil;

        [Header("Movimiento (en bloques)")]
        [SerializeField] float walkSpeed = 1.1f;
        [SerializeField] float chaseSpeed = 2.6f;
        [Tooltip("Ancho y alto del cuerpo, en bloques (para chocar con el mundo).")]
        [SerializeField] float width = 0.6f;
        [SerializeField] float height = 1.8f;

        [Header("Ataque (hostiles)")]
        [SerializeField] float detectRange = 16f;
        [SerializeField] float attackRange = 1.5f;
        [SerializeField] float attackDamage = 12f;
        [SerializeField] float attackCooldown = 1.1f;
        [SerializeField] bool despawnInDaylight = true;

        [Header("Al morir")]
        [Tooltip("Bloques que da (al inventario o como bloques sueltos).")]
        [SerializeField] BlockStack[] drops = new BlockStack[0];
        [SerializeField] int score = 10;

        [Header("Aspecto y sonido (opcional)")]
        [Tooltip("Piernas que se balancean al andar.")]
        [SerializeField] Transform[] legs = new Transform[0];
        [Tooltip("Cabeza que te mira cuando te persigue.")]
        [SerializeField] Transform head;
        [SerializeField] Color hurtColor = new Color(1f, 0.3f, 0.3f);
        [SerializeField] AudioClip idleSound;
        [SerializeField] AudioClip hurtSound;
        [SerializeField] AudioClip deathSound;

        const float kGravity = 25f, kJump = 7.6f;

        Health m_Health;
        VoxelWorld m_World;
        Vector3 m_Vel, m_Knock, m_Wander;
        bool m_Grounded, m_Chasing;
        float m_NextAttack, m_NextThink, m_FleeUntil, m_FlashUntil, m_Walk, m_NextIdleSound, m_DaySince = -1f;
        Renderer[] m_Renderers;
        MaterialPropertyBlock m_Block;

        public VoxelMobKind Kind { get { return kind; } }

        void Awake()
        {
            m_Health = GetComponent<Health>();
            m_Renderers = GetComponentsInChildren<Renderer>();
            m_Block = new MaterialPropertyBlock();
        }

        void OnEnable()
        {
            m_Health.onDamaged.AddListener(OnHurt);
            m_Health.onDeath.AddListener(OnDeath);
        }

        void OnDisable()
        {
            m_Health.onDamaged.RemoveListener(OnHurt);
            m_Health.onDeath.RemoveListener(OnDeath);
        }

        void Start()
        {
            m_World = VoxelWorld.Instance;
            if (m_World != null && transform.parent != m_World.transform) transform.SetParent(m_World.transform, true);
            m_NextIdleSound = Time.time + Random.Range(4f, 12f);
        }

        /// <summary>Empujón (lo llama la mano al pegar). dir en el mundo.</summary>
        public void Knockback(Vector3 worldDir)
        {
            if (m_World == null) return;
            var d = m_World.transform.InverseTransformDirection(worldDir);
            d.y = 0f;
            m_Knock = d.normalized * 6f;
            if (m_Grounded) m_Vel.y = 5f;
        }

        void Update()
        {
            if (m_World == null || !m_World.IsReady || m_Health.IsDead) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            Vector3 pos = transform.localPosition;

            // ---------------------------------------------------------- Pensar
            Vector3 want = Vector3.zero;
            float speed = walkSpeed;
            var player = VRPlayer.Instance;
            Vector3 toPlayer = Vector3.positiveInfinity;
            if (player != null) toPlayer = m_World.ToLocal(player.FeetPosition) - pos;
            float dist = player != null ? new Vector2(toPlayer.x, toPlayer.z).magnitude : float.PositiveInfinity;

            if (dist > 64f) { Destroy(gameObject); return; }

            m_Chasing = kind == VoxelMobKind.Hostil && dist < detectRange && !InCreative();
            if (m_Chasing)
            {
                want = new Vector3(toPlayer.x, 0f, toPlayer.z).normalized;
                speed = chaseSpeed;
                if (dist < attackRange && Mathf.Abs(toPlayer.y) < 2f) { want = Vector3.zero; TryAttack(toPlayer); }
            }
            else if (Time.time < m_FleeUntil && player != null)
            {
                want = -new Vector3(toPlayer.x, 0f, toPlayer.z).normalized;
                speed = chaseSpeed;
            }
            else
            {
                if (Time.time >= m_NextThink)
                {
                    m_NextThink = Time.time + Random.Range(2.5f, 6f);
                    m_Wander = Random.value < 0.4f ? Vector3.zero : Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                }
                want = m_Wander;
                // Al pasear, no se tira por un barranco
                if (want != Vector3.zero && !GroundAhead(pos + want * 0.8f)) { m_Wander = -m_Wander; want = Vector3.zero; }
            }

            // ---------------------------------------------------------- Moverse (eje a eje, chocando con los bloques)
            Vector3 h = want * speed + m_Knock;
            m_Knock = Vector3.MoveTowards(m_Knock, Vector3.zero, 12f * dt);
            bool blocked = false;
            var nx = pos + new Vector3(h.x * dt, 0f, 0f);
            if (!Blocked(nx)) pos = nx; else blocked = true;
            var nz = pos + new Vector3(0f, 0f, h.z * dt);
            if (!Blocked(nz)) pos = nz; else blocked = true;

            if (blocked && m_Grounded && want != Vector3.zero) m_Vel.y = kJump; // escalón: salta

            m_Vel.y -= kGravity * dt;
            var ny = pos + new Vector3(0f, m_Vel.y * dt, 0f);
            if (Blocked(ny))
            {
                if (m_Vel.y < 0f) { m_Grounded = true; pos.y = Mathf.Ceil(ny.y - 0.001f); }
                m_Vel.y = 0f;
            }
            else { m_Grounded = false; pos = ny; }
            transform.localPosition = pos;

            // ---------------------------------------------------------- Aspecto
            if (want != Vector3.zero)
            {
                var target = Quaternion.LookRotation(want, Vector3.up);
                transform.localRotation = Quaternion.RotateTowards(transform.localRotation, target, 360f * dt);
                m_Walk += dt * speed * 5f;
            }
            else m_Walk = Mathf.MoveTowards(m_Walk, Mathf.Round(m_Walk / Mathf.PI) * Mathf.PI, dt * 4f);
            for (int i = 0; i < legs.Length; i++)
                if (legs[i] != null) legs[i].localRotation = Quaternion.Euler(Mathf.Sin(m_Walk + i * Mathf.PI) * 35f, 0f, 0f);
            if (head != null && m_Chasing && player != null && player.Head != null)
                head.rotation = Quaternion.Slerp(head.rotation, Quaternion.LookRotation(player.Head.position - head.position), 8f * dt);

            UpdateFlash();
            if (idleSound != null && Time.time >= m_NextIdleSound) { VRAudio.PlayAt(idleSound, transform.position, 0.6f); m_NextIdleSound = Time.time + Random.Range(6f, 15f); }

            // ---------------------------------------------------------- Hostiles al sol
            var dn = VoxelDayNight.Instance;
            if (kind == VoxelMobKind.Hostil && despawnInDaylight && dn != null && !dn.IsNight)
            {
                if (m_DaySince < 0f) m_DaySince = Time.time + Random.Range(2f, 20f);
                else if (Time.time > m_DaySince) { Effects.Burst(transform.position + transform.up * height * 0.5f * m_World.BlockSize, new Color(0.9f, 0.9f, 0.9f), m_World.BlockSize, 0.4f); Destroy(gameObject); }
            }
            else m_DaySince = -1f;
        }

        static bool InCreative() { var g = VoxelGame.Instance; return g != null && g.IsCreative; }

        void TryAttack(Vector3 toPlayer)
        {
            if (Time.time < m_NextAttack || PlayerHealth.Instance == null) return;
            m_NextAttack = Time.time + attackCooldown;
            var dir = m_World.transform.TransformDirection(new Vector3(toPlayer.x, 0f, toPlayer.z).normalized);
            PlayerHealth.Instance.TakeDamage(new DamageInfo(attackDamage, transform.position, dir, gameObject));
        }

        /// <summary>¿El cuerpo, con los pies en p, se mete en algún bloque sólido?</summary>
        bool Blocked(Vector3 p)
        {
            float r = width * 0.5f;
            int x0 = Mathf.FloorToInt(p.x - r), x1 = Mathf.FloorToInt(p.x + r - 0.001f);
            int y0 = Mathf.FloorToInt(p.y), y1 = Mathf.FloorToInt(p.y + height - 0.001f);
            int z0 = Mathf.FloorToInt(p.z - r), z1 = Mathf.FloorToInt(p.z + r - 0.001f);
            for (int y = y0; y <= y1; y++)
            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
                if (m_World.IsSolid(x, y, z)) return true;
            return false;
        }

        bool GroundAhead(Vector3 p)
        {
            int x = Mathf.FloorToInt(p.x), z = Mathf.FloorToInt(p.z), y = Mathf.FloorToInt(p.y + 0.01f);
            for (int d = 1; d <= 3; d++) if (m_World.IsSolid(x, y - d, z)) return true;
            return false;
        }

        // ================================================================== Daño y muerte

        void OnHurt(float amount)
        {
            m_FlashUntil = Time.time + 0.18f;
            if (hurtSound != null) VRAudio.PlayAt(hurtSound, transform.position);
            if (kind == VoxelMobKind.Pacifico) m_FleeUntil = Time.time + 4f;
        }

        void UpdateFlash()
        {
            bool on = Time.time < m_FlashUntil;
            foreach (var r in m_Renderers)
            {
                if (r == null) continue;
                if (on) { m_Block.SetColor("_BaseColor", hurtColor); m_Block.SetColor("_Color", hurtColor); r.SetPropertyBlock(m_Block); }
                else r.SetPropertyBlock(null);
            }
        }

        void OnDeath()
        {
            var w = m_World;
            Vector3 center = transform.position + transform.up * height * 0.5f * (w != null ? w.BlockSize : 1f);
            if (deathSound != null) VRAudio.PlayAt(deathSound, center);
            Effects.Burst(center, new Color(0.9f, 0.9f, 0.9f), w != null ? w.BlockSize : 1f, 0.35f);
            if (GameManager.Instance != null && score != 0) GameManager.Instance.AddScore(score);
            var inv = BlockInventory.Instance;
            if (w != null)
                foreach (var d in drops)
                    if (d.block != null)
                    {
                        if (inv != null && !inv.Creative) VoxelDrop.Spawn(w, w.Registry.IdOf(d.block), d.count, center);
                    }
            Destroy(gameObject);
        }
    }
}
