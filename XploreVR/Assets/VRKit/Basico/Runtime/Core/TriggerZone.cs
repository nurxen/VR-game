using UnityEngine;
using UnityEngine.Events;

namespace VRKit
{
    /// <summary>
    /// Zona invisible que lanza eventos cuando algo entra o sale.
    /// Sirve para checkpoints, zonas de muerte, abrir puertas, empezar un diálogo, etc.
    /// Necesita un Collider marcado como "Is Trigger".
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class TriggerZone : MonoBehaviour
    {
        [Tooltip("Solo reacciona a objetos con este Tag (por ejemplo Player). Vacío = cualquier cosa.")]
        [SerializeField] string requiredTag = "Player";
        [Tooltip("Solo se dispara la primera vez.")]
        [SerializeField] bool triggerOnce = false;

        public UnityEvent<GameObject> onEnter = new UnityEvent<GameObject>();
        public UnityEvent<GameObject> onExit = new UnityEvent<GameObject>();

        bool m_Triggered;

        void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (triggerOnce && m_Triggered) return;
            var go = Resolve(other);
            if (go == null) return;
            m_Triggered = true;
            onEnter.Invoke(go);
        }

        void OnTriggerExit(Collider other)
        {
            var go = Resolve(other);
            if (go != null) onExit.Invoke(go);
        }

        GameObject Resolve(Collider other)
        {
            var go = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
            if (string.IsNullOrEmpty(requiredTag)) return go;
            if (go.CompareTag(requiredTag)) return go;
            if (other.CompareTag(requiredTag)) return other.gameObject;
            return null;
        }
    }
}
