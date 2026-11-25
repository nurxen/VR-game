using UnityEngine;

namespace VRKit
{
    /// <summary>La esfera de Effects.Burst: crece hasta Size y encoge al final.</summary>
    public class BurstFx : MonoBehaviour
    {
        public Color color = Color.white;
        public float size = 2f;
        public float duration = 0.3f;

        float m_T;

        void Start()
        {
            var r = GetComponent<Renderer>();
            if (r == null) return;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color); block.SetColor("_Color", color);
            r.SetPropertyBlock(block);
        }

        void Update()
        {
            m_T += Time.deltaTime / Mathf.Max(0.01f, duration);
            float grow = Mathf.Lerp(0.1f, size, Mathf.Clamp01(m_T * 1.5f));
            float shrink = m_T > 0.7f ? 1f - (m_T - 0.7f) / 0.3f : 1f;
            transform.localScale = Vector3.one * grow * Mathf.Max(0f, shrink);
            if (m_T >= 1f) Destroy(gameObject);
        }
    }
}
