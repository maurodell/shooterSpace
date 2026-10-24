using System.Collections.Generic;
using NavesShooter.Core;
using UnityEngine;

namespace NavesShooter.Asteroids
{
    // Mantiene `targetCount` cometas chicos activos alrededor de la cámara (que sigue al jugador). Los nuevos aparecen en
    // un anillo centrado en la cámara, siempre fuera de la vista: el radio interior nunca baja de la semidiagonal visible
    // más el radio del cometa. No aparecen encima de otro cometa, chico o grande. Los que se alejan más de `despawnDistance` vuelven al pool y se reponen en el mismo
    // frame, así la cantidad activa se mantiene estable. Una fracción apunta a la zona del jugador; el resto sale hacia
    // una dirección al azar dentro del semiplano que mira a la cámara, para que no se alejen apenas aparecen.
    public class AsteroidSpawner : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private Asteroid asteroidPrefab;
        [SerializeField, Tooltip("Centro del anillo de spawn y del despawn. Si está vacío usa Camera.main.")]
        private Camera targetCamera;
        [SerializeField, Tooltip("Hacia dónde apuntan los asteroides dirigidos. Sin jugador, todos salen al azar.")]
        private Transform player;
        [SerializeField, Tooltip("Al reiniciar la partida, todos los asteroides vuelven al pool.")]
        private GameManager gameManager;

        [Header("Cantidad")]
        [SerializeField, Min(0), Tooltip("Asteroides activos que se mantienen alrededor de la cámara.")]
        private int targetCount = 14;

        [Header("Distancias (desde la cámara)")]
        [SerializeField, Min(0f), Tooltip("Radio interior del anillo de spawn. Si queda dentro de la vista se agranda lo justo para que el asteroide aparezca fuera.")]
        private float spawnRadius = 12.5f;
        [SerializeField, Min(0f), Tooltip("Ancho del anillo: cada asteroide aparece a una distancia al azar entre el radio y el radio + ancho.")]
        private float spawnRingWidth = 3f;
        [SerializeField, Min(0f), Tooltip("Más lejos que esto el asteroide vuelve al pool. Nunca menor que el borde exterior del anillo.")]
        private float despawnDistance = 22f;

        [Header("Trayectoria")]
        [SerializeField, Range(0f, 1f), Tooltip("Fracción de asteroides que se dirige a la zona del jugador.")]
        private float aimedFraction = 0.5f;
        [SerializeField, Min(0f), Tooltip("Radio del área alrededor del jugador a la que apuntan los dirigidos (unidades).")]
        private float aimSpread = 3f;
        [SerializeField, Tooltip("Layers, además de la de los cometas, sobre las que no se puede spawnear (p. ej. la nave nodriza).")]
        private LayerMask extraBlockingLayers;

        private const float ViewMargin = 0.5f;   // unidades entre el asteroide y el borde visible al aparecer (cubre el ancho de línea)
        private const float SpawnGap = 0.3f;     // separación mínima con otros asteroides al aparecer
        private const float DespawnMargin = 1f;  // despawn mínimo = borde exterior del anillo + esto
        private const int SpawnAttempts = 6;     // posiciones probadas por asteroide antes de dejarlo para el próximo frame

        private readonly List<Asteroid> active = new List<Asteroid>();
        private ObjectPool<Asteroid> pool;
        private int blockingMask;
        private int resumeFrame;

        public int ActiveCount => active.Count;

        private void Awake()
        {
            if (asteroidPrefab == null)
            {
                Debug.LogError($"{nameof(AsteroidSpawner)}: falta asignar el prefab del asteroide.", this);
                enabled = false;
                return;
            }

            if (targetCamera == null) targetCamera = Camera.main;
            blockingMask = (1 << asteroidPrefab.gameObject.layer) | extraBlockingLayers.value;
            pool = new ObjectPool<Asteroid>(asteroidPrefab, targetCount, new GameObject("Asteroids").transform);
        }

        private void OnEnable()
        {
            if (gameManager != null) gameManager.Restarted += DespawnAll;
        }

        private void OnDisable()
        {
            if (gameManager != null) gameManager.Restarted -= DespawnAll;
        }

        // Devuelve todo al pool sin emitir `Destroyed`. No repone hasta el frame siguiente: para entonces la cámara ya
        // saltó a la nave reaparecida y el anillo de spawn queda fuera de la vista nueva, no de la vieja.
        private void DespawnAll()
        {
            pool.ReleaseAll();
            active.Clear();
            resumeFrame = Time.frameCount + 1;
        }

        private void Update()
        {
            if (Time.frameCount < resumeFrame) return;

            Vector2 center = targetCamera.transform.position;
            float despawn = EffectiveDespawnDistance(targetCamera);
            float despawnSqr = despawn * despawn;

            foreach (Asteroid asteroid in active)
            {
                if (((Vector2)asteroid.transform.position - center).sqrMagnitude > despawnSqr)
                    asteroid.Despawn();
            }

            RemoveInactive();
            while (active.Count < targetCount && TrySpawn(center)) { }
        }

        // Saca de la lista los que volvieron al pool: por distancia o destruidos por un disparo. Recorrido inverso con
        // swap-remove: el orden de la lista no importa.
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

        private bool TrySpawn(Vector2 center)
        {
            float radius = asteroidPrefab.Radius;
            float inner = InnerSpawnRadius(targetCamera, radius);

            for (int attempt = 0; attempt < SpawnAttempts; attempt++)
            {
                Vector2 position = center + RandomDirection() * Random.Range(inner, inner + spawnRingWidth);
                if (Physics2D.OverlapCircle(position, radius + SpawnGap, blockingMask) != null) continue;

                Asteroid asteroid = pool.Get(position, Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
                asteroid.Launch(pool, ChooseHeading(position, center));
                active.Add(asteroid);
                return true;
            }

            return false;
        }

        private Vector2 ChooseHeading(Vector2 position, Vector2 center)
        {
            if (player != null && Random.value < aimedFraction)
            {
                Vector2 target = (Vector2)player.position + Random.insideUnitCircle * aimSpread;
                return target - position;
            }

            // Dirección al azar, reflejada si se aleja de la cámara: cruza la zona de juego o la bordea, nunca huye.
            Vector2 heading = RandomDirection();
            return Vector2.Dot(heading, center - position) < 0f ? -heading : heading;
        }

        // Semidiagonal del área visible: el círculo más chico centrado en la cámara que la contiene entera.
        private static float ViewHalfDiagonal(Camera cam)
        {
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            return Mathf.Sqrt(halfWidth * halfWidth + halfHeight * halfHeight);
        }

        private float InnerSpawnRadius(Camera cam, float asteroidRadius)
        {
            return Mathf.Max(spawnRadius, ViewHalfDiagonal(cam) + asteroidRadius + ViewMargin);
        }

        // Si fuera menor que el borde exterior del anillo, un asteroide podría volver al pool en el mismo frame en que aparece.
        private float EffectiveDespawnDistance(Camera cam)
        {
            float outer = InnerSpawnRadius(cam, asteroidPrefab.Radius) + spawnRingWidth;
            return Mathf.Max(despawnDistance, outer + DespawnMargin);
        }

        private static Vector2 RandomDirection()
        {
            float angle = Random.Range(0f, 2f * Mathf.PI);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        private void OnDrawGizmosSelected()
        {
            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null || asteroidPrefab == null) return;

            Vector3 center = cam.transform.position;
            center.z = 0f;
            float inner = InnerSpawnRadius(cam, asteroidPrefab.Radius);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(center, inner);
            Gizmos.DrawWireSphere(center, inner + spawnRingWidth);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(center, EffectiveDespawnDistance(cam));
        }
    }
}
