using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace VRKit
{
    /// <summary>
    /// Aviso de tutorial pegado a un mando: «Aprieta el gatillo para disparar», con una flecha hacia el botón.
    /// Se queda hasta que el jugador hace la acción (un botón del mando, Complete() desde un evento, o coger algo)
    /// y entonces activa el siguiente aviso. Encadena varios para un tutorial completo.
    ///
    /// En VR los tutoriales en un panel lejano no funcionan: la gente no los lee.
    /// Pegarlos al mando, justo donde está el botón, sí.
    /// </summary>
    public class TutorialPrompt : MonoBehaviour
    {
        [SerializeField] Hand hand = Hand.Right;
        [TextArea] [SerializeField] string message = "Aprieta el gatillo";
        [Tooltip("Posición respecto al mando.")]
        [SerializeField] Vector3 offset = new Vector3(0.06f, 0.06f, 0.02f);
        [Tooltip("Botón que lo completa. Ejemplos: {TriggerButton}, {GripButton}, {PrimaryButton}, {SecondaryButton}. Vacío = solo con Complete().")]
        [SerializeField] string completeOnButton = "{TriggerButton}";
        [SerializeField] float showDelay = 0.5f;
        [Tooltip("Aviso siguiente (se activa al completar este).")]
        [SerializeField] TutorialPrompt next;
        [Tooltip("Bandera de ProgressSave: si ya se completó en otra partida, no vuelve a salir.")]
        [SerializeField] string rememberFlag = "";
        [SerializeField] AudioClip doneSound;

        public UnityEvent onCompleted = new UnityEvent();

        TextMeshPro m_Text;
        LineRenderer m_Line;
        InputAction m_Action;
        float m_ShownAt;
        bool m_Done, m_SkipRemembered;

        void OnEnable()
        {
            m_Done = false;
            // Si ya se hizo en otra partida, se salta (en el primer LateUpdate: no se puede desactivar dentro de OnEnable)
            m_SkipRemembered = !string.IsNullOrEmpty(rememberFlag) && ProgressSave.HasFlag(rememberFlag);
            m_ShownAt = Time.unscaledTime + showDelay;
            if (!string.IsNullOrEmpty(completeOnButton))
            {
                m_Action = new InputAction("Tutorial", InputActionType.Button);
                m_Action.AddBinding("<XRController>{" + (hand == Hand.Left ? "LeftHand" : "RightHand") + "}/" + completeOnButton);
                m_Action.Enable();
            }
            if (m_Text == null) Build();
        }

        void OnDisable()
        {
            if (m_Action != null) { m_Action.Disable(); m_Action.Dispose(); m_Action = null; }
        }

        void Build()
        {
            var go = new GameObject("Texto");
            go.transform.SetParent(transform, false);
            m_Text = go.AddComponent<TextMeshPro>();
            m_Text.fontSize = 0.25f;
            m_Text.alignment = TextAlignmentOptions.Left;
            m_Text.rectTransform.sizeDelta = new Vector2(0.35f, 0.1f);
            m_Text.rectTransform.pivot = new Vector2(0f, 0.5f);
            m_Text.color = Color.white;
            m_Text.outlineWidth = 0.25f;
            m_Text.outlineColor = new Color32(0, 0, 0, 220);
            m_Line = gameObject.AddComponent<LineRenderer>();
            m_Line.positionCount = 2;
            m_Line.widthMultiplier = 0.002f;
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            m_Line.material = new Material(sh) { color = new Color(1f, 0.85f, 0.35f) };
        }

        void LateUpdate()
        {
            if (m_Done) return;
            if (m_SkipRemembered) { m_SkipRemembered = false; Finish(false); return; }
            var p = VRPlayer.Instance;
            Transform h = p != null ? p.GetHand(hand) : null;
            bool visible = h != null && Time.unscaledTime >= m_ShownAt;
            m_Text.gameObject.SetActive(visible);
            m_Line.enabled = visible;
            if (!visible) return;

            Vector3 side = hand == Hand.Left ? -h.right : h.right;
            Vector3 pos = h.position + side * Mathf.Abs(offset.x) + Vector3.up * offset.y + h.forward * offset.z;
            m_Text.transform.position = pos;
            var cam = Camera.main;
            if (cam != null) m_Text.transform.rotation = Quaternion.LookRotation(pos - cam.transform.position);
            m_Text.text = message;
            m_Line.SetPosition(0, pos);
            m_Line.SetPosition(1, h.position);

            if (m_Action != null && m_Action.WasPressedThisFrame()) Complete();
        }

        public void Complete() { if (!m_Done) Finish(true); }

        void Finish(bool withFeedback)
        {
            m_Done = true;
            if (!string.IsNullOrEmpty(rememberFlag)) ProgressSave.SetFlag(rememberFlag);
            if (withFeedback && doneSound != null) VRAudio.Play2D(doneSound, 0.6f);
            onCompleted.Invoke();
            if (next != null) next.gameObject.SetActive(true);
            gameObject.SetActive(false);
        }
    }
}
