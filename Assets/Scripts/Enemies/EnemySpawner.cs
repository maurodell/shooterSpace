using System.Collections.Generic;
using NavesShooter.Core;
using NavesShooter.Weapons;
using UnityEngine;

namespace NavesShooter.Enemies
{
    // Hace aparecer naves enemigas de a una, cada `spawnInterval` segundos y hasta `maxEnemies` simultáneas. Aparecen
    // en el mismo anillo fuera de la vista que los cometas chicos, preferentemente por delante del jugador en la
    // dirección de la nave nodriza (`forwardChance`, dentro de ±`forwardSpread`), mirando hacia él. Nunca encima de un
    // cometa (los grandes cercanos a la cámara siempre tienen collider), de la nave nodriza ni de otro enemigo.
    // Con el máximo alcanzado el reloj no corre: después de destruir uno, el siguiente tarda un intervalo completo.
    // Las que se alejan más de `despawnDistance` vuelven al pool sin explotar. Es dueño de los pools de las naves y
    // de sus proyectiles (compartido por todas). Solo actúa en Playing.
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private EnemyShip enemyPrefab;
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField, Tooltip("Centro del anillo de spawn y del despawn. Si está vacío usa Camera.main.")]
        private Camera targetCamera;
        [SerializeField, Tooltip("A quién persiguen.")] private Transform player;
        [SerializeField, Tooltip("Da la dirección hacia la nave nodriza (B).")] private LevelManager levelManager;
        [SerializeField] private GameManager gameManager;

        [Header("Cantidad")]
        [SerializeField, Min(0), Tooltip("Enemigos activos a la vez como máximo.")] private int maxEnemies = 3;
        [SerializeField, Min(0.1f), Tooltip("Segundos entre apariciones.")] private float spawnInterval = 6f;
        [SerializeField, Min(0f), Tooltip("Segundos hasta el primer enemigo, al empezar y al reiniciar.")] private float initialDelay = 4f;

        [Header("Ubicación (desde la cámara)")]
        [SerializeField, Min(0f), Tooltip("Radio interior del anillo. Si queda dentro de la vista se agranda lo justo.")]
        private float spawnRadius = 12.5f;
        [SerializeField, Min(0f)] private float spawnRingWidth = 3f;
        [SerializeField, Min(0f), Tooltip("Más lejos que esto el enemigo vuelve al pool. Nunca menor que el borde exterior del anillo.")]
        private float despawnDistance = 30f;
        [SerializeField, Range(0f, 1f), Tooltip("Probabilidad de aparecer por delante del jugador (hacia B).")]
        private float forwardChance = 0.8f;
        [SerializeField, Range(0f, 180f), Tooltip("Desvío máximo respecto de la dirección hacia B, en grados a cada lado.")]
        private float forwardSpread = 60f;
        [SerializeField, Min(0f), Tooltip("Radio libre que necesita el punto de aparición.")] private float spawnClearance = 1.5f;
        [SerializeField, Tooltip("Layers sobre las que no se puede aparecer (cometas, nave nodriza, otros enemigos).")]
        private LayerMask blockingLayers;

        [Header("Pool")]
        [SerializeField, Min(0), Tooltip("Proyectiles enemigos precreados. El pool crece si se agotan.")]
        private int projectilePoolSize = 24;

        private const float ViewMargin = 1f;     // entre la nave y el borde visible al aparecer
        private const float DespawnMargin = 1f;
        private const int SpawnAttempts = 8;

        private readonly List<EnemyShip> active = new List<EnemyShip>();
        private ObjectPool<EnemyShip> pool;
        private ObjectPool<Projectile> projectilePool;
        private float timer;

        public int ActiveCount => active.Count;

        public int MaxEnemies
        {
            get => maxEnemies;
            set => maxEnemies = Mathf.Max(value, 0);
        }

        public float SpawnInterval
        {
            get => spawnInterval;
            set => spawnInterval = Mathf.Max(value, 0.1f);
        }

        private void Awake()
        {
            if (enemyPrefab == null || projectilePrefab == null || player == null)
            {
                Debug.LogError($"{nameof(EnemySpawner)}: faltan referencias (prefabs o jugador).", this);
                enabled = false;
                return;
            }

            if (targetCamera == null) targetCamera = Camera.main;
            pool = new ObjectPool<EnemyShip>(enemyPrefab, maxEnemies, new GameObject("Enemies").transform);
            projectilePool = new ObjectPool<Projectile>(projectilePrefab, projectilePoolSize, new GameObject("EnemyProjectiles").transform);
            timer = initialDelay;
        }

        private void OnEnable()
        {
            if (gameManager != null) gameManager.Restarted += DespawnAll;
        }

        private void OnDisable()
        {
            if (gameManager != null) gameManager.Restarted -= DespawnAll;
        }

        // Al reiniciar: todo al pool sin explotar ni sumar puntos, y vuelve a esperar `initialDelay`.
        private void DespawnAll()
        {
            pool.ReleaseAll();
            projectilePool.ReleaseAll();
            active.Clear();
            timer = initialDelay;
        }

        private void Update()
        {
            if (gameManager != null && gameManager.State != GameState.Playing) return;

            Vector2 center = targetCamera.transform.position;
            float despawn = EffectiveDespawnDistance(targetCamera);
            float despawnSqr = despawn * despawn;
            foreach (EnemyShip enemy in active)
            {
                if (((Vector2)enemy.transform.position - center).sqrMagnitude > despawnSqr)
                    enemy.Despawn();
            }
            RemoveInactive();

            if (active.Count >= maxEnemies)
            {
                timer = spawnInterval;
                return;
            }

            timer -= Time.deltaTime;
            if (timer > 0f) return;
            if (TrySpawn(center)) timer = spawnInterval;
        }

        // Saca de la lista las que volvieron al pool (destruidas o por distancia). El orden no importa: swap-remove.
        private void RemoveInactive()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (active[i].gameObject.activeSelf) continue;
                int last = active.Count - 1;
                active[i] = active[last];
                active.RemoveAt(last);
            }
        }

        // Si ningún intento encuentra lugar libre, reintenta el frame siguiente.
        private bool TrySpawn(Vector2 center)
        {
            float inner = InnerSpawnRadius(targetCamera);
            Vector2 forward = ForwardDirection();

            for (int attempt = 0; attempt < SpawnAttempts; attempt++)
            {
                Vector2 direction = Random.value < forwardChance
                    ? Rotate(forward, Random.Range(-forwardSpread, forwardSpread))
                    : Random.insideUnitCircle.normalized;
                if (direction.sqrMagnitude < 0.0001f) continue;

                Vector2 position = center + direction * Random.Range(inner, inner + spawnRingWidth);
                if (Physics2D.OverlapCircle(position, spawnClearance, blockingLayers) != null) continue;

                Vector2 toPlayer = (Vector2)player.position - position;
                float angle = Mathf.Atan2(-toPlayer.x, toPlayer.y) * Mathf.Rad2Deg;
                EnemyShip enemy = pool.Get(position, Quaternion.Euler(0f, 0f, angle));
                enemy.Launch(pool, projectilePool, player, gameManager);
                active.Add(enemy);
                return true;
            }

            return false;
        }

        // Del jugador hacia B; sin nivel, hacia donde apunta el jugador.
        private Vector2 ForwardDirection()
        {
            if (levelManager != null)
            {
                Vector2 toTarget = levelManager.TargetPoint - (Vector2)player.position;
                if (toTarget.sqrMagnitude > 0.0001f) return toTarget.normalized;
            }
            return player.up;
        }

        private static Vector2 Rotate(Vector2 v, float degrees) => (Vector2)(Quaternion.Euler(0f, 0f, degrees) * v);

        // Semidiagonal del área visible más el margen: el enemigo aparece siempre fuera de la vista.
        private float InnerSpawnRadius(Camera cam)
        {
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            float halfDiagonal = Mathf.Sqrt(halfWidth * halfWidth + halfHeight * halfHeight);
            return Mathf.Max(spawnRadius, halfDiagonal + ViewMargin);
        }

        private float EffectiveDespawnDistance(Camera cam)
        {
            return Mathf.Max(despawnDistance, InnerSpawnRadius(cam) + spawnRingWidth + DespawnMargin);
        }

        private void OnDrawGizmosSelected()
        {
            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null) return;

            Vector3 center = cam.transform.position;
            center.z = 0f;
            float inner = InnerSpawnRadius(cam);
            Gizmos.color = new Color(1f, 0.3f, 0.1f);
            Gizmos.DrawWireSphere(center, inner);
            Gizmos.DrawWireSphere(center, inner + spawnRingWidth);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(center, EffectiveDespawnDistance(cam));
        }
    }
}
