using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace VRKit
{
    /// <summary>
    /// Fundido a negro (o a cualquier color) delante de los ojos.
    /// En VR los cortes bruscos marean: úsalo al cambiar de escena, teletransportar o morir.
    /// Ponlo en cualquier objeto de la escena; se crea solo su panel delante de la cámara.
    /// </summary>
    public class ScreenFader : MonoBehaviour
    {
        public static ScreenFader Instance { get; private set; }

        [SerializeField] Color fadeColor = Color.black;
        [SerializeField] float defaultDuration = 0.4f;
        [Tooltip("La escena empieza en negro y aparece con un fundido.")]
        [SerializeField] bool fadeInOnStart = true;

        Canvas m_Canvas;
        Image m_Image;
        Coroutine m_Routine;
        float m_Alpha;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            Build();
            if (fadeInOnStart)
            {
                SetAlpha(1f, fadeColor);
                FadeIn();
            }
        }

        void Build()
        {
            var cam = Camera.main;
            if (cam == null) { Debug.LogWarning("[ScreenFader] No hay cámara con el tag MainCamera."); return; }

            var go = new GameObject("Screen Fader Canvas");
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, cam.nearClipPlane + 0.02f);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * 0.001f;

            m_Canvas = go.AddComponent<Canvas>();
            m_Canvas.renderMode = RenderMode.WorldSpace;
            m_Canvas.sortingOrder = 32000;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(2000f, 2000f); // 2 x 2 m a 1 cm de los ojos: tapa toda la vista

            var imgGo = new GameObject("Fade");
            imgGo.transform.SetParent(go.transform, false);
            m_Image = imgGo.AddComponent<Image>();
            m_Image.raycastTarget = false;
            var irt = (RectTransform)imgGo.transform;
            irt.anchorMin = Vector2.zero;
            irt.anchorMax = Vector2.one;
            irt.offsetMin = Vector2.zero;
            irt.offsetMax = Vector2.zero;
            SetAlpha(0f, fadeColor);
        }

        void SetAlpha(float a, Color c)
        {
            m_Alpha = a;
            if (m_Image == null) return;
            c.a = a;
            m_Image.color = c;
            m_Canvas.enabled = a > 0.001f;
        }

        public void FadeOut() { FadeOut(defaultDuration); }
        public void FadeIn() { FadeIn(defaultDuration); }
        public void FadeOut(float duration) { Run(FadeRoutine(1f, duration, fadeColor)); }
        public void FadeIn(float duration) { Run(FadeRoutine(0f, duration, fadeColor)); }

        /// <summary>Para esperar el fundido desde otra corrutina: yield return fader.FadeTo(1f, 0.5f).</summary>
        public IEnumerator FadeTo(float alpha, float duration)
        {
            if (m_Routine != null) StopCoroutine(m_Routine);
            yield return FadeRoutine(alpha, duration, fadeColor);
        }

        /// <summary>Destello rápido de un color (daño, curación, power-up).</summary>
        public void Flash(Color color, float duration)
        {
            Run(FlashRoutine(color, duration));
        }

        void Run(IEnumerator routine)
        {
            if (m_Routine != null) StopCoroutine(m_Routine);
            m_Routine = StartCoroutine(routine);
        }

        IEnumerator FadeRoutine(float target, float duration, Color color)
        {
            if (m_Image == null) yield break;
            float start = m_Alpha;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime; // funciona aunque el juego esté en pausa
                SetAlpha(Mathf.Lerp(start, target, t / duration), color);
                yield return null;
            }
            SetAlpha(target, color);
        }

        IEnumerator FlashRoutine(Color color, float duration)
        {
            if (m_Image == null) yield break;
            float peak = color.a;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(peak, 0f, t / duration), color);
                yield return null;
            }
            SetAlpha(0f, color);
        }
    }
}
