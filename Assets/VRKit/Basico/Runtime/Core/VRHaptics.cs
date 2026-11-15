using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRKit
{
    /// <summary>
    /// Vibración de los mandos. Todos los scripts del kit la usan a través de aquí,
    /// así que si cambia la API de XR Interaction Toolkit solo hay que tocar este archivo.
    /// </summary>
    public static class VRHaptics
    {
        /// <summary>Multiplica todas las vibraciones (opción «Intensidad de vibración»). 0 = sin vibración.</summary>
        public static float GlobalScale = 1f;

        public static void Send(IXRInteractor interactor, float amplitude, float duration)
        {
            amplitude *= GlobalScale;
            if (amplitude <= 0f || duration <= 0f) return;
            var input = interactor as XRBaseInputInteractor;
            if (input != null)
                input.SendHapticImpulse(Mathf.Clamp01(amplitude), duration);
        }

        /// <summary>Hace vibrar todos los mandos que haya bajo un objeto (por ejemplo, el XR Origin).</summary>
        public static void SendAll(GameObject root, float amplitude, float duration)
        {
            if (root == null) return;
            foreach (var input in root.GetComponentsInChildren<XRBaseInputInteractor>())
                Send(input, amplitude, duration);
        }

        /// <summary>Hace vibrar los mandos que haya bajo una mano (un Transform del rig).</summary>
        public static void SendHand(Transform hand, float amplitude, float duration)
        {
            if (hand == null) return;
            foreach (var input in hand.GetComponentsInChildren<XRBaseInputInteractor>())
                Send(input, amplitude, duration);
        }
    }
}
