using UnityEngine;

namespace VRKit
{
    /// <summary>
    /// Vida del jugador. Ponlo en el XR Origin (el objeto raíz del jugador).
    /// Al recibir daño: destello rojo en la vista y vibración en los dos mandos.
    /// Al morir: avisa al GameManager (fin de partida).
    /// </summary>
    public class PlayerHealth : Health
    {
        public static PlayerHealth Instance { get; private set; }

        /// <summary>
        /// Filtros que pueden reducir el daño antes de aplicarlo: (golpe, daño) → daño que pasa.
        /// Los usa el escudo (módulo Cuerpo a cuerpo); valen para armaduras, burbujas mágicas, etc.
        /// </summary>
        public static readonly System.Collections.Generic.List<System.Func<DamageInfo, float, float>> DamageFilters =
            new System.Collections.Generic.List<System.Func<DamageInfo, float, float>>();

        [Header("Respuesta al daño")]
        [SerializeField] Color damageFlashColor = new Color(0.8f, 0f, 0f, 0.45f);
        [SerializeField] float damageFlashTime = 0.35f;
        [SerializeField, Range(0f, 1f)] float damageHaptic = 0.6f;

        [Header("Regeneración")]
        [Tooltip("Vida por segundo que recupera (0 = no regenera).")]
        [SerializeField] float regenPerSecond = 0f;
        [Tooltip("Segundos sin recibir daño antes de empezar a regenerar.")]
        [SerializeField] float regenDelay = 4f;

        [Header("Dificultad")]
        [Tooltip("Multiplica el daño que recibe el jugador (lo cambia la opción de dificultad).")]
        [SerializeField] float damageTakenMultiplier = 1f;
        [Tooltip("Modo historia: el jugador no puede morir (se queda con 1 de vida).")]
        [SerializeField] bool invincible = false;

        float m_LastDamageTime = -999f;

        public float DamageTakenMultiplier { get { return damageTakenMultiplier; } set { damageTakenMultiplier = Mathf.Max(0f, value); } }
        public bool Invincible { get { return invincible; } set { invincible = value; } }

        /// <summary>Posición de la cabeza del jugador (para que los enemigos apunten).</summary>
        public Vector3 HeadPosition
        {
            get { return Camera.main != null ? Camera.main.transform.position : transform.position + Vector3.up * 1.6f; }
        }

        /// <summary>Posición de los pies del jugador, en el suelo bajo la cabeza.</summary>
        public Vector3 FeetPosition
        {
            get { var h = HeadPosition; return new Vector3(h.x, transform.position.y, h.z); }
        }

        protected override void Awake()
        {
            base.Awake();
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (regenPerSecond > 0f && !IsDead && Current < Max && Time.time - m_LastDamageTime > regenDelay)
                Heal(regenPerSecond * Time.deltaTime);
        }

        protected override float ModifyDamage(DamageInfo info)
        {
            float amount = info.amount * damageTakenMultiplier;
            foreach (var f in DamageFilters) amount = f(info, amount);   // escudos, armaduras…
            if (invincible) amount = Mathf.Min(amount, Current - 1f);
            return amount;
        }

        protected override void OnDamaged(DamageInfo info)
        {
            m_LastDamageTime = Time.time;
            if (ScreenFader.Instance != null) ScreenFader.Instance.Flash(damageFlashColor, damageFlashTime);
            VRHaptics.SendAll(gameObject, damageHaptic, 0.15f);
        }

        protected override void OnDied()
        {
            if (GameManager.Instance != null) GameManager.Instance.GameOver();
        }
    }
}
