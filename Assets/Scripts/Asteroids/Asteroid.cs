using System;
using System.Collections.Generic;
using NavesShooter.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace NavesShooter.Asteroids
{
    public enum AsteroidSize { Large, Medium, Small }

    // Asteroide destruido por un impacto, tal como estaba al romperse. No se emite cuando vuelve al pool por distancia.
    public readonly struct AsteroidDestroyed
    {
        public readonly AsteroidSize Size;
        public readonly Vector2 Position;
        public readonly Vector2 Velocity;

        public AsteroidDestroyed(AsteroidSize size, Vector2 position, Vector2 velocity)
        {
            Size = size;
            Position = position;
            Velocity = velocity;
        }
    }

    // Asteroide con contorno neón: polígono irregular que se regenera cada vez que sale del pool, así no hay dos
    // iguales. Rigidbody2D dinámico sin gravedad ni amortiguación: conserva la velocidad y el giro con que se lanza y
    // rebota contra otros cuerpos sin perder energía. Un impacto lo devuelve al pool y avisa por `Destroyed`; quien
    // escucha decide qué sigue (el spawner lo parte en fragmentos, el efecto dibuja la explosión).
    [RequireComponent(typeof(Rigidbody2D), typeof(PolygonCollider2D), typeof(LineRenderer))]
    public class Asteroid : MonoBehaviour, IDamageable
    {
        // Se invoca después de devolver el asteroide al pool: los suscriptores pueden pedir instancias (incluso esta).
        public static event Action<AsteroidDestroyed> Destroyed;

        [Serializable]
        public struct SizeSettings
        {
            [Tooltip("Radio máximo del contorno (unidades).")] public float radius;
            [Tooltip("Velocidad lineal mínima (x) y máxima (y), en unidades/s.")] public Vector2 speedRange;
            [Tooltip("Giro mínimo (x) y máximo (y) en valor absoluto, en grados/s. El sentido es aleatorio.")] public Vector2 spinRange;
        }

        [Header("Tamaños")]
        [SerializeField] private SizeSettings large = new SizeSettings
        {
            radius = 1.1f, speedRange = new Vector2(0.6f, 1.2f), spinRange = new Vector2(10f, 30f),
        };
        [SerializeField] private SizeSettings medium = new SizeSettings
        {
            radius = 0.65f, speedRange = new Vector2(1.1f, 1.9f), spinRange = new Vector2(25f, 60f),
        };
        [SerializeField] private SizeSettings small = new SizeSettings
        {
            radius = 0.35f, speedRange = new Vector2(1.8f, 2.8f), spinRange = new Vector2(50f, 120f),
        };

        [Header("Forma")]
        [SerializeField, Range(3, 32)] private int minVertices = 8;
        [SerializeField, Range(3, 32)] private int maxVertices = 12;
        [SerializeField, Range(0.1f, 1f), Tooltip("Radio mínimo de cada vértice, como fracción del radio del tamaño.")]
        private float minRadiusFactor = 0.7f;
        [SerializeField, Range(0f, 0.45f), Tooltip("Desvío angular máximo de cada vértice, como fracción de la separación entre vértices.")]
        private float angleJitter = 0.3f;
        [SerializeField] private float lineWidth = 0.06f;
        [SerializeField] private Color neonColor = new Color(1f, 0.2f, 0.85f);

        // Compartido por todas las instancias: sin fricción y rebote total, para que los choques no los frenen.
        private static PhysicsMaterial2D bounceMaterial;

        private readonly List<Vector2> points = new List<Vector2>(32);
        private Rigidbody2D body;
        private PolygonCollider2D polygon;
        private LineRenderer outline;
        private ObjectPool<Asteroid> pool;
        private bool pendingFirstStep;

        public AsteroidSize Size { get; private set; }

        // Radio máximo de un tamaño. Sirve sobre el prefab, sin instancia activa (el spawner lo usa para ubicar).
        public float GetRadius(AsteroidSize size) => Settings(size).radius;

        public float MaxRadius => Mathf.Max(large.radius, medium.radius, small.radius);

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

            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            // Masa proporcional al área del collider: los grandes empujan más que los chicos.
            body.useAutoMass = true;

            if (bounceMaterial == null)
                bounceMaterial = new PhysicsMaterial2D("AsteroidBounce") { friction = 0f, bounciness = 1f };
            polygon.sharedMaterial = bounceMaterial;

            ApplyOutlineStyle();
        }

        // Activación desde el pool: resetea todo el estado de la instancia reutilizada. Llamar después de pool.Get, que
        // ya la ubicó y le dio la rotación inicial. Velocidad y giro salen al azar de los rangos del tamaño; a la velocidad
        // se le suma `inheritedVelocity` (la parte que un fragmento conserva del asteroide del que salió).
        public void Launch(ObjectPool<Asteroid> owner, AsteroidSize size, Vector2 direction, Vector2 inheritedVelocity = default)
        {
            pool = owner;
            Size = size;

            SizeSettings settings = Settings(size);
            BuildShape(settings.radius);

            // La instancia suele venir de un Release en el mismo frame: con interpolación, el transform se dibujaría
            // entre la pose vieja (lejos, del otro lado de la cámara) y la nueva, cruzando la pantalla. Sin interpolar
            // hasta el primer paso de física, la pose previa queda en el punto de spawn.
            body.interpolation = RigidbodyInterpolation2D.None;
            pendingFirstStep = true;
            body.position = transform.position;
            body.rotation = transform.eulerAngles.z;
            body.linearVelocity = direction.normalized * Random.Range(settings.speedRange.x, settings.speedRange.y) + inheritedVelocity;
            float spin = Random.Range(settings.spinRange.x, settings.spinRange.y);
            body.angularVelocity = Random.value < 0.5f ? spin : -spin;
        }

        private void FixedUpdate()
        {
            if (!pendingFirstStep) return;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            pendingFirstStep = false;
        }

        // Ignora impactos antes de su primer paso de física: si dos proyectiles tocan el mismo asteroide en un paso, el
        // segundo callback llega cuando la instancia ya volvió al pool y quizás salió de nuevo como fragmento en el
        // mismo lugar; sin esta guarda, ese fragmento se rompería sin que nada lo haya tocado.
        public void TakeHit()
        {
            if (pendingFirstStep || !gameObject.activeSelf) return;

            var info = new AsteroidDestroyed(Size, body.position, body.linearVelocity);
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

        private SizeSettings Settings(AsteroidSize size)
        {
            switch (size)
            {
                case AsteroidSize.Medium: return medium;
                case AsteroidSize.Small: return small;
                default: return large;
            }
        }

        // Polígono de 8 a 12 vértices (según min/maxVertices) repartidos alrededor del centro, cada uno con un desvío
        // angular y un radio al azar. El desvío nunca llega a media separación, así los vértices no se cruzan de orden y
        // el contorno no se corta a sí mismo. Mismo camino para el collider y el LineRenderer.
        private void BuildShape(float radius)
        {
            int count = Random.Range(minVertices, Mathf.Max(minVertices, maxVertices) + 1);
            float step = 2f * Mathf.PI / count;

            points.Clear();
            for (int i = 0; i < count; i++)
            {
                float angle = (i + Random.Range(-angleJitter, angleJitter)) * step;
                float r = radius * Random.Range(minRadiusFactor, 1f);
                points.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r);
            }

            polygon.pathCount = 1;
            polygon.SetPath(0, points);

            outline.positionCount = count;
            for (int i = 0; i < count; i++) outline.SetPosition(i, points[i]);
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

        // Genera una forma grande de muestra para previsualizar en el Editor. En juego la forma la crea Launch.
        [ContextMenu("Generar forma de muestra")]
        public void PreviewShape()
        {
            CacheComponents();
            ApplyOutlineStyle();
            BuildShape(large.radius);
        }
    }
}
