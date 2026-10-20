using UnityEngine;
using UnityEngine.EventSystems;

namespace NavesShooter.UI
{
    // Feedback visual de un control táctil: opacidad baja en reposo y, al tocarlo, más opacidad y un poco más de escala.
    // Reacciona a los toques sobre su propio GameObject (botones) o a SetPressed desde otro componente (joystick).
    [RequireComponent(typeof(CanvasGroup))]
    public class TouchControlFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField, Range(0f, 1f)] private float idleAlpha = 0.28f;
        [SerializeField, Range(0f, 1f)] private float pressedAlpha = 0.7f;
        [SerializeField, Min(0f)] private float pressedScale = 1.1f;
        [SerializeField, Min(0f), Tooltip("Segundos de la transición entre reposo y presionado.")]
        private float transitionTime = 0.08f;
        [SerializeField, Tooltip("Interpretar las opacidades como percibidas. En espacio de color lineal el blending ocurre en lineal y " +
            "un alpha de 0.28 sobre fondo oscuro se ve cerca del 57%; con esto se ve como 28%.")]
        private bool perceptualAlpha = true;

        private CanvasGroup canvasGroup;
        private bool pressed;
        private float blend; // 0 = reposo, 1 = presionado

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            ApplyBlend();
        }

        private void OnDisable()
        {
            pressed = false;
            blend = 0f;
            ApplyBlend();
        }

        public void SetPressed(bool value) => pressed = value;

        public void OnPointerDown(PointerEventData eventData) => SetPressed(true);

        public void OnPointerUp(PointerEventData eventData) => SetPressed(false);

        private void Update()
        {
            float target = pressed ? 1f : 0f;
            if (Mathf.Approximately(blend, target)) return;
            blend = transitionTime > 0f ? Mathf.MoveTowards(blend, target, Time.unscaledDeltaTime / transitionTime) : target;
            ApplyBlend();
        }

        private void ApplyBlend()
        {
            if (canvasGroup != null)
            {
                float alpha = Mathf.Lerp(idleAlpha, pressedAlpha, blend);
                if (perceptualAlpha && QualitySettings.activeColorSpace == ColorSpace.Linear) alpha = Mathf.GammaToLinearSpace(alpha);
                canvasGroup.alpha = alpha;
            }
            transform.localScale = Vector3.one * Mathf.Lerp(1f, pressedScale, blend);
        }
    }
}
