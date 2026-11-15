using UnityEngine;

namespace VRKit
{
    /// <summary>
    /// Menú de muñeca: aparece al girar la mano y mirarte la muñeca, como un reloj.
    /// Para inventario, mapa, vida, objetivos o un botón de pausa rápido.
    /// Pon el menú (Canvas World Space pequeño) como hijo del mando izquierdo.
    /// </summary>
    public class WristMenu : MonoBehaviour
    {
        [Tooltip("La mano o el mando que lleva el menú.")]
        [SerializeField] Transform hand;
        [Tooltip("El objeto del menú que se enseña y oculta.")]
        [SerializeField] GameObject menu;
        [Tooltip("Dirección (en ejes locales de la mano) a la que mira la cara interna de la muñeca. " +
                 "Para el mando izquierdo de Quest suele ser (1,0,0) o (0,1,0): prueba hasta que encaje.")]
        [SerializeField] Vector3 wristLocalDirection = Vector3.right;
        [Tooltip("Lo de frente que tiene que estar la muñeca hacia la cara (0-1).")]
        [SerializeField, Range(0f, 1f)] float showThreshold = 0.7f;
        [Tooltip("Margen para que no parpadee en el límite.")]
        [SerializeField, Range(0f, 0.3f)] float hysteresis = 0.1f;

        Transform m_Head;
        bool m_Visible;

        void Start()
        {
            if (Camera.main != null) m_Head = Camera.main.transform;
            if (menu != null) menu.SetActive(false);
        }

        void Update()
        {
            if (hand == null || menu == null || m_Head == null) return;
            Vector3 wristDir = hand.TransformDirection(wristLocalDirection.normalized);
            Vector3 toHead = (m_Head.position - hand.position).normalized;
            float facing = Vector3.Dot(wristDir, toHead);

            bool show = m_Visible ? facing > showThreshold - hysteresis : facing > showThreshold;
            if (show != m_Visible)
            {
                m_Visible = show;
                menu.SetActive(show);
            }
        }
    }
}
