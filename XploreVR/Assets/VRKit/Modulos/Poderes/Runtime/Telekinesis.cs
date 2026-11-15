using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRKit
{
    /// <summary>
    /// Telequinesis con la mano vacía. Apunta a un objeto con física (se ilumina) y:
    ///  · Pull (guantes de gravedad, como Half-Life: Alyx): aprieta el grip y da un tirón de muñeca hacia ti.
    ///    El objeto sale volando en arco hacia tu mano y lo puedes atrapar.
    ///  · Levitate: mantén el gatillo y el objeto flota delante de tu mano y la sigue; al soltar sale lanzado
    ///    con el movimiento de la mano. El joystick lo acerca o lo aleja.
    /// Funciona con cualquier Rigidbody (ligero) o solo con los que tengan XR Grab Interactable.
    /// </summary>
    public class Telekinesis : MonoBehaviour
    {
        [SerializeField] Hand hand = Hand.Left;
        [SerializeField] bool allowPull = true;
        [SerializeField] bool allowLevitate = true;

        [Header("Apuntar")]
        [SerializeField] float range = 12f;
        [SerializeField] float aimRadius = 0.2f;
        [SerializeField] float maxMass = 20f;
        [Tooltip("Solo objetos que se pueden coger (con XR Grab Interactable).")]
        [SerializeField] bool onlyGrabbables = true;
        [SerializeField] LayerMask layers = ~0;
        [Tooltip("No actúa sobre objetos más cerca que esto (para no estorbar al agarre normal).")]
        [SerializeField] float minDistance = 0.7f;

        [Header("Tirón (Pull)")]
        [Tooltip("Velocidad de la mano hacia ti (m/s) que cuenta como tirón.")]
        [SerializeField] float flickSpeed = 1.6f;
        [Tooltip("Segundos que tarda el objeto en llegar a la mano.")]
        [SerializeField] float flightTime = 0.55f;

        [Header("Levitar")]
        [SerializeField] float followStrength = 12f;
        [SerializeField] float throwMultiplier = 2f;
        [SerializeField] float distanceSpeed = 3f;

        [Header("Respuesta")]
        [SerializeField] LineRenderer beam;
        [SerializeField] AudioClip pullSound;
        [SerializeField, Range(0f, 1f)] float haptic = 0.3f;

        public UnityEvent<GameObject> onPulled = new UnityEvent<GameObject>();
        public UnityEvent<GameObject> onLevitateStart = new UnityEvent<GameObject>();
        public UnityEvent<GameObject> onLevitateEnd = new UnityEvent<GameObject>();

        public Rigidbody Target { get; private set; }
        public bool IsLevitating { get { return m_Levitated != null; } }

        HandInput m_Input;
        Rigidbody m_Locked;      // objetivo fijado con el grip (esperando el tirón)
        Rigidbody m_Levitated;
        bool m_HadGravity;
        float m_HoldDistance;
        Vector3 m_LockedHandPos;

        void OnEnable() { m_Input = new HandInput(hand); }
        void OnDisable()
        {
            if (m_Input != null) m_Input.Dispose(); m_Input = null;
            EndLevitate(Vector3.zero);
            SetTarget(null);
        }

        void Update()
        {
            var p = VRPlayer.Instance;
            if (p == null || m_Input == null) return;
            Transform h = p.GetHand(hand);
            if (h == null) return;
            if (p.IsHolding(hand)) { EndLevitate(Vector3.zero); m_Locked = null; SetTarget(null); ShowBeam(false, h, Vector3.zero); return; }

            if (m_Levitated != null) { UpdateLevitate(h, p); return; }

            // ---- Apuntar
            if (m_Locked == null) SetTarget(FindTarget(h));

            // ---- Levitar
            if (allowLevitate && Target != null && m_Input.Trigger.WasPressedThisFrame()) { StartLevitate(h); return; }

            // ---- Tirón: fija con el grip y espera el gesto
            if (allowPull)
            {
                if (Target != null && m_Input.Grip.WasPressedThisFrame()) { m_Locked = Target; m_LockedHandPos = h.position; }
                if (m_Locked != null)
                {
                    if (!m_Input.Grip.IsPressed()) m_Locked = null;
                    else
                    {
                        Vector3 toHead = (p.Head.position - h.position).normalized;
                        Vector3 vel = p.GetHandVelocity(hand);
                        if (Vector3.Dot(vel, toHead) > flickSpeed || vel.y > flickSpeed * 1.2f) Pull(m_Locked, h);
                    }
                }
            }
            ShowBeam(Target != null, h, Target != null ? Target.worldCenterOfMass : Vector3.zero);
        }

        Rigidbody FindTarget(Transform h)
        {
            RaycastHit hit;
            if (!Physics.SphereCast(h.position, aimRadius, h.forward, out hit, range, layers, QueryTriggerInteraction.Ignore)) return null;
            var rb = hit.rigidbody;
            if (rb == null || rb.isKinematic || rb.mass > maxMass || hit.distance < minDistance) return null;
            if (VRPlayer.Instance != null && rb.transform.IsChildOf(VRPlayer.Instance.transform)) return null;
            var grab = rb.GetComponent<XRGrabInteractable>();
            if (onlyGrabbables && grab == null) return null;
            if (grab != null && grab.isSelected) return null;
            return rb;
        }

        void SetTarget(Rigidbody rb)
        {
            if (rb == Target) return;
            if (Target != null) GrabHighlight.Set(Target.gameObject, false);
            Target = rb;
            if (Target != null) { GrabHighlight.Set(Target.gameObject, true); var p = VRPlayer.Instance; if (p != null) VRHaptics.SendHand(p.GetHand(hand), haptic * 0.4f, 0.02f); }
        }

        void Pull(Rigidbody rb, Transform h)
        {
            m_Locked = null;
            // Velocidad para llegar a la mano en flightTime segundos siguiendo una parábola
            Vector3 to = h.position + Vector3.up * 0.1f;
            Vector3 from = rb.worldCenterOfMass;
            float t = flightTime;
            Vector3 v = (to - from) / t - 0.5f * Physics.gravity * t;
            if (!rb.useGravity) v = (to - from) / t;
            rb.linearVelocity = v;
            rb.angularVelocity = Random.insideUnitSphere * 2f;
            if (pullSound != null) VRAudio.PlayAt(pullSound, from);
            VRHaptics.SendHand(h, haptic + 0.2f, 0.08f);
            onPulled.Invoke(rb.gameObject);
            SetTarget(null);
        }

        void StartLevitate(Transform h)
        {
            m_Levitated = Target;
            m_HadGravity = m_Levitated.useGravity;
            m_Levitated.useGravity = false;
            m_HoldDistance = Mathf.Clamp(Vector3.Distance(h.position, m_Levitated.worldCenterOfMass), 1f, range);
            VRHaptics.SendHand(h, haptic, 0.06f);
            onLevitateStart.Invoke(m_Levitated.gameObject);
        }

        void UpdateLevitate(Transform h, VRPlayer p)
        {
            if (m_Levitated == null) return;
            m_HoldDistance = Mathf.Clamp(m_HoldDistance + m_Input.Stick.ReadValue<Vector2>().y * distanceSpeed * Time.deltaTime, minDistance, range);
            Vector3 goal = h.position + h.forward * m_HoldDistance;
            Vector3 v = (goal - m_Levitated.worldCenterOfMass) * followStrength;
            m_Levitated.linearVelocity = Vector3.ClampMagnitude(v, 25f);
            m_Levitated.angularVelocity *= 0.9f;
            ShowBeam(true, h, m_Levitated.worldCenterOfMass);
            if (Time.frameCount % 8 == 0) VRHaptics.SendHand(h, haptic * 0.3f, 0.02f);
            if (!m_Input.Trigger.IsPressed()) EndLevitate(p.GetHandVelocity(hand) * throwMultiplier);
        }

        void EndLevitate(Vector3 throwVelocity)
        {
            if (m_Levitated == null) return;
            m_Levitated.useGravity = m_HadGravity;
            if (throwVelocity.sqrMagnitude > 1f) m_Levitated.linearVelocity = throwVelocity;
            var go = m_Levitated.gameObject;
            m_Levitated = null;
            onLevitateEnd.Invoke(go);
            ShowBeam(false, null, Vector3.zero);
        }

        void ShowBeam(bool on, Transform h, Vector3 to)
        {
            if (beam == null) return;
            beam.enabled = on;
            if (!on || h == null) return;
            beam.positionCount = 2;
            beam.SetPosition(0, h.position);
            beam.SetPosition(1, to);
        }
    }
}
