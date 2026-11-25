using UnityEngine;

namespace VRKit
{
    /// <summary>Un trozo que sale volando, cae y se encoge. Lo crea VoxelFx.Debris.</summary>
    public class VoxelDebrisPiece : MonoBehaviour
    {
        public Vector3 velocity;
        public float gravity = 14f;
        public float life = 0.6f;
        float m_T;
        Vector3 m_Scale;

        void Start() { m_Scale = transform.localScale; }

        void Update()
        {
            m_T += Time.deltaTime;
            velocity += Vector3.down * gravity * Time.deltaTime;
            transform.position += velocity * Time.deltaTime;
            transform.localScale = m_Scale * Mathf.Clamp01(1f - m_T / life);
            if (m_T >= life) Destroy(gameObject);
        }
    }
}
