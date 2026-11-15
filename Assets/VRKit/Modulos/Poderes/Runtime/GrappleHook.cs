using UnityEngine;
using UnityEngine.Events;

namespace VRKit
{
    /// <summary>
    /// Gancho, telaraña o látigo de energía: apunta con la mano, aprieta el gatillo y una cuerda se engancha
    /// donde apuntas; mientras lo mantienes, te arrastra hacia ese punto. Suelta para soltarte.
    ///  · Pull: te arrastra a velocidad constante (más cómodo).
    ///  · Swing: te balanceas como Spider-Man (más mareante; ponlo como opción avanzada).
    /// Solo se engancha a capas de Grapple Layers (por ejemplo, una capa «Enganchable» en techos y árboles).
    /// Mientras tira, bloquea la locomoción normal (VRPlayer.SetLocomotionEnabled).
    /// </summary>
    public class GrappleHook : MonoBehaviour
    {
        public enum Mode { Pull, Swing }

        [SerializeField] Hand hand = Hand.Right;
        [SerializeField] Mode mode = Mode.Pull;
        [SerializeField] float range = 25f;
        [SerializeField] LayerMask grappleLayers = ~0;
        [SerializeField] float pullSpeed = 9f;
        [Tooltip("Se suelta solo al llegar a esta distancia del punto.")]
        [SerializeField] float arriveDistance = 1.2f;
        [Header("Swing")]
        [SerializeField] float gravity = 12f;
        [SerializeField] float reelSpeed = 2f;
        [Header("Presentación")]
        [SerializeField] LineRenderer rope;
        [SerializeField] AudioClip fireSound;
        [SerializeField] AudioClip attachSound;
        [Tooltip("Muestra un punto donde se engancharía (ayuda a apuntar).")]
        [SerializeField] Transform aimMarker;

        public UnityEvent onAttached = new UnityEvent();
        public UnityEvent onReleased = new UnityEvent();

        public bool IsAttached { get; private set; }

        HandInput m_Input;
        Vector3 m_Point;
        Vector3 m_Velocity;
        float m_RopeLength;

        void OnEnable() { m_Input = new HandInput(hand); if (rope != null) rope.enabled = false; }
        void OnDisable() { if (m_Input != null) m_Input.Dispose(); m_Input = null; Detach(); }

        void Update()
        {
            var p = VRPlayer.Instance;
            if (p == null || m_Input == null) return;
            Transform h = p.GetHand(hand);
            if (h == null) return;
            if (p.IsHolding(hand)) { Detach(); return; }

            RaycastHit hit;
            bool canHit = Physics.Raycast(h.position, h.forward, out hit, range, grappleLayers, QueryTriggerInteraction.Ignore)
                          && !hit.collider.transform.IsChildOf(p.transform);
            if (aimMarker != null)
            {
                aimMarker.gameObject.SetActive(canHit && !IsAttached);
                if (canHit) aimMarker.position = hit.point;
            }

            if (!IsAttached && m_Input.Trigger.WasPressedThisFrame())
            {
                if (fireSound != null) VRAudio.PlayAt(fireSound, h.position, 0.6f);
                if (canHit) Attach(hit.point, h, p);
            }
            if (IsAttached)
            {
                if (!m_Input.Trigger.IsPressed()) { Detach(); return; }
                Tick(h, p);
            }
        }

        void Attach(Vector3 point, Transform h, VRPlayer p)
        {
            IsAttached = true;
            m_Point = point;
            m_Velocity = Vector3.zero;
            m_RopeLength = Vector3.Distance(h.position, point);
            p.SetLocomotionEnabled(false);
            if (rope != null) rope.enabled = true;
            if (attachSound != null) VRAudio.PlayAt(attachSound, point);
            VRHaptics.SendHand(h, 0.6f, 0.1f);
            onAttached.Invoke();
        }

        void Tick(Transform h, VRPlayer p)
        {
            Vector3 body = h.position;
            Vector3 toPoint = m_Point - body;
            if (mode == Mode.Pull)
            {
                if (toPoint.magnitude < arriveDistance) { Detach(); return; }
                p.Move(toPoint.normalized * pullSpeed * Time.deltaTime);
            }
            else
            {
                // Péndulo: gravedad + la cuerda no deja alejarse más de su longitud
                m_Velocity += Vector3.down * gravity * Time.deltaTime;
                m_RopeLength = Mathf.Max(arriveDistance, m_RopeLength - reelSpeed * Time.deltaTime);
                Vector3 next = body + m_Velocity * Time.deltaTime;
                Vector3 off = next - m_Point;
                if (off.magnitude > m_RopeLength)
                {
                    next = m_Point + off.normalized * m_RopeLength;
                    m_Velocity = (next - body) / Time.deltaTime;
                }
                p.Move(next - body);
            }
            if (rope != null)
            {
                rope.positionCount = 2;
                rope.SetPosition(0, h.position);
                rope.SetPosition(1, m_Point);
            }
            if (Time.frameCount % 5 == 0) VRHaptics.SendHand(h, 0.15f, 0.02f);
        }

        public void Detach()
        {
            if (!IsAttached) return;
            IsAttached = false;
            if (VRPlayer.Instance != null) VRPlayer.Instance.SetLocomotionEnabled(true);
            if (rope != null) rope.enabled = false;
            onReleased.Invoke();
        }
    }
}
