using System.Text;
using NavesShooter.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NavesShooter.UI
{
    // Resumen del nivel: en LevelCompleted (después del salto) muestra `panel` con el número de nivel, el puntaje, los
    // enemigos eliminados y el tiempo, y un toque en cualquier parte pasa al nivel siguiente (LevelManager.AdvanceLevel).
    // Como el de GameOverScreen, el panel cubre toda la pantalla con un RaycastZone (los toques le llegan por bubbling
    // hasta este componente) y queda encima de los controles táctiles. Durante `inputDelay` los toques no cuentan y
    // "Tocá para continuar" todavía no aparece; después pulsa. Usa tiempo sin escalar: el juego está congelado.
    public class LevelCompletedScreen : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private LevelManager levelManager;
        [SerializeField] private ScoreCounter score;
        [SerializeField] private LevelStats stats;
        [SerializeField, Tooltip("Raíz del resumen y del área de toque. Se activa solo en LevelCompleted.")]
        private GameObject panel;
        [SerializeField, Tooltip("\"NIVEL n COMPLETADO\".")] private TMP_Text title;
        [SerializeField, Tooltip("Puntaje, enemigos eliminados y tiempo.")] private TMP_Text statsLabel;
        [SerializeField, Tooltip("\"Tocá para continuar\": aparece pasado `inputDelay` y pulsa.")] private TMP_Text continueLabel;
        [SerializeField, Min(0f), Tooltip("Segundos después de mostrar el resumen en que los toques todavía no continúan.")]
        private float inputDelay = 1f;
        [SerializeField, Min(0f), Tooltip("Pulsos por segundo de \"Tocá para continuar\".")] private float pulseRate = 1.2f;

        private readonly StringBuilder text = new StringBuilder(128);
        private float shownAt;

        private void Awake()
        {
            if (panel != null) panel.SetActive(false);
        }

        private void OnEnable()
        {
            if (gameManager != null) gameManager.StateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            if (gameManager != null) gameManager.StateChanged -= OnStateChanged;
        }

        private void Update()
        {
            if (continueLabel == null || panel == null || !panel.activeSelf) return;
            float elapsed = Time.unscaledTime - shownAt - inputDelay;
            float alpha = elapsed < 0f ? 0f : 0.55f + 0.45f * Mathf.Cos(elapsed * pulseRate * 2f * Mathf.PI);
            continueLabel.alpha = alpha;
        }

        private void OnStateChanged(GameState state)
        {
            bool completed = state == GameState.LevelCompleted;
            if (completed)
            {
                shownAt = Time.unscaledTime;
                Fill();
            }
            if (panel != null) panel.SetActive(completed);
        }

        private void Fill()
        {
            if (title != null && levelManager != null) title.SetText("NIVEL {0} COMPLETADO", levelManager.LevelNumber);
            if (continueLabel != null) continueLabel.alpha = 0f;
            if (statsLabel == null) return;

            int seconds = stats != null ? Mathf.FloorToInt(stats.Elapsed) : 0;
            text.Clear();
            text.Append("PUNTAJE\n<size=160%><color=#FFFFFF>").Append(score != null ? score.Score : 0).Append("</color></size>\n\n");
            text.Append("ENEMIGOS ELIMINADOS\n<size=160%><color=#FFFFFF>").Append(stats != null ? stats.EnemiesDestroyed : 0).Append("</color></size>\n\n");
            text.Append("TIEMPO\n<size=160%><color=#FFFFFF>").Append(seconds / 60).Append(':').Append((seconds % 60).ToString("00")).Append("</color></size>");
            statsLabel.SetText(text);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (gameManager == null || gameManager.State != GameState.LevelCompleted) return;
            if (Time.unscaledTime - shownAt < inputDelay) return;
            if (levelManager != null) levelManager.AdvanceLevel();
            else gameManager.Restart();
        }
    }
}
