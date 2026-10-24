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
    // al pool y avisa por `Destroyed` (explosión, puntaje); no se divide.
    [RequireComponent(typeof(Rigidbody2D), typeof(PolygonCollider2D), typeof(LineRenderer))]
    public class Asteroid : MonoBehaviour, IDamageable
    {
        // Se invoca después de devolver el cometa al pool.
        public static event Action<AsteroidDestroyed> Destroyed;

        [Header("Movimiento")]
        [SerializeField, Tooltip("Radio máximo del contorno (unidades).")] private float radius = 0.4f;
        [SerializeField, Tooltip("Velocidad lineal mínima (x) y máxima (y), en unidades/s.")]
        private Vector2 speedRange = new Vector2(1.8f, 2.8f);
        [SerializeField, Tooltip("Giro mínimo (x) y máximo (y) en valor absoluto, en grados/s. El sentido es aleatorio.")]
        private Vector2 spinRange = new Vector2(50f, 120f);

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
        private System.Random shapeRandom;
        private Rigidbody2D body;
        private PolygonCollider2D polygon;
        private LineRenderer outline;
        private ObjectPool<Asteroid> pool;
        private bool pendingFirstStep;

        // Sirve sobre el prefab, sin instancia activa (el spawner lo usa para ubicar).
        public float Radius => radius;

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
            shapeRandom = new System.Random(Random.Range(int.MinValue, int.MaxValue));

            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.useAutoMass = true;
            polygon.sharedMaterial = BounceMaterial;

            ApplyOutlineStyle();
        }

        // Activación desde el pool: resetea todo el estado de la instancia reutilizada. Llamar después de pool.Get, que
        // ya la ubicó y le dio la rotación inicial. Velocidad y giro salen al azar de los rangos.
        public void Launch(ObjectPool<Asteroid> owner, Vector2 direction)
        {
            pool = owner;
            BuildShape(shapeRandom);

            // La instancia suele venir de un Release en el mismo frame: con interpolación, el transform se dibujaría
            // entre la pose vieja (lejos, del otro lado de la cámara) y la nueva, cruzando la pantalla. Sin interpolar
            // hasta el primer paso de física, la pose previa queda en el punto de spawn.
            body.interpolation = RigidbodyInterpolation2D.None;
            pendingFirstStep = true;
            body.position = transform.position;
            body.rotation = transform.eulerAngles.z;
            body.linearVelocity = direction.normalized * Random.Range(speedRange.x, speedRange.y);
            float spin = Random.Range(spinRange.x, spinRange.y);
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

        private void BuildShape(System.Random random)
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

        // Genera una forma de muestra para previsualizar en el Editor. En juego la forma la crea Launch.
        [ContextMenu("Generar forma de muestra")]
        public void PreviewShape()
        {
            CacheComponents();
            ApplyOutlineStyle();
            BuildShape(new System.Random());
        }
    }
}
