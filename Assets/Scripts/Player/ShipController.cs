using NavesShooter.Input;
using UnityEngine;
using UnityEngine.Serialization;

namespace NavesShooter.Player
{
    // Nave del jugador: siempre se mueve hacia donde apunta (transform.up). En reposo deriva a `idleSpeed`;
    // mientras se mantiene el avance acelera hasta `thrustSpeed`. Solo puede girar mientras avanza,
    // con aceleración y desaceleración angular.
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
        private float thrustSpeed = 5f;
        [SerializeField, Tooltip("Unidades/s² al acelerar con el avance.")] private float linearAcceleration = 6f;
        [SerializeField, Tooltip("Unidades/s² al soltar el avance y volver a la velocidad de reposo.")] private float linearDeceleration = 3f;
        [SerializeField, Tooltip("Grados por segundo.")] private float maxTurnSpeed = 200f;
        [SerializeField, Tooltip("Grados/s² al aumentar el giro.")] private float angularAcceleration = 900f;
        [SerializeField, Tooltip("Grados/s² al soltar o invertir el giro.")] private float angularDeceleration = 1200f;

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
            bool thrust = input != null && input.Thrust;
            // Sin avance no hay giro: el objetivo angular vuelve a 0 y la desaceleración lo frena suave.
            float turn = thrust ? input.Turn : 0f;

            float targetLinear = thrust ? thrustSpeed : idleSpeed;
            float linearRate = targetLinear > speed ? linearAcceleration : linearDeceleration;
            speed = Mathf.MoveTowards(speed, targetLinear, linearRate * Time.fixedDeltaTime);

            // Derecha (+1) = sentido horario = rotación negativa en 2D.
            float targetSpeed = -turn * maxTurnSpeed;

            bool speedingUp = angularSpeed * targetSpeed >= 0f && Mathf.Abs(targetSpeed) > Mathf.Abs(angularSpeed);
            float rate = speedingUp ? angularAcceleration : angularDeceleration;
            angularSpeed = Mathf.MoveTowards(angularSpeed, targetSpeed, rate * Time.fixedDeltaTime);

            body.angularVelocity = angularSpeed;
            float radians = body.rotation * Mathf.Deg2Rad;
            body.linearVelocity = new Vector2(-Mathf.Sin(radians), Mathf.Cos(radians)) * speed;
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
