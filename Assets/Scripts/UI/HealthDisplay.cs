using NavesShooter.Core;
using UnityEngine;

namespace NavesShooter.UI
{
    // Vidas del jugador en el HUD: un ícono por punto de Health (rombos cian). Los puntos perdidos quedan tenues con
    // `lostAlpha`, opacidad percibida convertida a lineal como en TouchControlFeedback. Se actualiza con Health.Changed.
    public class HealthDisplay : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField, Tooltip("Un ícono por punto de vida, de izquierda a derecha.")] private NeonShapeGraphic[] icons;
        [SerializeField, Range(0f, 1f), Tooltip("Opacidad percibida de los puntos perdidos.")] private float lostAlpha = 0.15f;

        // En Start y no en OnEnable: el Awake de Health (que llena la vida) ya corrió.
        private void Start() => Refresh();

        private void OnEnable()
        {
            if (health != null) health.Changed += OnChanged;
        }

        private void OnDisable()
        {
            if (health != null) health.Changed -= OnChanged;
        }

        private void OnChanged(Health _) => Refresh();

        private void Refresh()
        {
            if (health == null || icons == null) return;

            float dim = lostAlpha;
            if (QualitySettings.activeColorSpace == ColorSpace.Linear) dim = Mathf.GammaToLinearSpace(dim);
            int current = health.Current;
            for (int i = 0; i < icons.Length; i++)
            {
                if (icons[i] == null) continue;
                Color color = icons[i].color;
                color.a = i < current ? 1f : dim;
                icons[i].color = color;
            }
        }
    }
}
