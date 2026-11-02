using NavesShooter.Core;
using NavesShooter.Input;
using UnityEngine;

namespace NavesShooter.Weapons
{
    // Arma de la nave: mientras se mantiene el disparo (o con `autoFire`) lanza proyectiles desde la punta, en la
    // dirección en que apunta la nave y sumando su velocidad. La cadencia se respeta también entre pulsaciones: el
    // primer disparo sale en el mismo frame en que se presiona, salvo que el anterior haya sido hace menos de un intervalo.
    // Con `ammo` asignado cada proyectil gasta munición; sin munición no dispara y suena un clic seco (como máximo uno
    // cada `dryFireInterval`). Sin `ammo`, munición infinita.
    [RequireComponent(typeof(Rigidbody2D))]
    public class ShipWeapon : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField, Tooltip("Al reiniciar la partida, los proyectiles en vuelo vuelven al pool.")]
        private GameManager gameManager;
        [SerializeField, Tooltip("Cadencia, velocidad y vida del proyectil (sección Weapon).")] private GameConfig config;
        [SerializeField, Tooltip("Munición del jugador. Vacío = munición infinita.")] private Ammo ammo;

        [Header("Disparo")]
        [SerializeField, Tooltip("Punto de salida en coordenadas locales de la nave (la punta).")]
        private Vector2 muzzleOffset = new Vector2(0f, 0.45f);
        [SerializeField, Tooltip("Dispara continuamente sin necesidad de mantener el botón.")] private bool autoFire;

        [Header("Sin munición")]
        [SerializeField, Tooltip("Reproduce el clic. Si falta, se agrega uno en Awake.")] private AudioSource audioSource;
        [SerializeField, Tooltip("Clic seco al disparar sin munición. Vacío = se genera uno corto por código.")]
        private AudioClip dryFireClip;
        [SerializeField, Range(0f, 1f)] private float dryFireVolume = 0.6f;
        [SerializeField, Min(0f), Tooltip("Segundos mínimos entre clics.")] private float dryFireInterval = 0.3f;

        [Header("Pool")]
        [SerializeField, Min(0), Tooltip("Proyectiles precreados. El pool crece si se agotan.")] private int initialPoolSize = 16;

        private Rigidbody2D body;
        private ObjectPool<Projectile> pool;
        private float cooldown;
        private float nextDryFireTime;

        public bool AutoFire
        {
            get => autoFire;
            set => autoFire = value;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            if (projectilePrefab == null || config == null)
            {
                Debug.LogError($"{nameof(ShipWeapon)}: faltan referencias (prefab del proyectil o GameConfig).", this);
                enabled = false;
                return;
            }

            // Contenedor en la raíz: si colgara de la nave, los proyectiles se moverían con ella.
            pool = new ObjectPool<Projectile>(projectilePrefab, initialPoolSize, new GameObject("PlayerProjectiles").transform);
            if (dryFireClip == null) dryFireClip = CreateDryFireClip();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }
            // Desde Awake y no desde OnEnable: la nave está inactiva cuando llega el reinicio.
            if (gameManager != null) gameManager.Restarted += OnRestarted;
        }

        private void OnDestroy()
        {
            if (gameManager != null) gameManager.Restarted -= OnRestarted;
        }

        private void OnRestarted()
        {
            pool.ReleaseAll();
            cooldown = 0f;
            nextDryFireTime = 0f;
            if (ammo != null) ammo.Restore();
        }

        private void Update()
        {
            // En pausa o con el nivel completado (timeScale 0) el cooldown no baja, pero el primer disparo saldría igual.
            if (gameManager != null && gameManager.State != GameState.Playing) return;
            if (cooldown > 0f) cooldown -= Time.deltaTime;

            bool firing = autoFire || (input != null && input.IsFiring);
            if (!firing || cooldown > 0f) return;

            if (ammo != null && !ammo.TryConsume(ammo.CostPerShot))
            {
                DryFire();
                return;
            }

            Fire();
            // Sumar en vez de asignar conserva la cadencia exacta aunque el intervalo no sea múltiplo del frame.
            cooldown += 1f / config.Weapon.fireRate;
        }

        private void Fire()
        {
            // Pose interpolada del transform (la que se ve), así el proyectil sale de la punta dibujada.
            Vector2 forward = transform.up;
            Vector2 origin = transform.TransformPoint(muzzleOffset);
            GameConfig.WeaponSettings weapon = config.Weapon;
            Vector2 velocity = forward * weapon.projectileSpeed + body.linearVelocity;

            float angle = Mathf.Atan2(-velocity.x, velocity.y) * Mathf.Rad2Deg;
            Projectile projectile = pool.Get(origin, Quaternion.Euler(0f, 0f, angle));
            projectile.Launch(pool, velocity, weapon.projectileLifetime);
        }

        private void DryFire()
        {
            // Time.time: en pausa no corre, pero en pausa tampoco se llega acá.
            if (Time.time < nextDryFireTime) return;
            nextDryFireTime = Time.time + dryFireInterval;
            if (dryFireClip != null) audioSource.PlayOneShot(dryFireClip, dryFireVolume);
        }

        // Clic placeholder: ~25 ms de ruido con caída exponencial sobre un golpe grave corto. Se genera una sola vez.
        private static AudioClip CreateDryFireClip()
        {
            const int sampleRate = 44100;
            int length = Mathf.CeilToInt(sampleRate * 0.025f);
            float[] samples = new float[length];
            var random = new System.Random(7);
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / sampleRate;
                float noise = (float)(random.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 400f);
                float thump = Mathf.Sin(2f * Mathf.PI * 180f * t) * Mathf.Exp(-t * 160f);
                samples[i] = Mathf.Clamp(noise * 0.6f + thump * 0.5f, -1f, 1f);
            }
            AudioClip clip = AudioClip.Create("DryFireClick", length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.TransformPoint(muzzleOffset), 0.05f);
        }
    }
}
