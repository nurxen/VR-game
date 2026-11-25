using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRKit
{
    /// <summary>
    /// Granada, bomba, poción explosiva, seta que estalla…
    ///  · Gatillo con ella en la mano: se arma (quita la anilla). La mecha empieza al armarla o al soltarla.
    ///  · Impact: explota al chocar después de lanzarla.
    /// La explosión hace daño en área (menos cuanto más lejos) y empuja los objetos con física.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class Grenade : MonoBehaviour
    {
        public enum Trigger { Fuse, Impact }

        [SerializeField] Trigger trigger = Trigger.Fuse;
        [SerializeField] float fuseTime = 3f;
        [Tooltip("La mecha empieza al soltarla (si no, al armarla: puedes «cocinarla» en la mano).")]
        [SerializeField] bool fuseStartsOnRelease = false;
        [Tooltip("Se arma sola al cogerla (sin apretar el gatillo).")]
        [SerializeField] bool armOnGrab = false;

        [Header("Explosión")]
        [SerializeField] float radius = 4f;
        [SerializeField] float damage = 80f;
        [SerializeField] float force = 12f;
        [SerializeField] bool hurtsPlayer = true;
        [SerializeField] GameObject explosionEffect;
        [SerializeField] AudioClip armSound;
        [SerializeField] AudioClip explosionSound;
        [Tooltip("Luz o malla que parpadea cuando está armada (opcional).")]
        [SerializeField] GameObject armedIndicator;

        public UnityEvent onArmed = new UnityEvent();
        public UnityEvent onExploded = new UnityEvent();

        public bool IsArmed { get; private set; }

        XRGrabInteractable m_Grab;
        bool m_Thrown, m_Exploded, m_FuseRunning;

        void Awake()
        {
            m_Grab = GetComponent<XRGrabInteractable>();
            if (armedIndicator != null) armedIndicator.SetActive(false);
        }

        void OnEnable()
        {
            m_Grab.activated.AddListener(OnActivate);
            m_Grab.selectEntered.AddListener(OnGrab);
            m_Grab.selectExited.AddListener(OnRelease);
        }

        void OnDisable()
        {
            m_Grab.activated.RemoveListener(OnActivate);
            m_Grab.selectEntered.RemoveListener(OnGrab);
            m_Grab.selectExited.RemoveListener(OnRelease);
        }

        void OnGrab(SelectEnterEventArgs a) { if (armOnGrab) Arm(); }
        void OnActivate(ActivateEventArgs a) { Arm(); }

        void OnRelease(SelectExitEventArgs a)
        {
            if (m_Grab.isSelected || !IsArmed) return;
            m_Thrown = true;
            if (trigger == Trigger.Fuse && fuseStartsOnRelease) StartFuse();
        }

        public void Arm()
        {
            if (IsArmed) return;
            IsArmed = true;
            if (armSound != null) VRAudio.PlayAt(armSound, transform.position);
            if (armedIndicator != null) StartCoroutine(Blink());
            if (m_Grab.firstInteractorSelecting != null) VRHaptics.Send(m_Grab.firstInteractorSelecting, 0.5f, 0.1f);
            onArmed.Invoke();
            if (trigger == Trigger.Fuse && !fuseStartsOnRelease) StartFuse();
        }

        void StartFuse()
        {
            if (m_FuseRunning) return;
            m_FuseRunning = true;
            Invoke(nameof(Explode), fuseTime);
        }

        void OnCollisionEnter(Collision c)
        {
            if (trigger == Trigger.Impact && IsArmed && m_Thrown && c.relativeVelocity.magnitude > 2f) Explode();
        }

        IEnumerator Blink()
        {
            float interval = 0.4f;
            while (!m_Exploded)
            {
                armedIndicator.SetActive(!armedIndicator.activeSelf);
                yield return new WaitForSeconds(interval);
                interval = Mathf.Max(0.06f, interval * 0.85f); // cada vez más rápido
            }
        }

        public void Explode()
        {
            if (m_Exploded) return;
            m_Exploded = true;
            Vector3 p = transform.position;
            if (m_Grab.isSelected && m_Grab.interactionManager != null)
                m_Grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)m_Grab);

            // El jugador recibe daño a través de su CharacterController (si lo tiene); con hurtsPlayer = false se le excluye
            Transform ignore = !hurtsPlayer && PlayerHealth.Instance != null ? PlayerHealth.Instance.transform : null;
            Damage.Explode(p, radius, damage, force, gameObject, ~0, true, ignore);
            if (explosionEffect != null) Instantiate(explosionEffect, p, Quaternion.identity);
            else Effects.Burst(p, new Color(1f, 0.55f, 0.15f), radius * 0.8f, 0.35f);
            if (explosionSound != null) VRAudio.PlayAt(explosionSound, p);
            if (VRPlayer.Instance != null && Vector3.Distance(VRPlayer.Instance.FeetPosition, p) < radius * 2f)
                VRHaptics.SendAll(VRPlayer.Instance.gameObject, 0.6f, 0.25f);
            onExploded.Invoke();
            Destroy(gameObject);
        }
    }
}
