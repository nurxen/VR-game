using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VRKit
{
    /// <summary>
    /// Selector de niveles para el menú principal: un botón por nivel, bloqueado hasta que se gana el anterior
    /// (LevelProgress lo desbloquea), con el récord de cada uno.
    /// Necesita un contenedor (con Vertical Layout Group) y un botón de plantilla dentro (se oculta y se copia).
    /// </summary>
    public class LevelSelectMenu : MonoBehaviour
    {
        [Serializable]
        public class Level
        {
            public string sceneName = "Nivel1";
            public string displayName = "Nivel 1";
            [Tooltip("Desbloqueado desde el principio.")]
            public bool unlockedByDefault;
        }

        [SerializeField] Level[] levels = new Level[0];
        [SerializeField] Transform container;
        [SerializeField] Button buttonTemplate;
        [Tooltip("Con esto activado, todos los niveles están abiertos (para probar).")]
        [SerializeField] bool unlockAllForTesting = false;

        void Start() { Build(); ProgressSave.Changed += Build; }
        void OnDestroy() { ProgressSave.Changed -= Build; }

        public void Build()
        {
            if (container == null || buttonTemplate == null) return;
            buttonTemplate.gameObject.SetActive(false);
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                var c = container.GetChild(i);
                if (c != buttonTemplate.transform) Destroy(c.gameObject);
            }
            for (int i = 0; i < levels.Length; i++)
            {
                var l = levels[i];
                bool open = unlockAllForTesting || l.unlockedByDefault || i == 0 || ProgressSave.IsUnlocked(l.sceneName);
                var b = Instantiate(buttonTemplate, container);
                b.gameObject.SetActive(true);
                b.interactable = open;
                int best = ProgressSave.GetBestScore(l.sceneName);
                var label = b.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = (open ? l.displayName : "[Bloqueado] " + l.displayName) + (best > 0 ? "   Récord: " + best : "");
                string scene = l.sceneName;
                b.onClick.AddListener(() => SceneLoader.Load(scene));
            }
        }

        /// <summary>Para un botón «Borrar progreso» en Opciones.</summary>
        public void ResetProgress() { ProgressSave.ResetAll(); }
    }
}
