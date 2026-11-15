using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRKit
{
    /// <summary>
    /// Palanca que se agarra y se mueve con la mano (interruptor, freno, acelerador, compuerta…).
    /// Gira alrededor del eje X local de «Handle». En reposo, el Handle apunta hacia arriba (su eje Y).
    ///
    ///   Raíz (VRLever)
    ///   └── Handle  (pivote, gira)     ← la base de la palanca
    ///       └── Pomo  (Collider + XR Simple Interactable)   ← lo que se agarra
    ///
    /// Value va de 0 (minAngle) a 1 (maxAngle). Con Notches = 2 se comporta como un interruptor de dos posiciones.
    /// </summary>
    public class VRLever : MonoBehaviour
    {
        [SerializeField] Transform handle;
        [Tooltip("Vacío = se busca en los hijos.")]
        [SerializeField] XRBaseInteractable interactable;
        [SerializeField] float minAngle = -45f;
        [SerializeField] float maxAngle = 45f;
        [Range(0f, 1f)] [SerializeField] float startValue = 0f;

        [Header("Comportamiento")]
        [Tooltip("Posiciones fijas a las que encaja al soltar (0 = movimiento libre, 2 = on/off).")]
        [SerializeField] int notches = 2;
        [Tooltip("Vuelve sola a startValue al soltarla (como un gatillo o un freno de mano con muelle).")]
        [SerializeField] bool returnWhenReleased = false;
        [SerializeField] float returnSpeed = 4f;
        [Tooltip("Por encima de este valor cuenta como «encendida»; por debajo de offThreshold, «apagada».")]
        [SerializeField] float onThreshold = 0.85f;
        [SerializeField] float offThreshold = 0.15f;

        [Header("Respuesta")]
        [SerializeField, Range(0f, 1f)] float notchHaptic = 0.3f;
        [SerializeField] AudioClip clickSound;

        public UnityEvent<float> onValueChanged = new UnityEvent<float>();
        public UnityEvent onTurnedOn = new UnityEvent();
        public UnityEvent onTurnedOff = new UnityEvent();

        public float Value { get; private set; }
        public bool IsOn { get; private set; }
        public bool IsGrabbed { get { return m_Interactor != null; } }

        IXRSelectInteractor m_Interactor;
        Quaternion m_BaseRot;
        int m_LastNotch = -1;

        void Awake()
        {
            if (handle == null) handle = transform;
            if (interactable == null) interactable = GetComponentInChildren<XRBaseInteractable>();
            m_BaseRot = handle.localRotation;
            SetValue(startValue, false);
            IsOn = Value >= onThreshold;
        }

        void OnEnable()
        {
            if (interactable == null) return;
            interactable.selectEntered.AddListener(OnGrab);
            interactable.selectExited.AddListener(OnRelease);
        }

        void OnDisable()
        {
            if (interactable == null) return;
            interactable.selectEntered.RemoveListener(OnGrab);
            interactable.selectExited.RemoveListener(OnRelease);
        }

        void OnGrab(SelectEnterEventArgs a) { m_Interactor = a.interactorObject; }

        void OnRelease(SelectExitEventArgs a)
        {
            m_Interactor = null;
            if (notches >= 2) SetValue(Mathf.Round(Value * (notches - 1)) / (notches - 1), true);
        }

        void Update()
        {
            if (m_Interactor != null)
            {
                var attach = m_Interactor.GetAttachTransform(interactable);
                Transform frame = handle.parent != null ? handle.parent : handle;
                Vector3 p = frame.InverseTransformPoint(attach.position) - handle.localPosition;
                p = Quaternion.Inverse(m_BaseRot) * p;
                float angle = Mathf.Atan2(p.z, p.y) * Mathf.Rad2Deg;
                SetValue(Mathf.InverseLerp(minAngle, maxAngle, Mathf.Clamp(angle, minAngle, maxAngle)), true);
            }
            else if (returnWhenReleased && !Mathf.Approximately(Value, startValue))
                SetValue(Mathf.MoveTowards(Value, startValue, returnSpeed * Time.deltaTime), true);
        }

        /// <summary>Mueve la palanca por código (por ejemplo, al cargar partida).</summary>
        public void SetValue(float v, bool notify)
        {
            v = Mathf.Clamp01(v);
            bool changed = !Mathf.Approximately(v, Value);
            Value = v;
            handle.localRotation = m_BaseRot * Quaternion.Euler(Mathf.Lerp(minAngle, maxAngle, v), 0f, 0f);
            if (!notify) return;
            if (changed) onValueChanged.Invoke(v);

            if (notches >= 2)
            {
                int n = Mathf.RoundToInt(v * (notches - 1));
                if (n != m_LastNotch && m_LastNotch >= 0) Click();
                m_LastNotch = n;
            }
            if (!IsOn && v >= onThreshold) { IsOn = true; if (notches < 2) Click(); onTurnedOn.Invoke(); }
            else if (IsOn && v <= offThreshold) { IsOn = false; if (notches < 2) Click(); onTurnedOff.Invoke(); }
        }

        void Click()
        {
            if (m_Interactor != null) VRHaptics.Send(m_Interactor, notchHaptic, 0.04f);
            if (clickSound != null) VRAudio.PlayAt(clickSound, handle.position, 0.7f);
        }
    }
}
