using NavesShooter.Asteroids;
using NavesShooter.Core;
using UnityEngine;

namespace NavesShooter.Player
{
    // Vida de la nave sobre Health (3 puntos). Chocar con algo de `hazardLayers` quita un punto: si lo tocado es
    // dañable (cometa chico, enemigo) también le pega; la nave sale empujada en dirección opuesta (contra un cometa
    // grande, hacia afuera). Cada daño hace vibrar y parpadear la nave durante la invulnerabilidad de Health; los
    // disparos enemigos llegan directo a Health (IDamageable). Sin vida explota (chispas cian), vibra más largo, se
    // desactiva y termina la partida. Al reiniciar reaparece en `spawnPoint` con vida completa e `invulnerability`
    // segundos sin recibir daño, parpadeando; mientras tanto su collider ignora `hazardLayers` y atraviesa los
    // asteroides sin empujarlos (en la invulnerabilidad tras un golpe, en cambio, sigue chocando).
    // Los eventos del GameManager se escuchan desde Awake y no desde OnEnable: la nave está inactiva cuando llega el reinicio.
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(Health))]
    public class ShipLife : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private ExplosionEffect explosion;
        [SerializeField, Tooltip("Renderer que parpadea durante la invulnerabilidad (el contorno del hijo Visual).")]
        private Renderer visual;

        [Header("Daño")]
        [SerializeField, Tooltip("Layers que quitan vida al tocarlas.")] private LayerMask hazardLayers;
        [SerializeField, Min(0f), Tooltip("Velocidad con que el choque empuja la nave lejos de lo que tocó (unidades/s).")]
        private float knockbackSpeed = 6f;
        [SerializeField, Min(0), Tooltip("Duración de la vibración al recibir daño, en milisegundos (solo Android).")]
        private int hitVibrationMilliseconds = 35;

        [Header("Muerte")]
        [SerializeField, Min(0), Tooltip("Duración de la vibración al morir, en milisegundos (solo Android; en iOS es un golpe háptico de duración fija).")]
        private int vibrationMilliseconds = 80;
        [SerializeField] private ExplosionEffect.BurstSettings burst = new ExplosionEffect.BurstSettings
        {
            count = 40, spread = 0.3f, speedRange = new Vector2(2f, 8f), sizeRange = new Vector2(0.05f, 0.1f), lifetimeRange = new Vector2(0.4f, 0.9f),
        };
        [SerializeField] private Color explosionColorA = new Color(0.2f, 1f, 1f);
        [SerializeField] private Color explosionColorB = new Color(0.85f, 1f, 1f);

        [Header("Reaparición")]
        [SerializeField, Tooltip("Lo fija el LevelManager al empezar (punto A).")] private Vector2 spawnPoint = Vector2.zero;
        [SerializeField, Tooltip("Rotación al reaparecer, en grados (convención de Rigidbody2D.rotation).")] private float spawnRotation;
        [SerializeField, Min(0f), Tooltip("Segundos sin recibir daño después de reaparecer.")] private float invulnerability = 2f;
        [SerializeField, Min(0.1f), Tooltip("Parpadeos por segundo durante la invulnerabilidad.")] private float blinkRate = 8f;

        private Rigidbody2D body;
        private Collider2D shipCollider;
        private Health health;
        private ShipController controller;
        private LayerMask defaultExcludeLayers;
        private float ghostTime;
        private bool pendingFirstStep;

        public Health Health => health;
        public bool IsInvulnerable => health.IsInvulnerable;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            shipCollider = GetComponent<Collider2D>();
            health = GetComponent<Health>();
            controller = GetComponent<ShipController>();
            defaultExcludeLayers = shipCollider.excludeLayers;
            health.Damaged += OnDamaged;
            health.Died += OnDied;
            if (gameManager != null) gameManager.Restarted += Respawn;
        }

        private void OnDestroy()
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
            if (gameManager != null) gameManager.Restarted -= Respawn;
        }

        private void Update()
        {
            if (ghostTime > 0f)
            {
                ghostTime -= Time.deltaTime;
                if (ghostTime <= 0f) SetGhost(false);
            }

            if (visual == null) return;
            bool shown = !health.IsInvulnerable || Mathf.Repeat(Time.time * blinkRate, 1f) >= 0.5f;
            if (visual.enabled != shown) visual.enabled = shown;
        }

        private void FixedUpdate()
        {
            if (!pendingFirstStep) return;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            pendingFirstStep = false;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!gameObject.activeSelf || health.IsDead || health.IsInvulnerable) return;
            if ((hazardLayers.value & (1 << collision.gameObject.layer)) == 0) return;

            // Desde el centro de lo tocado: contra un cometa grande, hacia afuera.
            Vector2 away = body.position - (Vector2)collision.collider.bounds.center;
            if (away.sqrMagnitude < 0.0001f) away = -(Vector2)transform.up;

            if (collision.collider.TryGetComponent(out IDamageable other)) other.TakeHit();
            if (!health.TakeDamage(1) || health.IsDead) return;
            if (controller != null) controller.Knockback(away.normalized * knockbackSpeed);
        }

        private void OnDamaged(Health _)
        {
            if (!health.IsDead) Haptics.Vibrate(hitVibrationMilliseconds);
        }

        private void OnDied(Health _) => Die();

        private void Die()
        {
            if (explosion != null)
                explosion.Explode(body.position, body.linearVelocity, burst, explosionColorA, explosionColorB);
            Haptics.Vibrate(vibrationMilliseconds);

            gameObject.SetActive(false);
            if (gameManager != null) gameManager.EndGame();
        }

        // Fija el punto de reaparición y ubica la nave ahí. Pensado para el arranque del nivel, antes del primer paso
        // de física (puede correr antes que el Awake de este componente).
        public void PlaceAtSpawn(Vector2 position, float rotation)
        {
            spawnPoint = position;
            spawnRotation = rotation;
            if (body == null) body = GetComponent<Rigidbody2D>();
            transform.SetPositionAndRotation(spawnPoint, Quaternion.Euler(0f, 0f, spawnRotation));
            body.position = spawnPoint;
            body.rotation = spawnRotation;
        }

        // Reaparición: se ubica antes de activarse, para que el Rigidbody2D arranque en el punto de spawn. Sin
        // interpolar hasta el primer paso de física, así no se dibuja un frame entre el lugar de la muerte y el spawn.
        // Si sigue activa (reinicio tras completar el nivel) se desactiva antes, para que los OnEnable reseteen su estado.
        private void Respawn()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
            transform.SetPositionAndRotation(spawnPoint, Quaternion.Euler(0f, 0f, spawnRotation));
            gameObject.SetActive(true);

            body.interpolation = RigidbodyInterpolation2D.None;
            pendingFirstStep = true;
            body.position = spawnPoint;
            body.rotation = spawnRotation;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;

            health.Restore();
            health.SetInvulnerable(invulnerability);
            ghostTime = invulnerability;
            SetGhost(invulnerability > 0f);
        }

        // Atraviesa lo peligroso sin empujarlo. Al terminar, si quedó superpuesta a algo, el contacto nuevo la daña.
        private void SetGhost(bool value)
        {
            shipCollider.excludeLayers = value ? defaultExcludeLayers | hazardLayers : defaultExcludeLayers;
        }
    }
}
