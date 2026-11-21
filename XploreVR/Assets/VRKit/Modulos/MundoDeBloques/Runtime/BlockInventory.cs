using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRKit
{
    /// <summary>Un bloque y una cantidad (recetas, objetos iniciales, lo que suelta una criatura).</summary>
    [Serializable]
    public struct BlockStack
    {
        public BlockType block;
        [Min(1)] public int count;
        public BlockStack(BlockType block, int count) { this.block = block; this.count = count; }
    }

    /// <summary>
    /// Los bloques que lleva el jugador y cuál tiene elegido para poner.
    /// En creativo hay de todo y no se gasta nada. Implementa IAmmoSource: el GameHUD del Básico
    /// enseña cuántos quedan del bloque elegido.
    /// </summary>
    public class BlockInventory : MonoBehaviour, IAmmoSource
    {
        public static BlockInventory Instance { get; private set; }

        [Tooltip("Creativo: todos los bloques, infinitos. Lo cambia el VoxelGame según el modo.")]
        [SerializeField] bool creative = false;
        [Tooltip("Lo que lleva el jugador al empezar un mundo nuevo en supervivencia.")]
        [SerializeField] BlockStack[] startingItems = new BlockStack[0];

        readonly Dictionary<byte, int> m_Counts = new Dictionary<byte, int>();
        readonly List<byte> m_Order = new List<byte>();   // en el orden en que se consiguieron
        readonly List<byte> m_Available = new List<byte>();
        byte m_Selected;

        /// <summary>Cualquier cambio: cantidades, bloque elegido o modo.</summary>
        public event Action Changed;

        public bool Creative
        {
            get { return creative; }
            set { creative = value; Rebuild(); }
        }

        /// <summary>Id del bloque elegido (0 = ninguno).</summary>
        public byte Selected { get { return m_Selected; } }
        public BlockType SelectedType { get { var w = VoxelWorld.Instance; return w != null && m_Selected != 0 ? w.Registry.Get(m_Selected) : null; } }
        public IList<byte> Available { get { return m_Available; } }
        public int SelectedIndex { get { return m_Available.IndexOf(m_Selected); } }

        public int Ammo { get { return creative ? -1 : Count(m_Selected); } }

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }
        void Start() { Rebuild(); }

        public int Count(byte id)
        {
            if (id == 0) return 0;
            if (creative) return 9999;
            int n;
            return m_Counts.TryGetValue(id, out n) ? n : 0;
        }

        public int Count(BlockType b) { var w = VoxelWorld.Instance; return w != null ? Count(w.Registry.IdOf(b)) : 0; }

        public void Add(byte id, int amount)
        {
            if (id == 0 || amount <= 0) return;
            int n;
            m_Counts.TryGetValue(id, out n);
            m_Counts[id] = n + amount;
            if (!m_Order.Contains(id)) m_Order.Add(id);
            Rebuild();
        }

        public void Add(BlockType b, int amount) { var w = VoxelWorld.Instance; if (w != null) Add(w.Registry.IdOf(b), amount); }

        /// <summary>Quita bloques. En creativo siempre puede. Devuelve false si no hay bastantes (y no quita nada).</summary>
        public bool Remove(byte id, int amount)
        {
            if (creative) return true;
            int n;
            if (!m_Counts.TryGetValue(id, out n) || n < amount) return false;
            n -= amount;
            if (n <= 0) { m_Counts.Remove(id); m_Order.Remove(id); }
            else m_Counts[id] = n;
            Rebuild();
            return true;
        }

        public bool Remove(BlockType b, int amount) { var w = VoxelWorld.Instance; return w != null && Remove(w.Registry.IdOf(b), amount); }

        public void Clear() { m_Counts.Clear(); m_Order.Clear(); m_Selected = 0; Rebuild(); }

        public void GiveStartingItems() { foreach (var s in startingItems) if (s.block != null) Add(s.block, s.count); }

        public void Select(byte id) { if (m_Available.Contains(id)) { m_Selected = id; Notify(); } }

        public void SelectNext(int step = 1)
        {
            if (m_Available.Count == 0) return;
            int i = Mathf.Max(0, m_Available.IndexOf(m_Selected));
            i = ((i + step) % m_Available.Count + m_Available.Count) % m_Available.Count;
            m_Selected = m_Available[i];
            Notify();
        }

        void Rebuild()
        {
            m_Available.Clear();
            var w = VoxelWorld.Instance;
            if (creative && w != null && w.Registry != null)
            {
                foreach (var b in w.Registry.blocks)
                    if (b != null && b.inCreativeMenu) { byte id = w.Registry.IdOf(b); if (id != 0) m_Available.Add(id); }
            }
            else m_Available.AddRange(m_Order);

            if (!m_Available.Contains(m_Selected)) m_Selected = m_Available.Count > 0 ? m_Available[0] : (byte)0;
            Notify();
        }

        void Notify() { if (Changed != null) Changed(); }

        // ------------------------------------------------------------------ Guardar (por nombre: sobrevive a cambios en la lista de bloques)

        public List<VoxelItemSave> ToSave()
        {
            var list = new List<VoxelItemSave>();
            var w = VoxelWorld.Instance;
            if (w == null) return list;
            foreach (var id in m_Order)
            {
                var b = w.Registry.Get(id);
                if (b != null) list.Add(new VoxelItemSave { block = b.displayName, count = m_Counts[id] });
            }
            return list;
        }

        public void FromSave(List<VoxelItemSave> items, string selected)
        {
            m_Counts.Clear(); m_Order.Clear();
            var w = VoxelWorld.Instance;
            if (w != null && items != null)
                foreach (var it in items)
                {
                    var b = w.Registry.Find(it.block);
                    if (b != null && it.count > 0) { m_Counts[b.Id] = it.count; m_Order.Add(b.Id); }
                }
            var sel = w != null ? w.Registry.Find(selected) : null;
            m_Selected = sel != null ? sel.Id : (byte)0;
            Rebuild();
        }
    }

    [Serializable]
    public class VoxelItemSave
    {
        public string block;
        public int count;
    }
}
