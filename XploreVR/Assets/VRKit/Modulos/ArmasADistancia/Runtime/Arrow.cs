using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRKit
{
    /// <summary>
    /// Flecha: se coge con la mano, se encaja en la cuerda del arco (BowString),
    /// vuela orientada según su velocidad, se clava donde impacta y hace daño.
    /// Después se puede volver a coger.
    ///
    /// Montaje: el eje Z (azul) de la flecha apunta hacia la punta.
    /// Componentes: Rigidbody + Collider + XRGrabInteractable + Arrow.
    /// Crea un hijo vacío "Tip" justo en la punta y arrástralo al campo Tip.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Arrow : MonoBehaviour
    {
        [SerializeField] Transform tip;
        [SerializeField] float damage = 25f;
        [Tooltip("A esta velocidad (m/s) o más, la flecha hace el daño completo.")]
        [SerializeField] float fullDamageSpeed = 20f;
        [Tooltip("Cuánto se hunde la punta al clavarse (metros).")]
        [SerializeField] float penetration = 0.05f;
        [SerializeField] bool stickIntoSurfaces = true;
        [Tooltip("Capas contra las que choca la flecha.")]
        [SerializeField] LayerMask hitLayers = ~0;
        [Tooltip("Ignorar al jugador (no te puedes disparar a ti mismo).")]
        [SerializeField] bool ignorePlayer = true;

        [Header("Vida útil")]
        [Tooltip("Segundos que dura clavada o tirada antes de desaparecer (0 = no desaparece).")]
        [SerializeField] float lifetimeAfterHit = 15f;
        [SerializeField] float lifetimeInFlight = 10f;

        [Header("Efectos")]
        [SerializeField] TrailRenderer trail;
        [SerializeField] AudioClip hitSound;
        [SerializeField] GameObject hitEffectPrefab;

        public UnityEvent<GameObject> onHit = new UnityEvent<GameObject>();

        Rigidbody m_Rigidbody;
        XRGrabInteractable m_Grab;
        Collider[] m_Colliders;
        Transform m_Shooter;
        Vector3 m_LastTipPos;
        float m_DespawnTime;

        public bool IsFlying { get; private set; }
        public bool IsNocked { get; private set; }
        public bool IsHeld { get { return m_Grab != null && m_Grab.isSelected; } }
        public XRGrabInteractable Grab { get { return m_Grab; } }
        public IXRSelectInteractor HoldingInteractor { get { return m_Grab != null ? m_Grab.firstInteractorSelecting : null; } }
        Transform Tip { get { return tip != null ? tip : transform; } }

        void Awake()
        {
            m_Rigidbody = GetComponent<Rigidbody>();
            m_Grab = GetComponent<XRGrabInteractable>();
            m_Colliders = GetComponentsInChildren<Collider>();
            if (m_Grab != null)
            {
                m_Grab.retainTransformParent = false; // si estaba clavada en un enemigo, al cogerla se suelta de él
                m_Grab.selectEntered.AddListener(OnGrabbed);
                m_Grab.selectExited.AddListener(OnReleased);
            }
            if (trail != null) trail.emitting = false;
        }

        void OnDestroy()
        {
            if (m_Grab != null)
            {
                m_Grab.selectEntered.RemoveListener(OnGrabbed);
                m_Grab.selectExited.RemoveListener(OnReleased);
            }
        }

        void OnGrabbed(SelectEnterEventArgs args)
        {
            IsFlying = false;
            m_DespawnTime = 0f;
            transform.SetParent(null, true);
            SetColliders(true);
            if (trail != null) trail.emitting = false;
        }

        void OnReleased(SelectExitEventArgs args)
        {
            if (IsNocked || IsFlying || m_Grab.isSelected) return;
            // Si se cogió estando clavada (cinemática), al soltarla debe volver a caer
            m_Rigidbody.isKinematic = false;
            SetDespawn(lifetimeAfterHit);
        }

        void SetDespawn(float lifetime)
        {
            m_DespawnTime = lifetime > 0f ? Time.time + lifetime : 0f;
        }

        /// <summary>La coloca en la cuerda del arco. La llama BowString.</summary>
        public void Nock(Transform nockPoint)
        {
            IsNocked = true;
            IsFlying = false;
            m_DespawnTime = 0f;
            if (m_Grab != null) m_Grab.enabled = false;
            m_Rigidbody.isKinematic = true;
            SetColliders(false);
            transform.SetParent(nockPoint, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        /// <summary>Sale de la cuerda sin disparar (se cae al suelo).</summary>
        public void Unnock()
        {
            IsNocked = false;
            transform.SetParent(null, true);
            m_Rigidbody.isKinematic = false;
            SetColliders(true);
            if (m_Grab != null) m_Grab.enabled = true;
            SetDespawn(lifetimeAfterHit);
        }

        /// <summary>Dispara la flecha con una velocidad. La llama BowString.</summary>
        public void Launch(Vector3 velocity, Transform shooter)
        {
            IsNocked = false;
            IsFlying = true;
            m_Shooter = shooter;
            transform.SetParent(null, true);
            if (m_Grab != null) m_Grab.enabled = false;
            SetColliders(false); // en vuelo detectamos impactos con rayos, más fiable a alta velocidad
            m_Rigidbody.isKinematic = false;
            m_Rigidbody.useGravity = true;
            m_Rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            m_Rigidbody.linearVelocity = velocity;
            m_Rigidbody.angularVelocity = Vector3.zero;
            m_LastTipPos = Tip.position;
            SetDespawn(lifetimeInFlight);
            if (trail != null) { trail.Clear(); trail.emitting = true; }
        }

        void FixedUpdate()
        {
            if (!IsFlying) return;

            Vector3 v = m_Rigidbody.linearVelocity;
            if (v.sqrMagnitude > 0.01f)
                m_Rigidbody.MoveRotation(Quaternion.LookRotation(v));

            Vector3 tipPos = Tip.position;
            Vector3 seg = tipPos - m_LastTipPos;
            if (seg.sqrMagnitude > 0f)
            {
                var hits = Physics.RaycastAll(m_LastTipPos, seg.normalized, seg.magnitude, hitLayers, QueryTriggerInteraction.Ignore);
                float best = float.MaxValue;
                RaycastHit chosen = default(RaycastHit);
                bool found = false;
                foreach (var h in hits)
                {
                    if (ShouldIgnore(h.collider)) continue;
                    if (h.distance < best) { best = h.distance; chosen = h; found = true; }
                }
                if (found) { HandleHit(chosen, v); return; }
            }
            m_LastTipPos = tipPos;
        }

        void Update()
        {
            if (m_DespawnTime > 0f && Time.time > m_DespawnTime && !IsHeld && !IsNocked)
                Destroy(gameObject);
        }

        bool ShouldIgnore(Collider c)
        {
            if (c.transform.IsChildOf(transform)) return true;
            if (m_Shooter != null && c.transform.IsChildOf(m_Shooter)) return true;
            if (ignorePlayer && PlayerHealth.Instance != null && c.transform.IsChildOf(PlayerHealth.Instance.transform)) return true;
            return false;
        }

        void HandleHit(RaycastHit hit, Vector3 velocity)
        {
            IsFlying = false;
            if (trail != null) trail.emitting = false;

            float speed = velocity.magnitude;
            float dmg = damage * Mathf.Clamp01(speed / Mathf.Max(0.01f, fullDamageSpeed));
            Damage.TryApply(hit.collider, new DamageInfo(dmg, hit.point, velocity.normalized, gameObject));

            if (hit.rigidbody != null && !hit.rigidbody.isKinematic)
                hit.rigidbody.AddForceAtPosition(velocity * m_Rigidbody.mass, hit.point, ForceMode.Impulse);

            if (hitSound != null) AudioSource.PlayClipAtPoint(hitSound, hit.point);
            if (hitEffectPrefab != null) Instantiate(hitEffectPrefab, hit.point, Quaternion.LookRotation(hit.normal));
            onHit.Invoke(hit.collider.gameObject);

            if (stickIntoSurfaces)
            {
                m_Rigidbody.isKinematic = true;
                Vector3 tipOffset = Tip.position - transform.position;
                transform.position = hit.point - tipOffset + transform.forward * penetration;
                transform.SetParent(hit.collider.transform, true);
            }
            else
            {
                m_Rigidbody.linearVelocity = velocity * 0.2f;
            }

            SetColliders(true);
            if (m_Grab != null) m_Grab.enabled = true; // se puede recoger
            SetDespawn(lifetimeAfterHit);
        }

        void SetColliders(bool on)
        {
            foreach (var c in m_Colliders) if (c != null) c.enabled = on;
        }
    }
}
