using UnityEngine;
using UnityEngine.Events;

namespace VRKit
{
    /// <summary>
    /// Día y noche: sol, luna, color del cielo, niebla y luz ambiente, y un cielo de ESTRELLAS que aparece al anochecer
    /// (con la luna entre ellas) y gira despacio. Si el cielo usa el material «Skybox/Procedural», el sol se ve en él.
    /// Los shaders del módulo leen la variable global _VRKitNight (0 de día, 1 de noche).
    /// </summary>
    public class SkyCycle : MonoBehaviour
    {
        public static SkyCycle Instance { get; private set; }

        [SerializeField] Light sun;
        [SerializeField] Light moon;
        [Tooltip("Minutos reales que dura un día entero.")]
        [SerializeField, Min(0.1f)] float dayMinutes = 20f;
        [Tooltip("0 = medianoche, 0,25 = amanecer, 0,5 = mediodía, 0,75 = atardecer.")]
        [SerializeField, Range(0f, 1f)] float timeOfDay = 0.32f;
        [SerializeField] bool running = true;
        [Tooltip("Las noches duran menos que los días (0,5 = la mitad).")]
        [SerializeField, Range(0.2f, 1f)] float nightSpeedFactor = 0.6f;

        [Header("Colores")]
        [SerializeField] Color daySky = new Color(0.58f, 0.76f, 0.95f);
        [SerializeField] Color sunsetSky = new Color(0.98f, 0.6f, 0.4f);
        [SerializeField] Color nightSky = new Color(0.04f, 0.06f, 0.13f);
        [SerializeField] Color dayAmbient = new Color(0.46f, 0.5f, 0.58f);
        [SerializeField] Color nightAmbient = new Color(0.13f, 0.15f, 0.24f);
        [SerializeField] Color sunColor = new Color(1f, 0.95f, 0.85f);
        [SerializeField] Color sunsetColor = new Color(1f, 0.6f, 0.35f);
        [SerializeField] float sunIntensity = 0.9f;
        [SerializeField] float moonIntensity = 0.22f;

        [Header("Estrellas")]
        [SerializeField] bool stars = true;
        [SerializeField, Range(200, 4000)] int starCount = 1500;
        [SerializeField] Material starMaterial;
        [SerializeField] float moonSize = 6f;

        [Header("Cueva")]
        [SerializeField] Color caveAmbient = new Color(0.08f, 0.09f, 0.12f);

        public UnityEvent onNightfall = new UnityEvent();
        public UnityEvent onDaybreak = new UnityEvent();

        static readonly int kNight = Shader.PropertyToID("_VRKitNight");
        Transform m_Dome;
        Material m_Skybox;
        bool m_Night;
        float m_BaseExposure = -1f;

        public float TimeOfDay { get { return timeOfDay; } set { timeOfDay = Mathf.Repeat(value, 1f); Apply(); } }
        /// <summary>0 = noche cerrada, 1 = pleno día.</summary>
        public float Daylight { get; private set; }
        public bool IsNight { get { return m_Night; } }
        public bool Running { get { return running; } set { running = value; } }
        /// <summary>Lo pone CaveSystem: dentro de una cueva no hay cielo.</summary>
        public bool InCave { get; set; }

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; Shader.SetGlobalFloat(kNight, 0f); }

        void Start()
        {
            m_Skybox = RenderSettings.skybox;
            if (m_Skybox != null && m_Skybox.HasProperty("_Exposure")) m_BaseExposure = m_Skybox.GetFloat("_Exposure");
            if (sun != null) RenderSettings.sun = sun;
            if (stars) BuildStars();
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            Daylight = ComputeDaylight(timeOfDay);
            m_Night = Daylight < 0.25f;
            Apply();
        }

        void Update()
        {
            if (running)
            {
                float speed = Daylight < 0.25f ? 1f / nightSpeedFactor : 1f;
                timeOfDay = Mathf.Repeat(timeOfDay + Time.deltaTime * speed / (dayMinutes * 60f), 1f);
            }
            Apply();
        }

        void LateUpdate()
        {
            if (m_Dome == null) return;
            var cam = Camera.main;
            if (cam != null) m_Dome.position = cam.transform.position;
        }

        static float ComputeDaylight(float t)
        {
            float elevation = Mathf.Sin((t - 0.25f) * Mathf.PI * 2f);
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.15f, 0.2f, elevation));
        }

        void Apply()
        {
            float elevation = Mathf.Sin((timeOfDay - 0.25f) * Mathf.PI * 2f);
            Daylight = ComputeDaylight(timeOfDay);
            float sunset = Mathf.Clamp01(1f - Mathf.Abs(elevation) * 3.5f) * Mathf.Clamp01(Daylight * 2.5f);
            float angle = (timeOfDay - 0.25f) * 360f;

            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(angle, -30f, 0f);
                sun.color = Color.Lerp(sunColor, sunsetColor, sunset);
                sun.intensity = InCave ? 0f : sunIntensity * Daylight;
                sun.enabled = !InCave && Daylight > 0.01f;
            }
            if (moon != null)
            {
                moon.transform.rotation = Quaternion.Euler(angle + 180f, -30f, 0f);
                moon.intensity = InCave ? 0f : moonIntensity * (1f - Daylight);
                moon.enabled = !InCave && Daylight < 0.99f;
            }

            Color sky = Color.Lerp(Color.Lerp(nightSky, daySky, Daylight), sunsetSky, sunset * 0.65f);
            RenderSettings.ambientLight = InCave ? caveAmbient : Color.Lerp(nightAmbient, dayAmbient, Daylight);
            RenderSettings.fogColor = InCave ? Color.black : sky;
            if (m_Skybox != null && m_BaseExposure > 0f) m_Skybox.SetFloat("_Exposure", Mathf.Lerp(0.08f, m_BaseExposure, Daylight));
            var cam = Camera.main;
            if (cam != null && cam.clearFlags == CameraClearFlags.SolidColor) cam.backgroundColor = InCave ? Color.black : sky;

            float night = InCave ? 0f : 1f - Daylight;
            Shader.SetGlobalFloat(kNight, night);
            if (m_Dome != null)
            {
                m_Dome.gameObject.SetActive(night > 0.01f);
                m_Dome.rotation = Quaternion.Euler(angle, -30f, 0f);
            }

            bool isNight = Daylight < 0.25f;
            if (isNight != m_Night)
            {
                m_Night = isNight;
                if (isNight) onNightfall.Invoke(); else onDaybreak.Invoke();
            }
        }

        /// <summary>Salta a la mañana (dormir, un botón del menú).</summary>
        public void SkipToMorning() { TimeOfDay = 0.27f; }

        // ------------------------------------------------------------------ Estrellas: una sola malla de cuadraditos en una esfera grande

        void BuildStars()
        {
            var cam = Camera.main;
            float radius = cam != null ? cam.farClipPlane * 0.85f : 800f;
            var verts = new System.Collections.Generic.List<Vector3>();
            var cols = new System.Collections.Generic.List<Color32>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var tris = new System.Collections.Generic.List<int>();
            var rnd = new System.Random(4242);

            for (int i = 0; i < starCount; i++)
            {
                // Punto al azar en la esfera
                float z = (float)rnd.NextDouble() * 2f - 1f, a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float r = Mathf.Sqrt(1f - z * z);
                var dir = new Vector3(r * Mathf.Cos(a), z, r * Mathf.Sin(a));
                float size = radius * (0.0016f + 0.003f * (float)(rnd.NextDouble() * rnd.NextDouble()));
                byte bright = (byte)(110 + rnd.Next(146));
                byte tw = (byte)rnd.Next(256);   // fase del parpadeo
                var tint = rnd.NextDouble() < 0.15 ? new Color32(bright, (byte)(bright * 0.85f), (byte)(bright * 0.7f), tw) : new Color32((byte)(bright * 0.9f), (byte)(bright * 0.95f), bright, tw);
                AddQuad(verts, cols, uvs, tris, dir * radius, dir, size, tint);
            }
            // La luna: en el lado opuesto al sol (la cúpula gira igual que el sol; el sol se ve en su -Z, la luna en +Z)
            AddQuad(verts, cols, uvs, tris, Vector3.forward * radius * 0.98f, Vector3.forward, radius * 0.0025f * moonSize * 4f, new Color32(235, 238, 255, 255), true);

            var mesh = new Mesh { name = "Estrellas" };
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * radius * 2.2f);
            var go = new GameObject("Cielo de estrellas");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = starMaterial != null ? starMaterial : new Material(Shader.Find("VRKit/Estrellas") ?? Shader.Find("Sprites/Default"));
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.AddComponent<DestroyMeshOnDestroy>();
            m_Dome = go.transform;
        }

        static void AddQuad(System.Collections.Generic.List<Vector3> v, System.Collections.Generic.List<Color32> c, System.Collections.Generic.List<Vector2> uv,
                            System.Collections.Generic.List<int> t, Vector3 center, Vector3 dir, float size, Color32 col, bool disc = false)
        {
            var right = Vector3.Cross(dir, Mathf.Abs(dir.y) > 0.9f ? Vector3.right : Vector3.up).normalized * size;
            var up = Vector3.Cross(right, dir).normalized * size;
            int s = v.Count;
            v.Add(center - right - up); v.Add(center - right + up); v.Add(center + right + up); v.Add(center + right - up);
            for (int i = 0; i < 4; i++) c.Add(col);
            // uv.y > 1 marca la luna (el shader no la hace parpadear)
            float k = disc ? 2f : 0f;
            uv.Add(new Vector2(0f, k)); uv.Add(new Vector2(0f, 1f + k)); uv.Add(new Vector2(1f, 1f + k)); uv.Add(new Vector2(1f, k));
            // El shader dibuja las dos caras (Cull Off)
            t.Add(s); t.Add(s + 1); t.Add(s + 2); t.Add(s); t.Add(s + 2); t.Add(s + 3);
        }
    }
}
