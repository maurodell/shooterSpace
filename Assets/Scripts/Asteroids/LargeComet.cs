using System.Collections.Generic;
using UnityEngine;

namespace NavesShooter.Asteroids
{
    // Datos de un cometa grande del recorrido. Existen todos desde el arranque del nivel; solo los cercanos a la cámara
    // tienen instancia (LargeCometField). La forma sale siempre igual de `shapeSeed`.
    public struct LargeCometData
    {
        public Vector2 Position;
        public float Radius;
        public float BaseAngle;   // rotación en t = 0 (grados)
        public float Spin;        // giro visual (grados/s, con signo)
        public int ShapeSeed;
    }

    // Cometa grande: obstáculo fijo e indestructible. Rigidbody2D kinematic que solo gira despacio, para no verse muerto:
    // el collider gira con el contorno, así lo que se ve es lo que choca. La nave muere al tocarlo; los cometas chicos
    // rebotan (kinematic = masa infinita); los proyectiles se descartan con un destello (Projectile.Blocked). Sin
    // interpolación: está quieto y el giro es tan lento que el paso de física no se nota.
    // El hijo `Inner` repite el contorno más chico y tenue, para que se lea más pesado que los chicos.
    [RequireComponent(typeof(Rigidbody2D), typeof(PolygonCollider2D), typeof(LineRenderer))]
    public class LargeComet : MonoBehaviour
    {
        [Header("Forma")]
        [SerializeField, Range(3, 32)] private int minVertices = 10;
        [SerializeField, Range(3, 32)] private int maxVertices = 15;
        [SerializeField, Range(0.1f, 1f), Tooltip("Radio mínimo de cada vértice, como fracción del radio del cometa.")]
        private float minRadiusFactor = 0.78f;
        [SerializeField, Range(0f, 0.45f), Tooltip("Desvío angular máximo de cada vértice, como fracción de la separación entre vértices.")]
        private float angleJitter = 0.3f;

        [Header("Render")]
        [SerializeField] private Material lineMaterial;
        [SerializeField] private Color neonColor = new Color(0.55f, 0.45f, 1f);
        [SerializeField] private float lineWidth = 0.1f;
        [SerializeField, Range(0.1f, 0.95f), Tooltip("Escala del contorno interior.")] private float innerScale = 0.6f;
        [SerializeField, Range(0f, 1f), Tooltip("Brillo del contorno interior respecto del exterior.")] private float innerBrightness = 0.4f;
        [SerializeField] private float innerWidth = 0.05f;

        private const string InnerName = "Inner";

        private readonly List<Vector2> points = new List<Vector2>(32);
        private readonly AsteroidShape.ShapeRandom shapeRandom = new AsteroidShape.ShapeRandom(0);
        private Rigidbody2D body;
        private PolygonCollider2D polygon;
        private LineRenderer outline;
        private LineRenderer inner;

        private void Reset()
        {
            var rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.None;
        }

        private void OnValidate()
        {
            maxVertices = Mathf.Max(maxVertices, minVertices);
        }

        private void Awake()
        {
            CacheComponents();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.interpolation = RigidbodyInterpolation2D.None;
            polygon.sharedMaterial = Asteroid.BounceMaterial;
            ApplyOutlineStyle();
        }

        // Activación desde el pool, después de pool.Get (que ya lo ubicó). La rotación sigue la del reloj de juego, así
        // un cometa que se desactiva y vuelve a aparecer no salta de ángulo.
        public void Activate(in LargeCometData data)
        {
            BuildShape(data.Radius, data.ShapeSeed);
            float rotation = data.BaseAngle + data.Spin * Time.time;
            transform.SetPositionAndRotation(data.Position, Quaternion.Euler(0f, 0f, rotation));
            body.position = data.Position;
            body.rotation = rotation;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = data.Spin;
        }

        private void BuildShape(float radius, int seed)
        {
            shapeRandom.Reset(seed);
            AsteroidShape.Build(points, shapeRandom, radius, minVertices, maxVertices, minRadiusFactor, angleJitter);
            AsteroidShape.Apply(points, polygon, outline);

            inner.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++) inner.SetPosition(i, points[i] * innerScale);
        }

        private void CacheComponents()
        {
            body = GetComponent<Rigidbody2D>();
            polygon = GetComponent<PolygonCollider2D>();
            outline = GetComponent<LineRenderer>();

            Transform child = transform.Find(InnerName);
            if (child == null)
            {
                child = new GameObject(InnerName, typeof(LineRenderer)).transform;
                child.SetParent(transform, false);
                child.gameObject.layer = gameObject.layer;
            }
            inner = child.GetComponent<LineRenderer>();
        }

        private void ApplyOutlineStyle()
        {
            Style(outline, lineWidth, neonColor);
            Color dim = neonColor * innerBrightness;
            dim.a = 1f;
            Style(inner, innerWidth, dim);
        }

        private void Style(LineRenderer line, float width, Color color)
        {
            line.useWorldSpace = false;
            line.loop = true;
            line.widthMultiplier = width;
            line.startColor = color;
            line.endColor = color;
            line.numCornerVertices = 2;
            line.alignment = LineAlignment.View;
            if (lineMaterial != null) line.sharedMaterial = lineMaterial;
        }

        // Forma de muestra para previsualizar en el Editor. En juego la arma Activate.
        [ContextMenu("Generar forma de muestra")]
        public void PreviewShape()
        {
            CacheComponents();
            ApplyOutlineStyle();
            BuildShape(2f, new System.Random().Next());
        }
    }
}
