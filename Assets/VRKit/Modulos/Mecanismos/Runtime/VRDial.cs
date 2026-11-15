using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRKit
{
    /// <summary>
    /// Rueda o pomo que se gira con la muñeca: caja fuerte, radio, válvula, timón, temporizador…
    /// Gira alrededor del eje Y local de «Knob». Coge el pomo y gira la mano como si abrieras un bote.
    ///
    /// Con Steps > 0 encaja en posiciones (con un «clic» en el mando) y avisa con onStepChanged.
    /// Sin límites (Limited desactivado) puede dar vueltas sin fin (válvulas, manivelas).
    /// </summary>
    public class VRDial : MonoBehaviour
    {
        [SerializeField] Transform knob;
        [SerializeField] XRBaseInteractable interactable;
        [SerializeField] bool limited = true;
        [SerializeField] float minAngle = 0f;
        [SerializeField] float maxAngle = 270f;
        [Tooltip("Número de posiciones (0 = giro continuo). Una caja fuerte de 0 a 9: 10.")]
        [SerializeField] int steps = 10;
        [Tooltip("Multiplica el giro de la muñeca (2 = media vuelta de muñeca da una vuelta).")]
        [SerializeField] float sensitivity = 1.5f;

        [Header("Respuesta")]
        [SerializeField, Range(0f, 1f)] float stepHaptic = 0.25f;
        [SerializeField] AudioClip stepSound;

        public UnityEvent<float> onValueChanged = new UnityEvent<float>();
        public UnityEvent<int> onStepChanged = new UnityEvent<int>();
        public UnityEvent onReleased = new UnityEvent();

        /// <summary>Ángulo actual (sin límites puede pasar de 360).</summary>
        public float Angle { get; private set; }
        public float Value { get { return limited ? Mathf.InverseLerp(minAngle, maxAngle, Angle) : Mathf.Repeat(Angle, 360f) / 360f; } }
        public int Step { get; private set; }
        /// <summary>Vueltas completas (para manivelas: «gira 3 vueltas para subir el puente»).</summary>
        public float Turns { get { return Angle / 360f; } }

        IXRSelectInteractor m_Interactor;
        Quaternion m_BaseRot;
        float m_LastHandAngle;

        void Awake()
        {
            if (knob == null) knob = transform;
            if (interactable == null) interactable = GetComponentInChildren<XRBaseInteractable>();
            m_BaseRot = knob.localRotation;
            Angle = limited ? minAngle : 0f;
            Apply(false);
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

        void OnGrab(SelectEnterEventArgs a)
        {
            m_Interactor = a.interactorObject;
            m_LastHandAngle = HandAngle();
        }

        void OnRelease(SelectExitEventArgs a)
        {
            m_Interactor = null;
            if (steps > 0) { Angle = StepToAngle(Step); Apply(false); }
            onReleased.Invoke();
        }

        void Update()
        {
            if (m_Interactor == null) return;
            float a = HandAngle();
            float delta = Mathf.DeltaAngle(m_LastHandAngle, a) * sensitivity;
            m_LastHandAngle = a;
            if (Mathf.Abs(delta) < 0.01f) return;
            Angle += delta;
            if (limited) Angle = Mathf.Clamp(Angle, minAngle, maxAngle);
            Apply(true);
        }

        /// <summary>Giro de la mano alrededor del eje del pomo.</summary>
        float HandAngle()
        {
            var attach = m_Interactor.GetAttachTransform(interactable);
            Transform frame = knob.parent != null ? knob.parent : knob;
            Vector3 r = Quaternion.Inverse(m_BaseRot) * frame.InverseTransformDirection(attach.right);
            return Mathf.Atan2(-r.z, r.x) * Mathf.Rad2Deg;
        }

        float StepAngle { get { return limited ? (maxAngle - minAngle) / Mathf.Max(1, steps - 1) : 360f / Mathf.Max(1, steps); } }
        float StepToAngle(int s) { return (limited ? minAngle : 0f) + s * StepAngle; }

        public void SetStep(int s)
        {
            Angle = StepToAngle(s);
            Apply(true);
        }

        void Apply(bool notify)
        {
            knob.localRotation = m_BaseRot * Quaternion.Euler(0f, Angle, 0f);
            if (!notify) return;
            onValueChanged.Invoke(Value);
            if (steps <= 0) return;
            int s = Mathf.RoundToInt((Angle - (limited ? minAngle : 0f)) / StepAngle);
            if (!limited) s = ((s % steps) + steps) % steps;
            if (s != Step)
            {
                Step = s;
                if (m_Interactor != null) VRHaptics.Send(m_Interactor, stepHaptic, 0.03f);
                if (stepSound != null) VRAudio.PlayAt(stepSound, knob.position, 0.6f, 0.1f);
                onStepChanged.Invoke(Step);
            }
        }
    }
}
