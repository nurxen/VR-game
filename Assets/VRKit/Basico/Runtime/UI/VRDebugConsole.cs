using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace VRKit
{
    /// <summary>
    /// Consola de depuración dentro del visor: muestra los Debug.Log, avisos y errores
    /// y los FPS. Imprescindible al probar en las Quest, donde no ves la Console de Unity.
    /// Pon un TMP_Text en un Canvas World Space (en la muñeca o flotando) y arrástralo aquí.
    /// </summary>
    public class VRDebugConsole : MonoBehaviour
    {
        [SerializeField] TMP_Text output;
        [SerializeField] int maxLines = 14;
        [SerializeField] bool showFps = true;
        [SerializeField] bool showStackTraceForErrors = false;
        [Tooltip("Desactívalo en la versión final: solo se ve en builds de desarrollo y en el editor.")]
        [SerializeField] bool onlyInDevelopmentBuilds = true;

        readonly Queue<string> m_Lines = new Queue<string>();
        readonly StringBuilder m_Builder = new StringBuilder();
        bool m_Dirty;
        float m_FpsTimer;
        int m_Frames;
        float m_Fps;

        void Awake()
        {
            if (onlyInDevelopmentBuilds && !Debug.isDebugBuild && !Application.isEditor)
                gameObject.SetActive(false);
        }

        void OnEnable() { Application.logMessageReceived += OnLog; }
        void OnDisable() { Application.logMessageReceived -= OnLog; }

        void OnLog(string message, string stackTrace, LogType type)
        {
            string color = type == LogType.Log ? "#FFFFFF" : type == LogType.Warning ? "#FFD34D" : "#FF6B6B";
            string line = "<color=" + color + ">" + message + "</color>";
            if (showStackTraceForErrors && (type == LogType.Error || type == LogType.Exception))
                line += "\n<size=70%>" + stackTrace + "</size>";
            m_Lines.Enqueue(line);
            while (m_Lines.Count > maxLines) m_Lines.Dequeue();
            m_Dirty = true;
        }

        void Update()
        {
            m_Frames++;
            m_FpsTimer += Time.unscaledDeltaTime;
            if (m_FpsTimer >= 0.5f)
            {
                m_Fps = m_Frames / m_FpsTimer;
                m_Frames = 0;
                m_FpsTimer = 0f;
                m_Dirty = true;
            }
            if (!m_Dirty || output == null) return;
            m_Dirty = false;

            m_Builder.Length = 0;
            if (showFps)
            {
                // Quest 2 va a 72 Hz por defecto: por debajo de ~70 FPS se nota y marea
                string c = m_Fps >= 70f ? "#7CFC9A" : m_Fps >= 55f ? "#FFD34D" : "#FF6B6B";
                m_Builder.Append("<b><color=").Append(c).Append(">").Append(Mathf.RoundToInt(m_Fps)).Append(" FPS</color></b>\n");
            }
            foreach (var l in m_Lines) m_Builder.Append(l).Append('\n');
            output.text = m_Builder.ToString();
        }

        public void Clear()
        {
            m_Lines.Clear();
            m_Dirty = true;
        }

        public void ToggleVisible()
        {
            if (output != null) output.gameObject.SetActive(!output.gameObject.activeSelf);
        }
    }
}
