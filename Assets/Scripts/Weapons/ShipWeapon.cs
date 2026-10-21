using NavesShooter.Core;
using NavesShooter.Input;
using UnityEngine;

namespace NavesShooter.Weapons
{
    // Arma de la nave: mientras se mantiene el disparo (o con `autoFire`) lanza proyectiles desde la punta, en la
    // dirección en que apunta la nave y sumando su velocidad. La cadencia se respeta también entre pulsaciones: el
    // primer disparo sale en el mismo frame en que se presiona, salvo que el anterior haya sido hace menos de un intervalo.
    [RequireComponent(typeof(Rigidbody2D))]
    public class ShipWeapon : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField, Tooltip("Al reiniciar la partida, los proyectiles en vuelo vuelven al pool.")]
        private GameManager gameManager;

        [Header("Disparo")]
        [SerializeField, Min(0.1f), Tooltip("Disparos por segundo.")] private float fireRate = 8f;
        [SerializeField, Tooltip("Velocidad del proyectil relativa a la nave (unidades/s).")] private float projectileSpeed = 14f;
        [SerializeField, Min(0.05f), Tooltip("Segundos hasta que el proyectil vuelve al pool si no choca.")] private float projectileLifetime = 1.2f;
        [SerializeField, Tooltip("Punto de salida en coordenadas locales de la nave (la punta).")]
        private Vector2 muzzleOffset = new Vector2(0f, 0.45f);
        [SerializeField, Tooltip("Dispara continuamente sin necesidad de mantener el botón.")] private bool autoFire;

        [Header("Pool")]
        [SerializeField, Min(0), Tooltip("Proyectiles precreados. El pool crece si se agotan.")] private int initialPoolSize = 16;

        private Rigidbody2D body;
        private ObjectPool<Projectile> pool;
        private float cooldown;

        public bool AutoFire
        {
            get => autoFire;
            set => autoFire = value;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            if (projectilePrefab == null)
            {
                Debug.LogError($"{nameof(ShipWeapon)}: falta asignar el prefab del proyectil.", this);
                enabled = false;
                return;
            }

            // Contenedor en la raíz: si colgara de la nave, los proyectiles se moverían con ella.
            pool = new ObjectPool<Projectile>(projectilePrefab, initialPoolSize, new GameObject("PlayerProjectiles").transform);
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
        }

        private void Update()
        {
            if (cooldown > 0f) cooldown -= Time.deltaTime;

            bool firing = autoFire || (input != null && input.IsFiring);
            if (!firing || cooldown > 0f) return;

            Fire();
            // Sumar en vez de asignar conserva la cadencia exacta aunque el intervalo no sea múltiplo del frame.
            cooldown += 1f / fireRate;
        }

        private void Fire()
        {
            // Pose interpolada del transform (la que se ve), así el proyectil sale de la punta dibujada.
            Vector2 forward = transform.up;
            Vector2 origin = transform.TransformPoint(muzzleOffset);
            Vector2 velocity = forward * projectileSpeed + body.linearVelocity;

            float angle = Mathf.Atan2(-velocity.x, velocity.y) * Mathf.Rad2Deg;
            Projectile projectile = pool.Get(origin, Quaternion.Euler(0f, 0f, angle));
            projectile.Launch(pool, velocity, projectileLifetime);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.TransformPoint(muzzleOffset), 0.05f);
        }
    }
}
