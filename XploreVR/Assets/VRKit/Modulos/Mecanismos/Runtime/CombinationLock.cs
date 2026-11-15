using UnityEngine;
using UnityEngine.Events;

namespace VRKit
{
    /// <summary>
    /// Cerradura de combinación con varios diales (VRDial): cuando todos están en su número, se abre.
    /// También sirve para cualquier puzle de «poner cada cosa en su posición» (espejos, estatuas, válvulas).
    /// </summary>
    public class CombinationLock : MonoBehaviour
    {
        [SerializeField] VRDial[] dials = new VRDial[0];
        [Tooltip("El paso correcto de cada dial, en el mismo orden.")]
        [SerializeField] int[] solution = new int[0];
        [Tooltip("Una vez resuelta, se queda resuelta aunque se muevan los diales.")]
        [SerializeField] bool stayUnlocked = true;
        [SerializeField] AudioClip solvedSound;

        public UnityEvent onSolved = new UnityEvent();
        public UnityEvent onUnsolved = new UnityEvent();

        public bool IsSolved { get; private set; }

        /// <summary>Configura los diales y la solución por código (lo usa el montador del editor).</summary>
        public void Setup(VRDial[] newDials, int[] newSolution) { dials = newDials; solution = newSolution; }

        void OnEnable() { foreach (var d in dials) if (d != null) d.onStepChanged.AddListener(OnStep); }
        void OnDisable() { foreach (var d in dials) if (d != null) d.onStepChanged.RemoveListener(OnStep); }

        void OnStep(int _)
        {
            if (IsSolved && stayUnlocked) return;
            bool ok = dials.Length > 0 && dials.Length == solution.Length;
            for (int i = 0; ok && i < dials.Length; i++) ok = dials[i] != null && dials[i].Step == solution[i];
            if (ok && !IsSolved)
            {
                IsSolved = true;
                if (solvedSound != null) VRAudio.PlayAt(solvedSound, transform.position);
                onSolved.Invoke();
            }
            else if (!ok && IsSolved)
            {
                IsSolved = false;
                onUnsolved.Invoke();
            }
        }
    }
}
