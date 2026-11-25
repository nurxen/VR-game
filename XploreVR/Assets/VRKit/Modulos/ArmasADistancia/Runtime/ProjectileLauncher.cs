using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRKit
{
    /// <summary>
    /// Cualquier cosa que dispara con el gatillo mientras la sujetas:
    /// pistola, varita mágica, ballesta, lanzaburbujas, lanzarredes...
    ///  - Modo Projectile: dispara un prefab con Projectile (se ve volar).
    ///  - Modo Raycast: impacto instantáneo (láser, pistola rápida), con trazo opcional.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class ProjectileLauncher : MonoBehaviour, IAmmoSource
    {
        public enum FireMode { Projectile, Raycast }

        [SerializeField] FireMode mode = FireMode.Projectile;
        [Tooltip("Punto de salida. Su eje Z (azul) apunta hacia donde dispara.")]
        [SerializeField] Transform muzzle;

        [Header("Cadencia y munición")]
        [Tooltip("Disparos por segundo.")]
        [SerializeField] float fireRate = 4f;
        [Tooltip("Mantener el gatillo dispara en ráfaga.")]
        [SerializeField] bool automatic = false;
        [Tooltip("Munición. -1 = infinita.")]
        [SerializeField] int maxAmmo = -1;

        [Header("Modo Projectile")]
        [SerializeField] Projectile projectilePrefab;
        [SerializeField] float projectileSpeed = 25f;
        [Tooltip("Dispersión aleatoria en grados.")]
        [SerializeField] float spread = 0f;

        [Header("Modo Raycast")]
        [SerializeField] float range = 100f;
        [SerializeField] float raycastDamage = 20f;
        [SerializeField] LayerMask hitLayers = ~0;
        [SerializeField] LineRenderer tracer;
        [SerializeField] float tracerTime = 0.05f;
        [SerializeField] GameObject impactEffectPrefab;

        [Header("Efectos")]
        [SerializeField] ParticleSystem muzzleFlash;
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip fireSound;
        [SerializeField] AudioClip emptySound;
        [SerializeField, Range(0f, 1f)] float hapticStrength = 0.5f;
        [SerializeField] float hapticDuration = 0.08f;

        [Header("Eventos")]
        public UnityEvent onFire = new UnityEvent();
        public UnityEvent onEmpty = new UnityEvent();
        public UnityEvent<int> onAmmoChanged = new UnityEvent<int>();

        XRGrabInteractable m_Grab;
        bool m_TriggerHeld;
        float m_NextFireTime;
        int m_Ammo;

        public int Ammo { get { return m_Ammo; } }

        void Awake()
        {
            m_Grab = GetComponent<XRGrabInteractable>();
            if (muzzle == null) muzzle = transform;
            m_Ammo = maxAmmo;
            if (tracer != null) tracer.enabled = false;
        }

        void OnEnable()
        {
            m_Grab.activated.AddListener(OnTriggerPressed);
            m_Grab.deactivated.AddListener(OnTriggerReleased);
            m_Grab.selectExited.AddListener(OnDropped);
        }

        void OnDisable()
        {
            m_Grab.activated.RemoveListener(OnTriggerPressed);
            m_Grab.deactivated.RemoveListener(OnTriggerReleased);
            m_Grab.selectExited.RemoveListener(OnDropped);
        }

        void OnTriggerPressed(ActivateEventArgs args) { m_TriggerHeld = true; TryFire(); }
        void OnTriggerReleased(DeactivateEventArgs args) { m_TriggerHeld = false; }
        void OnDropped(SelectExitEventArgs args) { if (!m_Grab.isSelected) m_TriggerHeld = false; }

        void Update()
        {
            if (automatic && m_TriggerHeld) TryFire();
        }

        public void TryFire()
        {
            if (Time.time < m_NextFireTime) return;
            m_NextFireTime = Time.time + 1f / Mathf.Max(0.01f, fireRate);

            if (m_Ammo == 0)
            {
                if (audioSource != null && emptySound != null) audioSource.PlayOneShot(emptySound);
                onEmpty.Invoke();
                return;
            }
            if (m_Ammo > 0) { m_Ammo--; onAmmoChanged.Invoke(m_Ammo); }

            if (mode == FireMode.Projectile) FireProjectile();
            else FireRaycast();

            if (muzzleFlash != null) muzzleFlash.Play();
            if (audioSource != null && fireSound != null) audioSource.PlayOneShot(fireSound);
            if (m_Grab.firstInteractorSelecting != null)
                VRHaptics.Send(m_Grab.firstInteractorSelecting, hapticStrength, hapticDuration);
            onFire.Invoke();
        }

        void FireProjectile()
        {
            if (projectilePrefab == null) return;
            Quaternion rot = muzzle.rotation;
            if (spread > 0f) rot *= Quaternion.Euler(Random.Range(-spread, spread), Random.Range(-spread, spread), 0f);
            var p = Instantiate(projectilePrefab, muzzle.position, rot);
            p.Launch(rot * Vector3.forward * projectileSpeed, transform);
        }

        void FireRaycast()
        {
            Vector3 origin = muzzle.position;
            Vector3 dir = muzzle.forward;
            Vector3 end = origin + dir * range;
            RaycastHit hit;
            if (Physics.Raycast(origin, dir, out hit, range, hitLayers, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                Damage.TryApply(hit.collider, new DamageInfo(raycastDamage, hit.point, dir, gameObject));
                if (hit.rigidbody != null && !hit.rigidbody.isKinematic)
                    hit.rigidbody.AddForceAtPosition(dir * 2f, hit.point, ForceMode.Impulse);
                if (impactEffectPrefab != null) Instantiate(impactEffectPrefab, hit.point, Quaternion.LookRotation(hit.normal));
            }
            if (tracer != null) StartCoroutine(ShowTracer(origin, end));
        }

        IEnumerator ShowTracer(Vector3 from, Vector3 to)
        {
            tracer.useWorldSpace = true;
            tracer.positionCount = 2;
            tracer.SetPosition(0, from);
            tracer.SetPosition(1, to);
            tracer.enabled = true;
            yield return new WaitForSeconds(tracerTime);
            tracer.enabled = false;
        }

        /// <summary>Recarga. Llámalo desde un botón, un socket de cargador o un gesto.</summary>
        public void Reload()
        {
            m_Ammo = maxAmmo;
            onAmmoChanged.Invoke(m_Ammo);
        }

        public void AddAmmo(int amount)
        {
            if (m_Ammo < 0) return;
            m_Ammo += amount;
            onAmmoChanged.Invoke(m_Ammo);
        }
    }
}
