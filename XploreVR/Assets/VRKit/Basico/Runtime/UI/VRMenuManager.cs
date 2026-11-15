using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace VRKit
{
    /// <summary>
    /// Menú que aparece flotando delante del jugador: pausa, opciones, fin de partida, victoria...
    /// Se abre y cierra con el botón Menú del mando izquierdo (o Escape en el editor).
    /// Cada "pantalla" del menú es un panel hijo del Canvas; este script enseña uno y oculta los demás.
    ///
    /// Montaje: Canvas en modo World Space con el componente "Tracked Device Graphic Raycaster"
    /// (ver README). Arrástralo a Menu Root.
    /// </summary>
    public class VRMenuManager : MonoBehaviour
    {
        [SerializeField] GameObject menuRoot;
        [Tooltip("Panel que se muestra al abrir con el botón (normalmente Pausa).")]
        [SerializeField] GameObject defaultPanel;
        [Tooltip("Todas las pantallas del menú. Solo una se ve a la vez.")]
        [SerializeField] GameObject[] panels = new GameObject[0];

        [Header("Botón para abrir")]
        [Tooltip("Acción de entrada (opcional). Vacío = botón Menú del mando izquierdo + Escape.")]
        [SerializeField] InputActionReference toggleAction;
        [Tooltip("Permitir abrir/cerrar con el botón (desactívalo en la escena de menú principal).")]
        [SerializeField] bool allowToggle = true;

        [Header("Colocación")]
        [SerializeField] float distance = 1.5f;
        [SerializeField] float heightOffset = -0.1f;
        [Tooltip("El menú se queda fijo en el mundo (true) o sigue la mirada (false).")]
        [SerializeField] bool stayInPlace = true;

        [Header("Comportamiento")]
        [Tooltip("Pausar la partida (GameManager) mientras está abierto.")]
        [SerializeField] bool pauseGame = true;
        [Tooltip("Componentes a desactivar con el menú abierto (por ejemplo la locomoción).")]
        [SerializeField] Behaviour[] disableWhileOpen = new Behaviour[0];
        [Tooltip("Abrir el menú al empezar la escena (menú principal).")]
        [SerializeField] bool openOnStart = false;

        public UnityEvent onOpened = new UnityEvent();
        public UnityEvent onClosed = new UnityEvent();

        InputAction m_Action;
        InputAction m_FallbackAction;
        Transform m_Head;

        public bool IsOpen { get { return menuRoot != null && menuRoot.activeSelf; } }

        void Awake()
        {
            if (menuRoot != null) menuRoot.SetActive(false);
        }

        void OnEnable()
        {
            if (toggleAction != null && toggleAction.action != null)
            {
                m_Action = toggleAction.action;
            }
            else
            {
                m_FallbackAction = new InputAction("VRKit Menu", InputActionType.Button);
                m_FallbackAction.AddBinding("<XRController>{LeftHand}/{MenuButton}");
                m_FallbackAction.AddBinding("<Keyboard>/escape");
                m_Action = m_FallbackAction;
            }
            m_Action.performed += OnTogglePressed;
            m_Action.Enable();
        }

        void OnDisable()
        {
            if (m_Action != null) m_Action.performed -= OnTogglePressed;
            if (m_FallbackAction != null) { m_FallbackAction.Disable(); m_FallbackAction.Dispose(); m_FallbackAction = null; }
        }

        void Start()
        {
            if (openOnStart) Open();
        }

        void LateUpdate()
        {
            if (IsOpen && !stayInPlace) PlaceInFront();
        }

        void OnTogglePressed(InputAction.CallbackContext ctx)
        {
            if (!allowToggle) return;
            var gm = GameManager.Instance;
            // No se cierra con el botón si la partida ha terminado (hay que elegir una opción)
            if (gm != null && (gm.State == GameState.GameOver || gm.State == GameState.Victory)) return;
            Toggle();
        }

        public void Toggle()
        {
            if (IsOpen) Close(); else Open();
        }

        public void Open() { OpenWithPanel(defaultPanel); }

        /// <summary>Abre el menú enseñando un panel concreto (por ejemplo, Game Over desde GameManager.onGameOver).</summary>
        public void OpenWithPanel(GameObject panel)
        {
            if (menuRoot == null) return;
            PlaceInFront();
            menuRoot.SetActive(true);
            ShowPanel(panel != null ? panel : defaultPanel);
            foreach (var b in disableWhileOpen) if (b != null) b.enabled = false;
            if (pauseGame && GameManager.Instance != null) GameManager.Instance.Pause();
            onOpened.Invoke();
        }

        public void Close()
        {
            if (menuRoot == null || !menuRoot.activeSelf) return;
            menuRoot.SetActive(false);
            foreach (var b in disableWhileOpen) if (b != null) b.enabled = true;
            if (pauseGame && GameManager.Instance != null) GameManager.Instance.Resume();
            onClosed.Invoke();
        }

        /// <summary>Cambia de pantalla dentro del menú (botones "Opciones", "Volver"...).</summary>
        public void ShowPanel(GameObject panel)
        {
            foreach (var p in panels) if (p != null) p.SetActive(p == panel);
            if (panel != null) panel.SetActive(true);
        }

        void PlaceInFront()
        {
            if (m_Head == null && Camera.main != null) m_Head = Camera.main.transform;
            if (m_Head == null || menuRoot == null) return;
            Vector3 fwd = m_Head.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = m_Head.up; // mirando justo arriba o abajo
            fwd.y = 0f;
            fwd.Normalize();
            Vector3 pos = m_Head.position + fwd * distance + Vector3.up * heightOffset;
            menuRoot.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd));
        }
    }
}
