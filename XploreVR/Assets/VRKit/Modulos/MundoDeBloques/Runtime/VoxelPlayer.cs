using UnityEngine;

namespace VRKit
{
    /// <summary>
    /// El cuerpo del jugador dentro del mundo de bloques: gravedad (si al romper el suelo no hay nada, caes),
    /// salto, subir escalones de un bloque andando, daño por caída y rescate si caes al vacío.
    /// Ponlo en el XR Origin, junto al VRPlayer. En las maquetas (bloques pequeños) no hace nada.
    /// Si vuestro XR Origin ya trae GravityProvider o JumpProvider (XRI 3.1+), usa esos y no duplica.
    /// </summary>
    public class VoxelPlayer : MonoBehaviour
    {
        [Header("Gravedad y salto")]
        [SerializeField] bool gravity = true;
        [Tooltip("m/s². La real es 9,8; los juegos de bloques usan más para que no parezca que flotas.")]
        [SerializeField] float gravityStrength = 18f;
        [SerializeField] bool jump = true;
        [Tooltip("Botón A (principal) de esta mano para saltar.")]
        [SerializeField] Hand jumpHand = Hand.Right;
        [Tooltip("Altura del salto, en bloques.")]
        [SerializeField] float jumpHeight = 1.2f;

        [Header("Comodidad")]
        [Tooltip("Al andar contra un bloque de altura 1, se sube solo (como el salto automático de Minecraft). Sin saltos = menos mareo.")]
        [SerializeField] bool autoStep = true;

        [Header("Supervivencia")]
        [SerializeField] bool fallDamage = true;
        [Tooltip("Bloques que puedes caer sin hacerte daño.")]
        [SerializeField] float safeFall = 3.5f;
        [SerializeField] float damagePerBlock = 8f;
        [Tooltip("Si caes por debajo de esta altura (en bloques), vuelves a tu punto de aparición.")]
        [SerializeField] float voidHeight = -8f;

        CharacterController m_CC;
        HandInput m_Input;
        bool m_ExternalGravity, m_ExternalJump, m_WasGrounded = true;
        float m_VelY, m_FallTop;

        public bool IsGrounded { get; private set; }

        void OnEnable() { if (jump) m_Input = new HandInput(jumpHand); }
        void OnDisable() { if (m_Input != null) { m_Input.Dispose(); m_Input = null; } }

        void Start()
        {
            m_CC = GetComponent<CharacterController>();
            if (m_CC == null) m_CC = GetComponentInChildren<CharacterController>();
            foreach (var mb in GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                string n = mb.GetType().Name;
                if (n == "GravityProvider") m_ExternalGravity = true;
                if (n == "JumpProvider") m_ExternalJump = true;
            }
            // XRI 3.0: el proveedor de movimiento solo aplica gravedad al andar. Si la ponemos nosotros, se la quitamos a él.
            if (gravity && !m_ExternalGravity)
                foreach (var mb in GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue;
                    var p = mb.GetType().GetProperty("useGravity");
                    if (p != null && p.PropertyType == typeof(bool) && p.CanWrite && mb.GetType().Name.Contains("MoveProvider")) p.SetValue(mb, false);
                }
            m_FallTop = transform.position.y;
        }

        void Update()
        {
            var world = VoxelWorld.Instance;
            if (world == null || !world.IsReady || m_CC == null || !m_CC.enabled) { m_VelY = 0f; return; }
            float bs = world.BlockSize;
            if (bs < 0.3f) return; // maqueta: el jugador no está dentro del mundo

            if (autoStep) m_CC.stepOffset = Mathf.Min(1.05f * bs, Mathf.Max(0.3f, m_CC.height - 2f * m_CC.radius - 0.01f));

            IsGrounded = m_CC.isGrounded;

            if (gravity && !m_ExternalGravity)
            {
                if (IsGrounded && m_VelY < 0f) m_VelY = -1f;
                else m_VelY -= gravityStrength * Time.deltaTime;

                if (jump && !m_ExternalJump && IsGrounded && m_Input != null && m_Input.Primary.WasPressedThisFrame())
                    m_VelY = Mathf.Sqrt(2f * gravityStrength * jumpHeight * bs);

                m_CC.Move(Vector3.up * (m_VelY * Time.deltaTime));
                if ((m_CC.collisionFlags & CollisionFlags.Above) != 0 && m_VelY > 0f) m_VelY = 0f;
                IsGrounded = m_CC.isGrounded;
            }

            // Daño por caída
            float y = transform.position.y;
            if (!IsGrounded) m_FallTop = Mathf.Max(m_FallTop, y);
            else
            {
                if (!m_WasGrounded)
                {
                    float fall = (m_FallTop - y) / bs;
                    var game = VoxelGame.Instance;
                    bool survival = game != null && game.Mode == VoxelGameMode.Supervivencia;
                    if (fallDamage && survival && fall > safeFall && PlayerHealth.Instance != null)
                        PlayerHealth.Instance.TakeDamage(new DamageInfo((fall - safeFall) * damagePerBlock, transform.position, Vector3.down, null));
                }
                m_FallTop = y;
            }
            m_WasGrounded = IsGrounded;

            // ¿Al vacío?
            if (world.ToLocal(transform.position).y < voidHeight)
            {
                m_VelY = 0f;
                if (VoxelGame.Instance != null) VoxelGame.Instance.Respawn();
                m_FallTop = transform.position.y;
            }
        }

        /// <summary>Para el movimiento vertical (después de un teletransporte).</summary>
        public void ResetFall() { m_VelY = 0f; m_FallTop = transform.position.y; m_WasGrounded = true; }
    }
}
