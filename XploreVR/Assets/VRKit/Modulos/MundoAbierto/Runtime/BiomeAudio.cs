using UnityEngine;
using UnityEngine.Audio;

namespace VRKit
{
    /// <summary>
    /// Música y sonido ambiente según el bioma donde está el jugador y si es de día o de noche (lo pone cada BiomeProfile).
    /// Pensado para un juego tranquilo: los cambios son con fundido, y entre canción y canción hay silencios.
    /// Conectad Music Group y Ambience Group al AudioMixer del SettingsManager para que respeten el volumen de las opciones.
    /// </summary>
    public class BiomeAudio : MonoBehaviour
    {
        [SerializeField] AudioMixerGroup musicGroup;
        [SerializeField] AudioMixerGroup ambienceGroup;
        [SerializeField, Range(0f, 1f)] float musicVolume = 0.55f;
        [SerializeField, Range(0f, 1f)] float ambienceVolume = 0.7f;
        [Tooltip("Segundos de fundido al cambiar de zona.")]
        [SerializeField] float crossfade = 4f;
        [Tooltip("Silencio entre canciones (mínimo y máximo, en segundos).")]
        [SerializeField] Vector2 silence = new Vector2(25f, 70f);
        [Tooltip("Cada cuánto se mira en qué bioma estás.")]
        [SerializeField] float checkEvery = 2f;

        AudioSource m_MusicA, m_MusicB, m_AmbA, m_AmbB;
        BiomeProfile m_Profile;
        bool m_Night;
        float m_NextCheck, m_NextSong;

        void Awake()
        {
            m_MusicA = MakeSource(musicGroup, false); m_MusicB = MakeSource(musicGroup, false);
            m_AmbA = MakeSource(ambienceGroup, true); m_AmbB = MakeSource(ambienceGroup, true);
            m_NextSong = Time.time + 3f;
        }

        AudioSource MakeSource(AudioMixerGroup group, bool loop)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            s.volume = 0f;
            s.outputAudioMixerGroup = group;
            return s;
        }

        void Update()
        {
            var world = OpenWorld.Instance;
            var player = VRPlayer.Instance;
            if (world == null || player == null || !world.IsReady) return;

            if (Time.time >= m_NextCheck)
            {
                m_NextCheck = Time.time + checkEvery;
                bool inCave = CaveSystem.Instance != null && CaveSystem.Instance.InCave;
                var profile = inCave ? null : world.ProfileOf(world.BiomeAt(player.FeetPosition));
                bool night = SkyCycle.Instance != null && SkyCycle.Instance.IsNight;
                if (profile != m_Profile || night != m_Night)
                {
                    m_Profile = profile; m_Night = night;
                    var amb = profile == null ? null : (night && profile.nightAmbience != null ? profile.nightAmbience : profile.dayAmbience);
                    if (m_AmbA.clip != amb) { Swap(ref m_AmbA, ref m_AmbB); m_AmbA.clip = amb; if (amb != null) m_AmbA.Play(); }
                    // Una canción en marcha termina sola; la siguiente ya será de la zona nueva
                    if (!m_MusicA.isPlaying) m_NextSong = Mathf.Min(m_NextSong, Time.time + 4f);
                }
            }

            if (!m_MusicA.isPlaying && Time.time >= m_NextSong) PlaySong();
            if (m_MusicA.isPlaying && m_MusicA.clip != null && m_MusicA.time >= m_MusicA.clip.length - 0.2f)
                m_NextSong = Time.time + Random.Range(silence.x, silence.y);

            float k = Time.deltaTime / Mathf.Max(0.1f, crossfade);
            m_MusicA.volume = Mathf.MoveTowards(m_MusicA.volume, m_MusicA.clip != null ? musicVolume : 0f, k * musicVolume);
            m_MusicB.volume = Mathf.MoveTowards(m_MusicB.volume, 0f, k * musicVolume);
            m_AmbA.volume = Mathf.MoveTowards(m_AmbA.volume, m_AmbA.clip != null ? ambienceVolume : 0f, k * ambienceVolume);
            m_AmbB.volume = Mathf.MoveTowards(m_AmbB.volume, 0f, k * ambienceVolume);
            if (m_MusicB.isPlaying && m_MusicB.volume <= 0f) m_MusicB.Stop();
            if (m_AmbB.isPlaying && m_AmbB.volume <= 0f) m_AmbB.Stop();
        }

        void PlaySong()
        {
            if (m_Profile == null) { m_NextSong = Time.time + 5f; return; }
            var list = m_Night && m_Profile.nightMusic.Length > 0 ? m_Profile.nightMusic : m_Profile.dayMusic;
            if (list == null || list.Length == 0) { m_NextSong = Time.time + 10f; return; }
            Swap(ref m_MusicA, ref m_MusicB);
            m_MusicA.clip = list[Random.Range(0, list.Length)];
            m_MusicA.volume = 0f;
            m_MusicA.Play();
            m_NextSong = float.MaxValue;
        }

        static void Swap(ref AudioSource a, ref AudioSource b) { var t = a; a = b; b = t; }
    }
}
