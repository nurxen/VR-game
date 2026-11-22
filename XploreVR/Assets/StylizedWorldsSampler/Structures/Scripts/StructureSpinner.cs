using UnityEngine;

namespace StylizedWorldsSampler
{
    /// <summary>
    /// Turns a part about one of its own axes: windmill sails, water wheels. With Follow Wind on, sails turn faster in
    /// a stronger wind (the Stylized Biomes wind, global _SB_WindStrength, when one is in the scene).
    /// </summary>
    public class StructureSpinner : MonoBehaviour
    {
        public Vector3 axis = Vector3.forward;
        [Tooltip("Degrees per second.")]
        public float speed = 30f;
        public bool followWind;

        static readonly int WindId = Shader.PropertyToID("_SB_WindStrength");

        void Update()
        {
            float s = speed;
            if (followWind)
            {
                float w = Shader.GetGlobalFloat(WindId);
                s *= w > 0f ? Mathf.Clamp(w * 1.6f, 0.3f, 3f) : 1f;
            }
            transform.Rotate(axis, s * Time.deltaTime, Space.Self);
        }
    }
}
