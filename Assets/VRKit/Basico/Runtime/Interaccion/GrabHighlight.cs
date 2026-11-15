using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRKit
{
    /// <summary>
    /// Ilumina un objeto cuando la mano o el rayo lo apuntan, para que el jugador sepa que se puede coger.
    /// En VR esto ayuda muchísimo: sin él, la gente no sabe qué es interactivo.
    /// Tiñe los materiales sin crear copias (MaterialPropertyBlock), así que no rompe el batching.
    /// Lo usa también la telequinesis (GrabHighlight.Set).
    /// </summary>
    public class GrabHighlight : MonoBehaviour
    {
        [SerializeField] Color highlightColor = new Color(1f, 0.85f, 0.4f);
        [Range(0f, 1f)] [SerializeField] float strength = 0.45f;
        [Tooltip("Apagar el resaltado mientras se sujeta.")]
        [SerializeField] bool hideWhileHeld = true;
        [Tooltip("Vacío = se busca en este objeto.")]
        [SerializeField] XRBaseInteractable interactable;

        static readonly int kBase = Shader.PropertyToID("_BaseColor");
        static readonly int kColor = Shader.PropertyToID("_Color");
        static readonly int kEmission = Shader.PropertyToID("_EmissionColor");

        Renderer[] m_Renderers;
        readonly List<Color> m_Base = new List<Color>();
        MaterialPropertyBlock m_Block;
        int m_Hovers;
        bool m_Forced;

        void Awake()
        {
            if (interactable == null) interactable = GetComponent<XRBaseInteractable>();
            m_Block = new MaterialPropertyBlock();
            m_Renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in m_Renderers) m_Base.Add(r.sharedMaterial != null ? r.sharedMaterial.color : Color.white);
        }

        void OnEnable()
        {
            if (interactable == null) return;
            interactable.hoverEntered.AddListener(OnHoverIn);
            interactable.hoverExited.AddListener(OnHoverOut);
            interactable.selectEntered.AddListener(OnSelect);
            interactable.selectExited.AddListener(OnDeselect);
        }

        void OnDisable()
        {
            if (interactable == null) return;
            interactable.hoverEntered.RemoveListener(OnHoverIn);
            interactable.hoverExited.RemoveListener(OnHoverOut);
            interactable.selectEntered.RemoveListener(OnSelect);
            interactable.selectExited.RemoveListener(OnDeselect);
            m_Hovers = 0; Refresh();
        }

        void OnHoverIn(HoverEnterEventArgs a) { m_Hovers++; Refresh(); }
        void OnHoverOut(HoverExitEventArgs a) { m_Hovers = Mathf.Max(0, m_Hovers - 1); Refresh(); }
        void OnSelect(SelectEnterEventArgs a) { Refresh(); }
        void OnDeselect(SelectExitEventArgs a) { Refresh(); }

        /// <summary>Encender o apagar el resaltado desde fuera (telequinesis, tutoriales).</summary>
        public void SetForced(bool on) { m_Forced = on; Refresh(); }

        /// <summary>Resalta cualquier objeto que tenga GrabHighlight (o no hace nada).</summary>
        public static void Set(GameObject go, bool on)
        {
            if (go == null) return;
            var h = go.GetComponentInParent<GrabHighlight>();
            if (h != null) h.SetForced(on);
        }

        void Refresh()
        {
            bool held = interactable != null && interactable.isSelected;
            bool on = (m_Hovers > 0 || m_Forced) && !(hideWhileHeld && held);
            for (int i = 0; i < m_Renderers.Length; i++)
            {
                var r = m_Renderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(m_Block);
                Color c = on ? Color.Lerp(m_Base[i], highlightColor, strength) : m_Base[i];
                m_Block.SetColor(kBase, c);
                m_Block.SetColor(kColor, c);
                m_Block.SetColor(kEmission, on ? highlightColor * (strength * 0.6f) : Color.black);
                r.SetPropertyBlock(m_Block);
            }
        }
    }
}
