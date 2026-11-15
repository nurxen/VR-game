using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRKit
{
    /// <summary>
    /// Al intentar coger este objeto, te pone en la mano una copia nueva de un prefab.
    /// Carcaj (saca flechas), caja de munición, montón de piedras, cesta de ingredientes...
    /// Ponlo junto a un XRSimpleInteractable en el contenedor.
    /// </summary>
    [RequireComponent(typeof(XRBaseInteractable))]
    public class SpawnOnGrab : MonoBehaviour
    {
        [Tooltip("Lo que aparece en la mano. Debe tener XRGrabInteractable.")]
        [SerializeField] XRGrabInteractable prefab;
        [Tooltip("Cuántos se pueden sacar. -1 = infinitos.")]
        [SerializeField] int stock = -1;
        [SerializeField] AudioClip spawnSound;

        public UnityEvent<GameObject> onSpawned = new UnityEvent<GameObject>();
        public UnityEvent onEmpty = new UnityEvent();

        XRBaseInteractable m_Interactable;

        public int Stock { get { return stock; } }

        void Awake()
        {
            m_Interactable = GetComponent<XRBaseInteractable>();
        }

        void OnEnable() { m_Interactable.selectEntered.AddListener(OnSelected); }
        void OnDisable() { m_Interactable.selectEntered.RemoveListener(OnSelected); }

        public void Refill(int amount) { stock = stock < 0 ? stock : stock + amount; }

        void OnSelected(SelectEnterEventArgs args)
        {
            // El cambio de selección se hace un frame después: cambiarla dentro
            // del propio evento de XRI puede dar errores.
            StartCoroutine(SwapIntoHand(args.interactorObject));
        }

        IEnumerator SwapIntoHand(IXRSelectInteractor interactor)
        {
            yield return null;
            var manager = m_Interactable.interactionManager;
            if (manager == null || interactor == null) yield break;

            if (m_Interactable.isSelected)
                manager.SelectExit(interactor, (IXRSelectInteractable)m_Interactable);

            if (prefab == null) yield break;
            if (stock == 0) { onEmpty.Invoke(); yield break; }
            if (stock > 0) stock--;

            Transform attach = interactor.GetAttachTransform(m_Interactable);
            var item = Instantiate(prefab, attach.position, attach.rotation);
            manager.SelectEnter(interactor, (IXRSelectInteractable)item);

            if (spawnSound != null) AudioSource.PlayClipAtPoint(spawnSound, attach.position);
            onSpawned.Invoke(item.gameObject);
        }
    }
}
