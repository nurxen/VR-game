using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace VRKit
{
    public enum VoxelHandRole
    {
        /// <summary>Gatillo: romper (mantener) y pegar a criaturas.</summary>
        Romper,
        /// <summary>Gatillo: poner el bloque elegido. Sobre una mesa de trabajo, abre la fabricación.</summary>
        Poner,
        /// <summary>Gatillo rompe, botón A/X pone. Para jugar con una sola mano.</summary>
        Ambos,
    }

    public enum VoxelAimMode
    {
        /// <summary>Un rayo desde el mando, hasta Reach bloques (como en Minecraft).</summary>
        Rayo,
        /// <summary>Tocar el bloque con el mando (maquetas sobre la mesa, construir a mano).</summary>
        Tocar,
        /// <summary>Si el mando toca un bloque, ese; si no, el rayo.</summary>
        RayoYTocar,
    }

    /// <summary>
    /// La mano que rompe y pone bloques. Uno por mano: por defecto la derecha rompe y la izquierda pone.
    /// Si la mano sujeta algo que no es una herramienta (una espada de otro módulo, una linterna), se aparta.
    /// </summary>
    public class VoxelHandTool : MonoBehaviour
    {
        [SerializeField] Hand hand = Hand.Right;
        [SerializeField] VoxelHandRole role = VoxelHandRole.Romper;
        [SerializeField] VoxelAimMode aim = VoxelAimMode.Rayo;
        [Tooltip("Alcance del rayo, en bloques.")]
        [SerializeField, Range(1f, 12f)] float reach = 5f;
        [Tooltip("Metros por delante del mando donde está la «punta» que toca.")]
        [SerializeField] float touchOffset = 0.06f;

        [Header("Romper")]
        [Tooltip("En creativo, segundos entre bloque y bloque si mantienes el gatillo.")]
        [SerializeField] float creativeRepeat = 0.22f;
        [Tooltip("Daño de un puñetazo (con herramienta, el de la herramienta).")]
        [SerializeField] float handDamage = 3f;
        [SerializeField] float attackCooldown = 0.35f;
        [Tooltip("Lo que se rompe cae como un bloquecito que viene hacia ti. Desmarcado: va directo al inventario.")]
        [SerializeField] bool dropsAsItems = true;

        [Header("Poner")]
        [Tooltip("Segundos entre bloque y bloque si mantienes el botón.")]
        [SerializeField] float placeRepeat = 0.25f;

        [Header("Aspecto")]
        [SerializeField] Material outlineMaterial;
        [SerializeField] Color breakColor = new Color(1f, 1f, 1f, 0.9f);
        [SerializeField] Color miningColor = new Color(1f, 0.35f, 0.25f, 1f);
        [SerializeField] Color placeColor = new Color(0.35f, 0.85f, 1f, 0.9f);
        [Tooltip("Grosor de la línea del contorno, en metros (se reduce en las maquetas).")]
        [SerializeField] float lineWidth = 0.012f;
        [Tooltip("Enseña en la mano el bloque elegido (solo si esta mano pone bloques).")]
        [SerializeField] bool showHeldBlock = true;

        [Header("Sonidos (si el bloque no tiene los suyos)")]
        [SerializeField] AudioClip breakSound;
        [SerializeField] AudioClip placeSound;
        [SerializeField] AudioClip hitSound;
        [SerializeField] AudioClip deniedSound;
        [Tooltip("Al recoger un bloque suelto del suelo.")]
        [SerializeField] AudioClip pickupSound;

        HandInput m_Input;
        Transform m_Hand;
        LineRenderer m_Line;
        MeshFilter m_HeldFilter;
        GameObject m_Held;
        VRMenuManager[] m_Menus;
        readonly List<IUIInteractor> m_UI = new List<IUIInteractor>();
        readonly Vector3[] m_Box = new Vector3[16];
        static readonly Collider[] s_Overlap = new Collider[16];

        Vector3Int m_MiningBlock = new Vector3Int(int.MinValue, 0, 0);
        float m_Progress, m_NextTick, m_NextRepeat, m_NextPlace, m_NextAttack, m_NextDenied;
        byte m_HeldShown = 255;

        public Hand Hand { get { return hand; } }
        public VoxelHandRole Role { get { return role; } set { role = value; } }
        /// <summary>De 0 a 1: cuánto falta para romper el bloque que estás picando.</summary>
        public float MiningProgress { get { return m_Progress; } }

        bool Breaks { get { return role != VoxelHandRole.Poner; } }
        bool Places { get { return role != VoxelHandRole.Romper; } }

        void OnEnable()
        {
            if (pickupSound != null) VoxelDrop.PickupSound = pickupSound;
            m_Input = new HandInput(hand);
        }

        void OnDisable()
        {
            if (m_Input != null) { m_Input.Dispose(); m_Input = null; }
            if (m_Line != null) m_Line.enabled = false;
            if (m_Held != null) m_Held.SetActive(false);
        }

        void OnDestroy()
        {
            if (m_Line != null) Destroy(m_Line.gameObject);
            if (m_Held != null) Destroy(m_Held);
        }

        bool ResolveHand()
        {
            if (m_Hand != null) return true;
            var p = VRPlayer.Instance;
            m_Hand = p != null ? p.GetHand(hand) : null;
            if (m_Hand == null) return false;
            m_Hand.GetComponentsInChildren(true, m_UI);
            m_Menus = FindObjectsByType<VRMenuManager>(FindObjectsSortMode.None);
            return true;
        }

        void Update()
        {
            var world = VoxelWorld.Instance;
            if (world == null || !world.IsReady || m_Input == null || !ResolveHand()) { ShowOutline(false, Vector3Int.zero, Color.clear); ShowHeld(0); return; }

            VoxelToolItem tool;
            bool holdingOther = HoldingSomethingElse(out tool);
            bool busy = holdingOther || Time.timeScale == 0f || MenuOpen() || PointingAtUI();
            if (busy) { ShowOutline(false, Vector3Int.zero, Color.clear); ShowHeld(0); m_Progress = 0f; return; }

            var inv = BlockInventory.Instance;
            bool creative = inv != null && inv.Creative;
            ShowHeld(Places && tool == null && inv != null ? inv.Selected : (byte)0);

            // ---------------------------------------------------------- ¿A qué apunto?
            Vector3 origin = m_Hand.position, dir = m_Hand.forward;
            bool hasBreak = false, hasPlace = false;
            Vector3Int breakCell = Vector3Int.zero, placeCell = Vector3Int.zero;
            float targetDist = float.PositiveInfinity;

            if (aim != VoxelAimMode.Rayo)
            {
                Vector3 tip = origin + dir * touchOffset;
                var cell = world.WorldToBlock(tip);
                if (world.GetBlock(cell) != 0) { hasBreak = true; breakCell = cell; targetDist = touchOffset; }
                else if (world.InBounds(cell)) { hasPlace = true; placeCell = cell; }
            }
            if (!hasBreak && aim != VoxelAimMode.Tocar)
            {
                VoxelHit hit;
                if (world.Raycast(new Ray(origin, dir), reach * world.BlockSize, out hit))
                {
                    hasBreak = true; breakCell = hit.block; targetDist = hit.distance;
                    if (!hasPlace && hit.normal != Vector3Int.zero) { hasPlace = true; placeCell = hit.Adjacent; }
                }
            }

            // ¿Hay una criatura (o cualquier cosa con vida) delante del bloque?
            Collider creature = null;
            Vector3 creaturePoint = Vector3.zero;
            if (aim != VoxelAimMode.Tocar)
            {
                RaycastHit ph;
                if (Physics.Raycast(origin, dir, out ph, reach * world.BlockSize, ~0, QueryTriggerInteraction.Ignore)
                    && ph.distance < targetDist && ph.collider.GetComponentInParent<VoxelChunk>() == null && !IsPlayer(ph.collider)
                    && ph.collider.GetComponentInParent<IDamageable>() != null)
                { creature = ph.collider; creaturePoint = ph.point; }
            }

            var registry = world.Registry;
            BlockType breakType = hasBreak ? registry.Get(world.GetBlock(breakCell)) : null;

            // ---------------------------------------------------------- Contorno
            if (creature != null) ShowOutline(false, Vector3Int.zero, Color.clear);
            else if (Breaks && hasBreak) ShowOutline(true, breakCell, Color.Lerp(breakColor, miningColor, m_Progress));
            else if (Places && hasBreak && breakType != null && breakType.opensCrafting) ShowOutline(true, breakCell, placeColor);
            else if (Places && hasPlace) ShowOutline(true, placeCell, placeColor);
            else ShowOutline(false, Vector3Int.zero, Color.clear);

            // ---------------------------------------------------------- Romper / pegar (gatillo)
            if (Breaks)
            {
                bool pressed = m_Input.Trigger.WasPressedThisFrame();
                bool held = m_Input.Trigger.IsPressed();
                if (creature != null)
                {
                    m_Progress = 0f;
                    if (pressed && Time.time >= m_NextAttack) Attack(creature, creaturePoint, dir, tool);
                }
                else if (held && hasBreak && breakType != null)
                    Mine(world, breakCell, breakType, tool, creative, pressed);
                else
                {
                    m_Progress = 0f;
                    m_MiningBlock = new Vector3Int(int.MinValue, 0, 0);
                    if (!held) m_NextRepeat = 0f;
                }
            }

            // ---------------------------------------------------------- Poner (gatillo o A/X)
            if (Places)
            {
                var button = role == VoxelHandRole.Ambos ? m_Input.Primary : m_Input.Trigger;
                bool pressed = button.WasPressedThisFrame();
                bool held = button.IsPressed();
                if (pressed && creature == null && hasBreak && breakType != null && breakType.opensCrafting)
                {
                    if (VoxelCrafting.Instance != null) VoxelCrafting.Instance.Open(true);
                    m_NextPlace = Time.time + 0.5f;
                }
                else if (pressed || (held && Time.time >= m_NextPlace))
                    TryPlace(world, hasPlace, placeCell, inv);
                if (!held) m_NextPlace = 0f;
            }
        }

        // ================================================================== Romper

        void Mine(VoxelWorld world, Vector3Int cell, BlockType type, VoxelToolItem tool, bool creative, bool pressed)
        {
            if (cell != m_MiningBlock) { m_MiningBlock = cell; m_Progress = 0f; }

            if (creative)
            {
                if (pressed || Time.time >= m_NextRepeat)
                {
                    Break(world, cell, type, tool, false);
                    m_NextRepeat = Time.time + creativeRepeat;
                }
                return;
            }

            if (type.hardness < 0f)
            {
                if (Time.time >= m_NextDenied) { VRHaptics.SendHand(m_Hand, 0.1f, 0.03f); m_NextDenied = Time.time + 0.4f; }
                return;
            }

            bool rightTool = tool != null && tool.toolType == type.bestTool && type.bestTool != VoxelToolType.Ninguna;
            float time = Mathf.Max(0.05f, type.hardness / (rightTool ? tool.speed : 1f));
            m_Progress += Time.deltaTime / time;

            if (Time.time >= m_NextTick)   // golpecitos mientras picas
            {
                VRHaptics.SendHand(m_Hand, 0.12f + 0.3f * m_Progress, 0.03f);
                m_NextTick = Time.time + 0.14f;
            }

            if (m_Progress >= 1f)
            {
                bool drops = !type.needsTool || rightTool;
                Break(world, cell, type, tool, drops);
                m_Progress = 0f;
                m_MiningBlock = new Vector3Int(int.MinValue, 0, 0);
            }
        }

        void Break(VoxelWorld world, Vector3Int cell, BlockType type, VoxelToolItem tool, bool drops)
        {
            Vector3 center = world.BlockCenter(cell);
            if (!world.SetBlock(cell, 0)) return;

            VoxelFx.Debris(world, type.Id, center);
            var clip = type.breakSound != null ? type.breakSound : breakSound;
            if (clip != null) VRAudio.PlayAt(clip, center);
            VRHaptics.SendHand(m_Hand, 0.45f, 0.06f);
            if (drops) Drop(world, type, center);
            if (tool != null) tool.Use();

            // Lo que estaba encima y no se sostiene solo (flores, hierba alta) cae también
            var above = cell + Vector3Int.up;
            var upType = world.GetBlockType(above);
            if (upType != null && !upType.solid)
            {
                world.SetBlock(above, 0);
                VoxelFx.Debris(world, upType.Id, world.BlockCenter(above));
                if (drops) Drop(world, upType, world.BlockCenter(above));
            }
        }

        void Drop(VoxelWorld world, BlockType type, Vector3 center)
        {
            var inv = BlockInventory.Instance;
            if (inv == null || inv.Creative || type.dropCount <= 0) return;
            var d = type.Drop;
            if (dropsAsItems) VoxelDrop.Spawn(world, d.Id, type.dropCount, center);
            else inv.Add(d.Id, type.dropCount);
        }

        void Attack(Collider target, Vector3 point, Vector3 dir, VoxelToolItem tool)
        {
            m_NextAttack = Time.time + attackCooldown;
            float dmg = tool != null ? tool.damage : handDamage;
            Damage.TryApply(target, new DamageInfo(dmg, point, dir, VRPlayer.Instance != null ? VRPlayer.Instance.gameObject : gameObject));
            var mob = target.GetComponentInParent<VoxelMob>();
            if (mob != null) mob.Knockback(dir);
            if (hitSound != null) VRAudio.PlayAt(hitSound, point);
            VRHaptics.SendHand(m_Hand, 0.6f, 0.08f);
            if (tool != null) tool.Use();
        }

        // ================================================================== Poner

        void TryPlace(VoxelWorld world, bool hasPlace, Vector3Int cell, BlockInventory inv)
        {
            m_NextPlace = Time.time + placeRepeat;
            if (!hasPlace || inv == null || inv.Selected == 0) { Denied(); return; }

            byte cur = world.GetBlock(cell);
            if (cur != 0)
            {
                var curType = world.Registry.Get(cur);
                if (curType == null || !curType.replaceable) { Denied(); return; }
            }
            byte id = inv.Selected;
            if (inv.Count(id) <= 0 || OverlapsPlayer(world, cell) || OverlapsBodies(world, cell)) { Denied(); return; }

            if (world.SetBlock(cell, id))
            {
                inv.Remove(id, 1);
                var type = world.Registry.Get(id);
                var clip = type != null && type.placeSound != null ? type.placeSound : placeSound;
                if (clip != null) VRAudio.PlayAt(clip, world.BlockCenter(cell));
                VRHaptics.SendHand(m_Hand, 0.25f, 0.04f);
            }
        }

        void Denied()
        {
            if (Time.time < m_NextDenied) return;
            m_NextDenied = Time.time + 0.4f;
            VRHaptics.SendHand(m_Hand, 0.1f, 0.03f);
            if (deniedSound != null) VRAudio.Play2D(deniedSound, 0.5f);
        }

        /// <summary>¿El bloque quedaría dentro del cuerpo del jugador?</summary>
        static bool OverlapsPlayer(VoxelWorld world, Vector3Int b)
        {
            var p = VRPlayer.Instance;
            if (p == null || p.Head == null) return false;
            Vector3 head = world.ToLocal(p.Head.position), feet = world.ToLocal(p.FeetPosition);
            float r = 0.25f / world.BlockSize;
            if (head.y + 0.1f / world.BlockSize < b.y || feet.y > b.y + 1) return false;
            float cx = Mathf.Clamp(head.x, b.x, b.x + 1), cz = Mathf.Clamp(head.z, b.z, b.z + 1);
            float dx = head.x - cx, dz = head.z - cz;
            return dx * dx + dz * dz < r * r;
        }

        /// <summary>¿Hay una criatura u otro objeto con collider en ese hueco?</summary>
        static bool OverlapsBodies(VoxelWorld world, Vector3Int b)
        {
            int n = Physics.OverlapBoxNonAlloc(world.BlockCenter(b), Vector3.one * (0.45f * world.BlockSize), s_Overlap, world.transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (s_Overlap[i].GetComponentInParent<VoxelChunk>() == null && !IsPlayer(s_Overlap[i])) return true;
            return false;
        }

        static bool IsPlayer(Collider c)
        {
            var p = VRPlayer.Instance;
            return p != null && c.transform.IsChildOf(p.transform);
        }

        // ================================================================== Comprobaciones

        bool MenuOpen()
        {
            if (m_Menus == null) return false;
            foreach (var m in m_Menus) if (m != null && m.IsOpen) return true;
            return false;
        }

        bool PointingAtUI()
        {
            foreach (var ui in m_UI)
            {
                TrackedDeviceModel model;
                if (ui != null && ui.TryGetUIModel(out model) && model.currentRaycast.isValid) return true;
            }
            return false;
        }

        /// <summary>True si la mano sujeta algo que no es una herramienta. Si es una herramienta, la devuelve.</summary>
        bool HoldingSomethingElse(out VoxelToolItem tool)
        {
            tool = null;
            foreach (var i in m_Hand.GetComponentsInChildren<XRBaseInteractor>())
            {
                if (i is XRSocketInteractor || !i.hasSelection) continue;
                var sel = i.interactablesSelected[0];
                tool = sel != null && sel.transform != null ? sel.transform.GetComponent<VoxelToolItem>() : null;
                return tool == null;
            }
            return false;
        }

        // ================================================================== Visual

        void ShowOutline(bool show, Vector3Int b, Color color)
        {
            if (!show) { if (m_Line != null) m_Line.enabled = false; return; }
            var world = VoxelWorld.Instance;
            if (m_Line == null)
            {
                var go = new GameObject("Contorno de bloque (" + hand + ")");
                m_Line = go.AddComponent<LineRenderer>();
                m_Line.useWorldSpace = true;
                m_Line.positionCount = 16;
                m_Line.numCornerVertices = 0;
                m_Line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                m_Line.receiveShadows = false;
                var mat = outlineMaterial;
                if (mat == null)
                {
                    var sh = Shader.Find("Universal Render Pipeline/Unlit");
                    if (sh == null) sh = Shader.Find("Sprites/Default");
                    mat = new Material(sh);
                }
                m_Line.sharedMaterial = mat;
            }
            m_Line.enabled = true;
            float e = 0.004f;
            float x0 = b.x - e, y0 = b.y - e, z0 = b.z - e, x1 = b.x + 1 + e, y1 = b.y + 1 + e, z1 = b.z + 1 + e;
            // Un solo trazo que recorre las 12 aristas (repitiendo alguna)
            Vector3[] c =
            {
                new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y0, z1), new Vector3(x0, y0, z1),
                new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1),
            };
            int[] path = { 0, 1, 2, 3, 0, 4, 5, 1, 5, 6, 2, 6, 7, 3, 7, 4 };
            for (int i = 0; i < 16; i++) m_Box[i] = world.ToWorld(c[path[i]]);
            m_Line.SetPositions(m_Box);
            m_Line.widthMultiplier = Mathf.Min(lineWidth, world.BlockSize * 0.04f) * (1f + m_Progress);
            m_Line.startColor = m_Line.endColor = color;
            if (m_Line.sharedMaterial != null && m_Line.sharedMaterial.HasProperty("_BaseColor")) m_Line.sharedMaterial.SetColor("_BaseColor", color);
        }

        void ShowHeld(byte id)
        {
            if (!showHeldBlock || m_Hand == null) id = 0;
            if (id == 0) { if (m_Held != null) m_Held.SetActive(false); m_HeldShown = 0; return; }
            var world = VoxelWorld.Instance;
            if (m_Held == null)
            {
                m_Held = new GameObject("Bloque en la mano");
                m_Held.transform.SetParent(m_Hand, false);
                m_Held.transform.localPosition = new Vector3(0f, -0.025f, 0.07f);
                m_Held.transform.localRotation = Quaternion.Euler(20f, 35f, 0f);
                m_Held.transform.localScale = Vector3.one * 0.06f;
                m_HeldFilter = m_Held.AddComponent<MeshFilter>();
                var r = m_Held.AddComponent<MeshRenderer>();
                r.sharedMaterial = world.Material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            m_Held.SetActive(true);
            if (id != m_HeldShown) { m_HeldFilter.sharedMesh = world.GetItemMesh(id); m_HeldShown = id; }
        }
    }
}
