using NavesShooter.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NavesShooter.UI
{
    // Pantalla de nivel completado: muestra `panel` en LevelCompleted y reinicia el nivel con un toque en cualquier parte.
    // Como el de GameOverScreen, el panel cubre toda la pantalla con un RaycastZone (los toques le llegan por bubbling
    // hasta este componente) y queda encima de los controles táctiles. `inputDelay` evita que el dedo que venía
    // jugando reinicie al instante.
    public class LevelCompletedScreen : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField, Tooltip("Raíz del texto y del área de toque. Se activa solo en LevelCompleted.")]
        private GameObject panel;
        [SerializeField, Min(0f), Tooltip("Segundos después de completar el nivel en que los toques todavía no reinician.")]
        private float inputDelay = 0.5f;

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

        private void OnStateChanged(GameState state)
        {
            bool completed = state == GameState.LevelCompleted;
            if (completed) shownAt = Time.unscaledTime;
            if (panel != null) panel.SetActive(completed);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (gameManager == null || gameManager.State != GameState.LevelCompleted) return;
            if (Time.unscaledTime - shownAt < inputDelay) return;
            gameManager.Restart();
        }
    }
}
