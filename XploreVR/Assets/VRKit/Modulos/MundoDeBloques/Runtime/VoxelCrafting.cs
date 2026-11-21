using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace VRKit
{
    /// <summary>
    /// Fabricar en VR sin menús: se abre un arco de recetas delante del jugador (bloquecitos flotando con sus ingredientes)
    /// y se fabrica tocando una con la mano. Las que no puedes pagar salen apagadas.
    ///
    ///   · B (mano derecha): recetas de bolsillo (las que no necesitan mesa).
    ///   · Botón de poner sobre una Mesa de trabajo: todas las recetas.
    /// Se cierra al pulsar B otra vez o al alejarte.
    /// </summary>
    public class VoxelCrafting : MonoBehaviour
    {
        public static VoxelCrafting Instance { get; private set; }

        [SerializeField] List<VoxelRecipe> recipes = new List<VoxelRecipe>();
        [Tooltip("Mano cuyo botón B/Y abre las recetas de bolsillo.")]
        [SerializeField] Hand pocketHand = Hand.Right;
        [SerializeField] bool pocketCrafting = true;
        [Tooltip("Radio del arco, en metros.")]
        [SerializeField] float radius = 0.42f;
        [SerializeField] float itemSize = 0.06f;
        [Tooltip("Se cierra si te alejas más de esto (metros) de donde se abrió.")]
        [SerializeField] float closeDistance = 1.5f;
        [SerializeField] AudioClip craftSound;
        [SerializeField] Color availableColor = Color.white;
        [SerializeField] Color missingColor = new Color(1f, 0.45f, 0.4f);

        class Entry { public VoxelRecipe recipe; public Transform item; public TextMeshPro label; public bool inside; }

        readonly List<Entry> m_Entries = new List<Entry>();
        Transform m_Root;
        HandInput m_Input;
        Vector3 m_OpenedAt;
        bool m_WithTable;

        public bool IsOpen { get { return m_Root != null && m_Root.gameObject.activeSelf; } }
        public List<VoxelRecipe> Recipes { get { return recipes; } }

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; if (m_Root != null) Destroy(m_Root.gameObject); }
        void OnEnable() { if (pocketCrafting) m_Input = new HandInput(pocketHand); }
        void OnDisable() { if (m_Input != null) { m_Input.Dispose(); m_Input = null; } Close(); }

        /// <summary>Abre las recetas delante del jugador. withTable = incluye las que necesitan mesa.</summary>
        public void Open(bool withTable)
        {
            var player = VRPlayer.Instance;
            var world = VoxelWorld.Instance;
            var inv = BlockInventory.Instance;
            if (player == null || player.Head == null || world == null || inv == null) return;
            if (IsOpen && withTable == m_WithTable) { Close(); return; }
            Close();
            m_WithTable = withTable;

            if (m_Root == null) m_Root = new GameObject("Fabricar").transform;
            m_Root.gameObject.SetActive(true);
            Vector3 fwd = player.HeadForwardFlat;
            m_OpenedAt = player.Head.position;
            m_Root.position = player.Head.position - Vector3.up * 0.38f;
            m_Root.rotation = Quaternion.LookRotation(fwd, Vector3.up);

            var list = new List<VoxelRecipe>();
            foreach (var r in recipes) if (r != null && (withTable || !r.needsTable)) list.Add(r);
            if (list.Count == 0) { Label(m_Root, new Vector3(0f, 0f, radius), withTable ? "No hay recetas" : "Sin recetas de bolsillo: busca una mesa", 0.25f, availableColor); return; }

            float arc = Mathf.Min(150f, 22f * list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                float a = list.Count == 1 ? 0f : Mathf.Lerp(-arc * 0.5f, arc * 0.5f, i / (float)(list.Count - 1));
                Vector3 pos = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, radius);
                var e = new Entry { recipe = list[i] };
                var holder = new GameObject(list[i].DisplayName).transform;
                holder.SetParent(m_Root, false);
                holder.localPosition = pos;
                e.item = MakeItem(holder, list[i], world);
                e.label = Label(holder, new Vector3(0f, -itemSize * 1.1f, 0f), "", 0.14f, availableColor);
                m_Entries.Add(e);
            }
            Title(withTable ? "Mesa de trabajo" : "Fabricar");
            UpdateLabels();
        }

        public void Close()
        {
            m_Entries.Clear();
            if (m_Root == null) return;
            for (int i = m_Root.childCount - 1; i >= 0; i--) Destroy(m_Root.GetChild(i).gameObject);
            m_Root.gameObject.SetActive(false);
        }

        Transform MakeItem(Transform parent, VoxelRecipe r, VoxelWorld world)
        {
            if (r.outputPrefab != null || r.output.block == null)
            {
                // Un objeto: se enseña una copia en miniatura sin física ni scripts
                var preview = new GameObject("Vista");
                preview.transform.SetParent(parent, false);
                if (r.outputPrefab != null)
                {
                    foreach (var mf in r.outputPrefab.GetComponentsInChildren<MeshFilter>())
                    {
                        var mr = mf.GetComponent<MeshRenderer>();
                        if (mr == null) continue;
                        var part = new GameObject(mf.name);
                        part.transform.SetParent(preview.transform, false);
                        part.transform.localPosition = r.outputPrefab.transform.InverseTransformPoint(mf.transform.position);
                        part.transform.localRotation = Quaternion.Inverse(r.outputPrefab.transform.rotation) * mf.transform.rotation;
                        part.transform.localScale = mf.transform.lossyScale;
                        part.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                        part.AddComponent<MeshRenderer>().sharedMaterials = mr.sharedMaterials;
                    }
                }
                preview.transform.localScale = Vector3.one * (itemSize * 2.2f);
                return preview.transform;
            }
            var go = new GameObject("Vista");
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * itemSize;
            go.AddComponent<MeshFilter>().sharedMesh = world.GetItemMesh(r.output.block.Id);
            go.AddComponent<MeshRenderer>().sharedMaterial = world.Material;
            return go.transform;
        }

        TextMeshPro Label(Transform parent, Vector3 pos, string text, float size, Color color)
        {
            var go = new GameObject("Texto");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var t = go.AddComponent<TextMeshPro>();
            t.text = text;
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center;
            t.color = color;
            t.rectTransform.sizeDelta = new Vector2(0.22f, 0.08f);
            return t;
        }

        void Title(string text)
        {
            var t = Label(m_Root, new Vector3(0f, 0.13f, radius), text, 0.3f, availableColor);
            t.fontStyle = FontStyles.Bold;
        }

        void UpdateLabels()
        {
            var inv = BlockInventory.Instance;
            foreach (var e in m_Entries)
            {
                bool ok = e.recipe.CanCraft(inv);
                int n = e.recipe.outputPrefab != null ? 1 : e.recipe.output.count;
                e.label.text = "<b>" + (n > 1 ? n + " " : "") + e.recipe.DisplayName + "</b>\n" + e.recipe.InputsText();
                e.label.color = ok ? availableColor : missingColor;
            }
        }

        void Update()
        {
            if (m_Input != null && m_Input.Secondary.WasPressedThisFrame())
            {
                if (IsOpen) Close(); else Open(false);
            }
            if (!IsOpen) return;

            var player = VRPlayer.Instance;
            if (player == null || player.Head == null || Vector3.Distance(player.Head.position, m_OpenedAt) > closeDistance) { Close(); return; }

            var cam = Camera.main;
            foreach (var e in m_Entries)
            {
                e.item.Rotate(0f, 50f * Time.deltaTime, 0f, Space.Self);
                if (cam != null) e.label.transform.rotation = Quaternion.LookRotation(e.label.transform.position - cam.transform.position, Vector3.up);

                // ¿La toca alguna mano?
                bool inside = Touching(player.LeftHand, e.item.position) || Touching(player.RightHand, e.item.position);
                if (inside && !e.inside) Craft(e);
                e.inside = inside;
            }
        }

        bool Touching(Transform h, Vector3 p)
        {
            return h != null && Vector3.Distance(h.position + h.forward * 0.04f, p) < itemSize * 1.1f;
        }

        void Craft(Entry e)
        {
            var inv = BlockInventory.Instance;
            var player = VRPlayer.Instance;
            if (inv == null) return;
            if (!e.recipe.CanCraft(inv))
            {
                VRHaptics.SendHand(player.LeftHand, 0.1f, 0.03f);
                VRHaptics.SendHand(player.RightHand, 0.1f, 0.03f);
                return;
            }
            foreach (var s in e.recipe.inputs) if (s.block != null) inv.Remove(s.block, s.count);
            if (e.recipe.outputPrefab != null)
            {
                Vector3 p = player.Head.position + player.HeadForwardFlat * 0.45f - Vector3.up * 0.35f;
                Instantiate(e.recipe.outputPrefab, p, Quaternion.LookRotation(player.HeadForwardFlat));
            }
            else if (e.recipe.output.block != null) inv.Add(e.recipe.output.block, Mathf.Max(1, e.recipe.output.count));

            if (craftSound != null) VRAudio.Play2D(craftSound, 0.6f);
            VRHaptics.SendHand(player.LeftHand, 0.35f, 0.05f);
            VRHaptics.SendHand(player.RightHand, 0.35f, 0.05f);
            FloatingText.Spawn("+ " + e.recipe.DisplayName, e.item.position + Vector3.up * 0.08f, availableColor, 0.6f);
            UpdateLabels();
        }
    }
}
