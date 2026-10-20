using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;

namespace NavesShooter.UI
{
    // Zona táctil del joystick (raycast target invisible). Reenvía los eventos de un solo dedo al OnScreenStick,
    // que es quien alimenta el stick del gamepad virtual.
    // Flotante: al tocar en cualquier lugar de la zona, la base aparece centrada en el toque y al soltar vuelve a su
    // posición por defecto. Fijo: la base no se mueve y solo cuentan los toques sobre ella.
    [RequireComponent(typeof(RectTransform))]
    public class FloatingJoystick : MonoBehaviour,
        IPointerDownHandler, IInitializePotentialDragHandler, IDragHandler, IPointerUpHandler
    {
        public enum Mode { Floating, Fixed }

        [SerializeField] private Mode mode = Mode.Floating;
        [SerializeField, Tooltip("Círculo exterior, hijo de esta zona y con pivot centrado. Su posición inicial es la posición por defecto.")]
        private RectTransform joystickBase;
        [SerializeField, Tooltip("OnScreenStick del círculo interior, hijo directo de la base.")]
        private OnScreenStick stick;
        [SerializeField] private TouchControlFeedback feedback;

        private RectTransform zone;
        private Vector2 defaultPosition;
        private bool tracking;
        private int pointerId;

        public Mode CurrentMode
        {
            get => mode;
            set
            {
                Release();
                mode = value;
                ApplyMode();
            }
        }

        private void Awake()
        {
            zone = (RectTransform)transform;
            defaultPosition = joystickBase.anchoredPosition;
            ApplyMode();
        }

        private void OnDisable()
        {
            Release();
        }

        private void OnValidate()
        {
            // Cambio de modo desde el Inspector durante Play.
            if (Application.isPlaying && zone != null) CurrentMode = mode;
        }

        private void ApplyMode()
        {
            // Flotante: el stick se mide desde el punto de toque (que es el centro de la base recién movida).
            // Fijo: se mide desde el centro de la base, así tocar el borde ya da dirección.
            stick.behaviour = mode == Mode.Floating
                ? OnScreenStick.Behaviour.RelativePositionWithStaticOrigin
                : OnScreenStick.Behaviour.ExactPositionWithStaticOrigin;
            joystickBase.anchoredPosition = defaultPosition;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // Un solo dedo controla el joystick; los demás toques en la zona se ignoran.
            if (tracking) return;
            if (mode == Mode.Fixed && !IsOverBase(eventData)) return;
            if (mode == Mode.Floating) MoveBaseTo(eventData);

            tracking = true;
            pointerId = eventData.pointerId;
            stick.OnPointerDown(eventData);
            if (feedback != null) feedback.SetPressed(true);
        }

        // Sin umbral de arrastre: el stick responde desde el primer píxel.
        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            eventData.useDragThreshold = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (tracking && eventData.pointerId == pointerId) stick.OnDrag(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (tracking && eventData.pointerId == pointerId) Release();
        }

        private void Release()
        {
            if (!tracking) return;
            tracking = false;
            stick.OnPointerUp(null);
            if (feedback != null) feedback.SetPressed(false);
            if (mode == Mode.Floating) joystickBase.anchoredPosition = defaultPosition;
        }

        // Centra la base en el toque, sin que se salga de la zona.
        private void MoveBaseTo(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(zone, eventData.position, eventData.pressEventCamera, out Vector2 local))
                return;
            Rect area = zone.rect;
            Vector2 half = joystickBase.rect.size * 0.5f;
            local.x = Mathf.Clamp(local.x, area.xMin + half.x, area.xMax - half.x);
            local.y = Mathf.Clamp(local.y, area.yMin + half.y, area.yMax - half.y);
            joystickBase.localPosition = local;
        }

        private bool IsOverBase(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(joystickBase, eventData.position, eventData.pressEventCamera, out Vector2 local))
                return false;
            float radius = Mathf.Min(joystickBase.rect.width, joystickBase.rect.height) * 0.5f;
            return (local - joystickBase.rect.center).sqrMagnitude <= radius * radius;
        }
    }
}
