using NavesShooter.Input;
using UnityEngine;
using UnityEngine.Serialization;

namespace NavesShooter.Player
{
    // Nave del jugador: siempre se mueve hacia donde apunta (transform.up). El joystick (MoveDirection) da una
    // dirección objetivo: fuera de la zona muerta la nave acelera hasta `thrustSpeed` y rota hacia ese ángulo con
    // aceleración y desaceleración angular (nunca de golpe: una dirección opuesta se resuelve con una curva).
    // Dentro de la zona muerta vuelve a `idleSpeed` y sigue recta.
    [RequireComponent(typeof(Rigidbody2D), typeof(PolygonCollider2D))]
    public class ShipController : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private PlayerInputReader input;
        [SerializeField, Tooltip("Contorno de la nave, en un hijo para que el banking no escale el collider.")]
        private LineRenderer outline;

        [Header("Movimiento")]
        [SerializeField, Tooltip("Velocidad en reposo, sin avance (unidades/s).")] private float idleSpeed = 0.6f;
        [SerializeField, FormerlySerializedAs("forwardSpeed"), Tooltip("Velocidad con el avance apretado (unidades/s).")]
        private float thrustSpeed = 4f;
        [SerializeField, Tooltip("Unidades/s² al acelerar con el avance.")] private float linearAcceleration = 6f;
        [SerializeField, Tooltip("Unidades/s² al soltar el avance y volver a la velocidad de reposo.")] private float linearDeceleration = 3f;
        [SerializeField, Tooltip("Grados por segundo.")] private float maxTurnSpeed = 200f;
        [SerializeField, Tooltip("Grados/s² al aumentar el giro.")] private float angularAcceleration = 900f;
        [SerializeField, Tooltip("Grados/s² al soltar o invertir el giro.")] private float angularDeceleration = 1200f;
        [SerializeField, Range(0f, 0.9f), Tooltip("Magnitud mínima del joystick para avanzar y girar. Dentro de ella la nave sigue recta.")]
        private float deadZone = 0.2f;

        [Header("Forma")]
        [SerializeField, Tooltip("Vértices locales del contorno y del collider. La punta mira a +Y.")]
        private Vector2[] shape =
        {
            new Vector2(0f, 0.45f),
            new Vector2(0.32f, -0.35f),
            new Vector2(-0.32f, -0.35f),
        };
        [SerializeField] private float lineWidth = 0.06f;
        [SerializeField] private Color neonColor = new Color(0.2f, 1f, 1f);

        [Header("Banking")]
        [SerializeField, Range(0f, 0.5f), Tooltip("Cuánto se angosta la nave en X al girar a velocidad máxima.")]
        private float bankAmount = 0.25f;

        private const float ReverseHysteresis = 20f; // grados

        private Rigidbody2D body;
        private float angularSpeed; // grados/s, positivo = antihorario
        private float speed;

        private void Reset()
        {
            var rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            speed = idleSpeed;
            ApplyShape();
        }

        private void FixedUpdate()
        {
            Vector2 direction = input != null ? input.MoveDirection : Vector2.zero;
            // Dentro de la zona muerta no hay avance ni giro: el objetivo angular vuelve a 0 y la desaceleración lo frena suave.
            bool thrust = direction.sqrMagnitude > deadZone * deadZone;

            float targetLinear = thrust ? thrustSpeed : idleSpeed;
            float linearRate = targetLinear > speed ? linearAcceleration : linearDeceleration;
            speed = Mathf.MoveTowards(speed, targetLinear, linearRate * Time.fixedDeltaTime);

            float targetSpeed = thrust ? TurnSpeedTowards(direction) : 0f;

            bool speedingUp = angularSpeed * targetSpeed >= 0f && Mathf.Abs(targetSpeed) > Mathf.Abs(angularSpeed);
            float rate = speedingUp ? angularAcceleration : angularDeceleration;
            angularSpeed = Mathf.MoveTowards(angularSpeed, targetSpeed, rate * Time.fixedDeltaTime);

            body.angularVelocity = angularSpeed;
            float radians = body.rotation * Mathf.Deg2Rad;
            body.linearVelocity = new Vector2(-Mathf.Sin(radians), Mathf.Cos(radians)) * speed;
        }

        // Velocidad angular deseada para llegar al ángulo de `direction`: la máxima que todavía permite frenar justo en
        // el objetivo con `angularDeceleration` (v² = 2·a·d), sin superar `maxTurnSpeed` ni pasarse en un solo paso.
        private float TurnSpeedTowards(Vector2 direction)
        {
            // Rotación 0 = +Y, positiva = antihoraria (convención de Rigidbody2D.rotation).
            float targetAngle = Mathf.Atan2(-direction.x, direction.y) * Mathf.Rad2Deg;
            float delta = Mathf.DeltaAngle(body.rotation, targetAngle);

            // Con el objetivo casi opuesto, mantener el sentido de giro en curso para no cambiar de lado si el stick tiembla.
            if (Mathf.Abs(delta) > 180f - ReverseHysteresis && angularSpeed * delta < 0f)
                delta += 360f * Mathf.Sign(angularSpeed);

            float distance = Mathf.Abs(delta);
            float turnSpeed = Mathf.Min(maxTurnSpeed, Mathf.Sqrt(2f * angularDeceleration * distance), distance / Time.fixedDeltaTime);
            return Mathf.Sign(delta) * turnSpeed;
        }

        private void Update()
        {
            if (outline == null) return;
            float bank = maxTurnSpeed > 0f ? Mathf.Abs(angularSpeed) / maxTurnSpeed : 0f;
            outline.transform.localScale = new Vector3(1f - bankAmount * bank, 1f, 1f);
        }

        // Copia `shape` al collider y al LineRenderer. Se llama en Awake; desde el menú contextual para previsualizar en el Editor.
        [ContextMenu("Aplicar forma")]
        public void ApplyShape()
        {
            var polygon = GetComponent<PolygonCollider2D>();
            polygon.pathCount = 1;
            polygon.SetPath(0, shape);

            if (outline == null) return;
            outline.useWorldSpace = false;
            outline.loop = true;
            outline.positionCount = shape.Length;
            for (int i = 0; i < shape.Length; i++) outline.SetPosition(i, shape[i]);
            outline.widthMultiplier = lineWidth;
            outline.startColor = neonColor;
            outline.endColor = neonColor;
            outline.numCornerVertices = 2;
            outline.alignment = LineAlignment.View;
        }
    }
}
