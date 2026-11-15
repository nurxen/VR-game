using TMPro;
using UnityEngine;

namespace VRKit
{
    /// <summary>
    /// Texto que aparece en el mundo, sube y se desvanece: «+10», «¡Récord!», «Oleada 2», daño, nombres.
    ///   FloatingText.Spawn("+10", posición, color);
    ///   FloatingText.SpawnInFront("Mochila llena", color);   // delante de los ojos
    /// </summary>
    public class FloatingText : MonoBehaviour
    {
        public static bool Enabled = true;

        TextMeshPro m_Text;
        float m_Life, m_Duration;
        Vector3 m_Velocity;

        public static FloatingText Spawn(string text, Vector3 position, Color color, float size = 1f, float duration = 1.2f)
        {
            if (!Enabled) return null;
            var go = new GameObject("Texto flotante");
            go.transform.position = position;
            var ft = go.AddComponent<FloatingText>();
            ft.m_Text = go.AddComponent<TextMeshPro>();
            ft.m_Text.text = text;
            ft.m_Text.fontSize = 1.2f * size;
            ft.m_Text.alignment = TextAlignmentOptions.Center;
            ft.m_Text.fontStyle = FontStyles.Bold;
            ft.m_Text.color = color;
            ft.m_Text.outlineWidth = 0.2f;
            ft.m_Text.outlineColor = new Color32(0, 0, 0, 200);
            ft.m_Text.rectTransform.sizeDelta = new Vector2(3f, 0.6f);
            ft.m_Duration = duration;
            ft.m_Velocity = Vector3.up * 0.35f;
            return ft;
        }

        /// <summary>Un mensaje delante del jugador, a la altura de los ojos.</summary>
        public static FloatingText SpawnInFront(string text, Color color, float size = 1f)
        {
            var p = VRPlayer.Instance;
            Transform head = p != null ? p.Head : (Camera.main != null ? Camera.main.transform : null);
            if (head == null) return null;
            Vector3 fwd = p != null ? p.HeadForwardFlat : head.forward;
            var ft = Spawn(text, head.position + fwd * 1.3f + Vector3.down * 0.15f, color, size, 2f);
            if (ft != null) ft.m_Velocity = Vector3.up * 0.08f;
            return ft;
        }

        void LateUpdate()
        {
            m_Life += Time.unscaledDeltaTime;
            transform.position += m_Velocity * Time.unscaledDeltaTime;
            var cam = Camera.main;
            if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
            float a = 1f - Mathf.Clamp01((m_Life - m_Duration * 0.6f) / (m_Duration * 0.4f));
            var c = m_Text.color; c.a = a; m_Text.color = c;
            if (m_Life >= m_Duration) Destroy(gameObject);
        }
    }
}
