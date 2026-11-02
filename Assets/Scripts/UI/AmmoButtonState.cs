using NavesShooter.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NavesShooter.UI
{
    // Apaga el botón de disparo sin munición: TouchControlFeedback a su opacidad deshabilitada y los gráficos del botón
    // (contorno e ícono) teñidos de `emptyColor`. Al recuperar munición vuelven a sus colores originales, tomados en Awake.
    // El botón sigue funcionando: tocarlo sin munición hace sonar el clic del arma.
    public class AmmoButtonState : MonoBehaviour
    {
        [SerializeField] private Ammo ammo;
        [SerializeField] private TouchControlFeedback feedback;
        [SerializeField, Tooltip("Vacío = todos los Graphic del objeto y sus hijos.")] private Graphic[] graphics;
        [SerializeField] private Color emptyColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        private Color[] originalColors;

        private void Awake()
        {
            if (feedback == null) feedback = GetComponent<TouchControlFeedback>();
            if (graphics == null || graphics.Length == 0) graphics = GetComponentsInChildren<Graphic>(true);
            originalColors = new Color[graphics.Length];
            for (int i = 0; i < graphics.Length; i++) originalColors[i] = graphics[i] != null ? graphics[i].color : Color.white;
        }

        // En Start y no en OnEnable: el Awake de Ammo (que fija la munición inicial) ya corrió.
        private void Start() => Refresh();

        private void OnEnable()
        {
            if (ammo != null) ammo.OnAmmoChanged += OnAmmoChanged;
        }

        private void OnDisable()
        {
            if (ammo != null) ammo.OnAmmoChanged -= OnAmmoChanged;
        }

        private void OnAmmoChanged(int current, int max) => Refresh();

        private void Refresh()
        {
            if (ammo == null) return;
            bool empty = ammo.IsEmpty;
            if (feedback != null) feedback.Disabled = empty;
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] == null) continue;
                // Conserva el alfa propio de cada gráfico; solo cambia el tono.
                Color color = empty ? emptyColor : originalColors[i];
                color.a = originalColors[i].a;
                graphics[i].color = color;
            }
        }
    }
}
