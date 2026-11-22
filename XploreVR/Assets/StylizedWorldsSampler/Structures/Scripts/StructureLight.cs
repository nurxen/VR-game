using UnityEngine;

namespace StylizedWorldsSampler
{
    /// <summary>
    /// A building's own light: a hearth fire that flickers, or a lamp that is lit at night. Night comes from the
    /// Stylized Biomes Day Night controller when one is in the scene (the global _SB_Night), or from Always On.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class StructureLight : MonoBehaviour
    {
        public enum Kind { Fire, Lamp }
        public Kind kind = Kind.Fire;
        [Tooltip("Lit all the time (ignore day and night).")]
        public bool alwaysOn;
        [Min(0f)] public float intensity = 2f;
        [Range(0f, 1f)] public float flicker = 0.25f;

        static readonly int NightId = Shader.PropertyToID("_SB_Night");
        Light lamp;
        float seed;

        void Awake() { lamp = GetComponent<Light>(); seed = Random.value * 100f; }

        void Update()
        {
            float night = Shader.GetGlobalFloat(NightId);
            // a fire burns all day (dimmer by day); lamps only at night, unless always on
            float on = alwaysOn ? 1f : kind == Kind.Fire ? Mathf.Lerp(0.55f, 1f, night) : Mathf.SmoothStep(0f, 1f, (night - 0.35f) / 0.45f);
            float t = Time.time * (kind == Kind.Fire ? 9f : 3f) + seed;
            float n = Mathf.PerlinNoise(t, seed) * 0.7f + Mathf.PerlinNoise(t * 2.3f, seed + 7f) * 0.3f;
            lamp.intensity = intensity * on * (1f - flicker + flicker * 2f * n);
            lamp.enabled = lamp.intensity > 0.01f;
        }
    }
}
