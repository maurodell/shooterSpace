using NavesShooter.Asteroids;
using NavesShooter.Core;
using TMPro;
using UnityEngine;

namespace NavesShooter.UI
{
    // Puntaje de la partida: suma por cada asteroide destruido (los chicos valen más, son más difíciles de acertar).
    // Solo cuenta en Playing: un proyectil que siga en vuelo tras la muerte no suma. Vuelve a 0 al reiniciar.
    public class ScoreCounter : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private TMP_Text label;

        [Header("Puntos por tamaño")]
        [SerializeField, Min(0)] private int largePoints = 20;
        [SerializeField, Min(0)] private int mediumPoints = 50;
        [SerializeField, Min(0)] private int smallPoints = 100;

        public int Score { get; private set; }

        private void Start() => Refresh();

        private void OnEnable()
        {
            Asteroid.Destroyed += OnAsteroidDestroyed;
            if (gameManager != null) gameManager.Restarted += ResetScore;
        }

        private void OnDisable()
        {
            Asteroid.Destroyed -= OnAsteroidDestroyed;
            if (gameManager != null) gameManager.Restarted -= ResetScore;
        }

        private void OnAsteroidDestroyed(AsteroidDestroyed destroyed)
        {
            if (gameManager != null && gameManager.State != GameState.Playing) return;
            Score += Points(destroyed.Size);
            Refresh();
        }

        private void ResetScore()
        {
            Score = 0;
            Refresh();
        }

        private int Points(AsteroidSize size)
        {
            switch (size)
            {
                case AsteroidSize.Medium: return mediumPoints;
                case AsteroidSize.Small: return smallPoints;
                default: return largePoints;
            }
        }

        private void Refresh()
        {
            if (label != null) label.SetText("{0}", Score);
        }
    }
}
