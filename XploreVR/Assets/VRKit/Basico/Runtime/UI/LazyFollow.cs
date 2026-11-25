using UnityEngine;

namespace VRKit
{
    /// <summary>
    /// Mantiene un objeto (panel, subtítulos, marcador) delante de la vista, pero «perezoso»:
    /// solo se recoloca cuando miras lejos de él, y lo hace suavemente. Pegar la interfaz a la cabeza
    /// (que se mueva exactamente con ella) marea; esto no.
    /// </summary>
    public class LazyFollow : MonoBehaviour
    {
        [SerializeField] Transform head;
        [SerializeField] float distance = 1.4f;
        [Tooltip("Altura respecto a los ojos (negativo = más abajo; los subtítulos van a -0,35).")]
        [SerializeField] float heightOffset = -0.2f;
        [Tooltip("Grados que puedes apartar la vista antes de que te siga.")]
        [SerializeField] float angleThreshold = 25f;
        [SerializeField] float followSpeed = 4f;
        [Tooltip("Inclinarse para quedar de frente a los ojos.")]
        [SerializeField] bool faceHead = true;

        bool m_Moving;

        void Start()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            Snap();
        }

        void OnEnable() { if (head != null) Snap(); }

        /// <summary>Cambiar distancia y altura por código.</summary>
        public void Configure(float newDistance, float newHeightOffset)
        {
            distance = newDistance;
            heightOffset = newHeightOffset;
        }

        public void Snap() { if (head != null) { transform.position = Target(); Face(); } }

        Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward; }

        Vector3 Target() { return head.position + Flat(head.forward) * distance + Vector3.up * heightOffset; }

        void LateUpdate()
        {
            if (head == null) return;
            float angle = Vector3.Angle(Flat(head.forward), Flat(transform.position - head.position));
            if (angle > angleThreshold || Vector3.Distance(transform.position, Target()) > distance * 0.6f) m_Moving = true;
            if (m_Moving)
            {
                transform.position = Vector3.Lerp(transform.position, Target(), 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime));
                if (Vector3.Distance(transform.position, Target()) < 0.02f) m_Moving = false;
            }
            Face();
        }

        void Face()
        {
            Vector3 d = transform.position - head.position;
            if (!faceHead) d.y = 0f;
            if (d.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(d);
        }
    }
}
