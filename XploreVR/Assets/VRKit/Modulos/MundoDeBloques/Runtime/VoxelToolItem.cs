using UnityEngine;

namespace VRKit
{
    /// <summary>
    /// Herramienta que se coge con la mano (pico, pala, hacha, espada). Mientras la sujetas, esa mano rompe
    /// más rápido los bloques de su tipo y pega más fuerte. Ponedla en un objeto con XR Grab Interactable.
    /// </summary>
    public class VoxelToolItem : MonoBehaviour
    {
        public VoxelToolType toolType = VoxelToolType.Pico;
        [Tooltip("Cuántas veces más rápido rompe los bloques de su tipo.")]
        [Min(1f)] public float speed = 4f;
        [Tooltip("Daño a las criaturas (la mano vacía hace el del VoxelHandTool).")]
        [Min(0f)] public float damage = 6f;
        [Tooltip("Usos antes de romperse (0 = no se rompe).")]
        [Min(0)] public int durability = 0;

        int m_Used;

        /// <summary>Lo llama la mano al romper un bloque o golpear. Si se gasta, desaparece.</summary>
        public void Use()
        {
            if (durability <= 0) return;
            m_Used++;
            if (m_Used >= durability)
            {
                Effects.Burst(transform.position, new Color(0.6f, 0.5f, 0.4f), 0.25f, 0.25f);
                Destroy(gameObject);
            }
        }

        public float Wear { get { return durability <= 0 ? 0f : (float)m_Used / durability; } }
    }
}
