using UnityEngine;
using UnityEngine.InputSystem;

namespace NavesShooter.Input
{
    // Lee el action map "Gameplay" y expone dirección de movimiento (MoveDirection) y disparo (IsFiring).
    // Los controles táctiles (OnScreenStick/OnScreenButton) alimentan las mismas acciones a través de un gamepad virtual.
    public class PlayerInputReader : MonoBehaviour
    {
        [SerializeField, Tooltip("Asset con el action map \"Gameplay\" (acciones Move y Fire).")]
        private InputActionAsset actions;

        public Vector2 MoveDirection { get; private set; }
        public bool IsFiring { get; private set; }

        private InputActionMap gameplay;
        private InputAction move;
        private InputAction fire;

        private void Awake()
        {
            if (actions == null)
            {
                Debug.LogError($"{nameof(PlayerInputReader)}: falta asignar el InputActionAsset.", this);
                enabled = false;
                return;
            }

            gameplay = actions.FindActionMap("Gameplay", throwIfNotFound: true);
            move = gameplay.FindAction("Move", throwIfNotFound: true);
            fire = gameplay.FindAction("Fire", throwIfNotFound: true);
        }

        private void OnEnable()
        {
            gameplay?.Enable();
        }

        private void OnDisable()
        {
            gameplay?.Disable();
            MoveDirection = Vector2.zero;
            IsFiring = false;
        }

        private void Update()
        {
            MoveDirection = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
            IsFiring = fire.IsPressed();
        }
    }
}
