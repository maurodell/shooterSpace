using NavesShooter.Asteroids;
using NavesShooter.Core;
using NavesShooter.Enemies;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace NavesShooter.UI
{
    // Puntaje de la partida: suma `points` por cada cometa chico destruido (los grandes son indestructibles) y los
    // puntos de cada nave enemiga destruida (EnemyDestroyed.Points).
    // Solo cuenta en Playing: un proyectil que siga en vuelo tras la muerte no suma. Vuelve a 0 al reiniciar.
    public class ScoreCounter : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private TMP_Text label;

        [SerializeField, Min(0), FormerlySerializedAs("smallPoints"), Tooltip("Puntos por cometa chico destruido.")]
        private int points = 100;

        public int Score { get; private set; }

        private void Start() => Refresh();

        private void OnEnable()
        {
            Asteroid.Destroyed += OnAsteroidDestroyed;
            EnemyShip.Destroyed += OnEnemyDestroyed;
            if (gameManager != null) gameManager.Restarted += ResetScore;
        }

        private void OnDisable()
        {
            Asteroid.Destroyed -= OnAsteroidDestroyed;
            EnemyShip.Destroyed -= OnEnemyDestroyed;
            if (gameManager != null) gameManager.Restarted -= ResetScore;
        }

        private void OnAsteroidDestroyed(AsteroidDestroyed destroyed) => Add(points);

        private void OnEnemyDestroyed(EnemyDestroyed destroyed) => Add(destroyed.Points);

        private void Add(int amount)
        {
            if (gameManager != null && gameManager.State != GameState.Playing) return;
            Score += amount;
            Refresh();
        }

        private void ResetScore()
        {
            Score = 0;
            Refresh();
        }

        private void Refresh()
        {
            if (label != null) label.SetText("{0}", Score);
        }
    }
}
