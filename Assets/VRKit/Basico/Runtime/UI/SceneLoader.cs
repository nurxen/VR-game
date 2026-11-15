using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRKit
{
    /// <summary>
    /// Cambia de escena con fundido a negro. Llamadas estáticas desde código
    /// (SceneLoader.Load("Nivel1")) o métodos públicos para botones del menú
    /// (añade este componente a un objeto del menú y arrástralo al OnClick).
    /// Recuerda añadir las escenas en File > Build Profiles.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        static SceneLoader s_Runner;
        static bool s_Loading;

        // ---- Métodos para botones (OnClick) ----
        public void LoadScene(string sceneName) { Load(sceneName); }
        public void ReloadScene() { ReloadCurrent(); }
        public void QuitGame() { Quit(); }

        // ---- Métodos estáticos para código ----
        public static void Load(string sceneName)
        {
            if (s_Loading) return;
            Runner().StartCoroutine(LoadRoutine(sceneName));
        }

        public static void ReloadCurrent()
        {
            Load(SceneManager.GetActiveScene().name);
        }

        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        static SceneLoader Runner()
        {
            if (s_Runner == null)
            {
                var go = new GameObject("[SceneLoader]");
                DontDestroyOnLoad(go);
                s_Runner = go.AddComponent<SceneLoader>();
            }
            return s_Runner;
        }

        static IEnumerator LoadRoutine(string sceneName)
        {
            s_Loading = true;
            Time.timeScale = 1f;
            var fader = ScreenFader.Instance;
            if (fader != null) yield return fader.FadeTo(1f, 0.4f);

            var op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                Debug.LogError("[SceneLoader] No existe la escena '" + sceneName + "'. ¿Está añadida en Build Profiles?");
                if (fader != null) fader.FadeIn();
                s_Loading = false;
                yield break;
            }
            while (!op.isDone) yield return null;
            // La nueva escena hace su propio fundido de entrada si tiene un ScreenFader con fadeInOnStart.
            s_Loading = false;
        }
    }
}
