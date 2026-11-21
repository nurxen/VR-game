using UnityEngine;

namespace VRKit
{
    /// <summary>
    /// Andar y girar con los gatillos, para quien prefiere no usar los joysticks:
    ///   · Gatillo izquierdo: avanzar hacia donde miras (cuanto más aprietas, más rápido). Mantén X para ir hacia atrás.
    ///   · Gatillo derecho: girar la vista a la derecha. Mantén A para girar a la izquierda.
    /// Ponedlo en el XR Origin, junto al VRPlayer. Los joysticks de XRI siguen funcionando igual.
    /// Ojo: si en vuestro juego los gatillos hacen otra cosa (disparar, romper bloques), no lo uséis ahí.
    /// </summary>
    public class TriggerLocomotion : MonoBehaviour
    {
        public enum TurnStyle { Suave, PorSaltos }
        public enum Direction { DondeMiras, DondeApuntaElMando }

        /// <summary>Lo sube el modo desarrollador (VRDevMode).</summary>
        public static float SpeedMultiplier = 1f;

        [Header("Andar")]
        [SerializeField] Hand moveHand = Hand.Left;
        [Tooltip("Metros por segundo con el gatillo a fondo.")]
        [SerializeField] float moveSpeed = 3f;
        [SerializeField] Direction direction = Direction.DondeMiras;
        [Tooltip("Mantener el botón principal (X) de esa mano para ir hacia atrás.")]
        [SerializeField] bool backwardWithPrimary = true;

        [Header("Girar")]
        [SerializeField] Hand turnHand = Hand.Right;
        [Tooltip("Suave: gira mientras aprietas. Por saltos: un salto por pulsación (marea menos).")]
        [SerializeField] TurnStyle turnStyle = TurnStyle.Suave;
        [Tooltip("Grados por segundo (Suave).")]
        [SerializeField] float turnSpeed = 75f;
        [Tooltip("Grados por salto (Por saltos).")]
        [SerializeField] float snapAngle = 30f;
        [Tooltip("Mantener el botón principal (A) de esa mano para girar a la izquierda.")]
        [SerializeField] bool leftWithPrimary = true;

        [Header("Gatillo")]
        [Tooltip("Por debajo de esto, el gatillo no cuenta (evita moverse al rozarlo).")]
        [SerializeField, Range(0f, 0.5f)] float deadZone = 0.15f;

        HandInput m_Move, m_Turn;
        bool m_SnapReady = true;

        void OnEnable()
        {
            m_Move = new HandInput(moveHand);
            m_Turn = turnHand == moveHand ? m_Move : new HandInput(turnHand);
        }

        void OnDisable()
        {
            if (m_Turn != null && m_Turn != m_Move) m_Turn.Dispose();
            if (m_Move != null) m_Move.Dispose();
            m_Move = m_Turn = null;
        }

        void Update()
        {
            var player = VRPlayer.Instance;
            if (player == null || player.Head == null || m_Move == null || !player.LocomotionEnabled || Time.timeScale == 0f) return;

            // Andar
            float t = m_Move.Trigger.ReadValue<float>();
            if (t > deadZone)
            {
                float k = (t - deadZone) / (1f - deadZone);
                Vector3 fwd;
                if (direction == Direction.DondeApuntaElMando && player.GetHand(moveHand) != null)
                {
                    fwd = player.GetHand(moveHand).forward;
                    fwd.y = 0f;
                    fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : player.HeadForwardFlat;
                }
                else fwd = player.HeadForwardFlat;
                if (backwardWithPrimary && m_Move.Primary.IsPressed()) fwd = -fwd;
                player.Move(fwd * (moveSpeed * k * SpeedMultiplier * Time.deltaTime));
            }

            // Girar
            float r = m_Turn.Trigger.ReadValue<float>();
            float sign = leftWithPrimary && m_Turn.Primary.IsPressed() ? -1f : 1f;
            if (r > deadZone)
            {
                if (turnStyle == TurnStyle.Suave)
                    Turn(player, sign * turnSpeed * (r - deadZone) / (1f - deadZone) * Time.deltaTime);
                else if (m_SnapReady)
                {
                    Turn(player, sign * snapAngle);
                    m_SnapReady = false;
                }
            }
            else m_SnapReady = true;
        }

        /// <summary>Gira al jugador alrededor de su cabeza (no del centro del cuarto).</summary>
        static void Turn(VRPlayer player, float degrees)
        {
            player.transform.RotateAround(player.Head.position, Vector3.up, degrees);
        }
    }
}
