using NavesShooter.Core;
using UnityEngine;

namespace NavesShooter.Background
{
    // Estrellas 3D detrás del gameplay, para la cámara en perspectiva: un ParticleSystem con partículas quietas
    // repartidas en profundidad (`depthRange`, z del mundo). El parallax lo da la perspectiva sola. Cada estrella se
    // reparte dentro del área visible en su propia z, así en pantalla quedan parejas a toda profundidad; cuando sale de
    // esa área + `margin` se corre un múltiplo del área y reaparece del lado opuesto (como StarfieldParallax). Las
    // partículas se leen y escriben con un buffer precreado, y solo se reescriben en los frames en que alguna se recicla.
    // El tamaño se define en unidades del plano del gameplay (z = 0) y se escala con la distancia: una estrella lejana
    // no queda en un punto invisible. Sin simulación propia: loop sin emisión, vida "infinita", velocidad 0.
    // Se configura entero por código en Awake; lo editado en sus módulos desde el Inspector se pisa.
    // Orden de ejecución alto: su LateUpdate corre después del de la cámara (CameraFollow).
    [DefaultExecutionOrder(1000)]
    [RequireComponent(typeof(ParticleSystem))]
    public class ParticleStarfield : MonoBehaviour
    {
        private const float Lifetime = 100000f;

        [SerializeField] private Camera targetCamera;
        [SerializeField, Tooltip("Material unlit con color de vértice (Starfield.mat).")]
        private Material material;
        [SerializeField, Min(1)] private int starCount = 400;
        [SerializeField, Tooltip("Profundidad mínima (x) y máxima (y) de las estrellas, en z del mundo.")]
        private Vector2 depthRange = new Vector2(300f, 1000f);
        [SerializeField, Tooltip("Lado de cada estrella visto en el plano del gameplay (mín, máx), en unidades.")]
        private Vector2 sizeRange = new Vector2(0.03f, 0.07f);
        [SerializeField, Tooltip("Brillo/opacidad de cada estrella, 0..1 (mín, máx).")]
        private Vector2 brightnessRange = new Vector2(0.25f, 0.8f);
        [SerializeField, Range(0f, 1f), Tooltip("Brillo y tamaño de la estrella más lejana respecto de la más cercana.")]
        private float farFactor = 0.6f;
        [SerializeField] private Color baseColor = Color.white;
        [SerializeField, Tooltip("Tono hacia el que se mezcla al azar cada estrella.")]
        private Color tintColor = new Color(0.6f, 0.9f, 1f);
        [SerializeField, Min(0f), Tooltip("Margen alrededor del área visible antes de reciclar, en fracción del área.")]
        private float margin = 0.1f;
        [SerializeField] private int sortingOrder = -120;
        [SerializeField] private int seed = 4321;

        private ParticleSystem system;
        private ParticleSystem.Particle[] particles;
        // Medio ancho/alto visible por unidad de distancia a la cámara (con margen) con que se repartieron las estrellas.
        private Vector2 spreadPerDistance;

        private void Awake()
        {
            Configure();
        }

        private void Start()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null || targetCamera.orthographic)
            {
                Debug.LogError("ParticleStarfield: necesita una cámara en perspectiva.", this);
                enabled = false;
                return;
            }
            system.Play();
            Fill();
        }

        private void LateUpdate()
        {
            // Más campo visible (resolución, fov): redistribuir. Con el zoom hacia adentro del salto alcanzan las mismas.
            Vector2 needed = SpreadPerDistance();
            if (needed.x > spreadPerDistance.x || needed.y > spreadPerDistance.y)
            {
                Fill();
                return;
            }

            Vector3 cam = targetCamera.transform.position;
            int count = system.GetParticles(particles);
            bool dirty = false;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = particles[i].position;
                float distance = p.z - cam.z;
                Vector2 half = spreadPerDistance * distance;
                bool moved = Wrap(ref p.x, p.x - cam.x, half.x);
                moved |= Wrap(ref p.y, p.y - cam.y, half.y);
                if (!moved) continue;
                particles[i].position = p;
                dirty = true;
            }
            if (dirty) system.SetParticles(particles, count);
        }

        // Si la estrella quedó fuera de [-half, half] respecto a la cámara, la corre un múltiplo del área.
        private static bool Wrap(ref float value, float rel, float half)
        {
            float size = half * 2f;
            if (rel > half)
            {
                value -= size * Mathf.Ceil((rel - half) / size);
                return true;
            }
            if (rel < -half)
            {
                value += size * Mathf.Ceil((-half - rel) / size);
                return true;
            }
            return false;
        }

        private Vector2 SpreadPerDistance()
        {
            float tan = Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * (1f + margin);
            return new Vector2(tan * targetCamera.aspect, tan);
        }

        private void Fill()
        {
            spreadPerDistance = SpreadPerDistance();
            Vector3 cam = targetCamera.transform.position;
            // Distancia de la cámara al plano del gameplay: ahí `sizeRange` es el tamaño que se ve.
            float gameplayDistance = Mathf.Max(0.01f, -cam.z);
            var rnd = new System.Random(seed);

            for (int i = 0; i < particles.Length; i++)
            {
                float depth = Mathf.Lerp(depthRange.x, depthRange.y, Next(rnd));
                float distance = depth - cam.z;
                Vector2 half = spreadPerDistance * distance;
                float far = Mathf.InverseLerp(depthRange.x, depthRange.y, depth);
                float fade = Mathf.Lerp(1f, farFactor, far);

                // Al cuadrado: más estrellas chicas que grandes.
                float t = Next(rnd);
                float size = Mathf.Lerp(sizeRange.x, sizeRange.y, t * t) * fade * distance / gameplayDistance;

                Color color = Color.Lerp(baseColor, tintColor, Next(rnd));
                color.a = Mathf.Lerp(brightnessRange.x, brightnessRange.y, Next(rnd)) * fade;

                particles[i] = new ParticleSystem.Particle
                {
                    position = new Vector3(
                        cam.x + Mathf.Lerp(-half.x, half.x, Next(rnd)),
                        cam.y + Mathf.Lerp(-half.y, half.y, Next(rnd)),
                        depth),
                    velocity = Vector3.zero,
                    startSize = size,
                    startColor = color,
                    startLifetime = Lifetime,
                    remainingLifetime = Lifetime,
                };
            }
            system.SetParticles(particles, particles.Length);
        }

        [ContextMenu("Configurar sistema")]
        private void Configure()
        {
            system = GetComponent<ParticleSystem>();
            var main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            main.maxParticles = starCount;
            main.startLifetime = Lifetime;
            main.startSpeed = 0f;

            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;

            var particleRenderer = GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.alignment = ParticleSystemRenderSpace.View;
            particleRenderer.sortingOrder = sortingOrder;
            // Sin tope de tamaño en pantalla: las lejanas son grandes en mundo a propósito.
            particleRenderer.maxParticleSize = 1f;
            particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;
            if (material != null) particleRenderer.sharedMaterial = material;

            if (particles == null || particles.Length != starCount) particles = new ParticleSystem.Particle[starCount];
        }

        private void Reset() => Configure();

        private static float Next(System.Random rnd) => (float)rnd.NextDouble();
    }
}
