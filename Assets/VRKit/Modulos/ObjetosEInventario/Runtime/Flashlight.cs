using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRKit
{
    /// <summary>
    /// Linterna (o varita, antorcha, láser...) que se enciende y apaga con el gatillo
    /// mientras la sujetas. Opcional: batería que se gasta.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class Flashlight : MonoBehaviour
    {
        [SerializeField] Light lightSource;
        [SerializeField] bool startOn = false;
        [Tooltip("Se apaga sola al soltarla.")]
        [SerializeField] bool turnOffWhenDropped = false;
        [Tooltip("Segundos de batería. 0 = infinita.")]
        [SerializeField] float batterySeconds = 0f;
        [SerializeField] AudioClip clickSound;

        public UnityEvent<bool> onToggled = new UnityEvent<bool>();
        public UnityEvent onBatteryEmpty = new UnityEvent();

        XRGrabInteractable m_Grab;
        float m_Battery;
        float m_BaseIntensity;

        public bool IsOn { get { return lightSource != null && lightSource.enabled; } }
        public float BatteryNormalized { get { return batterySeconds > 0f ? m_Battery / batterySeconds : 1f; } }

        void Awake()
        {
            m_Grab = GetComponent<XRGrabInteractable>();
            if (lightSource == null) lightSource = GetComponentInChildren<Light>(true);
            m_Battery = batterySeconds;
            if (lightSource != null)
            {
                m_BaseIntensity = lightSource.intensity;
                lightSource.enabled = startOn;
            }
        }

        void OnEnable()
        {
            m_Grab.activated.AddListener(OnActivated);
            m_Grab.selectExited.AddListener(OnDropped);
        }

        void OnDisable()
        {
            m_Grab.activated.RemoveListener(OnActivated);
            m_Grab.selectExited.RemoveListener(OnDropped);
        }

        void Update()
        {
            if (!IsOn || batterySeconds <= 0f) return;
            m_Battery -= Time.deltaTime;
            // Parpadea un poco cuando queda menos del 15 %
            float n = BatteryNormalized;
            lightSource.intensity = n < 0.15f ? m_BaseIntensity * Random.Range(0.3f, 1f) : m_BaseIntensity;
            if (m_Battery <= 0f)
            {
                m_Battery = 0f;
                SetOn(false);
                onBatteryEmpty.Invoke();
            }
        }

        void OnActivated(ActivateEventArgs args)
        {
            Toggle();
            VRHaptics.Send(args.interactorObject, 0.15f, 0.03f);
        }

        void OnDropped(SelectExitEventArgs args)
        {
            if (turnOffWhenDropped && !m_Grab.isSelected) SetOn(false);
        }

        public void Toggle() { SetOn(!IsOn); }

        public void SetOn(bool on)
        {
            if (lightSource == null) return;
            if (on && batterySeconds > 0f && m_Battery <= 0f) on = false;
            lightSource.enabled = on;
            if (clickSound != null) AudioSource.PlayClipAtPoint(clickSound, transform.position, 0.5f);
            onToggled.Invoke(on);
        }

        public void Recharge(float seconds)
        {
            m_Battery = batterySeconds > 0f ? Mathf.Min(batterySeconds, m_Battery + seconds) : 0f;
        }
    }
}
