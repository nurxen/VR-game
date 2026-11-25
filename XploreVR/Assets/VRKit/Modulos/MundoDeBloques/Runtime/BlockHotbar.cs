using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace VRKit
{
    /// <summary>
    /// La barra de bloques en la muñeca: bloquecitos de verdad flotando sobre el mando, con cuántos te quedan.
    /// Para elegir: Y = siguiente, X = anterior (en la mano de la barra), o tócalos con la otra mano.
    /// Si hay más bloques que huecos, la barra pasa de página sola.
    /// </summary>
    public class BlockHotbar : MonoBehaviour
    {
        [SerializeField] Hand hand = Hand.Left;
        [SerializeField, Range(3, 12)] int slots = 9;
        [Tooltip("Separación entre bloques, en metros.")]
        [SerializeField] float spacing = 0.034f;
        [Tooltip("Tamaño de cada bloquecito, en metros.")]
        [SerializeField] float blockSize = 0.022f;
        [Tooltip("Posición de la barra respecto al mando (X derecha, Y arriba, Z adelante).")]
        [SerializeField] Vector3 localOffset = new Vector3(0f, 0.055f, -0.07f);
        [SerializeField] Vector3 localEuler = new Vector3(-35f, 0f, 0f);
        [Tooltip("Elegir tocando los bloques con la otra mano.")]
        [SerializeField] bool touchToSelect = true;
        [SerializeField] bool buttonsToSelect = true;
        [SerializeField] bool showCounts = true;
        [SerializeField] Color textColor = Color.white;

        class Slot { public GameObject go; public MeshFilter filter; public TextMeshPro count; public byte id; }

        readonly List<Slot> m_Slots = new List<Slot>();
        HandInput m_Input;
        Transform m_Root, m_Hand, m_OtherHand;
        TextMeshPro m_Name;
        BlockInventory m_Inv;
        bool m_Dirty = true;
        int m_Touching = -1;

        void OnEnable() { if (buttonsToSelect) m_Input = new HandInput(hand); }
        void OnDisable()
        {
            if (m_Input != null) { m_Input.Dispose(); m_Input = null; }
            if (m_Inv != null) m_Inv.Changed -= OnChanged;
            m_Inv = null;
            if (m_Root != null) m_Root.gameObject.SetActive(false);
        }
        void OnDestroy() { if (m_Root != null) Destroy(m_Root.gameObject); }
        void OnChanged() { m_Dirty = true; }

        bool Setup()
        {
            var world = VoxelWorld.Instance;
            var player = VRPlayer.Instance;
            if (world == null || !world.IsReady || player == null) return false;
            if (m_Inv == null)
            {
                m_Inv = BlockInventory.Instance;
                if (m_Inv == null) return false;
                m_Inv.Changed += OnChanged;
                m_Dirty = true;
            }
            if (m_Root != null) { m_Root.gameObject.SetActive(true); return true; }

            m_Hand = player.GetHand(hand);
            m_OtherHand = player.GetHand(hand == Hand.Left ? Hand.Right : Hand.Left);
            if (m_Hand == null) return false;
            m_Root = new GameObject("Barra de bloques").transform;
            m_Root.SetParent(m_Hand, false);
            m_Root.localPosition = localOffset;
            m_Root.localRotation = Quaternion.Euler(localEuler);

            for (int i = 0; i < slots; i++)
            {
                var s = new Slot();
                s.go = new GameObject("Hueco " + (i + 1));
                s.go.transform.SetParent(m_Root, false);
                s.go.transform.localPosition = new Vector3((i - (slots - 1) * 0.5f) * spacing, 0f, 0f);
                s.go.transform.localScale = Vector3.one * blockSize;
                s.filter = s.go.AddComponent<MeshFilter>();
                var r = s.go.AddComponent<MeshRenderer>();
                r.sharedMaterial = world.Material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (showCounts) s.count = MakeText(m_Root, s.go.transform.localPosition + new Vector3(0f, -0.022f, 0f), 0.09f);
                m_Slots.Add(s);
            }
            m_Name = MakeText(m_Root, new Vector3(0f, 0.032f, 0f), 0.13f);
            return true;
        }

        TextMeshPro MakeText(Transform parent, Vector3 pos, float size)
        {
            var go = new GameObject("Texto");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var t = go.AddComponent<TextMeshPro>();
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center;
            t.color = textColor;
            t.enableWordWrapping = false;
            t.rectTransform.sizeDelta = new Vector2(0.3f, 0.03f);
            return t;
        }

        void Update()
        {
            if (!Setup()) return;

            if (m_Input != null)
            {
                if (m_Input.Secondary.WasPressedThisFrame()) { m_Inv.SelectNext(1); Tick(m_Hand); }
                if (m_Input.Primary.WasPressedThisFrame()) { m_Inv.SelectNext(-1); Tick(m_Hand); }
            }
            if (m_Dirty) Refresh();
            if (touchToSelect) CheckTouch();
            FaceTexts();
        }

        void Refresh()
        {
            m_Dirty = false;
            var world = VoxelWorld.Instance;
            var list = m_Inv.Available;
            int sel = Mathf.Max(0, m_Inv.SelectedIndex);
            int page = list.Count > 0 ? sel / slots : 0;
            for (int i = 0; i < m_Slots.Count; i++)
            {
                var s = m_Slots[i];
                int idx = page * slots + i;
                s.id = idx < list.Count ? list[idx] : (byte)0;
                s.go.SetActive(s.id != 0);
                if (s.count != null) s.count.gameObject.SetActive(s.id != 0 && !m_Inv.Creative);
                if (s.id == 0) continue;
                s.filter.sharedMesh = world.GetItemMesh(s.id);
                bool selected = s.id == m_Inv.Selected;
                s.go.transform.localScale = Vector3.one * blockSize * (selected ? 1.45f : 1f);
                var p = s.go.transform.localPosition; p.y = selected ? 0.008f : 0f; s.go.transform.localPosition = p;
                if (s.count != null) s.count.text = m_Inv.Count(s.id).ToString();
            }
            if (m_Name != null)
            {
                var t = m_Inv.SelectedType;
                string pages = list.Count > slots ? "  (" + (page + 1) + "/" + ((list.Count + slots - 1) / slots) + ")" : "";
                m_Name.text = t != null ? t.displayName + pages : (m_Inv.Creative ? "" : "Vacío: rompe bloques");
            }
        }

        void CheckTouch()
        {
            if (m_OtherHand == null) return;
            Vector3 tip = m_OtherHand.position + m_OtherHand.forward * 0.04f;
            int touching = -1;
            for (int i = 0; i < m_Slots.Count; i++)
                if (m_Slots[i].id != 0 && Vector3.Distance(tip, m_Slots[i].go.transform.position) < spacing * 0.6f) { touching = i; break; }
            if (touching >= 0 && touching != m_Touching)
            {
                m_Inv.Select(m_Slots[touching].id);
                Tick(m_OtherHand);
            }
            m_Touching = touching;
        }

        void FaceTexts()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 head = cam.transform.position;
            foreach (var s in m_Slots) if (s.count != null && s.count.gameObject.activeSelf) Face(s.count.transform, head);
            if (m_Name != null) Face(m_Name.transform, head);
        }

        // TextMeshPro se lee desde -Z: el eje Z del texto tiene que apuntar lejos de la cabeza
        static void Face(Transform t, Vector3 head) { t.rotation = Quaternion.LookRotation(t.position - head, Vector3.up); }

        static void Tick(Transform h) { VRHaptics.SendHand(h, 0.2f, 0.03f); }
    }
}
