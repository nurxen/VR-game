using UnityEngine;

namespace VRKit
{
    /// <summary>
    /// Barra de vida flotante encima de cualquier cosa con Health (enemigos, jefes, la torre que defiendes).
    /// Se crea sola (dos rectángulos), mira siempre a la cámara y se esconde con la vida llena.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class HealthBar : MonoBehaviour
    {
        [SerializeField] float height = 2.2f;
        [SerializeField] float width = 0.8f;
        [SerializeField] Color fillColor = new Color(0.9f, 0.2f, 0.2f);
        [SerializeField] bool alwaysVisible = false;

        Health m_Health;
        Transform m_Root, m_Fill;
        static Material s_Mat;

        void Start()
        {
            m_Health = GetComponent<Health>();
            m_Root = new GameObject("Barra de vida").transform;
            m_Root.SetParent(transform, false);
            m_Root.localPosition = Vector3.up * height;
            Vector3 s = transform.lossyScale;
            m_Root.localScale = new Vector3(1f / Mathf.Max(0.01f, s.x), 1f / Mathf.Max(0.01f, s.y), 1f / Mathf.Max(0.01f, s.z));
            Quad("Fondo", new Color(0f, 0f, 0f, 0.85f), 0f).localScale = new Vector3(width + 0.04f, 0.1f, 1f);
            m_Fill = Quad("Vida", fillColor, -0.005f);
        }

        Transform Quad(string n, Color c, float z)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.name = n;
            q.transform.SetParent(m_Root, false);
            q.transform.localPosition = new Vector3(0f, 0f, z);
            q.transform.localScale = new Vector3(width, 0.06f, 1f);
            if (s_Mat == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Unlit");
                if (sh == null) sh = Shader.Find("Unlit/Color");
                s_Mat = new Material(sh);
            }
            var r = q.GetComponent<Renderer>();
            r.sharedMaterial = s_Mat;
            var b = new MaterialPropertyBlock();
            b.SetColor("_BaseColor", c); b.SetColor("_Color", c);
            r.SetPropertyBlock(b);
            return q.transform;
        }

        void LateUpdate()
        {
            if (m_Root == null) return;
            bool show = !m_Health.IsDead && (alwaysVisible || m_Health.Normalized < 0.999f);
            m_Root.gameObject.SetActive(show);
            if (!show) return;
            float f = m_Health.Normalized;
            m_Fill.localScale = new Vector3(width * f, 0.06f, 1f);
            m_Fill.localPosition = new Vector3(-(1f - f) * width * 0.5f, 0f, -0.005f);
            var cam = Camera.main;
            if (cam != null) m_Root.rotation = cam.transform.rotation;
        }
    }
}
