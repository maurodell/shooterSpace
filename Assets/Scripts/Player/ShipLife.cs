using NavesShooter.Asteroids;
using NavesShooter.Core;
using UnityEngine;

namespace NavesShooter.Player
{
    // Vida de la nave: un solo golpe. Al chocar con algo de `hazardLayers` explota (chispas cian), vibra, se desactiva
    // y termina la partida. Al reiniciar reaparece en `spawnPoint` con `invulnerability` segundos sin poder morir,
    // parpadeando; mientras tanto su collider ignora esas layers y atraviesa los asteroides sin empujarlos.
    // Los eventos del GameManager se escuchan desde Awake y no desde OnEnable: la nave está inactiva cuando llega el reinicio.
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public class ShipLife : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private ExplosionEffect explosion;
        [SerializeField, Tooltip("Renderer que parpadea durante la invulnerabilidad (el contorno del hijo Visual).")]
        private Renderer visual;

        [Header("Muerte")]
        [SerializeField, Tooltip("Layers que matan a la nave al tocarla.")] private LayerMask hazardLayers;
        [SerializeField, Min(0), Tooltip("Duración de la vibración al morir, en milisegundos (solo Android; iOS usa la del sistema).")]
        private int vibrationMilliseconds = 80;
        [SerializeField] private ExplosionEffect.BurstSettings burst = new ExplosionEffect.BurstSettings
        {
            count = 40, spread = 0.3f, speedRange = new Vector2(2f, 8f), sizeRange = new Vector2(0.05f, 0.1f), lifetimeRange = new Vector2(0.4f, 0.9f),
        };
        [SerializeField] private Color explosionColorA = new Color(0.2f, 1f, 1f);
        [SerializeField] private Color explosionColorB = new Color(0.85f, 1f, 1f);

        [Header("Reaparición")]
        [SerializeField] private Vector2 spawnPoint = Vector2.zero;
        [SerializeField, Min(0f), Tooltip("Segundos sin poder morir después de reaparecer.")] private float invulnerability = 2f;
        [SerializeField, Min(0.1f), Tooltip("Parpadeos por segundo durante la invulnerabilidad.")] private float blinkRate = 8f;

        private Rigidbody2D body;
        private Collider2D shipCollider;
        private LayerMask defaultExcludeLayers;
        private float invulnerableTime;
        private bool pendingFirstStep;

        public bool IsInvulnerable => invulnerableTime > 0f;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            shipCollider = GetComponent<Collider2D>();
            defaultExcludeLayers = shipCollider.excludeLayers;
            if (gameManager != null) gameManager.Restarted += Respawn;
        }

        private void OnDestroy()
        {
            if (gameManager != null) gameManager.Restarted -= Respawn;
        }

        private void Update()
        {
            if (invulnerableTime <= 0f) return;

            invulnerableTime -= Time.deltaTime;
            if (invulnerableTime <= 0f)
            {
                SetInvulnerable(false);
                return;
            }

            if (visual != null) visual.enabled = Mathf.Repeat(invulnerableTime * blinkRate, 1f) >= 0.5f;
        }

        private void FixedUpdate()
        {
            if (!pendingFirstStep) return;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            pendingFirstStep = false;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (IsInvulnerable || !gameObject.activeSelf) return;
            if ((hazardLayers.value & (1 << collision.gameObject.layer)) == 0) return;
            Die();
        }

        private void Die()
        {
            if (explosion != null)
                explosion.Explode(body.position, body.linearVelocity, burst, explosionColorA, explosionColorB);
            Haptics.Vibrate(vibrationMilliseconds);

            gameObject.SetActive(false);
            if (gameManager != null) gameManager.EndGame();
        }

        // Reaparición: se ubica antes de activarse, para que el Rigidbody2D arranque en el punto de spawn. Sin
        // interpolar hasta el primer paso de física, así no se dibuja un frame entre el lugar de la muerte y el spawn.
        private void Respawn()
        {
            transform.SetPositionAndRotation(spawnPoint, Quaternion.identity);
            gameObject.SetActive(true);

            body.interpolation = RigidbodyInterpolation2D.None;
            pendingFirstStep = true;
            body.position = spawnPoint;
            body.rotation = 0f;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;

            SetInvulnerable(invulnerability > 0f);
        }

        private void SetInvulnerable(bool value)
        {
            invulnerableTime = value ? invulnerability : 0f;
            shipCollider.excludeLayers = value ? defaultExcludeLayers | hazardLayers : defaultExcludeLayers;
            if (visual != null) visual.enabled = true;
        }
    }
}
