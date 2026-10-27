using NavesShooter.Core;
using NavesShooter.Input;
using UnityEngine;

namespace NavesShooter.Player
{
    // Nave del jugador: siempre se mueve hacia donde apunta (transform.up). El joystick (MoveDirection) da una
    // dirección objetivo: fuera de la zona muerta la nave acelera hasta `thrustSpeed` y rota hacia ese ángulo con
    // aceleración y desaceleración angular (nunca de golpe: una dirección opuesta se resuelve con una curva).
    // Dentro de la zona muerta vuelve a `idleSpeed` y sigue recta. Un golpe (`Knockback`) suma un empuje que se
    // disipa a `knockbackDecay`, encima de la velocidad de avance. Con piloto automático (`StartAutopilot`, en el
    // acople con la nave nodriza) ignora el input y va sola a un punto y un ángulo, frenando al llegar.
    [RequireComponent(typeof(Rigidbody2D), typeof(PolygonCollider2D))]
    public class ShipController : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private PlayerInputReader input;
        [SerializeField, Tooltip("Contorno de la nave, en un hijo para que el banking no escale el collider.")]
        private LineRenderer outline;
        [SerializeField, Tooltip("Velocidades, giro, zona muerta, banking y empuje (sección Ship).")] private GameConfig config;

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

        private const float ReverseHysteresis = 20f; // grados

        private GameConfig.ShipSettings settings;
        private Rigidbody2D body;
        private float angularSpeed; // grados/s, positivo = antihorario
        private float speed;
        private Vector2 knockback;
        private bool autopilot;
        private Vector2 autopilotPoint;
        private float autopilotAngle;

        public bool Autopilot => autopilot;

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
            ApplyShape();
            if (config == null)
            {
                Debug.LogError($"{nameof(ShipController)}: falta asignar el GameConfig.", this);
                enabled = false;
                return;
            }

            settings = config.Ship;
            speed = settings.idleSpeed;
        }

        // Al reaparecer (ShipLife reactiva la nave) arranca quieta, sin el giro ni la velocidad con que murió.
        private void OnEnable()
        {
            if (settings == null) return;
            speed = settings.idleSpeed;
            angularSpeed = 0f;
            knockback = Vector2.zero;
            autopilot = false;
        }

        // Toma el control: va a `point` y gira hasta `angle` (grados, convención de Rigidbody2D.rotation). Se suelta al
        // reactivarse la nave (reaparición).
        public void StartAutopilot(Vector2 point, float angle)
        {
            autopilot = true;
            autopilotPoint = point;
            autopilotAngle = angle;
            knockback = Vector2.zero;
        }

        // Empuje por un choque: se suma a la velocidad y se disipa solo. La nave pierde el avance, así el empuje no
        // queda anulado por el thrust que la llevaba contra el obstáculo.
        public void Knockback(Vector2 velocity)
        {
            knockback = velocity;
            speed = Mathf.Min(speed, settings.idleSpeed);
        }

        private void FixedUpdate()
        {
            if (autopilot)
            {
                AutopilotStep();
                return;
            }

            Vector2 direction = input != null ? input.MoveDirection : Vector2.zero;
            // Dentro de la zona muerta no hay avance ni giro: el objetivo angular vuelve a 0 y la desaceleración lo frena suave.
            bool thrust = direction.sqrMagnitude > settings.deadZone * settings.deadZone;

            float targetLinear = thrust ? settings.thrustSpeed : settings.idleSpeed;
            float linearRate = targetLinear > speed ? settings.linearAcceleration : settings.linearDeceleration;
            speed = Mathf.MoveTowards(speed, targetLinear, linearRate * Time.fixedDeltaTime);

            float targetSpeed = thrust ? TurnSpeedTowards(direction) : 0f;

            bool speedingUp = angularSpeed * targetSpeed >= 0f && Mathf.Abs(targetSpeed) > Mathf.Abs(angularSpeed);
            float rate = speedingUp ? settings.angularAcceleration : settings.angularDeceleration;
            angularSpeed = Mathf.MoveTowards(angularSpeed, targetSpeed, rate * Time.fixedDeltaTime);

            body.angularVelocity = angularSpeed;
            float radians = body.rotation * Mathf.Deg2Rad;
            knockback = Vector2.MoveTowards(knockback, Vector2.zero, settings.knockbackDecay * Time.fixedDeltaTime);
            body.linearVelocity = new Vector2(-Mathf.Sin(radians), Mathf.Cos(radians)) * speed + knockback;
        }

        // Velocidad proporcional a la distancia al punto (sin pasar `thrustSpeed`) con la aceleración normal, y el mismo
        // giro con frenado que con el joystick, hacia el ángulo fijo.
        private void AutopilotStep()
        {
            Vector2 desired = Vector2.ClampMagnitude((autopilotPoint - body.position) * settings.autopilotGain, settings.thrustSpeed);
            body.linearVelocity = Vector2.MoveTowards(body.linearVelocity, desired, settings.linearAcceleration * Time.fixedDeltaTime);
            speed = Mathf.Min(body.linearVelocity.magnitude, settings.thrustSpeed);

            float radians = autopilotAngle * Mathf.Deg2Rad;
            float targetSpeed = TurnSpeedTowards(new Vector2(-Mathf.Sin(radians), Mathf.Cos(radians)));
            bool speedingUp = angularSpeed * targetSpeed >= 0f && Mathf.Abs(targetSpeed) > Mathf.Abs(angularSpeed);
            float rate = speedingUp ? settings.angularAcceleration : settings.angularDeceleration;
            angularSpeed = Mathf.MoveTowards(angularSpeed, targetSpeed, rate * Time.fixedDeltaTime);
            body.angularVelocity = angularSpeed;
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
            // Mathf.Min anidado: la sobrecarga de tres argumentos es `params` y aloca un array en cada llamada.
            float turnSpeed = Mathf.Min(Mathf.Min(settings.maxTurnSpeed, Mathf.Sqrt(2f * settings.angularDeceleration * distance)), distance / Time.fixedDeltaTime);
            return Mathf.Sign(delta) * turnSpeed;
        }

        private void Update()
        {
            if (outline == null) return;
            float bank = settings.maxTurnSpeed > 0f ? Mathf.Abs(angularSpeed) / settings.maxTurnSpeed : 0f;
            outline.transform.localScale = new Vector3(1f - settings.bankAmount * bank, 1f, 1f);
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
