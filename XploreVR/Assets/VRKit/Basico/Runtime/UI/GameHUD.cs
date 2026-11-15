using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VRKit
{
    /// <summary>Algo que tiene munición (para que el marcador la enseñe). -1 = infinita.</summary>
    public interface IAmmoSource { int Ammo { get; } }

    /// <summary>
    /// Marcador: puntos, tiempo, vida del jugador y munición.
    /// En VR no hay "pantalla": pon el HUD en un Canvas World Space en la muñeca,
    /// en un objeto del escenario (un marcador en la pared) o a 1,5 m de la cámara.
    /// Todos los campos son opcionales.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        [SerializeField] TMP_Text scoreText;
        [SerializeField] string scoreFormat = "Puntos: {0}";
        [SerializeField] TMP_Text timeText;
        [Tooltip("Barra de vida: Image en modo Filled.")]
        [SerializeField] Image healthBar;
        [SerializeField] TMP_Text healthText;
        [Tooltip("Cualquier componente con munición (IAmmoSource): pistola, varita, ballesta…")]
        [SerializeField] MonoBehaviour weapon;
        [SerializeField] TMP_Text ammoText;

        int m_LastScore = int.MinValue;
        int m_LastSeconds = int.MinValue;
        float m_LastHealth = -1f;
        int m_LastAmmo = int.MinValue;

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm != null)
            {
                if (scoreText != null && gm.Score != m_LastScore)
                {
                    m_LastScore = gm.Score;
                    scoreText.text = string.Format(scoreFormat, gm.Score);
                }
                if (timeText != null)
                {
                    float t = gm.TimeRemaining >= 0f ? gm.TimeRemaining : gm.ElapsedTime;
                    int secs = Mathf.CeilToInt(t);
                    if (secs != m_LastSeconds)
                    {
                        m_LastSeconds = secs;
                        timeText.text = string.Format("{0}:{1:00}", secs / 60, secs % 60);
                    }
                }
            }

            var player = PlayerHealth.Instance;
            if (player != null && !Mathf.Approximately(player.Current, m_LastHealth))
            {
                m_LastHealth = player.Current;
                if (healthBar != null) healthBar.fillAmount = player.Normalized;
                if (healthText != null) healthText.text = Mathf.CeilToInt(player.Current).ToString();
            }

            var ammo = weapon as IAmmoSource;
            if (ammo != null && ammoText != null && ammo.Ammo != m_LastAmmo)
            {
                m_LastAmmo = ammo.Ammo;
                ammoText.text = ammo.Ammo < 0 ? "--" : ammo.Ammo.ToString();
            }
        }
    }
}
