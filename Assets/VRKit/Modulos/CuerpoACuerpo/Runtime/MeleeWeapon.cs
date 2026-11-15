using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRKit
{
    /// <summary>
    /// Arma cuerpo a cuerpo: espada, hacha, maza, sartén, bate, varita-espada…
    /// El daño depende de lo rápido que se mueve la punta: un golpe flojo no hace nada, un tajo con ganas sí.
    /// Así el jugador tiene que mover el brazo de verdad (es lo que hace divertido el combate VR).
    ///
    ///  · Tip: un hijo en la punta del filo. Se mide su velocidad.
    ///  · Los Collider del filo pueden ser normales o Is Trigger (recomendado: el arma no rebota ni tiembla).
    ///  · Cada enemigo solo recibe un golpe cada Per Target Cooldown segundos (un tajo no cuenta 5 veces).
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class MeleeWeapon : MonoBehaviour
    {
        [SerializeField] Transform tip;
        [Header("Daño")]
        [Tooltip("Daño por cada m/s de velocidad de la punta.")]
        [SerializeField] float damagePerSpeed = 6f;
        [Tooltip("Por debajo de esta velocidad (m/s) el golpe no hace daño.")]
        [SerializeField] float minSpeed = 1.8f;
        [SerializeField] float maxDamage = 60f;
        [SerializeField] float perTargetCooldown = 0.4f;
        [Tooltip("Empujón a los Rigidbody golpeados.")]
        [SerializeField] float knockback = 3f;
        [Tooltip("Solo hace daño si alguien lo sujeta (si no, una espada caída mataría enemigos).")]
        [SerializeField] bool requireHeld = true;

        [Header("Respuesta")]
        [SerializeField, Range(0f, 1f)] float hitHapticMax = 0.9f;
        [SerializeField] AudioClip[] hitSounds = new AudioClip[0];
        [SerializeField] AudioClip[] swingSounds = new AudioClip[0];
        [Tooltip("Velocidad de la punta a partir de la cual suena el «zas» al cortar el aire.")]
        [SerializeField] float swingSoundSpeed = 4f;
        [SerializeField] GameObject hitEffect;
        [SerializeField] TrailRenderer trail;

        public UnityEvent<float> onHit = new UnityEvent<float>();

        public float TipSpeed { get; private set; }

        XRGrabInteractable m_Grab;
        Vector3 m_LastTip;
        readonly Dictionary<Object, float> m_LastHit = new Dictionary<Object, float>();
        float m_LastSwingSound;

        void Awake()
        {
            m_Grab = GetComponent<XRGrabInteractable>();
            if (tip == null) tip = transform;
            m_LastTip = tip.position;
        }

        void Update()
        {
            Vector3 p = tip.position;
            if (Time.deltaTime > 0f) TipSpeed = Mathf.Lerp(TipSpeed, (p - m_LastTip).magnitude / Time.deltaTime, 0.6f);
            m_LastTip = p;

            bool active = !requireHeld || m_Grab.isSelected;
            if (trail != null) trail.emitting = active && TipSpeed > minSpeed;
            if (active && TipSpeed > swingSoundSpeed && Time.time - m_LastSwingSound > 0.4f && swingSounds.Length > 0)
            {
                m_LastSwingSound = Time.time;
                VRAudio.PlayRandom(swingSounds, p, 0.5f);
            }
        }

        void OnCollisionEnter(Collision c) { TryHit(c.collider, c.GetContact(0).point); }
        void OnTriggerEnter(Collider other) { TryHit(other, other.ClosestPoint(tip.position)); }

        void TryHit(Collider other, Vector3 point)
        {
            if (requireHeld && !m_Grab.isSelected) return;
            if (other.transform.IsChildOf(transform)) return;
            if (VRPlayer.Instance != null && other.transform.IsChildOf(VRPlayer.Instance.transform)) return;
            if (TipSpeed < minSpeed) return;

            var target = other.GetComponentInParent<IDamageable>() as Component;
            Object key = target != null ? (Object)target : other;
            float last;
            if (m_LastHit.TryGetValue(key, out last) && Time.time - last < perTargetCooldown) return;
            m_LastHit[key] = Time.time;

            float dmg = Mathf.Min(maxDamage, TipSpeed * damagePerSpeed);
            Vector3 dir = (point - m_LastTip).sqrMagnitude > 0.0001f ? (point - m_LastTip).normalized : transform.forward;
            Damage.TryApply(other, new DamageInfo(dmg, point, dir, gameObject));
            var rb = other.attachedRigidbody;
            if (rb != null && !rb.isKinematic) rb.AddForceAtPosition(dir * knockback * (dmg / maxDamage), point, ForceMode.Impulse);

            float k = Mathf.Clamp01(dmg / maxDamage);
            if (m_Grab.firstInteractorSelecting != null) VRHaptics.Send(m_Grab.firstInteractorSelecting, Mathf.Lerp(0.2f, hitHapticMax, k), 0.08f);
            VRAudio.PlayRandom(hitSounds, point, Mathf.Lerp(0.4f, 1f, k));
            if (hitEffect != null) Instantiate(hitEffect, point, Quaternion.LookRotation(-dir));
            onHit.Invoke(dmg);
        }
    }
}
