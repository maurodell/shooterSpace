using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace NavesShooter.Input
{
    // Traduce el input a avance (Thrust) y giro (Turn: -1 izquierda, 1 derecha).
    // Táctil: cualquier toque es avance; la mitad izquierda/derecha decide el giro y ambas a la vez van recto.
    // Teclado: W/↑ avance, A/D y ←/→ giro. Mouse como toque simulado para probar en el Editor.
    public class PlayerInputReader : MonoBehaviour
    {
        [SerializeField, Tooltip("En el Editor, el mouse genera toques simulados.")]
        private bool simulateTouchInEditor = true;

        public float Turn { get; private set; }
        public bool Thrust { get; private set; }

        private bool simulatingTouch;

        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
#if UNITY_EDITOR
            if (simulateTouchInEditor)
            {
                TouchSimulation.Enable();
                simulatingTouch = true;
            }
#endif
        }

        private void OnDisable()
        {
            if (simulatingTouch)
            {
                TouchSimulation.Disable();
                simulatingTouch = false;
            }
            EnhancedTouchSupport.Disable();
            Turn = 0f;
            Thrust = false;
        }

        private void Update()
        {
#if UNITY_EDITOR
            // El Input System del Editor apaga TouchSimulation al entrar a Play (domain reload) y en cada cambio
            // de settings si "Simulate Touch Input" del Input Debugger está desactivado. Se reactiva acá.
            if (simulatingTouch && (TouchSimulation.instance == null || !TouchSimulation.instance.enabled))
                TouchSimulation.Enable();
#endif

            bool left = false;
            bool right = false;
            float half = Screen.width * 0.5f;

            var touches = Touch.activeTouches;
            for (int i = 0; i < touches.Count; i++)
            {
                var touch = touches[i];
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) continue;

                if (touch.screenPosition.x < half) left = true;
                else right = true;
            }

            float turn = (right ? 1f : 0f) - (left ? 1f : 0f);
            bool thrust = left || right;

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) turn -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) turn += 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) thrust = true;
            }

            Turn = Mathf.Clamp(turn, -1f, 1f);
            Thrust = thrust;
        }
    }
}
