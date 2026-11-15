using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace VRKit
{
    /// <summary>
    /// Conecta el GameManager con el progreso guardado: al ganar, guarda la mejor puntuación del nivel
    /// y desbloquea el siguiente. Ponlo junto al GameManager de cada nivel.
    /// </summary>
    public class LevelProgress : MonoBehaviour
    {
        [Tooltip("Nombre del nivel para guardar. Vacío = nombre de la escena.")]
        [SerializeField] string levelId = "";
        [Tooltip("Escena que se desbloquea al ganar (vacío = ninguna).")]
        [SerializeField] string unlocksLevel = "";
        [Tooltip("Texto opcional donde enseñar el récord (por ejemplo en el panel de Victoria).")]
        [SerializeField] TMP_Text bestScoreLabel;

        public UnityEvent onNewRecord = new UnityEvent();

        string Id { get { return string.IsNullOrEmpty(levelId) ? SceneManager.GetActiveScene().name : levelId; } }

        void Start()
        {
            var gm = GameManager.Instance;
            if (gm != null) gm.onVictory.AddListener(OnVictory);
            ProgressSave.Unlock(Id); // si estás jugando este nivel, está desbloqueado
            Refresh(false);
        }

        void OnVictory()
        {
            var gm = GameManager.Instance;
            bool record = ProgressSave.RecordScore(Id, gm != null ? gm.Score : 0);
            if (!string.IsNullOrEmpty(unlocksLevel)) ProgressSave.Unlock(unlocksLevel);
            Refresh(record);
            if (record) onNewRecord.Invoke();
        }

        void Refresh(bool record)
        {
            if (bestScoreLabel == null) return;
            bestScoreLabel.text = (record ? "¡Nuevo récord! " : "Récord: ") + ProgressSave.GetBestScore(Id);
        }
    }
}
