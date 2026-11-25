using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRKit
{
    /// <summary>
    /// Portal: al meter la cabeza en él, apareces en el destino (con un fundido rápido para no marear).
    /// Los objetos lanzados que lo atraviesan también pasan, conservando su velocidad (girada al nuevo portal).
    /// Dos portales enlazados (cada uno destino del otro) funcionan en los dos sentidos.
    /// Necesita un Collider con Is Trigger.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class PortalTeleporter : MonoBehaviour
    {
        [Tooltip("Dónde apareces. Su eje Z (azul) es hacia donde mirarás. Puede ser otro portal.")]
        [SerializeField] Transform destination;
        [Tooltip("Separación hacia delante del destino (para no caer dentro del otro portal).")]
        [SerializeField] float exitOffset = 1f;
        [SerializeField] bool keepPlayerFacing = false;
        [SerializeField] bool teleportObjects = true;
        [SerializeField] float fadeTime = 0.12f;
        [SerializeField] AudioClip sound;

        public UnityEvent onPlayerTeleported = new UnityEvent();

        static float s_LastTeleport = -10f;
        readonly Dictionary<Rigidbody, float> m_Recent = new Dictionary<Rigidbody, float>();
        Collider m_Col;

        void Awake() { m_Col = GetComponent<Collider>(); m_Col.isTrigger = true; }

        void Update()
        {
            var p = VRPlayer.Instance;
            if (destination == null || p == null || p.Head == null) return;
            if (Time.time - s_LastTeleport < 1f) return; // evita ir y volver en bucle entre dos portales
            Vector3 head = p.Head.position;
            if ((m_Col.ClosestPoint(head) - head).sqrMagnitude > 0.0001f) return; // la cabeza no está dentro

            s_LastTeleport = Time.time;
            Vector3 exit = destination.position + destination.forward * exitOffset;
            exit.y = destination.position.y;
            float? yaw = keepPlayerFacing ? (float?)null : destination.eulerAngles.y;
            p.TeleportWithFade(exit, yaw, fadeTime);
            if (sound != null) { VRAudio.PlayAt(sound, transform.position); VRAudio.PlayAt(sound, exit); }
            onPlayerTeleported.Invoke();
        }

        void OnTriggerEnter(Collider other)
        {
            if (!teleportObjects || destination == null) return;
            var rb = other.attachedRigidbody;
            if (rb == null || rb.isKinematic) return;
            float t;
            if (m_Recent.TryGetValue(rb, out t) && Time.time - t < 1f) return;
            var grab = rb.GetComponent<XRGrabInteractable>();
            if (grab != null && grab.isSelected) return;
            if (VRPlayer.Instance != null && rb.transform.IsChildOf(VRPlayer.Instance.transform)) return;

            // Gira la velocidad: lo que entra por delante de este portal sale por delante del otro
            Quaternion turn = destination.rotation * Quaternion.Euler(0f, 180f, 0f) * Quaternion.Inverse(transform.rotation);
            rb.position = destination.position + destination.forward * exitOffset * 0.5f;
            rb.transform.position = rb.position;
            rb.linearVelocity = turn * rb.linearVelocity;
            m_Recent[rb] = Time.time;
            var other2 = destination.GetComponent<PortalTeleporter>();
            if (other2 != null) other2.m_Recent[rb] = Time.time;
        }
    }
}
