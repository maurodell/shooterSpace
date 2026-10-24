using NavesShooter.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NavesShooter.UI
{
    // Pantalla de pausa: muestra `panel` en Paused y reanuda la partida con un toque en cualquier parte.
    // Como el de GameOverScreen, el panel cubre toda la pantalla con un RaycastZone (los toques le llegan por bubbling
    // hasta este componente) y queda encima de los controles táctiles, así el toque que reanuda no mueve ni dispara.
    public class PauseScreen : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField, Tooltip("Raíz del texto y del área de toque. Se activa solo en Paused.")]
        private GameObject panel;

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
            if (panel != null) panel.SetActive(state == GameState.Paused);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (gameManager == null || gameManager.State != GameState.Paused) return;
            gameManager.Resume();
        }
    }
}
