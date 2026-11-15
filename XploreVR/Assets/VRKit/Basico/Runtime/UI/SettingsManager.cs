using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.UI;

namespace VRKit
{
    /// <summary>
    /// Opciones del juego, guardadas entre partidas (PlayerPrefs). Tres grupos:
    ///
    ///  MOVIMIENTO      giro suave o por saltos · ángulo del salto · velocidad de giro · andar con joystick o teletransporte
    ///                  · velocidad al andar · viñeta de confort · modo sentado · mano dominante
    ///  SONIDO          volumen general · música · efectos
    ///  ACCESIBILIDAD   subtítulos y su tamaño · intensidad de vibración · dificultad · textos flotantes
    ///
    /// Cada opción tiene su método público (SetSmoothTurn, SetMoveSpeed…) para conectarlo a un Toggle o Slider.
    /// Los proveedores de locomoción de XRI se cambian por nombre de propiedad, así que funciona con distintas
    /// versiones de los Starter Assets.
    /// </summary>
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }
        public static readonly string[] DifficultyNames = { "Historia", "Fácil", "Normal", "Difícil" };
        static readonly float[] kDifficultyDamage = { 0.25f, 0.6f, 1f, 1.6f };

        [Header("Rig de XR Interaction Toolkit")]
        [Tooltip("Los 'Controller Input Action Manager' de cada mano (Starter Assets). Se les cambia smoothTurnEnabled / smoothMotionEnabled.")]
        [SerializeField] MonoBehaviour[] controllerInputManagers = new MonoBehaviour[0];
        [Tooltip("Snap Turn Provider, Continuous Turn Provider y Continuous Move Provider del XR Origin.\n" +
                 "Se les cambia turnAmount, turnSpeed y moveSpeed.")]
        [SerializeField] MonoBehaviour[] locomotionProviders = new MonoBehaviour[0];

        [Header("Alternativa: activar/desactivar componentes")]
        [SerializeField] Behaviour[] snapTurnComponents = new Behaviour[0];
        [SerializeField] Behaviour[] smoothTurnComponents = new Behaviour[0];
        [SerializeField] Behaviour[] teleportComponents = new Behaviour[0];
        [SerializeField] Behaviour[] smoothMoveComponents = new Behaviour[0];

        [Header("Confort y sonido")]
        [Tooltip("Objeto de la viñeta de confort (Tunneling Vignette en Starter Assets).")]
        [SerializeField] GameObject comfortVignette;
        [Tooltip("Opcional. Parámetros expuestos del mezclador. Sin mezclador, el general usa AudioListener.volume.")]
        [SerializeField] AudioMixer mixer;
        [SerializeField] string masterParam = "MasterVolume";
        [SerializeField] string musicParam = "MusicVolume";
        [SerializeField] string sfxParam = "SfxVolume";
        [Tooltip("Altura extra de la vista en modo sentado (metros).")]
        [SerializeField] float seatedHeightOffset = 0.45f;

        [Header("Controles del panel (opcional: para que enseñen el valor guardado)")]
        [SerializeField] Toggle smoothTurnToggle;
        [SerializeField] Slider snapAngleSlider;
        [SerializeField] TMP_Text snapAngleLabel;
        [SerializeField] Slider turnSpeedSlider;
        [SerializeField] Toggle smoothMoveToggle;
        [SerializeField] Slider moveSpeedSlider;
        [SerializeField] Toggle vignetteToggle;
        [SerializeField] Toggle seatedToggle;
        [SerializeField] Toggle leftHandedToggle;
        [SerializeField] Slider volumeSlider;
        [SerializeField] TMP_Text volumeLabel;
        [SerializeField] Slider musicSlider;
        [SerializeField] Slider sfxSlider;
        [SerializeField] Toggle subtitlesToggle;
        [SerializeField] Slider subtitleSizeSlider;
        [SerializeField] Slider hapticsSlider;
        [SerializeField] Slider difficultySlider;
        [SerializeField] TMP_Text difficultyLabel;
        [SerializeField] Toggle floatingTextToggle;

        public UnityEvent onSettingsChanged = new UnityEvent();

        // ---- Valores actuales
        public bool SmoothTurn { get; private set; }
        public float SnapAngle { get; private set; }
        public float TurnSpeed { get; private set; }
        public bool SmoothMove { get; private set; }
        public float MoveSpeed { get; private set; }
        public bool Vignette { get; private set; }
        public bool Seated { get; private set; }
        public bool LeftHanded { get; private set; }
        public float Volume { get; private set; }
        public float Music { get; private set; }
        public float Sfx { get; private set; }
        public bool Subtitles { get; private set; }
        public float SubtitleSize { get; private set; }
        public float Haptics { get; private set; }
        public int Difficulty { get; private set; }
        public bool FloatingTexts { get; private set; }

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }
        void Start() { Load(); ApplyAll(); }

        void Load()
        {
            SmoothTurn = GetB("smoothTurn", false);   // por defecto: giro por saltos (marea menos)
            SnapAngle = GetF("snapAngle", 45f);
            TurnSpeed = GetF("turnSpeed", 90f);
            SmoothMove = GetB("smoothMove", false);   // por defecto: teletransporte
            MoveSpeed = GetF("moveSpeed", 2f);
            Vignette = GetB("vignette", true);
            Seated = GetB("seated", false);
            LeftHanded = GetB("leftHanded", false);
            Volume = GetF("volume", 0.8f);
            Music = GetF("music", 0.7f);
            Sfx = GetF("sfx", 1f);
            Subtitles = GetB("subtitles", true);
            SubtitleSize = GetF("subtitleSize", 1f);
            Haptics = GetF("haptics", 1f);
            Difficulty = PlayerPrefs.GetInt("vrkit.difficulty", 2);
            FloatingTexts = GetB("floatingTexts", true);
        }

        // ================================================================ Métodos para el panel

        public void SetSmoothTurn(bool on) { SmoothTurn = on; SetB("smoothTurn", on); ApplyAll(); }
        /// <summary>Ángulo del giro por saltos (15-90). Con un Slider de 1 a 6 se convierte a 15, 30… 90.</summary>
        public void SetSnapAngle(float v) { SnapAngle = v <= 6f ? Mathf.Round(v) * 15f : Mathf.Round(v / 15f) * 15f; SetF("snapAngle", SnapAngle); ApplyAll(); }
        public void SetTurnSpeed(float v) { TurnSpeed = Mathf.Clamp(v, 30f, 240f); SetF("turnSpeed", TurnSpeed); ApplyAll(); }
        public void SetSmoothMove(bool on) { SmoothMove = on; SetB("smoothMove", on); ApplyAll(); }
        public void SetMoveSpeed(float v) { MoveSpeed = Mathf.Clamp(v, 0.5f, 5f); SetF("moveSpeed", MoveSpeed); ApplyAll(); }
        public void SetVignette(bool on) { Vignette = on; SetB("vignette", on); ApplyAll(); }
        public void SetSeated(bool on) { Seated = on; SetB("seated", on); ApplyAll(); }
        public void SetLeftHanded(bool on) { LeftHanded = on; SetB("leftHanded", on); ApplyAll(); }
        public void SetVolume(float v) { Volume = Mathf.Clamp01(v); SetF("volume", Volume); ApplyAll(); }
        public void SetMusicVolume(float v) { Music = Mathf.Clamp01(v); SetF("music", Music); ApplyAll(); }
        public void SetSfxVolume(float v) { Sfx = Mathf.Clamp01(v); SetF("sfx", Sfx); ApplyAll(); }
        public void SetSubtitles(bool on) { Subtitles = on; SetB("subtitles", on); ApplyAll(); }
        public void SetSubtitleSize(float v) { SubtitleSize = Mathf.Clamp(v, 0.7f, 1.6f); SetF("subtitleSize", SubtitleSize); ApplyAll(); }
        public void SetHaptics(float v) { Haptics = Mathf.Clamp01(v); SetF("haptics", Haptics); ApplyAll(); }
        public void SetDifficulty(float v) { Difficulty = Mathf.Clamp(Mathf.RoundToInt(v), 0, DifficultyNames.Length - 1); PlayerPrefs.SetInt("vrkit.difficulty", Difficulty); ApplyAll(); }
        public void SetFloatingTexts(bool on) { FloatingTexts = on; SetB("floatingTexts", on); ApplyAll(); }

        /// <summary>Recolocar al jugador mirando al frente (botón «Recentrar»).</summary>
        public void Recenter(Transform spawn)
        {
            if (VRPlayer.Instance != null) VRPlayer.Instance.Recenter(spawn);
        }

        public void ResetToDefaults()
        {
            foreach (var k in new[] { "smoothTurn", "snapAngle", "turnSpeed", "smoothMove", "moveSpeed", "vignette", "seated", "leftHanded",
                                      "volume", "music", "sfx", "subtitles", "subtitleSize", "haptics", "difficulty", "floatingTexts" })
                PlayerPrefs.DeleteKey("vrkit." + k);
            Load();
            ApplyAll();
        }

        // ================================================================ Aplicar

        void ApplyAll()
        {
            foreach (var m in controllerInputManagers)
            {
                SetProperty(m, "smoothTurnEnabled", SmoothTurn);
                SetProperty(m, "smoothMotionEnabled", SmoothMove);
            }
            foreach (var p in locomotionProviders)
            {
                SetProperty(p, "turnAmount", SnapAngle);
                SetProperty(p, "turnSpeed", TurnSpeed);
                SetProperty(p, "moveSpeed", MoveSpeed);
            }
            SetEnabled(snapTurnComponents, !SmoothTurn);
            SetEnabled(smoothTurnComponents, SmoothTurn);
            SetEnabled(teleportComponents, !SmoothMove);
            SetEnabled(smoothMoveComponents, SmoothMove);
            if (comfortVignette != null) comfortVignette.SetActive(Vignette);

            var player = VRPlayer.Instance;
            if (player != null)
            {
                player.SetHeightOffset(Seated ? seatedHeightOffset : 0f);
                player.DominantHand = LeftHanded ? Hand.Left : Hand.Right;
            }

            // Sonido
            if (mixer != null)
            {
                mixer.SetFloat(masterParam, ToDb(Volume));
                mixer.SetFloat(musicParam, ToDb(Music));
                mixer.SetFloat(sfxParam, ToDb(Sfx));
            }
            else AudioListener.volume = Volume;
            VRAudio.SfxVolume = Sfx;

            // Accesibilidad
            SubtitleSystem.Enabled = Subtitles;
            SubtitleSystem.SizeMultiplier = SubtitleSize;
            VRHaptics.GlobalScale = Haptics;
            FloatingText.Enabled = FloatingTexts;
            var ph = PlayerHealth.Instance;
            if (ph != null)
            {
                ph.DamageTakenMultiplier = kDifficultyDamage[Difficulty];
                ph.Invincible = Difficulty == 0;
            }

            RefreshControls();
            PlayerPrefs.Save();
            onSettingsChanged.Invoke();
        }

        void RefreshControls()
        {
            SetT(smoothTurnToggle, SmoothTurn); SetT(smoothMoveToggle, SmoothMove); SetT(vignetteToggle, Vignette);
            SetT(seatedToggle, Seated); SetT(leftHandedToggle, LeftHanded); SetT(subtitlesToggle, Subtitles); SetT(floatingTextToggle, FloatingTexts);
            SetS(snapAngleSlider, snapAngleSlider != null && snapAngleSlider.maxValue <= 6f ? SnapAngle / 15f : SnapAngle);
            SetS(turnSpeedSlider, TurnSpeed); SetS(moveSpeedSlider, MoveSpeed);
            SetS(volumeSlider, Volume); SetS(musicSlider, Music); SetS(sfxSlider, Sfx);
            SetS(subtitleSizeSlider, SubtitleSize); SetS(hapticsSlider, Haptics); SetS(difficultySlider, Difficulty);
            if (snapAngleLabel != null) snapAngleLabel.text = "Ángulo de giro: " + Mathf.RoundToInt(SnapAngle) + "°";
            if (volumeLabel != null) volumeLabel.text = "Volumen general: " + Mathf.RoundToInt(Volume * 100f) + " %";
            if (difficultyLabel != null) difficultyLabel.text = "Dificultad: " + DifficultyNames[Difficulty];
        }

        // ================================================================ Ayudas

        static float ToDb(float v) { return v > 0.0001f ? Mathf.Log10(v) * 20f : -80f; }
        static bool GetB(string k, bool d) { return PlayerPrefs.GetInt("vrkit." + k, d ? 1 : 0) == 1; }
        static float GetF(string k, float d) { return PlayerPrefs.GetFloat("vrkit." + k, d); }
        static void SetB(string k, bool v) { PlayerPrefs.SetInt("vrkit." + k, v ? 1 : 0); }
        static void SetF(string k, float v) { PlayerPrefs.SetFloat("vrkit." + k, v); }
        static void SetT(Toggle t, bool v) { if (t != null) t.SetIsOnWithoutNotify(v); }
        static void SetS(Slider s, float v) { if (s != null) s.SetValueWithoutNotify(v); }

        static void SetEnabled(Behaviour[] list, bool on)
        {
            foreach (var b in list) if (b != null) b.enabled = on;
        }

        // Se hace por nombre para no depender de la versión exacta de XRI / Starter Assets.
        static void SetProperty(MonoBehaviour target, string property, object value)
        {
            if (target == null) return;
            var p = target.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance);
            if (p == null || !p.CanWrite) return;
            if (p.PropertyType == typeof(bool) && value is bool) p.SetValue(target, value, null);
            else if (p.PropertyType == typeof(float) && value is float) p.SetValue(target, value, null);
        }
    }
}
