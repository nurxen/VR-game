using System;
using UnityEngine.InputSystem;

namespace VRKit
{
    /// <summary>
    /// Los botones de una mano (gatillo, grip, botón principal A/X, secundario B/Y y el joystick),
    /// leídos directamente del mando. Lo usan los poderes que funcionan con la mano vacía
    /// (hechizos, telequinesis, gancho), porque no hay ningún objeto agarrado que dé eventos de XRI.
    ///
    ///   var input = new HandInput(Hand.Right);
    ///   if (input.Trigger.WasPressedThisFrame()) …
    ///   input.Dispose();   // en OnDisable
    /// </summary>
    public class HandInput : IDisposable
    {
        public readonly InputAction Trigger;
        public readonly InputAction Grip;
        public readonly InputAction Primary;
        public readonly InputAction Secondary;
        public readonly InputAction Stick;

        public HandInput(Hand hand, string keyboardTrigger = null, string keyboardGrip = null)
        {
            string h = hand == Hand.Left ? "LeftHand" : "RightHand";
            Trigger = Button("<XRController>{" + h + "}/{TriggerButton}", keyboardTrigger);
            Grip = Button("<XRController>{" + h + "}/{GripButton}", keyboardGrip);
            Primary = Button("<XRController>{" + h + "}/{PrimaryButton}", null);
            Secondary = Button("<XRController>{" + h + "}/{SecondaryButton}", null);
            Stick = new InputAction("Stick", InputActionType.Value);
            Stick.AddBinding("<XRController>{" + h + "}/{Primary2DAxis}");
            Stick.Enable();
        }

        static InputAction Button(string binding, string keyboard)
        {
            var a = new InputAction(binding, InputActionType.Button);
            a.AddBinding(binding);
            if (!string.IsNullOrEmpty(keyboard)) a.AddBinding(keyboard);
            a.Enable();
            return a;
        }

        public void Dispose()
        {
            foreach (var a in new[] { Trigger, Grip, Primary, Secondary, Stick }) { a.Disable(); a.Dispose(); }
        }
    }
}
