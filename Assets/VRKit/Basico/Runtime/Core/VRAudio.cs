using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace VRKit
{
    /// <summary>
    /// Sonidos 3D sin crear y destruir objetos cada vez (AudioSource.PlayClipAtPoint crea uno nuevo por sonido,
    /// y en Quest 2 eso se nota). Reutiliza un grupo de fuentes y varía un poco el tono para que
    /// cien golpes seguidos no suenen idénticos.
    ///
    ///   VRAudio.PlayAt(clip, posición);
    ///   VRAudio.PlayAt(clip, posición, volumen: 0.5f, pitchVariation: 0.1f);
    /// </summary>
    public static class VRAudio
    {
        /// <summary>Volumen de los efectos (lo cambia la opción «Efectos»).</summary>
        public static float SfxVolume = 1f;
        /// <summary>Grupo del mezclador por el que salen los efectos (opcional).</summary>
        public static AudioMixerGroup Output;

        const int kMaxSources = 24;
        static readonly List<AudioSource> s_Sources = new List<AudioSource>();
        static int s_Next;
        static Transform s_Root;

        public static AudioSource PlayAt(AudioClip clip, Vector3 position, float volume = 1f, float pitchVariation = 0.05f, float spatialBlend = 1f)
        {
            if (clip == null || SfxVolume <= 0f) return null;
            var src = GetSource();
            src.transform.position = position;
            src.clip = clip;
            src.volume = Mathf.Clamp01(volume) * SfxVolume;
            src.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            src.spatialBlend = spatialBlend;
            src.outputAudioMixerGroup = Output;
            src.Play();
            return src;
        }

        /// <summary>Sonido no posicional (interfaz, avisos).</summary>
        public static AudioSource Play2D(AudioClip clip, float volume = 1f)
        {
            var cam = Camera.main;
            return PlayAt(clip, cam != null ? cam.transform.position : Vector3.zero, volume, 0f, 0f);
        }

        /// <summary>Uno al azar de una lista (pasos, golpes, impactos).</summary>
        public static AudioSource PlayRandom(AudioClip[] clips, Vector3 position, float volume = 1f)
        {
            if (clips == null || clips.Length == 0) return null;
            return PlayAt(clips[Random.Range(0, clips.Length)], position, volume, 0.08f);
        }

        static AudioSource GetSource()
        {
            if (s_Root == null)
            {
                s_Root = new GameObject("[VRAudio]").transform;
                s_Sources.Clear();
                s_Next = 0;
            }
            // Primero una libre; si todas suenan, se reutiliza la más antigua
            for (int i = 0; i < s_Sources.Count; i++)
                if (s_Sources[i] != null && !s_Sources[i].isPlaying) return s_Sources[i];
            if (s_Sources.Count < kMaxSources)
            {
                var go = new GameObject("Fuente");
                go.transform.SetParent(s_Root, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 1f; s.maxDistance = 25f;
                s_Sources.Add(s);
                return s;
            }
            s_Next = (s_Next + 1) % s_Sources.Count;
            return s_Sources[s_Next];
        }
    }
}
