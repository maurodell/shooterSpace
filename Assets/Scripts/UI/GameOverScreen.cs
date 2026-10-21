using NavesShooter.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NavesShooter.UI
{
    // Pantalla de game over: muestra `panel` en GameOver y reinicia la partida con un toque en cualquier parte.
    // El panel cubre toda la pantalla con un RaycastZone (los toques le llegan por bubbling hasta este componente) y
    // queda encima de los controles táctiles, así un toque no mueve el joystick ni dispara.
    // `inputDelay` evita reiniciar sin querer con el dedo que estaba jugando cuando la nave murió.
    public class GameOverScreen : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField, Tooltip("Raíz del texto y del área de toque. Se activa solo en GameOver.")]
        private GameObject panel;
        [SerializeField, Min(0f), Tooltip("Segundos después de morir en que los toques todavía no reinician.")]
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
            bool gameOver = state == GameState.GameOver;
            if (gameOver) shownAt = Time.unscaledTime;
            if (panel != null) panel.SetActive(gameOver);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (gameManager == null || gameManager.State != GameState.GameOver) return;
            if (Time.unscaledTime - shownAt < inputDelay) return;
            gameManager.Restart();
        }
    }
}
