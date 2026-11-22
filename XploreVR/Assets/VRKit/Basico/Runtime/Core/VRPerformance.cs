using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace VRKit
{
    /// <summary>
    /// Rendimiento en las gafas. Ponedlo en el XR Origin (lo pone el builder del mundo abierto).
    ///   · Foveated rendering: dibuja con menos detalle los bordes de la vista, donde el ojo casi no ve.
    ///     En Quest es de lo que más FPS da. Hace falta activar la feature «Foveated Rendering» en
    ///     Project Settings › XR Plug-in Management › OpenXR › Android (Tools › VRKit › Optimizar para Quest lo hace).
    ///   · Render scale: menos de 1 = menos píxeles (más FPS, algo más borroso).
    ///   · VRPerformance.Fps y VRPerformance.DisplayFrameRate para quien los necesite (mapa, calidad automática).
    /// Si algo no está disponible en vuestra versión de Unity, avisa una vez y sigue.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class VRPerformance : MonoBehaviour
    {
        [Tooltip("0 = sin foveated rendering, 1 = el máximo. En Quest 2, 0.5–1.")]
        [SerializeField, Range(0f, 1f)] float foveation = 0.75f;
        [Tooltip("Si las gafas lo permiten, el detalle sigue a la mirada (Quest Pro).")]
        [SerializeField] bool gazeFoveation = true;
        [Tooltip("Resolución de dibujo. 1 = la normal. 0.8–0.9 da bastantes FPS en Quest 2.")]
        [SerializeField, Range(0.5f, 1.4f)] float renderScale = 1f;

        /// <summary>FPS medios de los últimos instantes.</summary>
        public static float Fps { get; private set; }
        /// <summary>Milisegundos por frame (media).</summary>
        public static float FrameMs { get { return Fps > 0f ? 1000f / Fps : 0f; } }

        static float s_Refresh;
        static float s_NextQuery;
        static bool s_Warned;

        /// <summary>Frecuencia de la pantalla de las gafas (72, 90, 120…). 72 si no se sabe todavía.</summary>
        public static float DisplayFrameRate
        {
            get
            {
                if (s_Refresh <= 0f && Time.unscaledTime >= s_NextQuery)
                {
                    s_NextQuery = Time.unscaledTime + 3f;
                    var disp = Display();
                    if (disp != null)
                    {
                        var m = disp.GetType().GetMethod("TryGetDisplayRefreshRate");
                        if (m != null)
                        {
                            var args = new object[] { 0f };
                            try { if ((bool)m.Invoke(disp, args) && (float)args[0] > 20f) s_Refresh = (float)args[0]; } catch { }
                        }
                    }
                }
                return s_Refresh > 0f ? s_Refresh : 72f;
            }
        }

        public float Foveation { get { return foveation; } set { foveation = Mathf.Clamp01(value); ApplyFoveation(); } }
        public float RenderScale { get { return renderScale; } set { renderScale = Mathf.Clamp(value, 0.5f, 1.4f); ApplyRenderScale(); } }

        float m_Sum; int m_Frames;

        IEnumerator Start()
        {
            // El XR tarda unos frames en arrancar
            for (int i = 0; i < 20 && Display() == null; i++) yield return null;
            ApplyFoveation();
            ApplyRenderScale();
        }

        void Update()
        {
            m_Sum += Time.unscaledDeltaTime;
            m_Frames++;
            if (m_Sum >= 0.5f) { Fps = m_Frames / m_Sum; m_Sum = 0f; m_Frames = 0; }
        }

        void ApplyFoveation()
        {
            var disp = Display();
            if (disp == null) return;
            try
            {
                var level = disp.GetType().GetProperty("foveatedRenderingLevel");
                if (level == null) { WarnOnce("esta versión de Unity no tiene foveated rendering"); return; }
                level.SetValue(disp, foveation);
                var flags = disp.GetType().GetProperty("foveatedRenderingFlags");
                if (flags != null && flags.PropertyType.IsEnum)
                    flags.SetValue(disp, Enum.Parse(flags.PropertyType, gazeFoveation ? "GazeAllowed" : "None"));
            }
            catch (Exception ex) { WarnOnce("foveated rendering: " + ex.Message); }
        }

        void ApplyRenderScale()
        {
            var t = FindType("UnityEngine.XR.XRSettings");
            var p = t != null ? t.GetProperty("eyeTextureResolutionScale", BindingFlags.Public | BindingFlags.Static) : null;
            if (p == null) return;
            try { p.SetValue(null, renderScale); } catch (Exception ex) { WarnOnce("render scale: " + ex.Message); }
        }

        static void WarnOnce(string msg)
        {
            if (s_Warned) return;
            s_Warned = true;
            Debug.LogWarning("[VRKit] VRPerformance: " + msg + ". El juego funciona igual.");
        }

        // ------------------------------------------------------------------ XRDisplaySubsystem por reflexión
        // (así compila en cualquier versión y sin el paquete de XR instalado)

        static object Display()
        {
            var dispType = FindType("UnityEngine.XR.XRDisplaySubsystem");
            var mgr = FindType("UnityEngine.SubsystemManager");
            if (dispType == null || mgr == null) return null;
            try
            {
                MethodInfo get = null;
                foreach (var m in mgr.GetMethods(BindingFlags.Public | BindingFlags.Static))
                    if (m.Name == "GetSubsystems" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1) { get = m; break; }
                if (get == null) return null;
                var listType = typeof(System.Collections.Generic.List<>).MakeGenericType(dispType);
                var list = (IList)Activator.CreateInstance(listType);
                get.MakeGenericMethod(dispType).Invoke(null, new object[] { list });
                foreach (var d in list)
                {
                    var running = d.GetType().GetProperty("running");
                    if (running == null || (bool)running.GetValue(d)) return d;
                }
            }
            catch { }
            return null;
        }

        static readonly System.Collections.Generic.Dictionary<string, Type> s_Types = new System.Collections.Generic.Dictionary<string, Type>();
        static Type FindType(string fullName)
        {
            Type t;
            if (s_Types.TryGetValue(fullName, out t)) return t;
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { t = a.GetType(fullName); } catch { t = null; }
                if (t != null) break;
            }
            s_Types[fullName] = t;
            return t;
        }
    }
}
