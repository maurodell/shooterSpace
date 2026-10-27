using System;
using System.Collections.Generic;
using NavesShooter.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace NavesShooter.Asteroids
{
    // Cometa chico destruido por un impacto, tal como estaba al romperse. No se emite cuando vuelve al pool por distancia.
    public readonly struct AsteroidDestroyed
    {
        public readonly Vector2 Position;
        public readonly Vector2 Velocity;

        public AsteroidDestroyed(Vector2 position, Vector2 velocity)
        {
            Position = position;
            Velocity = velocity;
        }
    }

    // Cometa chico en movimiento, con contorno neón: polígono irregular que se regenera cada vez que sale del pool, así
    // no hay dos iguales. Rigidbody2D dinámico sin gravedad ni amortiguación: conserva la velocidad y el giro con que se
    // lanza y rebota sin perder energía (también contra los cometas grandes, que son kinematic). Un impacto lo devuelve
    // al pool y avisa por `Destroyed` (explosión, puntaje); no se divide. Radio, velocidad y giro salen de la sección
    // Asteroids del GameConfig, que le pasa el spawner en `Launch`.
    [RequireComponent(typeof(Rigidbody2D), typeof(PolygonCollider2D), typeof(LineRenderer))]
    public class Asteroid : MonoBehaviour, IDamageable
    {
        // Se invoca después de devolver el cometa al pool.
        public static event Action<AsteroidDestroyed> Destroyed;

        [Header("Forma")]
        [SerializeField, Range(3, 32)] private int minVertices = 8;
        [SerializeField, Range(3, 32)] private int maxVertices = 12;
        [SerializeField, Range(0.1f, 1f), Tooltip("Radio mínimo de cada vértice, como fracción de `radius`.")]
        private float minRadiusFactor = 0.7f;
        [SerializeField, Range(0f, 0.45f), Tooltip("Desvío angular máximo de cada vértice, como fracción de la separación entre vértices.")]
        private float angleJitter = 0.3f;
        [SerializeField] private float lineWidth = 0.06f;
        [SerializeField] private Color neonColor = new Color(1f, 0.2f, 0.85f);

        // Compartido por los cometas chicos y los grandes: sin fricción y rebote total, para que los choques no los frenen.
        private static PhysicsMaterial2D bounceMaterial;

        internal static PhysicsMaterial2D BounceMaterial
        {
            get
            {
                if (bounceMaterial == null)
                    bounceMaterial = new PhysicsMaterial2D("AsteroidBounce") { friction = 0f, bounciness = 1f };
                return bounceMaterial;
            }
        }

        private readonly List<Vector2> points = new List<Vector2>(32);
        private AsteroidShape.ShapeRandom shapeRandom;
        private Rigidbody2D body;
        private PolygonCollider2D polygon;
        private LineRenderer outline;
        private ObjectPool<Asteroid> pool;
        private bool pendingFirstStep;

        private void Reset()
        {
            var rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        private void OnValidate()
        {
            maxVertices = Mathf.Max(maxVertices, minVertices);
        }

        private void Awake()
        {
            CacheComponents();
            shapeRandom = new AsteroidShape.ShapeRandom(Random.Range(int.MinValue, int.MaxValue));

            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.useAutoMass = true;
            polygon.sharedMaterial = BounceMaterial;

            ApplyOutlineStyle();
        }

        // Activación desde el pool: resetea todo el estado de la instancia reutilizada. Llamar después de pool.Get, que
        // ya la ubicó y le dio la rotación inicial. Velocidad y giro salen al azar de los rangos de `settings`; la
        // velocidad se multiplica por `speedScale` (dificultad).
        public void Launch(ObjectPool<Asteroid> owner, Vector2 direction, GameConfig.AsteroidSettings settings, float speedScale = 1f)
        {
            pool = owner;
            BuildShape(shapeRandom, settings.radius);

            // La instancia suele venir de un Release en el mismo frame: con interpolación, el transform se dibujaría
            // entre la pose vieja (lejos, del otro lado de la cámara) y la nueva, cruzando la pantalla. Sin interpolar
            // hasta el primer paso de física, la pose previa queda en el punto de spawn.
            body.interpolation = RigidbodyInterpolation2D.None;
            pendingFirstStep = true;
            body.position = transform.position;
            body.rotation = transform.eulerAngles.z;
            body.linearVelocity = direction.normalized * (Random.Range(settings.speedRange.x, settings.speedRange.y) * speedScale);
            float spin = Random.Range(settings.spinRange.x, settings.spinRange.y);
            body.angularVelocity = Random.value < 0.5f ? spin : -spin;
        }

        private void FixedUpdate()
        {
            if (!pendingFirstStep) return;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            pendingFirstStep = false;
        }

        // Ignora impactos antes de su primer paso de física: la instancia puede haber salido del pool en el lugar de
        // otro cometa recién roto, y un callback atrasado de ese paso no debe romperla sin que nada la haya tocado.
        public void TakeHit()
        {
            if (pendingFirstStep || !gameObject.activeSelf) return;

            var info = new AsteroidDestroyed(body.position, body.linearVelocity);
            Despawn();
            Destroyed?.Invoke(info);
        }

        // Sin recarga de dominio al entrar en Play, los estáticos sobreviven entre sesiones.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Destroyed = null;

        public void Despawn()
        {
            if (pool != null) pool.Release(this);
            else gameObject.SetActive(false);
        }

        private void BuildShape(AsteroidShape.ShapeRandom random, float radius)
        {
            AsteroidShape.Build(points, random, radius, minVertices, maxVertices, minRadiusFactor, angleJitter);
            AsteroidShape.Apply(points, polygon, outline);
        }

        private void CacheComponents()
        {
            body = GetComponent<Rigidbody2D>();
            polygon = GetComponent<PolygonCollider2D>();
            outline = GetComponent<LineRenderer>();
        }

        private void ApplyOutlineStyle()
        {
            outline.useWorldSpace = false;
            outline.loop = true;
            outline.widthMultiplier = lineWidth;
            outline.startColor = neonColor;
            outline.endColor = neonColor;
            outline.numCornerVertices = 2;
            outline.alignment = LineAlignment.View;
        }

        // Genera una forma de muestra (con el radio por defecto del GameConfig) para previsualizar en el Editor. En
        // juego la forma la crea Launch.
        [ContextMenu("Generar forma de muestra")]
        public void PreviewShape()
        {
            CacheComponents();
            ApplyOutlineStyle();
            BuildShape(new AsteroidShape.ShapeRandom(Random.Range(int.MinValue, int.MaxValue)), new GameConfig.AsteroidSettings().radius);
        }
    }
}
