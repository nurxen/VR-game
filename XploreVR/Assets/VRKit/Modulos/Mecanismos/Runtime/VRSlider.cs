using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRKit
{
    /// <summary>
    /// Pieza que se desliza en línea recta al tirar de ella: cajón, palanca de mezclador,
    /// cerrojo de un arma, persiana, el émbolo de una jeringuilla…
    /// Se mueve por el eje Z local del padre de «Slide», entre 0 y Length metros.
    /// </summary>
    public class VRSlider : MonoBehaviour
    {
        [SerializeField] Transform slide;
        [SerializeField] XRBaseInteractable interactable;
        [SerializeField] float length = 0.4f;
        [Range(0f, 1f)] [SerializeField] float startValue = 0f;
        [Tooltip("Posiciones fijas (0 = libre).")]
        [SerializeField] int notches = 0;
        [Tooltip("Vuelve a 0 al soltar (cerrojo con muelle).")]
        [SerializeField] bool springBack = false;
        [SerializeField] float springSpeed = 3f;
        [SerializeField, Range(0f, 1f)] float endHaptic = 0.35f;
        [SerializeField] AudioClip endSound;

        public UnityEvent<float> onValueChanged = new UnityEvent<float>();
        public UnityEvent onReachedEnd = new UnityEvent();
        public UnityEvent onReachedStart = new UnityEvent();

        public float Value { get; private set; }

        IXRSelectInteractor m_Interactor;
        Vector3 m_Start;
        float m_GrabOffset;
        bool m_AtEnd, m_AtStart = true;

        void Awake()
        {
            if (slide == null) slide = transform;
            if (interactable == null) interactable = GetComponentInChildren<XRBaseInteractable>();
            m_Start = slide.localPosition;
            SetValue(startValue, false);
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

        float HandDistance()
        {
            var attach = m_Interactor.GetAttachTransform(interactable);
            Transform frame = slide.parent != null ? slide.parent : slide;
            return frame.InverseTransformPoint(attach.position).z - m_Start.z;
        }

        void OnGrab(SelectEnterEventArgs a)
        {
            m_Interactor = a.interactorObject;
            m_GrabOffset = Value * length - HandDistance();
        }

        void OnRelease(SelectExitEventArgs a)
        {
            m_Interactor = null;
            if (notches >= 2) SetValue(Mathf.Round(Value * (notches - 1)) / (notches - 1), true);
        }

        void Update()
        {
            if (m_Interactor != null) SetValue((HandDistance() + m_GrabOffset) / length, true);
            else if (springBack && Value > 0f) SetValue(Mathf.MoveTowards(Value, 0f, springSpeed * Time.deltaTime), true);
        }

        public void SetValue(float v, bool notify)
        {
            v = Mathf.Clamp01(v);
            bool changed = !Mathf.Approximately(v, Value);
            Value = v;
            slide.localPosition = m_Start + Vector3.forward * (v * length);
            if (!notify || !changed) return;
            onValueChanged.Invoke(v);
            if (!m_AtEnd && v >= 0.99f) { m_AtEnd = true; Edge(); onReachedEnd.Invoke(); }
            if (v < 0.95f) m_AtEnd = false;
            if (!m_AtStart && v <= 0.01f) { m_AtStart = true; Edge(); onReachedStart.Invoke(); }
            if (v > 0.05f) m_AtStart = false;
        }

        void Edge()
        {
            if (m_Interactor != null) VRHaptics.Send(m_Interactor, endHaptic, 0.05f);
            if (endSound != null) VRAudio.PlayAt(endSound, slide.position, 0.7f);
        }
    }
}
