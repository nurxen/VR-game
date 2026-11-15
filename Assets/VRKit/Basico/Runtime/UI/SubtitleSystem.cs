using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace VRKit
{
    /// <summary>
    /// Subtítulos y diálogos hablados: una caja de texto abajo en la vista que sigue la mirada sin marear.
    /// Las frases se ponen en cola: si llegan dos a la vez, se dicen una detrás de otra.
    ///  · Desde el Inspector (TriggerZone, EventSequence, botón): Say("Bienvenido a la nave").
    ///  · Con quién habla y audio: Say("Capitana", "¡Cuidado!", clip).
    ///  · Formato rápido en un solo texto: "Capitana: ¡Cuidado!" (lo de antes de los dos puntos es el nombre).
    /// Se crea su propio Canvas al empezar. Las opciones pueden apagarlo o cambiar el tamaño.
    /// </summary>
    public class SubtitleSystem : MonoBehaviour
    {
        public static SubtitleSystem Instance { get; private set; }
        public static bool Enabled = true;
        public static float SizeMultiplier = 1f;

        [SerializeField] float charsPerSecond = 15f;
        [SerializeField] float minDuration = 2f;
        [SerializeField] float gapBetweenLines = 0.3f;
        [SerializeField] Color speakerColor = new Color(1f, 0.8f, 0.35f);
        [SerializeField] AudioSource voiceSource;

        public UnityEvent<string> onLineStarted = new UnityEvent<string>();
        public UnityEvent onQueueFinished = new UnityEvent();

        struct Line { public string speaker, text; public AudioClip clip; public float duration; }
        readonly Queue<Line> m_Queue = new Queue<Line>();
        TMP_Text m_Label;
        GameObject m_Box;
        bool m_Running;

        public bool IsSpeaking { get { return m_Running; } }

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            Build();
            if (voiceSource == null) { voiceSource = gameObject.AddComponent<AudioSource>(); voiceSource.spatialBlend = 0f; voiceSource.playOnAwake = false; }
        }

        void Build()
        {
            var root = new GameObject("Subtítulos");
            root.transform.SetParent(transform, false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 500;
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(1100f, 220f);
            rt.localScale = Vector3.one * 0.001f;
            var follow = root.AddComponent<LazyFollow>();
            follow.Configure(1.5f, -0.38f);

            m_Box = new GameObject("Caja", typeof(RectTransform), typeof(Image));
            m_Box.transform.SetParent(root.transform, false);
            var brt = (RectTransform)m_Box.transform;
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = brt.offsetMax = Vector2.zero;
            m_Box.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            var textGo = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(m_Box.transform, false);
            var trt = (RectTransform)textGo.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = new Vector2(30f, 15f); trt.offsetMax = new Vector2(-30f, -15f);
            m_Label = textGo.GetComponent<TextMeshProUGUI>();
            m_Label.alignment = TextAlignmentOptions.Center;
            m_Label.color = Color.white;
            m_Label.enableWordWrapping = true;
            m_Box.SetActive(false);
        }

        /// <summary>Para el Inspector: «Nombre: texto» o solo «texto».</summary>
        public void Say(string line)
        {
            int i = line.IndexOf(':');
            if (i > 0 && i < 24) Say(line.Substring(0, i).Trim(), line.Substring(i + 1).Trim(), null);
            else Say("", line, null);
        }

        public void Say(string speaker, string text, AudioClip clip, float duration = 0f)
        {
            if (duration <= 0f) duration = clip != null ? clip.length : Mathf.Max(minDuration, text.Length / charsPerSecond);
            m_Queue.Enqueue(new Line { speaker = speaker, text = text, clip = clip, duration = duration });
            if (!m_Running) StartCoroutine(Run());
        }

        /// <summary>Atajo estático.</summary>
        public static void SayStatic(string speaker, string text, AudioClip clip = null)
        {
            if (Instance != null) Instance.Say(speaker, text, clip);
        }

        public void Clear()
        {
            m_Queue.Clear();
            StopAllCoroutines();
            m_Running = false;
            if (voiceSource != null) voiceSource.Stop();
            if (m_Box != null) m_Box.SetActive(false);
        }

        IEnumerator Run()
        {
            m_Running = true;
            while (m_Queue.Count > 0)
            {
                var l = m_Queue.Dequeue();
                if (l.clip != null && voiceSource != null) { voiceSource.clip = l.clip; voiceSource.Play(); }
                if (Enabled && m_Label != null)
                {
                    m_Label.fontSize = 46f * SizeMultiplier;
                    string hex = ColorUtility.ToHtmlStringRGB(speakerColor);
                    m_Label.text = string.IsNullOrEmpty(l.speaker) ? l.text : "<color=#" + hex + ">" + l.speaker + ":</color> " + l.text;
                    m_Box.SetActive(true);
                }
                onLineStarted.Invoke(l.text);
                yield return new WaitForSecondsRealtime(l.duration);
                if (m_Box != null) m_Box.SetActive(false);
                yield return new WaitForSecondsRealtime(gapBetweenLines);
            }
            m_Running = false;
            onQueueFinished.Invoke();
        }
    }
}
