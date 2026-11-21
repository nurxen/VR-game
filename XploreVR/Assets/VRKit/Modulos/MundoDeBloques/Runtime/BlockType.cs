using UnityEngine;

namespace VRKit
{
    /// <summary>Herramientas que rompen más rápido ciertos bloques.</summary>
    public enum VoxelToolType { Ninguna, Pico, Pala, Hacha, Espada }

    /// <summary>
    /// Un tipo de bloque: tierra, piedra, tablones… Es un asset: botón derecho › Create › VRKit › Mundo de bloques › Bloque.
    /// Para que exista en el mundo, añadidlo a la lista del BlockRegistry.
    /// </summary>
    [CreateAssetMenu(menuName = "VRKit/Mundo de bloques/Bloque", fileName = "Bloque")]
    public class BlockType : ScriptableObject
    {
        [Tooltip("Nombre que se ve en el inventario. También identifica el bloque en las partidas guardadas: si lo cambiáis, los guardados antiguos perderán ese bloque.")]
        public string displayName = "Bloque";

        [Header("Aspecto")]
        public BlockShape shape = BlockShape.Cubo;
        [Tooltip("Casilla del atlas para la cara de arriba (0 = arriba a la izquierda, contando por filas).")]
        public int tileTop;
        [Tooltip("Casilla del atlas para los lados (y para las dos caras de un bloque en Cruz).")]
        public int tileSide;
        [Tooltip("Casilla del atlas para la cara de abajo.")]
        public int tileBottom;
        [Tooltip("Tiene huecos o transparencias (hojas, cristal): no tapa las caras de al lado.")]
        public bool transparent = false;
        [Tooltip("Color de las partículas al romperlo.")]
        public Color particleColor = new Color(0.5f, 0.4f, 0.3f);

        [Header("Comportamiento")]
        [Tooltip("Se choca con él. Desmarcado: flores, hierba alta (se atraviesan).")]
        public bool solid = true;
        [Tooltip("Al poner un bloque encima, este se sustituye (hierba alta, flores).")]
        public bool replaceable = false;
        [Tooltip("Segundos para romperlo con la mano en supervivencia. Negativo = irrompible (lecho de roca).")]
        public float hardness = 0.75f;
        [Tooltip("Herramienta que lo rompe más rápido.")]
        public VoxelToolType bestTool = VoxelToolType.Ninguna;
        [Tooltip("Sin esa herramienta se rompe, pero no suelta nada (la piedra sin pico).")]
        public bool needsTool = false;
        [Tooltip("Qué suelta al romperlo. Vacío = el propio bloque. La piedra suelta roca, la hierba suelta tierra.")]
        public BlockType drop;
        [Min(0)] public int dropCount = 1;
        [Tooltip("Al usarlo (botón de poner) abre la fabricación con las recetas de mesa.")]
        public bool opensCrafting = false;
        [Tooltip("Sale en el inventario del modo creativo.")]
        public bool inCreativeMenu = true;

        [Header("Sonido (opcional)")]
        public AudioClip breakSound;
        public AudioClip placeSound;

        /// <summary>Número del bloque en el mundo (lo asigna el BlockRegistry; 0 es el aire).</summary>
        public byte Id { get; internal set; }

        public BlockType Drop { get { return drop != null ? drop : this; } }

        public BlockMeshInfo ToMeshInfo()
        {
            return new BlockMeshInfo
            {
                exists = true,
                shape = shape,
                opaque = shape == BlockShape.Cubo && !transparent,
                solid = solid,
                tileTop = tileTop,
                tileSide = tileSide,
                tileBottom = tileBottom,
            };
        }
    }
}
