using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRKit
{
    /// <summary>
    /// Devuelve un objeto a su sitio si se cae del mapa, se aleja demasiado
    /// o lleva un rato tirado sin que nadie lo coja. Imprescindible para objetos clave
    /// (una llave, el arco) que el jugador puede perder.
    /// </summary>
    public class ReturnToSpawn : MonoBehaviour
    {
        [Tooltip("Si el objeto cae por debajo de esta altura, vuelve.")]
        [SerializeField] float minHeight = -5f;
        [Tooltip("Distancia máxima al punto de origen (0 = sin límite).")]
        [SerializeField] float maxDistance = 0f;
        [Tooltip("Segundos tirado fuera de su sitio antes de volver (0 = nunca).")]
        [SerializeField] float idleReturnTime = 0f;
        [Tooltip("Punto al que vuelve. Vacío = la posición inicial.")]
        [SerializeField] Transform spawnPoint;

        Vector3 m_StartPos;
        Quaternion m_StartRot;
        Rigidbody m_Rigidbody;
        XRBaseInteractable m_Interactable;
        float m_IdleTimer;

        void Awake()
        {
            m_StartPos = transform.position;
            m_StartRot = transform.rotation;
            m_Rigidbody = GetComponent<Rigidbody>();
            m_Interactable = GetComponent<XRBaseInteractable>();
        }

        void Update()
        {
            bool held = m_Interactable != null && m_Interactable.isSelected;
            Vector3 home = spawnPoint != null ? spawnPoint.position : m_StartPos;

            if (transform.position.y < minHeight) { Return(); return; }
            if (!held && maxDistance > 0f && Vector3.Distance(transform.position, home) > maxDistance) { Return(); return; }

            if (idleReturnTime > 0f && !held && Vector3.Distance(transform.position, home) > 0.05f)
            {
                m_IdleTimer += Time.deltaTime;
                if (m_IdleTimer >= idleReturnTime) Return();
            }
            else
            {
                m_IdleTimer = 0f;
            }
        }

        public void Return()
        {
            m_IdleTimer = 0f;
            Vector3 pos = spawnPoint != null ? spawnPoint.position : m_StartPos;
            Quaternion rot = spawnPoint != null ? spawnPoint.rotation : m_StartRot;

            if (m_Rigidbody != null)
            {
                if (!m_Rigidbody.isKinematic)
                {
                    m_Rigidbody.linearVelocity = Vector3.zero;
                    m_Rigidbody.angularVelocity = Vector3.zero;
                }
                m_Rigidbody.position = pos;
                m_Rigidbody.rotation = rot;
            }
            transform.SetPositionAndRotation(pos, rot);
        }
    }
}
