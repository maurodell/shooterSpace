using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace NavesShooter.Asteroids
{
    // Explosión de los asteroides destruidos: chispas neón que salen del punto de ruptura, se frenan y se desvanecen
    // rápido. Cada chispa es un quad estirado según su velocidad (stretched billboard): a toda velocidad se ve como una
    // línea y, frenada, como un cuadradito. Un solo ParticleSystem para todas las explosiones: cada una emite con `Emit`
    // sobre el mismo sistema, así no hay instancias que crear ni pool que mantener, y todas se dibujan juntas.
    // El sistema se configura entero por código en Awake; lo que se edite en sus módulos desde el Inspector se pisa.
    [RequireComponent(typeof(ParticleSystem))]
    public class ExplosionEffect : MonoBehaviour
    {
        [Serializable]
        public struct BurstSettings
        {
            [Min(0), Tooltip("Chispas por explosión.")] public int count;
            [Tooltip("Radio del área de la que salen las chispas (unidades).")] public float spread;
            [Tooltip("Velocidad inicial mínima (x) y máxima (y), en unidades/s.")] public Vector2 speedRange;
            [Tooltip("Ancho mínimo (x) y máximo (y) de cada chispa (unidades).")] public Vector2 sizeRange;
            [Tooltip("Duración mínima (x) y máxima (y), en segundos.")] public Vector2 lifetimeRange;
        }

        [Header("Por tamaño de asteroide")]
        [SerializeField] private BurstSettings large = new BurstSettings
        {
            count = 30, spread = 0.7f, speedRange = new Vector2(2f, 7f), sizeRange = new Vector2(0.05f, 0.09f), lifetimeRange = new Vector2(0.35f, 0.7f),
        };
        [SerializeField] private BurstSettings medium = new BurstSettings
        {
            count = 18, spread = 0.4f, speedRange = new Vector2(1.8f, 5.5f), sizeRange = new Vector2(0.04f, 0.07f), lifetimeRange = new Vector2(0.3f, 0.55f),
        };
        [SerializeField] private BurstSettings small = new BurstSettings
        {
            count = 10, spread = 0.2f, speedRange = new Vector2(1.5f, 4.5f), sizeRange = new Vector2(0.03f, 0.05f), lifetimeRange = new Vector2(0.25f, 0.45f),
        };

        [Header("Movimiento")]
        [SerializeField, Range(0f, 1f), Tooltip("Fracción de la velocidad del asteroide que heredan las chispas.")]
        private float inheritVelocity = 0.4f;
        [SerializeField, Min(0f), Tooltip("Frenado de las chispas (1/s).")]
        private float drag = 4f;

        [Header("Aspecto")]
        [SerializeField, Tooltip("Material de las chispas (Neon.mat). Vacío: deja el que tenga el renderer.")]
        private Material material;
        [SerializeField, Tooltip("Cada chispa toma un color al azar entre estos dos.")]
        private Color colorA = new Color(1f, 0.2f, 0.85f);
        [SerializeField] private Color colorB = new Color(1f, 0.85f, 1f);
        [SerializeField, Min(0f), Tooltip("Largo de la chispa quieta, en múltiplos de su ancho.")]
        private float lengthScale = 1f;
        [SerializeField, Min(0f), Tooltip("Largo extra por unidad/s de velocidad.")]
        private float velocityScale = 0.05f;
        [SerializeField] private int sortingOrder = 10;
        [SerializeField, Min(1)] private int maxParticles = 600;

        private ParticleSystem particles;

        // Al agregar el componente deja el sistema configurado también en la escena, para que no emita por su cuenta
        // (los valores por defecto de un ParticleSystem nuevo emiten en loop desde que arranca).
        private void Reset() => Configure();

        private void Awake()
        {
            Configure();
            // Sistema en loop sin emisión propia: queda corriendo y solo simula lo que llega por Emit.
            particles.Play();
        }

        private void OnEnable() => Asteroid.Destroyed += OnAsteroidDestroyed;

        private void OnDisable() => Asteroid.Destroyed -= OnAsteroidDestroyed;

        private void OnAsteroidDestroyed(AsteroidDestroyed destroyed)
        {
            Explode(destroyed.Position, destroyed.Velocity, destroyed.Size);
        }

        public void Explode(Vector2 position, Vector2 velocity, AsteroidSize size)
        {
            BurstSettings burst = Settings(size);
            Vector2 inherited = velocity * inheritVelocity;
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };

            for (int i = 0; i < burst.count; i++)
            {
                float angle = Random.Range(0f, 2f * Mathf.PI);
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                emit.position = position + direction * Random.Range(0f, burst.spread);
                emit.velocity = direction * Random.Range(burst.speedRange.x, burst.speedRange.y) + inherited;
                emit.startSize = Random.Range(burst.sizeRange.x, burst.sizeRange.y);
                emit.startLifetime = Random.Range(burst.lifetimeRange.x, burst.lifetimeRange.y);
                emit.startColor = Color.Lerp(colorA, colorB, Random.value);
                particles.Emit(emit, 1);
            }
        }

        private BurstSettings Settings(AsteroidSize size)
        {
            switch (size)
            {
                case AsteroidSize.Medium: return medium;
                case AsteroidSize.Small: return small;
                default: return large;
            }
        }

        [ContextMenu("Configurar sistema")]
        private void Configure()
        {
            particles = GetComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            main.maxParticles = maxParticles;

            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.enabled = false;

            // Alfa de 1 a 0 a lo largo de la vida: se desvanecen mientras se frenan.
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(fade);

            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.4f));

            // Solo frenado: el límite es tan alto que nunca recorta.
            var limitVelocity = particles.limitVelocityOverLifetime;
            limitVelocity.enabled = true;
            limitVelocity.limit = 1000f;
            limitVelocity.drag = drag;
            limitVelocity.multiplyDragByParticleSize = false;
            limitVelocity.multiplyDragByParticleVelocity = false;

            var particleRenderer = GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            particleRenderer.lengthScale = lengthScale;
            particleRenderer.velocityScale = velocityScale;
            particleRenderer.cameraVelocityScale = 0f;
            particleRenderer.sortingOrder = sortingOrder;
            if (material != null) particleRenderer.sharedMaterial = material;
        }
    }
}
