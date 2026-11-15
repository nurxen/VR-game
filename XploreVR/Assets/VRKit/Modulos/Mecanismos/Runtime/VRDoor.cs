using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace VRKit
{
    /// <summary>
    /// Puerta, verja, trampilla o compuerta que se abre por código o eventos:
    /// una palanca, un botón, una llave en un socket, un puzle resuelto o una zona.
    ///  · Hinge: gira sobre su eje Y (bisagra) hasta Open Angle.
    ///  · Slide: se desplaza Open Offset (puertas de nave espacial, rejas que suben).
    /// Si está cerrada con llave (Locked), Open() no hace nada y suena «bloqueada».
    /// </summary>
    public class VRDoor : MonoBehaviour
    {
        public enum Mode { Hinge, Slide }

        [SerializeField] Mode mode = Mode.Hinge;
        [Tooltip("La parte que se mueve. Vacío = este objeto.")]
        [SerializeField] Transform door;
        [SerializeField] float openAngle = 100f;
        [SerializeField] Vector3 openOffset = new Vector3(0f, 2.5f, 0f);
        [SerializeField] float openTime = 0.8f;
        [SerializeField] AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Estado")]
        [SerializeField] bool startOpen = false;
        [SerializeField] bool locked = false;
        [Tooltip("Se cierra sola a los X segundos de abrirse (0 = no).")]
        [SerializeField] float autoCloseAfter = 0f;

        [Header("Sonido")]
        [SerializeField] AudioClip openSound;
        [SerializeField] AudioClip closeSound;
        [SerializeField] AudioClip lockedSound;
        [SerializeField] AudioClip unlockSound;

        public UnityEvent onOpened = new UnityEvent();
        public UnityEvent onClosed = new UnityEvent();
        public UnityEvent onTriedLocked = new UnityEvent();

        public bool IsOpen { get; private set; }
        public bool IsLocked { get { return locked; } }

        Quaternion m_ClosedRot;
        Vector3 m_ClosedPos;
        float m_T;
        Coroutine m_Routine;

        void Awake()
        {
            if (door == null) door = transform;
            m_ClosedRot = door.localRotation;
            m_ClosedPos = door.localPosition;
            if (startOpen) { IsOpen = true; m_T = 1f; Apply(1f); }
        }

        public void Open()
        {
            if (IsOpen) return;
            if (locked)
            {
                if (lockedSound != null) VRAudio.PlayAt(lockedSound, door.position);
                onTriedLocked.Invoke();
                return;
            }
            IsOpen = true;
            if (openSound != null) VRAudio.PlayAt(openSound, door.position);
            Run(1f);
            onOpened.Invoke();
            if (autoCloseAfter > 0f) { CancelInvoke(nameof(Close)); Invoke(nameof(Close), autoCloseAfter); }
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (closeSound != null) VRAudio.PlayAt(closeSound, door.position);
            Run(0f);
            onClosed.Invoke();
        }

        public void Toggle() { if (IsOpen) Close(); else Open(); }

        /// <summary>Abre o cierra según un valor (útil con la palanca: On Turned On / Off o On Value Changed).</summary>
        public void SetOpen(bool open) { if (open) Open(); else Close(); }

        /// <summary>Apertura parcial 0-1 siguiendo una palanca o un dial (sin animación).</summary>
        public void SetOpenAmount(float amount)
        {
            if (locked) return;
            if (m_Routine != null) StopCoroutine(m_Routine);
            m_T = Mathf.Clamp01(amount);
            IsOpen = m_T > 0.5f;
            Apply(m_T);
        }

        public void Lock() { locked = true; }

        public void Unlock()
        {
            if (!locked) return;
            locked = false;
            if (unlockSound != null) VRAudio.PlayAt(unlockSound, door.position);
        }

        /// <summary>Quitar el cerrojo y abrir de una vez (para el evento de una llave).</summary>
        public void UnlockAndOpen() { Unlock(); Open(); }

        void Run(float target)
        {
            if (m_Routine != null) StopCoroutine(m_Routine);
            m_Routine = StartCoroutine(Animate(target));
        }

        IEnumerator Animate(float target)
        {
            while (!Mathf.Approximately(m_T, target))
            {
                m_T = Mathf.MoveTowards(m_T, target, Time.deltaTime / Mathf.Max(0.01f, openTime));
                Apply(curve.Evaluate(m_T));
                yield return null;
            }
        }

        void Apply(float k)
        {
            if (mode == Mode.Hinge) door.localRotation = m_ClosedRot * Quaternion.Euler(0f, openAngle * k, 0f);
            else door.localPosition = m_ClosedPos + openOffset * k;
        }
    }
}
