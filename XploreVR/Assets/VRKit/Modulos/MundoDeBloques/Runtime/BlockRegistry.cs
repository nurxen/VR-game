using System.Collections.Generic;
using UnityEngine;

namespace VRKit
{
    /// <summary>
    /// La lista de bloques del juego, la textura atlas y el material. El orden de la lista da los números (ids):
    /// el primero es el 1 (el 0 es el aire). Podéis añadir bloques al final sin miedo; las partidas guardadas
    /// guardan los nombres, así que también se puede reordenar.
    /// </summary>
    [CreateAssetMenu(menuName = "VRKit/Mundo de bloques/Registro de bloques", fileName = "Registro de bloques")]
    public class BlockRegistry : ScriptableObject
    {
        [Tooltip("Textura con todas las caras de los bloques en casillas iguales. Importadla con Filter Mode: Point y sin Mip Maps.")]
        public Texture2D atlas;
        [Min(1)] public int atlasColumns = VoxelAtlasPainter.Columns;
        [Min(1)] public int atlasRows = VoxelAtlasPainter.Rows;
        [Tooltip("Material con el shader VRKit/Voxel y el atlas. Vacío = se crea al empezar.")]
        public Material material;
        public List<BlockType> blocks = new List<BlockType>();

        BlockMeshInfo[] m_Info;
        BlockType[] m_ById;
        Dictionary<string, BlockType> m_ByName;

        public BlockMeshInfo[] MeshInfo { get { Init(); return m_Info; } }

        /// <summary>Asigna los ids y prepara las tablas. Se llama solo; llamadlo otra vez si cambiáis la lista en juego.</summary>
        public void Init(bool force = false)
        {
            if (m_Info != null && !force) return;
            m_Info = new BlockMeshInfo[256];
            m_ById = new BlockType[256];
            m_ByName = new Dictionary<string, BlockType>();
            int count = Mathf.Min(blocks.Count, 255);
            if (blocks.Count > 255) Debug.LogWarning("[VRKit] Máximo 255 tipos de bloque: los demás se ignoran.");
            for (int i = 0; i < count; i++)
            {
                var b = blocks[i];
                if (b == null) continue;
                byte id = (byte)(i + 1);
                b.Id = id;
                m_ById[id] = b;
                m_Info[id] = b.ToMeshInfo();
                if (!m_ByName.ContainsKey(b.displayName)) m_ByName.Add(b.displayName, b);
            }
        }

        void OnEnable() { m_Info = null; }
        void OnValidate() { m_Info = null; }

        public BlockType Get(byte id) { Init(); return m_ById[id]; }
        public byte IdOf(BlockType b) { Init(); return b != null && m_ById[b.Id] == b ? b.Id : (byte)0; }
        public BlockType Find(string displayName) { Init(); BlockType b; return displayName != null && m_ByName.TryGetValue(displayName, out b) ? b : null; }

        public VoxelAtlasLayout Layout
        {
            get { return new VoxelAtlasLayout(atlasColumns, atlasRows, atlas != null ? atlas.width : 128); }
        }

        /// <summary>Nombres por id, para guardar partidas (y traducir ids si la lista cambia).</summary>
        public string[] Palette()
        {
            Init();
            var p = new string[256];
            for (int i = 1; i < 256; i++) p[i] = m_ById[i] != null ? m_ById[i].displayName : null;
            return p;
        }
    }
}
