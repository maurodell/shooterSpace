using System;
using UnityEngine;

namespace NavesShooter.Core
{
    public enum GameState { Playing, Paused, GameOver, Jumping, LevelCompleted }

    // Ciclo de la partida: Playing → GameOver (la nave murió) → Playing (reintento, sin recargar la escena).
    // Playing → Jumping (la nave llegó a la zona de acople: secuencia del salto, con el tiempo corriendo y sin control
    // del jugador) → LevelCompleted (resumen; el juego queda congelado) → Playing (siguiente nivel, vía LevelManager).
    // Playing ↔ Paused: se pausa sola al perder el foco (barra de notificaciones, Centro de Control, llamada, app en
    // segundo plano) con Time.timeScale = 0. Así Android e iOS se comportan igual: iOS congela el juego mientras está
    // inactivo, pero Android lo deja correr detrás de la barra de notificaciones; en los dos, al volver queda en pausa.
    // No conoce a nadie: cada sistema se suscribe a sus eventos y se resetea solo (pools, nave, cámara, puntaje).
    public class GameManager : MonoBehaviour
    {
        [SerializeField, Tooltip("Pausar también en el Editor al perder el foco. Apagado por defecto: el Game view pierde " +
            "el foco con cada click en otra ventana.")]
        private bool pauseOnFocusLossInEditor;

        // Al reiniciar, antes de volver a Playing: momento para devolver objetos al pool y reubicar la nave.
        public event Action Restarted;
        public event Action<GameState> StateChanged;

        public GameState State { get; private set; } = GameState.Playing;

        public void EndGame()
        {
            if (State != GameState.Playing) return;
            SetState(GameState.GameOver);
        }

        // Llegó a la nave nodriza: empieza la secuencia del salto. El tiempo sigue corriendo, pero todo lo que solo
        // actúa en Playing (spawners, disparo, puntaje, daño a la nave) se detiene.
        public void BeginJump()
        {
            if (State != GameState.Playing) return;
            SetState(GameState.Jumping);
        }

        // Terminó la secuencia del salto: se congela el gameplay hasta el reinicio (siguiente nivel).
        public void CompleteLevel()
        {
            if (State != GameState.Jumping) return;
            Time.timeScale = 0f;
            SetState(GameState.LevelCompleted);
        }

        public void Restart()
        {
            if (State != GameState.GameOver && State != GameState.LevelCompleted) return;
            Time.timeScale = 1f;
            Restarted?.Invoke();
            SetState(GameState.Playing);
        }

        public void Pause()
        {
            if (State != GameState.Playing) return;
            Time.timeScale = 0f;
            SetState(GameState.Paused);
        }

        public void Resume()
        {
            if (State != GameState.Paused) return;
            Time.timeScale = 1f;
            SetState(GameState.Playing);
        }

        // Android: barra de notificaciones o diálogo del sistema encima. iOS: la app pasa a inactiva.
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) PauseOnFocusLoss();
        }

        // Las dos: la app pasa a segundo plano.
        private void OnApplicationPause(bool paused)
        {
            if (paused) PauseOnFocusLoss();
        }

        private void PauseOnFocusLoss()
        {
            if (Application.isEditor && !pauseOnFocusLossInEditor) return;
            Pause();
        }

        private void OnDestroy()
        {
            if (State == GameState.Paused || State == GameState.LevelCompleted) Time.timeScale = 1f;
        }

        private void SetState(GameState state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }
    }
}
