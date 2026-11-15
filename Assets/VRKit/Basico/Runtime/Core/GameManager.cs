using UnityEngine;
using UnityEngine.Events;

namespace VRKit
{
    public enum GameState { Playing, Paused, GameOver, Victory }

    /// <summary>
    /// Bucle básico de partida: puntuación, tiempo, pausa, derrota y victoria.
    /// Pon uno por escena de juego (un GameObject vacío llamado "GameManager").
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Tooltip("Segundos de partida. 0 = sin límite de tiempo.")]
        [SerializeField] float timeLimit = 0f;
        [Tooltip("Puntos para ganar. 0 = no se gana por puntos.")]
        [SerializeField] int scoreToWin = 0;
        [Tooltip("Al pausar se para el tiempo (física, animaciones). Los menús siguen funcionando.")]
        [SerializeField] bool freezeTimeOnPause = true;

        [Header("Eventos")]
        public UnityEvent<int> onScoreChanged = new UnityEvent<int>();
        public UnityEvent onPaused = new UnityEvent();
        public UnityEvent onResumed = new UnityEvent();
        public UnityEvent onGameOver = new UnityEvent();
        public UnityEvent onVictory = new UnityEvent();

        public GameState State { get; private set; }
        public int Score { get; private set; }
        public float ElapsedTime { get; private set; }
        public float TimeLimit { get { return timeLimit; } }
        /// <summary>Segundos restantes, o -1 si no hay límite.</summary>
        public float TimeRemaining { get { return timeLimit > 0f ? Mathf.Max(0f, timeLimit - ElapsedTime) : -1f; } }
        public bool IsPlaying { get { return State == GameState.Playing; } }

        void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
            State = GameState.Playing;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (State != GameState.Playing) return;
            ElapsedTime += Time.deltaTime;
            if (timeLimit > 0f && ElapsedTime >= timeLimit)
            {
                if (scoreToWin > 0) GameOver();   // se acabó el tiempo sin llegar a los puntos
                else Victory();                  // modo supervivencia: aguantar el tiempo es ganar
            }
        }

        public void AddScore(int points)
        {
            if (State != GameState.Playing || points == 0) return;
            Score += points;
            onScoreChanged.Invoke(Score);
            if (scoreToWin > 0 && Score >= scoreToWin) Victory();
        }

        public void Pause()
        {
            if (State != GameState.Playing) return;
            State = GameState.Paused;
            if (freezeTimeOnPause) Time.timeScale = 0f;
            onPaused.Invoke();
        }

        public void Resume()
        {
            if (State != GameState.Paused) return;
            State = GameState.Playing;
            Time.timeScale = 1f;
            onResumed.Invoke();
        }

        public void TogglePause()
        {
            if (State == GameState.Playing) Pause();
            else if (State == GameState.Paused) Resume();
        }

        public void GameOver()
        {
            if (State == GameState.GameOver || State == GameState.Victory) return;
            State = GameState.GameOver;
            Time.timeScale = 1f;
            onGameOver.Invoke();
        }

        public void Victory()
        {
            if (State == GameState.GameOver || State == GameState.Victory) return;
            State = GameState.Victory;
            Time.timeScale = 1f;
            onVictory.Invoke();
        }

        public void RestartLevel()
        {
            SceneLoader.ReloadCurrent();
        }
    }
}
