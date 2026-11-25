using UnityEngine;
using UnityEngine.Events;

namespace VRKit
{
    /// <summary>
    /// Ciclo de día y noche: mueve el sol, cambia el color del cielo y de la niebla y oscurece los bloques
    /// (el shader VRKit/Voxel lee la oscuridad de una variable global, así no hace falta iluminación en tiempo real).
    /// De noche salen las criaturas hostiles (VoxelMobSpawner).
    /// </summary>
    public class VoxelDayNight : MonoBehaviour
    {
        public static VoxelDayNight Instance { get; private set; }

        [SerializeField] Light sun;
        [Tooltip("Minutos reales que dura un día completo.")]
        [SerializeField, Min(0.1f)] float dayMinutes = 12f;
        [Tooltip("0 = medianoche, 0,25 = amanecer, 0,5 = mediodía, 0,75 = atardecer.")]
        [SerializeField, Range(0f, 1f)] float timeOfDay = 0.3f;
        [SerializeField] bool running = true;

        [Header("Colores")]
        [SerializeField] Color daySky = new Color(0.55f, 0.75f, 0.98f);
        [SerializeField] Color sunsetSky = new Color(0.95f, 0.55f, 0.35f);
        [SerializeField] Color nightSky = new Color(0.03f, 0.04f, 0.09f);
        [SerializeField] Color dayAmbient = new Color(0.75f, 0.78f, 0.82f);
        [SerializeField] Color nightAmbient = new Color(0.12f, 0.13f, 0.2f);
        [Tooltip("Cuánto se oscurecen los bloques de noche (0 = nada, 1 = negro).")]
        [SerializeField, Range(0f, 1f)] float nightDarkness = 0.72f;
        [Tooltip("Cambiar el fondo de la cámara a color liso (como el cielo de Minecraft). Desmarcado = skybox.")]
        [SerializeField] bool controlCamera = true;

        public UnityEvent onNightfall = new UnityEvent();
        public UnityEvent onDaybreak = new UnityEvent();

        static readonly int kDarkness = Shader.PropertyToID("_VRKitVoxelDarkness");
        bool m_Night;

        public float TimeOfDay { get { return timeOfDay; } set { timeOfDay = Mathf.Repeat(value, 1f); Apply(); } }
        /// <summary>De 0 (noche cerrada) a 1 (pleno día).</summary>
        public float Daylight { get; private set; }
        public bool IsNight { get { return m_Night; } }
        public bool Running { get { return running; } set { running = value; } }

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; Shader.SetGlobalFloat(kDarkness, 0f); }

        void Start()
        {
            m_Night = ComputeDaylight(timeOfDay) < 0.3f;
            Apply();
        }

        void Update()
        {
            if (running) timeOfDay = Mathf.Repeat(timeOfDay + Time.deltaTime / (dayMinutes * 60f), 1f);
            Apply();
        }

        static float ComputeDaylight(float t)
        {
            float elevation = Mathf.Sin((t - 0.25f) * Mathf.PI * 2f);   // -1 medianoche, 1 mediodía
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.18f, 0.25f, elevation));
        }

        void Apply()
        {
            float elevation = Mathf.Sin((timeOfDay - 0.25f) * Mathf.PI * 2f);
            Daylight = ComputeDaylight(timeOfDay);

            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler((timeOfDay - 0.25f) * 360f, 170f, 0f);
                sun.intensity = Mathf.Lerp(0f, 1.1f, Daylight);
                sun.enabled = Daylight > 0.01f;
            }

            // Cerca del horizonte, el cielo se tiñe de atardecer
            float sunset = Mathf.Clamp01(1f - Mathf.Abs(elevation) * 4f) * Mathf.Clamp01(Daylight * 2f);
            Color sky = Color.Lerp(Color.Lerp(nightSky, daySky, Daylight), sunsetSky, sunset * 0.6f);
            RenderSettings.ambientLight = Color.Lerp(nightAmbient, dayAmbient, Daylight);
            RenderSettings.fogColor = sky;
            if (VoxelWorld.Instance != null) VoxelWorld.Instance.FogColor = sky;
            if (controlCamera && Camera.main != null)
            {
                Camera.main.clearFlags = CameraClearFlags.SolidColor;
                Camera.main.backgroundColor = sky;
            }
            Shader.SetGlobalFloat(kDarkness, (1f - Daylight) * nightDarkness);

            bool night = Daylight < 0.3f;
            if (night != m_Night)
            {
                m_Night = night;
                if (night) onNightfall.Invoke(); else onDaybreak.Invoke();
            }
        }

        /// <summary>Saltar a la mañana (una cama, un botón del menú).</summary>
        public void SkipToMorning() { TimeOfDay = 0.27f; }
    }
}
