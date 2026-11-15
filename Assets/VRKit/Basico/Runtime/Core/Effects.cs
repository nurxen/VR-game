using UnityEngine;

namespace VRKit
{
    /// <summary>
    /// Efectos de prototipo hechos con formas básicas, para que todo se vea aunque aún no tengáis partículas.
    /// Cambiadlos por vuestros efectos cuando los tengáis (los scripts aceptan un prefab de efecto).
    /// </summary>
    public static class Effects
    {
        /// <summary>Una esfera que crece y desaparece (explosiones, hechizos, golpes).</summary>
        public static void Burst(Vector3 position, Color color, float size, float duration)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = "Efecto";
            go.transform.position = position;
            var fx = go.AddComponent<BurstFx>();
            fx.color = color; fx.size = size; fx.duration = duration;
        }
    }
}
