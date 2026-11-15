using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRKit
{
    /// <summary>
    /// Cuerda del arco. Con una mano coges el arco; con la otra agarras la cuerda,
    /// tiras hacia atrás y sueltas para disparar. Cuanto más tensas, más rápido sale la flecha.
    ///
    /// Dos formas de cargar flecha (se pueden usar las dos a la vez):
    ///  - Automática: al agarrar la cuerda aparece una flecha (modo arcade).
    ///  - Manual: coges una flecha (por ejemplo de un carcaj con SpawnOnGrab) y la acercas a la cuerda.
    ///
    /// Montaje (ver README): va en un hijo "String" del arco con XRSimpleInteractable y un
    /// Collider marcado como Is Trigger. El eje Z (azul) de "Nock Rest Point" apunta hacia donde dispara.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class BowString : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("El XRGrabInteractable del arco (el mango).")]
        [SerializeField] XRGrabInteractable bow;
        [Tooltip("Punto de la cuerda en reposo, donde se coloca la flecha. Su eje Z apunta hacia delante.")]
        [SerializeField] Transform nockRestPoint;
        [SerializeField] Transform stringTop;
        [SerializeField] Transform stringBottom;
        [Tooltip("LineRenderer de 3 puntos que dibuja la cuerda.")]
        [SerializeField] LineRenderer stringLine;

        [Header("Disparo")]
        [Tooltip("Cuántos metros se puede tensar la cuerda.")]
        [SerializeField] float maxPullDistance = 0.5f;
        [Tooltip("Tensión mínima (0-1) para que la flecha salga disparada.")]
        [SerializeField, Range(0f, 1f)] float minPullToFire = 0.15f;
        [SerializeField] float maxArrowSpeed = 40f;
        [Tooltip("Hay que sujetar el arco para poder tensar la cuerda.")]
        [SerializeField] bool requireBowHeld = true;

        [Header("Flechas")]
        [Tooltip("Al agarrar la cuerda sin flecha, aparece una de este prefab. Vacío = solo carga manual.")]
        [SerializeField] Arrow autoArrowPrefab;
        [Tooltip("Flechas disponibles en modo automático. -1 = infinitas.")]
        [SerializeField] int autoArrowStock = -1;

        [Header("Sonido y vibración")]
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip nockSound;
        [SerializeField] AudioClip releaseSound;
        [SerializeField, Range(0f, 1f)] float releaseHaptic = 0.7f;

        [Header("Eventos")]
        public UnityEvent<float> onPullChanged = new UnityEvent<float>();
        public UnityEvent<float> onFired = new UnityEvent<float>();

        XRSimpleInteractable m_StringInteractable;
        IXRSelectInteractor m_PullingHand;
        Arrow m_NockedArrow;
        bool m_ArrowWasAutoSpawned;
        Transform m_NockHolder;
        float m_Pull;
        float m_LastHapticPull;

        public float Pull { get { return m_Pull; } }
        public bool HasArrow { get { return m_NockedArrow != null; } }

        void Awake()
        {
            m_StringInteractable = GetComponent<XRSimpleInteractable>();
            if (stringLine != null)
            {
                stringLine.positionCount = 3;
                stringLine.useWorldSpace = true;
            }
            // Objeto que se mueve con la cuerda y sujeta la flecha
            m_NockHolder = new GameObject("Nock Holder").transform;
            m_NockHolder.SetParent(nockRestPoint, false);
        }

        void OnEnable()
        {
            m_StringInteractable.selectEntered.AddListener(OnStringGrabbed);
            m_StringInteractable.selectExited.AddListener(OnStringReleased);
        }

        void OnDisable()
        {
            m_StringInteractable.selectEntered.RemoveListener(OnStringGrabbed);
            m_StringInteractable.selectExited.RemoveListener(OnStringReleased);
        }

        void OnStringGrabbed(SelectEnterEventArgs args)
        {
            m_PullingHand = args.interactorObject;
            m_LastHapticPull = 0f;
            if (m_NockedArrow == null && autoArrowPrefab != null && autoArrowStock != 0)
            {
                if (autoArrowStock > 0) autoArrowStock--;
                var arrow = Instantiate(autoArrowPrefab, m_NockHolder.position, m_NockHolder.rotation);
                NockArrow(arrow, true);
            }
        }

        void OnStringReleased(SelectExitEventArgs args)
        {
            if (args.interactorObject != m_PullingHand) return;
            var hand = m_PullingHand;
            m_PullingHand = null;

            if (m_NockedArrow != null)
            {
                if (m_Pull >= minPullToFire) Fire(hand);
                else if (m_ArrowWasAutoSpawned) { Destroy(m_NockedArrow.gameObject); m_NockedArrow = null; }
                // Si la flecha era manual y no se tensó, se queda en la cuerda
            }
            SetPull(0f);
        }

        void Update()
        {
            if (m_PullingHand != null && (!requireBowHeld || bow == null || bow.isSelected))
            {
                Vector3 handPos = m_PullingHand.GetAttachTransform(m_StringInteractable).position;
                float d = Vector3.Dot(handPos - nockRestPoint.position, -nockRestPoint.forward);
                SetPull(Mathf.Clamp01(d / Mathf.Max(0.01f, maxPullDistance)));

                // Pequeños "clics" de vibración al tensar
                if (Mathf.Abs(m_Pull - m_LastHapticPull) > 0.1f)
                {
                    VRHaptics.Send(m_PullingHand, 0.1f + m_Pull * 0.3f, 0.02f);
                    m_LastHapticPull = m_Pull;
                }
            }
            else if (m_PullingHand != null)
            {
                SetPull(0f);
            }
            UpdateStringVisual();
        }

        void SetPull(float pull)
        {
            if (Mathf.Approximately(pull, m_Pull)) return;
            m_Pull = pull;
            m_NockHolder.localPosition = Vector3.back * (m_Pull * maxPullDistance);
            onPullChanged.Invoke(m_Pull);
        }

        void UpdateStringVisual()
        {
            if (stringLine == null || stringTop == null || stringBottom == null) return;
            stringLine.SetPosition(0, stringTop.position);
            stringLine.SetPosition(1, m_NockHolder.position);
            stringLine.SetPosition(2, stringBottom.position);
        }

        void NockArrow(Arrow arrow, bool autoSpawned)
        {
            m_NockedArrow = arrow;
            m_ArrowWasAutoSpawned = autoSpawned;
            arrow.Nock(m_NockHolder);
            if (audioSource != null && nockSound != null) audioSource.PlayOneShot(nockSound);
        }

        void Fire(IXRSelectInteractor hand)
        {
            var arrow = m_NockedArrow;
            m_NockedArrow = null;
            float speed = maxArrowSpeed * m_Pull;
            arrow.Launch(nockRestPoint.forward * speed, bow != null ? bow.transform : transform.root);

            if (audioSource != null && releaseSound != null) audioSource.PlayOneShot(releaseSound, 0.5f + m_Pull * 0.5f);
            VRHaptics.Send(hand, releaseHaptic * m_Pull, 0.1f);
            if (bow != null && bow.firstInteractorSelecting != null)
                VRHaptics.Send(bow.firstInteractorSelecting, releaseHaptic * m_Pull, 0.15f);
            onFired.Invoke(m_Pull);
        }

        // Carga manual: una flecha sujeta con la mano toca la cuerda
        void OnTriggerEnter(Collider other)
        {
            if (m_NockedArrow != null || m_PullingHand != null) return;
            var arrow = other.GetComponentInParent<Arrow>();
            if (arrow == null || !arrow.IsHeld || arrow.IsNocked) return;
            if (requireBowHeld && bow != null && !bow.isSelected) return;
            StartCoroutine(TransferArrowToString(arrow));
        }

        IEnumerator TransferArrowToString(Arrow arrow)
        {
            var hand = arrow.HoldingInteractor;
            var manager = m_StringInteractable.interactionManager;
            yield return null; // cambiar la selección fuera del evento de física
            if (hand == null || manager == null || arrow == null || !arrow.IsHeld) yield break;

            manager.SelectExit(hand, (IXRSelectInteractable)arrow.Grab);
            NockArrow(arrow, false);
            // La misma mano pasa a sujetar la cuerda: ya puedes tensar
            manager.SelectEnter(hand, (IXRSelectInteractable)m_StringInteractable);
        }

        /// <summary>Quita la flecha de la cuerda sin disparar.</summary>
        public void DropArrow()
        {
            if (m_NockedArrow == null) return;
            m_NockedArrow.Unnock();
            m_NockedArrow = null;
        }
    }
}
