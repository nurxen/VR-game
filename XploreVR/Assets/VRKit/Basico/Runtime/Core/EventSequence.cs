using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace VRKit
{
    /// <summary>
    /// Una lista de pasos con esperas: «espera 2 s → enciende la luz → subtítulo → espera 3 s → abre la puerta».
    /// Para escenas scriptadas, tutoriales, la intro de un jefe o una trampa. Sin programar: todo en el Inspector.
    /// Se lanza con Play() (desde una TriggerZone, un botón…) o al empezar.
    /// </summary>
    public class EventSequence : MonoBehaviour
    {
        [Serializable]
        public class Step
        {
            public string note = "Paso";
            [Tooltip("Segundos de espera ANTES de este paso.")]
            public float delay = 1f;
            public UnityEvent action = new UnityEvent();
        }

        [SerializeField] Step[] steps = new Step[0];
        [SerializeField] bool playOnStart = false;
        [Tooltip("Solo se puede reproducir una vez.")]
        [SerializeField] bool playOnce = true;
        [SerializeField] bool loop = false;
        [Tooltip("Usar tiempo real (sigue durante la pausa o la cámara lenta).")]
        [SerializeField] bool unscaledTime = false;

        public UnityEvent onFinished = new UnityEvent();

        public bool IsPlaying { get; private set; }
        bool m_Played;
        Coroutine m_Routine;

        void Start() { if (playOnStart) Play(); }

        /// <summary>Añade un paso (lo usa el montador del editor).</summary>
        public Step AddStep(string note, float delay)
        {
            var list = new System.Collections.Generic.List<Step>(steps);
            var s = new Step { note = note, delay = delay };
            list.Add(s);
            steps = list.ToArray();
            return s;
        }

        public void Play()
        {
            if (IsPlaying || (playOnce && m_Played)) return;
            m_Played = true;
            m_Routine = StartCoroutine(Run());
        }

        public void Stop()
        {
            if (m_Routine != null) StopCoroutine(m_Routine);
            IsPlaying = false;
        }

        /// <summary>Salta las esperas (botón «Saltar intro»).</summary>
        public void SkipToEnd()
        {
            Stop();
            foreach (var s in steps) s.action.Invoke();
            onFinished.Invoke();
        }

        IEnumerator Run()
        {
            IsPlaying = true;
            do
            {
                foreach (var s in steps)
                {
                    if (s.delay > 0f)
                    {
                        if (unscaledTime) yield return new WaitForSecondsRealtime(s.delay);
                        else yield return new WaitForSeconds(s.delay);
                    }
                    s.action.Invoke();
                }
            } while (loop);
            IsPlaying = false;
            onFinished.Invoke();
        }
    }
}
