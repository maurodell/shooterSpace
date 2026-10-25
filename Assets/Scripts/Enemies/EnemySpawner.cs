using System.Collections.Generic;
using NavesShooter.Core;
using NavesShooter.Weapons;
using UnityEngine;
using UnityEngine.Serialization;

namespace NavesShooter.Enemies
{
    // Hace aparecer naves enemigas en oleadas, con los valores del DifficultyDirector según el progreso: cada oleada
    // tiene `MaxEnemies` naves (las simultáneas como máximo), que aparecen de a una cada `EnemySpawnInterval` segundos y
    // solo si entran en el presupuesto de amenaza (las que faltan quedan reservadas: los cometas chicos no les ocupan
    // el lugar). Cuando ya no queda ninguna de la oleada (destruidas o perdidas de vista) hay `WaveBreak` segundos de
    // calma antes de la siguiente. Con `MaxEnemies` en 0 (arranque del nivel) no empieza ninguna oleada. Aparecen
    // en el mismo anillo fuera de la vista que los cometas chicos, preferentemente por delante del jugador en la
    // dirección de la nave nodriza (`forwardChance`, dentro de ±`forwardSpread`), mirando hacia él. Nunca encima de un
    // cometa (los grandes cercanos a la cámara siempre tienen collider), de la nave nodriza ni de otro enemigo.
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
        [SerializeField, Tooltip("Tamaño de oleada, intervalos, agresividad y presupuesto de amenaza.")]
        private DifficultyDirector director;

        [Header("Oleadas")]
        [SerializeField, Min(0f), Tooltip("Segundos mínimos hasta la primera oleada, al empezar y al reiniciar.")] private float initialDelay = 4f;

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
        [SerializeField, Min(0), FormerlySerializedAs("maxEnemies"), Tooltip("Naves precreadas. El pool crece si se agotan.")]
        private int initialPoolSize = 4;
        [SerializeField, Min(0), Tooltip("Proyectiles enemigos precreados. El pool crece si se agotan.")]
        private int projectilePoolSize = 24;

        private const float ViewMargin = 1f;     // entre la nave y el borde visible al aparecer
        private const float DespawnMargin = 1f;
        private const int SpawnAttempts = 8;

        private readonly List<EnemyShip> active = new List<EnemyShip>();
        private ObjectPool<EnemyShip> pool;
        private ObjectPool<Projectile> projectilePool;
        private float timer;
        private bool inWave;
        private int waveRemaining;

        public int ActiveCount => active.Count;
        public bool InWave => inWave;
        public int WaveSize { get; private set; }
        // Naves de la oleada en curso que todavía no aparecieron.
        public int WaveRemaining => waveRemaining;
        // Segundos hasta que pueda empezar la próxima oleada (0 durante una oleada).
        public float CalmRemaining => inWave ? 0f : Mathf.Max(timer, 0f);

        private void Awake()
        {
            if (enemyPrefab == null || projectilePrefab == null || player == null || director == null)
            {
                Debug.LogError($"{nameof(EnemySpawner)}: faltan referencias (prefabs, jugador o director).", this);
                enabled = false;
                return;
            }

            if (targetCamera == null) targetCamera = Camera.main;
            pool = new ObjectPool<EnemyShip>(enemyPrefab, initialPoolSize, new GameObject("Enemies").transform);
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

        // Al reiniciar: todo al pool sin explotar ni sumar puntos, se corta la oleada y vuelve a esperar `initialDelay`.
        private void DespawnAll()
        {
            pool.ReleaseAll();
            projectilePool.ReleaseAll();
            active.Clear();
            timer = initialDelay;
            inWave = false;
            waveRemaining = 0;
            WaveSize = 0;
            director.ReportActive(ThreatKind.Enemy, 0);
            director.ReportReserved(ThreatKind.Enemy, 0);
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
            director.ReportActive(ThreatKind.Enemy, active.Count);

            timer = Mathf.Max(timer - Time.deltaTime, 0f);
            if (!inWave)
            {
                // Calma (o tramo sin enemigos: la oleada empieza apenas el progreso la permite).
                if (timer > 0f || director.MaxEnemies == 0) return;
                inWave = true;
                WaveSize = waveRemaining = director.MaxEnemies;
            }

            if (waveRemaining > 0)
            {
                // Sin presupuesto o sin lugar libre espera, con la nave reservada, y reintenta el frame siguiente.
                if (timer <= 0f && director.CanAfford(ThreatKind.Enemy) && TrySpawn(center))
                {
                    waveRemaining--;
                    timer = director.EnemySpawnInterval;
                    director.ReportActive(ThreatKind.Enemy, active.Count);
                }
            }
            else if (active.Count == 0)
            {
                inWave = false;
                timer = director.WaveBreak;
            }

            director.ReportReserved(ThreatKind.Enemy, waveRemaining);
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
                enemy.Launch(pool, projectilePool, player, gameManager, director.EnemyFireRate, director.EnemyInaccuracy);
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
