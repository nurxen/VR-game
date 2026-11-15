using System;
using UnityEngine;
using UnityEngine.Events;

namespace VRKit
{
    /// <summary>
    /// Lanzar hechizos desde la palma, con la mano vacía (bruja, mago, dios, superpoderes).
    ///  · Mantén el gatillo: se carga una esfera en la palma (más carga = más daño y más velocidad).
    ///  · Suelta: sale disparada hacia donde apunta la mano.
    ///  · Botón A/X: cambia de hechizo.
    /// Cada hechizo usa un prefab con Projectile (el mismo que la varita). Pon uno de estos por mano.
    /// </summary>
    public class HandCaster : MonoBehaviour
    {
        [Serializable]
        public class Spell
        {
            public string name = "Bola de fuego";
            public Projectile projectile;
            public Color color = new Color(1f, 0.5f, 0.2f);
            public float damage = 20f;
            [Tooltip("Daño con carga completa = damage × esto.")]
            public float fullChargeMultiplier = 2.5f;
            public float minSpeed = 8f;
            public float maxSpeed = 22f;
            [Tooltip("Segundos para cargar del todo.")]
            public float chargeTime = 1f;
            public float cooldown = 0.5f;
            public AudioClip castSound;
        }

        [SerializeField] Hand hand = Hand.Right;
        [SerializeField] Spell[] spells = new Spell[0];
        [Tooltip("Distancia de la esfera delante de la mano.")]
        [SerializeField] float palmOffset = 0.09f;
        [Tooltip("Carga mínima para que salga el hechizo (evita disparos sin querer).")]
        [Range(0f, 1f)] [SerializeField] float minCharge = 0.15f;
        [SerializeField] AudioClip chargeSound;
        [SerializeField] AudioClip switchSound;

        public UnityEvent<string> onCast = new UnityEvent<string>();
        public UnityEvent<string> onSpellChanged = new UnityEvent<string>();

        public Spell Current { get { return spells.Length > 0 ? spells[m_Index] : null; } }
        public float Charge { get; private set; }

        HandInput m_Input;
        int m_Index;
        bool m_Charging;
        float m_ReadyAt;
        GameObject m_Orb;

        void OnEnable() { m_Input = new HandInput(hand); }

        /// <summary>Configura mano y hechizos por código (lo usa el montador del editor).</summary>
        public void Setup(Hand newHand, Spell[] newSpells) { hand = newHand; spells = newSpells; }
        void OnDisable() { if (m_Input != null) m_Input.Dispose(); m_Input = null; CancelCharge(); }

        void Update()
        {
            var p = VRPlayer.Instance;
            var gm = GameManager.Instance;
            if (p == null || m_Input == null || spells.Length == 0) return;
            if (gm != null && gm.State != GameState.Playing) { CancelCharge(); return; }
            Transform h = p.GetHand(hand);
            if (h == null) return;
            if (p.IsHolding(hand)) { CancelCharge(); return; } // con algo en la mano no se lanzan hechizos

            if (m_Input.Primary.WasPressedThisFrame()) NextSpell(h);

            if (m_Input.Trigger.WasPressedThisFrame() && Time.time >= m_ReadyAt) StartCharge(h);
            if (m_Charging)
            {
                Charge = Mathf.Clamp01(Charge + Time.deltaTime / Mathf.Max(0.05f, Current.chargeTime));
                m_Orb.transform.position = h.position + h.forward * palmOffset;
                m_Orb.transform.localScale = Vector3.one * Mathf.Lerp(0.03f, 0.12f, Charge);
                if (Time.frameCount % 6 == 0) VRHaptics.SendHand(h, Mathf.Lerp(0.05f, 0.35f, Charge), 0.03f);
                if (m_Input.Trigger.WasReleasedThisFrame()) Release(h);
            }
        }

        void StartCharge(Transform h)
        {
            m_Charging = true;
            Charge = 0f;
            m_Orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(m_Orb.GetComponent<Collider>());
            m_Orb.name = "Carga";
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", Current.color); block.SetColor("_Color", Current.color);
            m_Orb.GetComponent<Renderer>().SetPropertyBlock(block);
            var l = m_Orb.AddComponent<Light>();
            l.type = LightType.Point; l.color = Current.color; l.range = 1.5f; l.intensity = 1.5f;
            if (chargeSound != null) VRAudio.PlayAt(chargeSound, h.position, 0.5f);
        }

        void Release(Transform h)
        {
            var s = Current;
            float charge = Charge;
            CancelCharge();
            if (charge < minCharge || s.projectile == null) return;

            Vector3 pos = h.position + h.forward * palmOffset;
            var proj = Instantiate(s.projectile, pos, Quaternion.LookRotation(h.forward));
            proj.Configure(s.damage * Mathf.Lerp(1f, s.fullChargeMultiplier, charge), true);
            proj.transform.localScale *= Mathf.Lerp(0.7f, 1.4f, charge);
            proj.Launch(h.forward * Mathf.Lerp(s.minSpeed, s.maxSpeed, charge), VRPlayer.Instance.transform);
            if (s.castSound != null) VRAudio.PlayAt(s.castSound, pos);
            VRHaptics.SendHand(h, Mathf.Lerp(0.3f, 0.8f, charge), 0.1f);
            m_ReadyAt = Time.time + s.cooldown;
            onCast.Invoke(s.name);
        }

        void CancelCharge()
        {
            m_Charging = false;
            Charge = 0f;
            if (m_Orb != null) Destroy(m_Orb);
        }

        void NextSpell(Transform h)
        {
            if (spells.Length < 2) return;
            m_Index = (m_Index + 1) % spells.Length;
            FloatingText.Spawn(Current.name, h.position + Vector3.up * 0.12f, Current.color, 0.6f);
            if (switchSound != null) VRAudio.PlayAt(switchSound, h.position, 0.6f);
            VRHaptics.SendHand(h, 0.2f, 0.04f);
            onSpellChanged.Invoke(Current.name);
        }
    }
}
