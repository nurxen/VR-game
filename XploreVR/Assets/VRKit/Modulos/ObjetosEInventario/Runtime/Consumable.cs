using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRKit
{
    /// <summary>
    /// Comer o beber llevándote el objeto a la boca: poción, manzana, seta mágica, taza de té…
    /// Mantenlo junto a la boca medio segundo y se consume: cura, da puntos, activa efectos
    /// (cámara lenta, visión nocturna… lo que conectes en On Consumed).
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class Consumable : MonoBehaviour
    {
        [SerializeField] float heal = 25f;
        [Tooltip("Cuántas veces se puede usar (bocados, tragos).")]
        [Min(1)] [SerializeField] int uses = 1;
        [Tooltip("Segundos junto a la boca para consumir un uso.")]
        [SerializeField] float holdTime = 0.5f;
        [SerializeField] float mouthRadius = 0.13f;
        [Tooltip("Al acabarse: destruir, o cambiarlo por otro objeto (frasco vacío, corazón de manzana).")]
        [SerializeField] GameObject emptyPrefab;
        [SerializeField] string objectiveId = "";
        [SerializeField] AudioClip consumeSound;
        [SerializeField, Range(0f, 1f)] float haptic = 0.4f;

        public UnityEvent onConsumed = new UnityEvent();
        public UnityEvent onFinished = new UnityEvent();

        public int UsesLeft { get { return uses; } }

        XRGrabInteractable m_Grab;
        float m_Timer;

        void Awake() { m_Grab = GetComponent<XRGrabInteractable>(); }

        void Update()
        {
            var p = VRPlayer.Instance;
            if (!m_Grab.isSelected || p == null || p.Head == null) { m_Timer = 0f; return; }
            Vector3 mouth = p.Head.position - p.Head.up * 0.09f + p.Head.forward * 0.05f;
            if (Vector3.Distance(transform.position, mouth) > mouthRadius) { m_Timer = 0f; return; }
            m_Timer += Time.deltaTime;
            if (m_Timer >= holdTime) { m_Timer = 0f; Consume(); }
        }

        public void Consume()
        {
            if (uses <= 0) return;
            uses--;
            if (heal > 0f && PlayerHealth.Instance != null) PlayerHealth.Instance.Heal(heal);
            ObjectiveTracker.ReportStatic(objectiveId);
            if (consumeSound != null) VRAudio.PlayAt(consumeSound, transform.position);
            if (m_Grab.firstInteractorSelecting != null) VRHaptics.Send(m_Grab.firstInteractorSelecting, haptic, 0.12f);
            if (heal > 0f) FloatingText.Spawn("+" + Mathf.RoundToInt(heal), transform.position + Vector3.up * 0.1f, new Color(0.5f, 1f, 0.55f));
            onConsumed.Invoke();
            if (uses > 0) return;

            onFinished.Invoke();
            if (emptyPrefab != null) Instantiate(emptyPrefab, transform.position, transform.rotation);
            Destroy(gameObject);
        }
    }
}
