using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace VRKit
{
    /// <summary>
    /// Tiempo bala: el mundo va a cámara lenta y tú no (tu cabeza y tus manos siguen a velocidad normal,
    /// porque las mueve el seguimiento de las gafas, no la física).
    ///  · Se activa con un botón (por defecto Y en el mando izquierdo, o T en el teclado) o llamando a Activate().
    ///  · Gasta energía mientras dura y la recupera poco a poco.
    ///  · Ajusta fixedDeltaTime para que la física siga siendo suave, y baja el tono del sonido.
    /// Respeta la pausa del GameManager.
    /// </summary>
    public class TimeController : MonoBehaviour
    {
        public static TimeController Instance { get; private set; }

        [Header("Cámara lenta")]
        [Range(0.05f, 1f)] [SerializeField] float slowScale = 0.3f;
        [Tooltip("Segundos (reales) que tarda en entrar y salir de la cámara lenta.")]
        [SerializeField] float blendTime = 0.25f;

        [Header("Energía")]
        [SerializeField] float maxEnergy = 5f;
        [Tooltip("Energía que recupera por segundo real cuando no se usa.")]
        [SerializeField] float rechargePerSecond = 0.5f;
        [Tooltip("Energía mínima para poder activarlo.")]
        [SerializeField] float minEnergyToStart = 1f;

        [Header("Activación")]
        [Tooltip("Mantener el botón = cámara lenta. Si no, cada pulsación la enciende o apaga.")]
        [SerializeField] bool holdToActivate = false;
        [SerializeField] string binding = "<XRController>{LeftHand}/{SecondaryButton}";
        [SerializeField] string keyboardBinding = "<Keyboard>/t";

        [Header("Sonido")]
        [Tooltip("Opcional: parámetro expuesto de tono (Pitch) del mezclador, para que el sonido también vaya lento.")]
        [SerializeField] AudioMixer mixer;
        [SerializeField] string pitchParam = "MasterPitch";

        public UnityEvent onSlowStart = new UnityEvent();
        public UnityEvent onSlowEnd = new UnityEvent();

        public bool IsActive { get; private set; }
        public float Energy { get; private set; }
        public float Energy01 { get { return maxEnergy > 0f ? Energy / maxEnergy : 0f; } }

        InputAction m_Action;
        float m_Current = 1f;
        float m_BaseFixed;

        void Awake()
        {
            Instance = this;
            Energy = maxEnergy;
            m_BaseFixed = Time.fixedDeltaTime;
        }

        void OnEnable()
        {
            m_Action = new InputAction("Tiempo bala", InputActionType.Button);
            if (!string.IsNullOrEmpty(binding)) m_Action.AddBinding(binding);
            if (!string.IsNullOrEmpty(keyboardBinding)) m_Action.AddBinding(keyboardBinding);
            m_Action.Enable();
        }

        void OnDisable()
        {
            if (m_Action != null) { m_Action.Disable(); m_Action.Dispose(); m_Action = null; }
            if (IsActive) Deactivate();
            Apply(1f);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            var gm = GameManager.Instance;
            bool paused = gm != null && gm.State == GameState.Paused;
            if (paused) return; // la pausa manda
            if (gm != null && gm.State != GameState.Playing)
            {
                // Fin de partida: todo vuelve a velocidad normal
                if (IsActive) Deactivate();
                if (!Mathf.Approximately(m_Current, 1f) || !Mathf.Approximately(Time.fixedDeltaTime, m_BaseFixed)) { m_Current = 1f; Apply(1f); }
                return;
            }

            if (m_Action != null)
            {
                if (holdToActivate)
                {
                    if (m_Action.IsPressed() && !IsActive) Activate();
                    else if (!m_Action.IsPressed() && IsActive) Deactivate();
                }
                else if (m_Action.WasPressedThisFrame()) { if (IsActive) Deactivate(); else Activate(); }
            }

            float dt = Time.unscaledDeltaTime;
            if (IsActive)
            {
                Energy -= dt;
                if (Energy <= 0f) { Energy = 0f; Deactivate(); }
            }
            else Energy = Mathf.Min(maxEnergy, Energy + rechargePerSecond * dt);

            float target = IsActive ? slowScale : 1f;
            m_Current = blendTime > 0f ? Mathf.MoveTowards(m_Current, target, dt / blendTime) : target;
            Apply(m_Current);
        }

        public void Activate()
        {
            if (IsActive || Energy < minEnergyToStart) return;
            IsActive = true;
            onSlowStart.Invoke();
        }

        public void Deactivate()
        {
            if (!IsActive) return;
            IsActive = false;
            onSlowEnd.Invoke();
        }

        /// <summary>Cámara lenta durante unos segundos sin gastar energía (para un momento épico scriptado).</summary>
        public void SlowFor(float realSeconds)
        {
            StopAllCoroutines();
            StartCoroutine(SlowRoutine(realSeconds));
        }

        System.Collections.IEnumerator SlowRoutine(float s)
        {
            float saved = Energy;
            IsActive = true; onSlowStart.Invoke();
            yield return new WaitForSecondsRealtime(s);
            Energy = saved;
            Deactivate();
        }

        void Apply(float scale)
        {
            Time.timeScale = scale;
            Time.fixedDeltaTime = m_BaseFixed * scale;
            if (mixer != null) mixer.SetFloat(pitchParam, Mathf.Lerp(0.6f, 1f, Mathf.InverseLerp(slowScale, 1f, scale)));
        }
    }
}
