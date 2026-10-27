using NavesShooter.Enemies;
using UnityEngine;

namespace NavesShooter.Core
{
    // Estadísticas del intento actual del nivel, para el resumen: tiempo jugado (solo en Playing, con tiempo escalado:
    // la pausa no cuenta) y naves enemigas eliminadas (solo en Playing: las que explotan al acoplarse no cuentan).
    // Vuelven a 0 al reiniciar (muerte o nivel siguiente).
    public class LevelStats : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;

        public float Elapsed { get; private set; }
        public int EnemiesDestroyed { get; private set; }

        private void OnEnable()
        {
            EnemyShip.Destroyed += OnEnemyDestroyed;
            if (gameManager != null) gameManager.Restarted += ResetStats;
        }

        private void OnDisable()
        {
            EnemyShip.Destroyed -= OnEnemyDestroyed;
            if (gameManager != null) gameManager.Restarted -= ResetStats;
        }

        private void Update()
        {
            if (gameManager == null || gameManager.State == GameState.Playing) Elapsed += Time.deltaTime;
        }

        private void OnEnemyDestroyed(EnemyDestroyed destroyed)
        {
            if (gameManager == null || gameManager.State == GameState.Playing) EnemiesDestroyed++;
        }

        private void ResetStats()
        {
            Elapsed = 0f;
            EnemiesDestroyed = 0;
        }
    }
}
