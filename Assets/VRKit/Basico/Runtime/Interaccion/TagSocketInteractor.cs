using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRKit
{
    /// <summary>
    /// Hueco (socket) que solo acepta objetos con ciertos Tags.
    /// Cerraduras con llave, pedestales de puzles, fundas de arma, ranuras de pila...
    /// Usa los eventos "Select Entered / Exited" del Inspector para reaccionar
    /// (abrir la puerta, encender la máquina, etc.).
    /// </summary>
    public class TagSocketInteractor : XRSocketInteractor
    {
        [Tooltip("Tags aceptados. Vacío = acepta cualquier objeto agarrable.")]
        [SerializeField] string[] acceptedTags = new string[0];

        public override bool CanHover(IXRHoverInteractable interactable)
        {
            return base.CanHover(interactable) && Accepts(interactable);
        }

        public override bool CanSelect(IXRSelectInteractable interactable)
        {
            return base.CanSelect(interactable) && Accepts(interactable);
        }

        bool Accepts(IXRInteractable interactable)
        {
            if (acceptedTags == null || acceptedTags.Length == 0) return true;
            var go = interactable.transform.gameObject;
            foreach (var t in acceptedTags)
                if (!string.IsNullOrEmpty(t) && go.CompareTag(t)) return true;
            return false;
        }
    }
}
