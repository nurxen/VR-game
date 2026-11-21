using UnityEngine;

namespace VRKit
{
    /// <summary>
    /// Una receta: ingredientes (bloques) → bloques nuevos, o un objeto (una herramienta, por ejemplo).
    /// Botón derecho › Create › VRKit › Mundo de bloques › Receta. Añadidla a la lista del VoxelCrafting.
    /// </summary>
    [CreateAssetMenu(menuName = "VRKit/Mundo de bloques/Receta", fileName = "Receta")]
    public class VoxelRecipe : ScriptableObject
    {
        public BlockStack[] inputs = new BlockStack[0];
        [Tooltip("Bloques que da. Vacío si la receta da un objeto.")]
        public BlockStack output;
        [Tooltip("Objeto que aparece delante del jugador (un pico, una antorcha…). Opcional.")]
        public GameObject outputPrefab;
        [Tooltip("Nombre que se ve si da un objeto.")]
        public string outputName = "";
        [Tooltip("Solo se puede hacer en una mesa de trabajo.")]
        public bool needsTable = false;

        public string DisplayName
        {
            get
            {
                if (outputPrefab != null) return string.IsNullOrEmpty(outputName) ? outputPrefab.name : outputName;
                return output.block != null ? output.block.displayName : name;
            }
        }

        public bool CanCraft(BlockInventory inv)
        {
            if (inv == null) return false;
            foreach (var s in inputs) if (s.block != null && inv.Count(s.block) < s.count) return false;
            return true;
        }

        /// <summary>Texto corto con los ingredientes: «2 Tronco · 1 Piedra».</summary>
        public string InputsText()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var s in inputs)
            {
                if (s.block == null) continue;
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append(s.count).Append(' ').Append(s.block.displayName);
            }
            return sb.ToString();
        }
    }
}
