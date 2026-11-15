using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRKit
{
    /// <summary>
    /// Botón físico 3D que se pulsa con el dedo (poke) o apuntando con el rayo.
    /// Ponlo junto a un XRSimpleInteractable. Para pulsarlo con el dedo,
    /// añade también un componente "XR Poke Filter".
    /// </summary>
    [RequireComponent(typeof(XRBaseInteractable))]
    public class VRButton : MonoBehaviour
    {
        [Tooltip("La parte que se hunde al pulsar.")]
        [SerializeField] Transform pressVisual;
        [SerializeField] Vector3 pressOffset = new Vector3(0f, -0.01f, 0f);
        [SerializeField] float cooldown = 0.3f;
        [Tooltip("Funciona como interruptor: una pulsación enciende, otra apaga.")]
        [SerializeField] bool isToggle = false;
        [SerializeField] AudioClip clickSound;

        public UnityEvent onPressed = new UnityEvent();
        public UnityEvent onReleased = new UnityEvent();
        public UnityEvent<bool> onToggled = new UnityEvent<bool>();

        XRBaseInteractable m_Interactable;
        Vector3 m_RestPos;
        float m_LastPress = -999f;

        public bool IsOn { get; private set; }

        void Awake()
        {
            m_Interactable = GetComponent<XRBaseInteractable>();
            if (pressVisual != null) m_RestPos = pressVisual.localPosition;
        }

        void OnEnable()
        {
            m_Interactable.selectEntered.AddListener(OnPress);
            m_Interactable.selectExited.AddListener(OnRelease);
        }

        void OnDisable()
        {
            m_Interactable.selectEntered.RemoveListener(OnPress);
            m_Interactable.selectExited.RemoveListener(OnRelease);
        }

        void OnPress(SelectEnterEventArgs args)
        {
            if (Time.time - m_LastPress < cooldown) return;
            m_LastPress = Time.time;
            if (pressVisual != null) pressVisual.localPosition = m_RestPos + pressOffset;
            if (clickSound != null) AudioSource.PlayClipAtPoint(clickSound, transform.position, 0.6f);
            VRHaptics.Send(args.interactorObject, 0.3f, 0.05f);
            onPressed.Invoke();
            if (isToggle)
            {
                IsOn = !IsOn;
                onToggled.Invoke(IsOn);
            }
        }

        void OnRelease(SelectExitEventArgs args)
        {
            if (pressVisual != null) pressVisual.localPosition = m_RestPos;
            onReleased.Invoke();
        }
    }
}
