using System;
using UnityEngine;

namespace NavesShooter.Core
{
    public enum GameState { Playing, GameOver }

    // Ciclo de la partida: Playing → GameOver (la nave murió) → Playing (reintento, sin recargar la escena).
    // No conoce a nadie: cada sistema se suscribe a sus eventos y se resetea solo (pools, nave, cámara, puntaje).
    public class GameManager : MonoBehaviour
    {
        // Al reiniciar, antes de volver a Playing: momento para devolver objetos al pool y reubicar la nave.
        public event Action Restarted;
        public event Action<GameState> StateChanged;

        public GameState State { get; private set; } = GameState.Playing;

        public void EndGame()
        {
            if (State != GameState.Playing) return;
            SetState(GameState.GameOver);
        }

        public void Restart()
        {
            if (State != GameState.GameOver) return;
            Restarted?.Invoke();
            SetState(GameState.Playing);
        }

        private void SetState(GameState state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }
    }
}
