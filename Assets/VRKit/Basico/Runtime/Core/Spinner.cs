using UnityEngine;

namespace VRKit
{
    /// <summary>Gira y flota sin parar: monedas, gemas, portales, objetos mágicos, cosas que llaman la atención.</summary>
    public class Spinner : MonoBehaviour
    {
        [SerializeField] Vector3 degreesPerSecond = new Vector3(0f, 90f, 0f);
        [SerializeField] float bobHeight = 0.05f;
        [SerializeField] float bobSpeed = 2f;
        [Tooltip("Usar tiempo real (sigue girando en pausa y cámara lenta).")]
        [SerializeField] bool unscaled = false;

        Vector3 m_Start;
        float m_Seed;

        void Start() { m_Start = transform.localPosition; m_Seed = Random.value * 10f; }

        void Update()
        {
            float dt = unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = unscaled ? Time.unscaledTime : Time.time;
            transform.Rotate(degreesPerSecond * dt, Space.Self);
            if (bobHeight > 0f) transform.localPosition = m_Start + Vector3.up * Mathf.Sin(t * bobSpeed + m_Seed) * bobHeight;
        }
    }
}
