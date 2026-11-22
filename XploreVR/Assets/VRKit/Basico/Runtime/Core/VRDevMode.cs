using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VRKit
{
    /// <summary>
    /// Modo desarrollador: para probar el juego deprisa. Ponedlo en el XR Origin (o en cualquier objeto).
    ///
    /// Activar / desactivar: mantén pulsados LOS DOS joysticks (hundirlos) un segundo, o F1 en el teclado.
    /// Con el modo activo:
    ///   · Clic en el joystick derecho: velocidad ×1 → ×3 → ×8 → ×20.
    ///   · Clic en el joystick izquierdo: volar (sin gravedad; el joystick lleva hacia donde miras, también arriba y abajo;
    ///     grip derecho = subir, grip izquierdo = bajar).
    ///   · X (mano izquierda): adelantar el reloj 2 horas (si hay ciclo de día y noche). (Y abre el mapa.)
    ///   · B (mano derecha): invencible sí / no.
    /// Por seguridad, solo funciona en el editor y en las Development Build (no en la versión final).
    /// </summary>
    public class VRDevMode : MonoBehaviour
    {
        [SerializeField] bool activeAtStart = false;
        [SerializeField] float[] speeds = { 1f, 3f, 8f, 20f };
        [Tooltip("Solo en el editor y en Development Build.")]
        [SerializeField] bool onlyInDevelopment = true;
        [SerializeField] bool showMessages = true;

        public static bool Active { get; private set; }
        public static bool Flying { get; private set; }

        InputAction m_LeftClick, m_RightClick, m_Toggle;
        HandInput m_Left, m_Right;
        float m_BothSince = -1f;
        int m_SpeedIndex;
        bool m_FlyProvider;
        readonly Dictionary<MonoBehaviour, float> m_BaseSpeeds = new Dictionary<MonoBehaviour, float>();

        bool Allowed { get { return !onlyInDevelopment || Application.isEditor || Debug.isDebugBuild; } }

        void OnEnable()
        {
            m_LeftClick = Button("<XRController>{LeftHand}/{Primary2DAxisClick}");
            m_RightClick = Button("<XRController>{RightHand}/{Primary2DAxisClick}");
            m_Toggle = Button("<Keyboard>/f1");
            m_Left = new HandInput(Hand.Left);
            m_Right = new HandInput(Hand.Right);
        }

        void OnDisable()
        {
            foreach (var a in new[] { m_LeftClick, m_RightClick, m_Toggle }) if (a != null) a.Dispose();
            if (m_Left != null) m_Left.Dispose();
            if (m_Right != null) m_Right.Dispose();
            if (Active) SetActive(false);
        }

        static InputAction Button(string binding)
        {
            var a = new InputAction(binding, InputActionType.Button);
            a.AddBinding(binding);
            a.Enable();
            return a;
        }

        void Start() { if (activeAtStart && Allowed) SetActive(true); }

        void Update()
        {
            if (!Allowed) return;

            // Los dos joysticks hundidos un segundo (o F1): activar / desactivar
            bool both = m_LeftClick.IsPressed() && m_RightClick.IsPressed();
            if (both) { if (m_BothSince < 0f) m_BothSince = Time.unscaledTime; }
            else m_BothSince = -1f;
            if ((both && Time.unscaledTime - m_BothSince > 1f) || m_Toggle.WasPressedThisFrame())
            {
                m_BothSince = float.MaxValue;   // no repetir hasta soltar
                SetActive(!Active);
                return;
            }
            if (!Active || both) return;

            if (m_RightClick.WasReleasedThisFrame()) { m_SpeedIndex = (m_SpeedIndex + 1) % speeds.Length; ApplySpeed(); Say("Velocidad ×" + speeds[m_SpeedIndex]); }
            if (m_LeftClick.WasReleasedThisFrame()) { SetFlying(!Flying); Say(Flying ? "Volando" : "Andando"); }
            if (m_Left.Primary.WasPressedThisFrame() && m_Left.Trigger.ReadValue<float>() < 0.15f && (VRPlayer.Instance == null || VRPlayer.Instance.LocomotionEnabled)) AdvanceTime(2f / 24f);
            if (m_Right.Secondary.WasPressedThisFrame() && PlayerHealth.Instance != null)
            {
                PlayerHealth.Instance.Invincible = !PlayerHealth.Instance.Invincible;
                Say(PlayerHealth.Instance.Invincible ? "Invencible" : "Se puede recibir daño");
            }
            if (Flying) Fly();
        }

        public void SetActive(bool on)
        {
            Active = on;
            if (!on) { m_SpeedIndex = 0; SetFlying(false); }
            else if (m_SpeedIndex == 0 && speeds.Length > 1) m_SpeedIndex = 1;
            ApplySpeed();
            Say(on ? "Modo desarrollador: ON (clic joystick derecho = velocidad, izquierdo = volar, X = +2 h, B = invencible)" : "Modo desarrollador: OFF");
        }

        void ApplySpeed()
        {
            float mult = Active ? speeds[Mathf.Clamp(m_SpeedIndex, 0, speeds.Length - 1)] : 1f;
            TriggerLocomotion.SpeedMultiplier = mult;
            var player = VRPlayer.Instance;
            if (player == null) return;
            // Los proveedores de movimiento de XRI tienen «moveSpeed»: se multiplica su valor original
            foreach (var mb in player.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                var p = mb.GetType().GetProperty("moveSpeed");
                if (p == null || p.PropertyType != typeof(float) || !p.CanWrite) continue;
                float baseSpeed;
                if (!m_BaseSpeeds.TryGetValue(mb, out baseSpeed)) { baseSpeed = (float)p.GetValue(mb); m_BaseSpeeds[mb] = baseSpeed; }
                p.SetValue(mb, baseSpeed * mult);
            }
        }

        void SetFlying(bool on)
        {
            Flying = on;
            var player = VRPlayer.Instance;
            if (player == null) return;
            foreach (var mb in player.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                var t = mb.GetType();
                if (t.Name == "GravityProvider") mb.enabled = !on;
                var fly = t.GetProperty("enableFly");
                if (fly != null && fly.PropertyType == typeof(bool) && fly.CanWrite) { fly.SetValue(mb, on); m_FlyProvider = true; }
                var grav = t.GetProperty("useGravity");
                if (grav != null && grav.PropertyType == typeof(bool) && grav.CanWrite && t.Name.Contains("MoveProvider")) grav.SetValue(mb, !on);
            }
        }

        /// <summary>Volar: subir y bajar con los grips; y con el joystick izquierdo si XRI no vuela por sí mismo.</summary>
        void Fly()
        {
            var player = VRPlayer.Instance;
            if (player == null || player.Head == null) return;
            Vector2 s = m_FlyProvider ? Vector2.zero : m_Left.Stick.ReadValue<Vector2>();
            float up = m_Right.Grip.ReadValue<float>() - m_Left.Grip.ReadValue<float>();
            if (s.sqrMagnitude < 0.02f && Mathf.Abs(up) < 0.1f) return;
            var head = player.Head;
            Vector3 dir = head.forward * s.y + head.right * s.x + Vector3.up * up;
            float mult = speeds[Mathf.Clamp(m_SpeedIndex, 0, speeds.Length - 1)];
            // Volar atraviesa todo: se mueve el XR Origin directamente
            player.transform.position += dir * (3f * mult * Time.deltaTime);
        }

        static void AdvanceTime(float fraction)
        {
            // Cualquier componente con una propiedad TimeOfDay (SkyCycle, VoxelDayNight…)
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                var p = mb.GetType().GetProperty("TimeOfDay");
                if (p == null || p.PropertyType != typeof(float) || !p.CanWrite || !p.CanRead) continue;
                p.SetValue(mb, Mathf.Repeat((float)p.GetValue(mb) + fraction, 1f));
            }
        }

        void Say(string text)
        {
            if (!showMessages) return;
            SubtitleSystem.SayStatic("Desarrollador", text);
            Debug.Log("[VRKit] " + text);
        }
    }
}
