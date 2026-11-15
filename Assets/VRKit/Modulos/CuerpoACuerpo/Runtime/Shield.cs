using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRKit
{
    /// <summary>
    /// Escudo (o cualquier cosa que tape: tapa de cubo, libro de hechizos, barrera mágica).
    /// Bloquea de dos formas:
    ///  · Los proyectiles chocan con su Collider y se paran.
    ///  · Los golpes cuerpo a cuerpo al jugador se reducen si el escudo está entre la cabeza y el atacante
    ///    (PlayerHealth le pregunta a Shield.Filter antes de quitar vida).
    /// Su eje Z (azul) es la cara que protege.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class Shield : MonoBehaviour, IDamageable
    {
        static readonly List<Shield> s_Held = new List<Shield>();

        [Tooltip("1 = bloquea todo el daño, 0,5 = la mitad.")]
        [Range(0f, 1f)] [SerializeField] float blockAmount = 1f;
        [Tooltip("Ángulo máximo entre la cara del escudo y el atacante.")]
        [SerializeField] float blockAngle = 70f;
        [Tooltip("El escudo tiene que estar a menos de esta distancia de la cabeza para proteger.")]
        [SerializeField] float maxDistanceFromHead = 0.9f;
        [Tooltip("Aguante del escudo: daño que soporta antes de romperse (-1 = irrompible).")]
        [SerializeField] float durability = -1f;
        [SerializeField] AudioClip blockSound;
        [SerializeField, Range(0f, 1f)] float blockHaptic = 0.7f;

        public UnityEvent onBlocked = new UnityEvent();
        public UnityEvent onBroken = new UnityEvent();

        XRGrabInteractable m_Grab;

        void Awake()
        {
            m_Grab = GetComponent<XRGrabInteractable>();
            // Se engancha a la vida del jugador y a los proyectiles (que no saben nada de escudos)
            if (!PlayerHealth.DamageFilters.Contains(Filter)) PlayerHealth.DamageFilters.Add(Filter);
            if (!Projectile.HeadHitBlockers.Contains(BlocksPoint)) Projectile.HeadHitBlockers.Add(BlocksPoint);
        }
        void Update()
        {
            bool held = m_Grab.isSelected;
            if (held && !s_Held.Contains(this)) s_Held.Add(this);
            else if (!held) s_Held.Remove(this);
        }
        void OnDisable() { s_Held.Remove(this); }

        /// <summary>Lo llama PlayerHealth: devuelve el daño que pasa tras los escudos.</summary>
        public static float Filter(DamageInfo info, float amount)
        {
            var p = VRPlayer.Instance;
            if (s_Held.Count == 0 || p == null || p.Head == null) return amount;
            Vector3 from = info.source != null ? info.source.transform.position : info.point - info.direction;
            foreach (var s in s_Held)
                if (s != null && s.Covers(p.Head.position, from)) return amount * (1f - s.Absorb(amount));
            return amount;
        }

        /// <summary>¿Hay un escudo sujeto en este punto, mirando hacia donde viene el golpe? (para proyectiles a la cabeza)</summary>
        public static bool BlocksPoint(Vector3 point, Vector3 travelDir)
        {
            foreach (var s in s_Held)
            {
                if (s == null || Vector3.Distance(s.transform.position, point) > 0.35f) continue;
                if (Vector3.Angle(s.transform.forward, -travelDir) < s.blockAngle) { s.Absorb(0f); return true; }
            }
            return false;
        }

        bool Covers(Vector3 head, Vector3 attacker)
        {
            Vector3 toShield = transform.position - head;
            if (toShield.magnitude > maxDistanceFromHead) return false;
            Vector3 toAttacker = attacker - head;
            if (Vector3.Angle(toShield, toAttacker) > 55f) return false;                         // el escudo no está en esa dirección
            return Vector3.Angle(transform.forward, attacker - transform.position) < blockAngle; // y le da la cara
        }

        float Absorb(float amount)
        {
            if (blockSound != null) VRAudio.PlayAt(blockSound, transform.position);
            if (m_Grab.firstInteractorSelecting != null) VRHaptics.Send(m_Grab.firstInteractorSelecting, blockHaptic, 0.1f);
            onBlocked.Invoke();
            if (durability > 0f)
            {
                durability -= amount * blockAmount;
                if (durability <= 0f) { onBroken.Invoke(); gameObject.SetActive(false); }
            }
            return blockAmount;
        }

        /// <summary>Un proyectil choca con el escudo: lo para.</summary>
        public void TakeDamage(DamageInfo info) { Absorb(info.amount); }
    }
}
