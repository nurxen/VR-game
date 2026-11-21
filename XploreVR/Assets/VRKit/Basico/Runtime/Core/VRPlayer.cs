using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRKit
{
    public enum Hand { Left, Right }

    /// <summary>
    /// Punto de acceso único al jugador: cabeza, manos, pies, velocidad de cada mano,
    /// teletransportar, recentrar, altura (modo sentado) y bloquear la locomoción.
    /// Ponlo en el XR Origin. Los demás scripts lo encuentran con VRPlayer.Instance.
    ///
    /// Si no rellenas las referencias, las busca solas por nombre en el rig de Starter Assets
    /// («Main Camera», «Left Controller», «Right Controller», «Camera Offset»).
    /// </summary>
    public class VRPlayer : MonoBehaviour
    {
        public static VRPlayer Instance { get; private set; }

        [Header("Rig (vacío = se buscan solos)")]
        [SerializeField] Transform head;
        [SerializeField] Transform leftHand;
        [SerializeField] Transform rightHand;
        [Tooltip("El objeto «Camera Offset» del XR Origin (para subir o bajar la altura).")]
        [SerializeField] Transform cameraOffset;
        [SerializeField] CharacterController characterController;

        [Header("Locomoción")]
        [Tooltip("Componentes de movimiento (Move, Turn, Teleport, Gravity…). Se desactivan con SetLocomotionEnabled(false).")]
        [SerializeField] Behaviour[] locomotionComponents = new Behaviour[0];

        [Header("Mano dominante")]
        [SerializeField] Hand dominantHand = Hand.Right;

        public Transform Head { get { return head; } }
        public Transform LeftHand { get { return leftHand; } }
        public Transform RightHand { get { return rightHand; } }
        public Hand DominantHand { get { return dominantHand; } set { dominantHand = value; } }
        public CharacterController Controller { get { return characterController; } }

        /// <summary>Punto del suelo justo debajo de la cabeza.</summary>
        public Vector3 FeetPosition
        {
            get { var h = head != null ? head.position : transform.position; return new Vector3(h.x, transform.position.y, h.z); }
        }

        /// <summary>Hacia dónde mira el jugador, sin inclinación (para colocar menús y objetos).</summary>
        public Vector3 HeadForwardFlat
        {
            get
            {
                if (head == null) return transform.forward;
                var f = head.forward; f.y = 0f;
                if (f.sqrMagnitude < 0.001f) { f = head.up; f.y = 0f; }
                return f.normalized;
            }
        }

        /// <summary>Altura de los ojos sobre el suelo.</summary>
        public float EyeHeight { get { return head != null ? head.position.y - transform.position.y : 1.6f; } }

        readonly Vector3[] m_HandVel = new Vector3[2];
        readonly Vector3[] m_LastHandPos = new Vector3[2];
        float m_BaseOffsetY;
        int m_LocomotionLocks;

        void Awake()
        {
            Instance = this;
            AutoFind();
            if (cameraOffset != null) m_BaseOffsetY = cameraOffset.localPosition.y;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void AutoFind()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            if (characterController == null) characterController = GetComponent<CharacterController>();
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                bool isHand = n.Contains("controller") || n.Contains("hand");
                if (leftHand == null && isHand && n.Contains("left") && !n.Contains("interactor")) leftHand = t;
                if (rightHand == null && isHand && n.Contains("right") && !n.Contains("interactor")) rightHand = t;
                if (cameraOffset == null && n == "camera offset") cameraOffset = t;
            }
        }

        void Update()
        {
            TrackVelocity(0, leftHand);
            TrackVelocity(1, rightHand);
        }

        void TrackVelocity(int i, Transform hand)
        {
            if (hand == null || Time.deltaTime <= 0f) return;
            Vector3 v = (hand.position - m_LastHandPos[i]) / Time.deltaTime;
            m_HandVel[i] = Vector3.Lerp(m_HandVel[i], v, 0.5f); // suavizado: el tracking tiembla
            m_LastHandPos[i] = hand.position;
        }

        // ================================================================ Manos

        public Transform GetHand(Hand h) { return h == Hand.Left ? leftHand : rightHand; }
        public Transform GetDominantHand() { return GetHand(dominantHand); }
        public Transform GetOffHand() { return GetHand(dominantHand == Hand.Left ? Hand.Right : Hand.Left); }

        /// <summary>Velocidad de la mano en m/s (para golpes, lanzamientos y gestos).</summary>
        public Vector3 GetHandVelocity(Hand h) { return m_HandVel[h == Hand.Left ? 0 : 1]; }

        /// <summary>True si esa mano está sujetando algo (para no lanzar hechizos con una espada en la mano).</summary>
        public bool IsHolding(Hand h)
        {
            var hand = GetHand(h);
            if (hand == null) return false;
            foreach (var i in hand.GetComponentsInChildren<XRBaseInteractor>())
                if (i.hasSelection && !(i is XRSocketInteractor)) return true;
            return false;
        }

        // ================================================================ Mover al jugador

        /// <summary>Lleva al jugador a un punto del suelo. Si se da yaw, también lo gira para mirar en esa dirección.</summary>
        public void TeleportTo(Vector3 feetPosition, float? yaw = null)
        {
            bool cc = characterController != null && characterController.enabled;
            if (cc) characterController.enabled = false;

            if (yaw.HasValue && head != null)
            {
                float delta = Mathf.DeltaAngle(head.eulerAngles.y, yaw.Value);
                transform.RotateAround(head.position, Vector3.up, delta);
            }
            transform.position += feetPosition - FeetPosition;

            if (cc) characterController.enabled = true;
        }

        /// <summary>Teletransporte con fundido a negro (más cómodo).</summary>
        public void TeleportWithFade(Vector3 feetPosition, float? yaw = null, float fade = 0.15f)
        {
            StartCoroutine(FadeTeleport(feetPosition, yaw, fade));
        }

        System.Collections.IEnumerator FadeTeleport(Vector3 pos, float? yaw, float fade)
        {
            var f = ScreenFader.Instance;
            if (f != null) yield return f.FadeTo(1f, fade);
            TeleportTo(pos, yaw);
            if (f != null) yield return f.FadeTo(0f, fade);
        }

        /// <summary>Recolocar al jugador en un punto mirando hacia su eje Z (botón «Recentrar»).</summary>
        public void Recenter(Transform target)
        {
            if (target != null) TeleportTo(target.position, target.eulerAngles.y);
        }

        /// <summary>Mueve al jugador respetando paredes si hay CharacterController.</summary>
        public void Move(Vector3 delta)
        {
            if (characterController != null && characterController.enabled) characterController.Move(delta);
            else transform.position += delta;
        }

        /// <summary>Sube o baja la vista (modo sentado: +0,4 m aprox.).</summary>
        public void SetHeightOffset(float meters)
        {
            if (cameraOffset == null) return;
            var p = cameraOffset.localPosition;
            p.y = m_BaseOffsetY + meters;
            cameraOffset.localPosition = p;
        }

        /// <summary>
        /// Bloquea o libera la locomoción (andar, girar, teletransporte). Lleva la cuenta:
        /// si dos scripts la bloquean, hace falta que los dos la liberen.
        /// </summary>
        /// <summary>False si algún script ha bloqueado la locomoción (cargando, menú abierto…).</summary>
        public bool LocomotionEnabled { get { return m_LocomotionLocks == 0; } }

        public void SetLocomotionEnabled(bool enabled)
        {
            m_LocomotionLocks = Mathf.Max(0, m_LocomotionLocks + (enabled ? -1 : 1));
            bool on = m_LocomotionLocks == 0;
            foreach (var b in locomotionComponents) if (b != null) b.enabled = on;
        }

        public void AddLocomotionComponents(IEnumerable<Behaviour> list)
        {
            var l = new List<Behaviour>(locomotionComponents);
            l.AddRange(list);
            locomotionComponents = l.ToArray();
        }
    }
}
