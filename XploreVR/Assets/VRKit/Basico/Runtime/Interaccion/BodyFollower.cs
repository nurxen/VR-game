using UnityEngine;

namespace VRKit
{
    /// <summary>
    /// Hace que un objeto siga al cuerpo del jugador a la altura de la cintura:
    /// cinturón de inventario, fundas, carcaj a la espalda, bolsa de pociones...
    /// Pon los sockets (TagSocketInteractor / XRSocketInteractor) como hijos de este objeto.
    /// </summary>
    public class BodyFollower : MonoBehaviour
    {
        [Tooltip("La cámara del XR Origin. Vacío = Camera.main.")]
        [SerializeField] Transform head;
        [Tooltip("Distancia por debajo de los ojos (0.6 ≈ cintura de un adulto).")]
        [SerializeField] float distanceBelowHead = 0.6f;
        [Tooltip("Desplazamiento hacia delante respecto al cuerpo.")]
        [SerializeField] float forwardOffset = 0f;
        [Tooltip("Solo gira cuando la cabeza se desvía más de estos grados (evita que tiemble).")]
        [SerializeField] float yawThreshold = 40f;
        [SerializeField] float turnSpeed = 6f;

        float m_Yaw;
        bool m_Turning;

        void Start()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            if (head != null) m_Yaw = head.eulerAngles.y;
        }

        void LateUpdate()
        {
            if (head == null) return;

            float headYaw = head.eulerAngles.y;
            float delta = Mathf.DeltaAngle(m_Yaw, headYaw);
            if (Mathf.Abs(delta) > yawThreshold) m_Turning = true;
            if (m_Turning)
            {
                m_Yaw = Mathf.LerpAngle(m_Yaw, headYaw, Time.deltaTime * turnSpeed);
                if (Mathf.Abs(Mathf.DeltaAngle(m_Yaw, headYaw)) < 2f) m_Turning = false;
            }

            Quaternion bodyRot = Quaternion.Euler(0f, m_Yaw, 0f);
            Vector3 pos = head.position + Vector3.down * distanceBelowHead + bodyRot * Vector3.forward * forwardOffset;
            transform.SetPositionAndRotation(pos, bodyRot);
        }
    }
}
