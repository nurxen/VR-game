using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRKit
{
    /// <summary>
    /// Mochila invisible: suelta un objeto por encima del hombro y se guarda; mete la mano ahí y aprieta
    /// el grip para sacar el último que guardaste. Es el inventario más natural en VR (Half-Life: Alyx lo hace así).
    ///
    /// Montaje: un objeto con Collider (Is Trigger) + XR Simple Interactable + este script,
    /// colocado detrás del hombro como hijo de un BodyFollower (o de la cámara).
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class BackpackStorage : MonoBehaviour
    {
        [SerializeField] int capacity = 8;
        [Tooltip("Solo guarda objetos de estas categorías (VRItem). Vacío = cualquiera.")]
        [SerializeField] string[] acceptedCategories = new string[0];
        [SerializeField] AudioClip storeSound;
        [SerializeField] AudioClip takeSound;
        [SerializeField] AudioClip fullSound;
        [SerializeField, Range(0f, 1f)] float haptic = 0.35f;

        public UnityEvent<GameObject> onStored = new UnityEvent<GameObject>();
        public UnityEvent<GameObject> onTaken = new UnityEvent<GameObject>();

        public int Count { get { return m_Stored.Count; } }
        public IReadOnlyList<XRGrabInteractable> Stored { get { return m_Stored; } }

        readonly List<XRGrabInteractable> m_Stored = new List<XRGrabInteractable>();
        readonly Dictionary<XRGrabInteractable, UnityEngine.Events.UnityAction<SelectExitEventArgs>> m_Watching =
            new Dictionary<XRGrabInteractable, UnityEngine.Events.UnityAction<SelectExitEventArgs>>();
        XRSimpleInteractable m_Zone;

        void Awake() { m_Zone = GetComponent<XRSimpleInteractable>(); }
        void OnEnable() { m_Zone.selectEntered.AddListener(OnZoneGrabbed); }
        void OnDisable()
        {
            m_Zone.selectEntered.RemoveListener(OnZoneGrabbed);
            foreach (var kv in m_Watching) if (kv.Key != null) kv.Key.selectExited.RemoveListener(kv.Value);
            m_Watching.Clear();
        }

        // ---- Guardar: un objeto sujeto entra en la zona y lo sueltas dentro
        void OnTriggerEnter(Collider other)
        {
            var g = other.GetComponentInParent<XRGrabInteractable>();
            if (g == null || !g.isSelected || m_Watching.ContainsKey(g) || m_Stored.Contains(g)) return;
            UnityEngine.Events.UnityAction<SelectExitEventArgs> h = a => TryStore(g);
            m_Watching[g] = h;
            g.selectExited.AddListener(h);
        }

        void OnTriggerExit(Collider other)
        {
            var g = other.GetComponentInParent<XRGrabInteractable>();
            if (g != null) StartCoroutine(UnwatchLater(g)); // un frame después: si lo soltó justo al salir, que dé tiempo
        }

        IEnumerator UnwatchLater(XRGrabInteractable g)
        {
            yield return null;
            Unwatch(g);
        }

        void Unwatch(XRGrabInteractable g)
        {
            UnityEngine.Events.UnityAction<SelectExitEventArgs> h;
            if (g != null && m_Watching.TryGetValue(g, out h)) { g.selectExited.RemoveListener(h); m_Watching.Remove(g); }
        }

        void TryStore(XRGrabInteractable g)
        {
            if (g == null || g.isSelected) return; // aún lo sujeta otra mano
            Unwatch(g);
            if (!Accepts(g.gameObject)) return;
            if (m_Stored.Count >= capacity)
            {
                if (fullSound != null) VRAudio.PlayAt(fullSound, transform.position);
                FloatingText.SpawnInFront("Mochila llena", new Color(1f, 0.5f, 0.4f));
                return;
            }
            m_Stored.Add(g);
            g.gameObject.SetActive(false);
            if (storeSound != null) VRAudio.PlayAt(storeSound, transform.position, 0.8f);
            if (VRPlayer.Instance != null) { VRHaptics.SendHand(VRPlayer.Instance.LeftHand, haptic, 0.05f); VRHaptics.SendHand(VRPlayer.Instance.RightHand, haptic, 0.05f); }
            onStored.Invoke(g.gameObject);
        }

        bool Accepts(GameObject go)
        {
            if (acceptedCategories == null || acceptedCategories.Length == 0) return true;
            string cat = VRItem.CategoryOf(go);
            foreach (var c in acceptedCategories) if (c == cat) return true;
            return false;
        }

        // ---- Sacar: aprietas el grip dentro de la zona con la mano vacía
        void OnZoneGrabbed(SelectEnterEventArgs a)
        {
            StartCoroutine(TakeIntoHand(a.interactorObject));
        }

        IEnumerator TakeIntoHand(IXRSelectInteractor interactor)
        {
            yield return null; // cambiar la selección dentro del propio evento de XRI da problemas
            var manager = m_Zone.interactionManager;
            if (manager == null) yield break;
            if (m_Zone.isSelected) manager.SelectExit(interactor, (IXRSelectInteractable)m_Zone);
            if (m_Stored.Count == 0) yield break;

            var item = m_Stored[m_Stored.Count - 1];
            m_Stored.RemoveAt(m_Stored.Count - 1);
            if (item == null) yield break;
            var attach = interactor.GetAttachTransform(m_Zone);
            item.transform.SetPositionAndRotation(attach.position, attach.rotation);
            item.gameObject.SetActive(true);
            manager.SelectEnter(interactor, (IXRSelectInteractable)item);
            if (takeSound != null) VRAudio.PlayAt(takeSound, attach.position, 0.8f);
            onTaken.Invoke(item.gameObject);
        }
    }
}
