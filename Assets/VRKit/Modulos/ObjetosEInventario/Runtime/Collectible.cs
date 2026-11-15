using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRKit
{
    /// <summary>
    /// Algo que se recoge y desaparece: moneda, gema, seta, estrella, página de un diario, orbe de energía…
    ///  · Touch: basta con tocarlo con la mano (o atravesarlo con la cabeza).
    ///  · Grab: hay que cogerlo (necesita un XR Grab o Simple Interactable).
    /// Suma puntos, avanza un objetivo, guarda un contador persistente y puede reaparecer.
    /// </summary>
    public class Collectible : MonoBehaviour
    {
        public enum Mode { Touch, Grab }

        [SerializeField] Mode mode = Mode.Touch;
        [SerializeField] float touchRadius = 0.18f;
        [Tooltip("También al atravesarlo con el cuerpo (cabeza).")]
        [SerializeField] bool collectWithHead = true;

        [Header("Recompensa")]
        [SerializeField] int score = 10;
        [Tooltip("Objetivo que avanza (ObjectiveTracker). Vacío = ninguno.")]
        [SerializeField] string objectiveId = "";
        [Tooltip("Contador guardado entre partidas (ProgressSave), por ejemplo «monedas». Vacío = ninguno.")]
        [SerializeField] string savedCounter = "";
        [Tooltip("Cura al jugador (orbes de vida).")]
        [SerializeField] float heal = 0f;
        [Tooltip("Un id único hace que, una vez cogido, no vuelva a aparecer nunca (coleccionables secretos).")]
        [SerializeField] string uniqueId = "";

        [Header("Presentación")]
        [SerializeField] AudioClip collectSound;
        [SerializeField] GameObject collectEffect;
        [SerializeField] bool showFloatingText = true;
        [SerializeField, Range(0f, 1f)] float haptic = 0.3f;
        [Tooltip("Segundos hasta reaparecer (0 = no reaparece).")]
        [SerializeField] float respawnTime = 0f;

        public UnityEvent onCollected = new UnityEvent();

        XRBaseInteractable m_Interactable;
        bool m_Collected;
        Vector3 m_Scale;

        void Awake()
        {
            m_Scale = transform.localScale;
            m_Interactable = GetComponent<XRBaseInteractable>();
            if (!string.IsNullOrEmpty(uniqueId) && ProgressSave.HasFlag("coleccionable:" + uniqueId)) gameObject.SetActive(false);
        }

        void OnEnable() { if (m_Interactable != null) m_Interactable.selectEntered.AddListener(OnGrab); }
        void OnDisable() { if (m_Interactable != null) m_Interactable.selectEntered.RemoveListener(OnGrab); }

        void OnGrab(SelectEnterEventArgs a)
        {
            if (mode == Mode.Grab) Collect(a.interactorObject.transform);
        }

        void Update()
        {
            if (m_Collected || mode != Mode.Touch) return;
            var p = VRPlayer.Instance;
            if (p == null) return;
            Vector3 pos = transform.position;
            if (Near(p.LeftHand, pos)) { Collect(p.LeftHand); return; }
            if (Near(p.RightHand, pos)) { Collect(p.RightHand); return; }
            if (collectWithHead && p.Head != null && Vector3.Distance(p.Head.position, pos) < touchRadius * 2f) Collect(null);
        }

        bool Near(Transform hand, Vector3 pos) { return hand != null && Vector3.Distance(hand.position, pos) < touchRadius; }

        public void Collect(Transform hand)
        {
            if (m_Collected) return;
            m_Collected = true;
            if (score != 0 && GameManager.Instance != null) GameManager.Instance.AddScore(score);
            ObjectiveTracker.ReportStatic(objectiveId);
            if (!string.IsNullOrEmpty(savedCounter)) ProgressSave.AddInt(savedCounter, 1);
            if (!string.IsNullOrEmpty(uniqueId)) ProgressSave.SetFlag("coleccionable:" + uniqueId);
            if (heal > 0f && PlayerHealth.Instance != null) PlayerHealth.Instance.Heal(heal);
            if (collectSound != null) VRAudio.PlayAt(collectSound, transform.position);
            if (collectEffect != null) Instantiate(collectEffect, transform.position, Quaternion.identity);
            if (showFloatingText && score != 0) FloatingText.Spawn("+" + score, transform.position + Vector3.up * 0.1f, new Color(1f, 0.85f, 0.3f));
            if (hand != null) VRHaptics.SendHand(hand, haptic, 0.06f);
            onCollected.Invoke();

            if (m_Interactable != null && m_Interactable.isSelected && m_Interactable.interactionManager != null)
                m_Interactable.interactionManager.CancelInteractableSelection((IXRSelectInteractable)m_Interactable);
            StartCoroutine(Vanish());
        }

        IEnumerator Vanish()
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime * 6f) { transform.localScale = m_Scale * (1f - t); yield return null; }
            if (respawnTime <= 0f) { gameObject.SetActive(false); yield break; }
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
            transform.localScale = m_Scale;
            yield return new WaitForSeconds(respawnTime);
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = true;
            m_Collected = false;
        }
    }
}
