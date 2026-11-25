using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRKit
{
    /// <summary>
    /// Objeto que se coge y se lanza (piedra, pelota, cuchillo, botella...).
    /// XRGrabInteractable ya hace el agarre y el lanzamiento; este script añade
    /// fuerza extra, sonidos, vibración y saber si "va lanzado" (para hacer daño).
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class Throwable : MonoBehaviour
    {
        [Header("Lanzamiento")]
        [Tooltip("Multiplica la velocidad del lanzamiento. 1 = realista; 1.5-2 se siente mejor en VR.")]
        [SerializeField] float throwForceMultiplier = 1.5f;
        [SerializeField] float throwSpinMultiplier = 1f;
        [Tooltip("Segundos que el objeto cuenta como 'lanzado' tras soltarlo.")]
        [SerializeField] float thrownWindow = 3f;

        [Header("Sonido y vibración")]
        [SerializeField] AudioClip grabSound;
        [SerializeField] AudioClip impactSound;
        [SerializeField] float impactSoundMinSpeed = 1.5f;
        [SerializeField, Range(0f, 1f)] float grabHaptic = 0.25f;

        [Header("Eventos")]
        public UnityEvent onGrabbed = new UnityEvent();
        public UnityEvent onThrown = new UnityEvent();
        public UnityEvent<float> onImpact = new UnityEvent<float>();

        XRGrabInteractable m_Grab;
        float m_ReleaseTime = -999f;
        bool m_InFlight;

        public bool IsHeld { get { return m_Grab != null && m_Grab.isSelected; } }
        /// <summary>True desde que se suelta hasta el primer impacto (o hasta que pasa thrownWindow).</summary>
        public bool IsThrown { get { return m_InFlight && Time.time - m_ReleaseTime < thrownWindow; } }

        void Awake()
        {
            m_Grab = GetComponent<XRGrabInteractable>();
            m_Grab.throwOnDetach = true;
            m_Grab.throwVelocityScale = throwForceMultiplier;
            m_Grab.throwAngularVelocityScale = throwSpinMultiplier;
        }

        void OnEnable()
        {
            m_Grab.selectEntered.AddListener(OnGrab);
            m_Grab.selectExited.AddListener(OnRelease);
        }

        void OnDisable()
        {
            m_Grab.selectEntered.RemoveListener(OnGrab);
            m_Grab.selectExited.RemoveListener(OnRelease);
        }

        void OnGrab(SelectEnterEventArgs args)
        {
            m_InFlight = false;
            if (grabSound != null) AudioSource.PlayClipAtPoint(grabSound, transform.position);
            VRHaptics.Send(args.interactorObject, grabHaptic, 0.05f);
            onGrabbed.Invoke();
        }

        void OnRelease(SelectExitEventArgs args)
        {
            if (m_Grab.isSelected) return; // aún lo sujeta la otra mano
            m_ReleaseTime = Time.time;
            m_InFlight = true;
            onThrown.Invoke();
        }

        void OnCollisionEnter(Collision collision)
        {
            float speed = collision.relativeVelocity.magnitude;
            if (speed >= impactSoundMinSpeed)
            {
                if (impactSound != null)
                    AudioSource.PlayClipAtPoint(impactSound, collision.GetContact(0).point, Mathf.Clamp01(speed / 10f));
                onImpact.Invoke(speed);
            }
            // El primer impacto "consume" el lanzamiento, pero dejamos un instante
            // para que DamageOnImpact pueda leer IsThrown en este mismo choque.
            if (m_InFlight) Invoke(nameof(EndFlight), 0f);
        }

        void EndFlight()
        {
            m_InFlight = false;
        }
    }
}
