using System;
using System.Collections.Generic;
using NavesShooter.Asteroids;
using NavesShooter.Core;
using NavesShooter.Enemies;
using UnityEngine;
using Random = UnityEngine.Random;

namespace NavesShooter.Pickups
{
    // Cristales de munición (AmmoPickup) del nivel, de dos fuentes:
    // - Recorrido: grupos de `groupSize` cristales repartidos a lo largo de A→B, uno cada `groupSpacing / routeDensity`
    //   unidades en promedio, con un desvío lateral de hasta `lateralSpread`. Se generan con la semilla del nivel
    //   (RandomStream.Pickups) cada vez que el LargeCometField arma su campo, en zonas libres: el círculo del grupo
    //   (más la deriva del cristal y `cometClearance`) no toca ningún cometa grande. Nada entre A y `startOffset` ni en
    //   la zona de llegada. Son datos: solo los cercanos a la cámara tienen GameObject del pool, igual que los cometas
    //   grandes (se activan a `activationRadius` y se liberan a `activationRadius + hysteresis`). Uno recogido no vuelve
    //   hasta reiniciar el nivel.
    // - Drop: un cometa chico destruido suelta `smallCometDropCount` con probabilidad `smallCometDropChance`; una nave
    //   enemiga, `enemyDropCount` con `enemyDropChance`. Solo en Playing (la explosión del final del nivel no suelta
    //   nada). Expiran solos; más allá de `despawnDistance` de la cámara vuelven al pool.
    // También reproduce el sonido de recolección (uno corto ascendente, generado por código si no hay clip).
    public class PickupSpawner : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private LevelManager levelManager;
        [SerializeField, Tooltip("Los grupos del recorrido se generan al armarse el campo y lo evitan.")]
        private LargeCometField cometField;
        [SerializeField, Tooltip("Al reiniciar, todo vuelve al pool y el recorrido se repone. Solo hay drops en Playing.")]
        private GameManager gameManager;
        [SerializeField, Tooltip("Cristales, imán, recorrido y drops (sección Pickups).")] private GameConfig config;
        [SerializeField, Tooltip("Munición del jugador (PlayerShip): la recargan los cristales y decide el imán.")]
        private Ammo playerAmmo;
        [SerializeField] private AmmoPickup pickupPrefab;
        [SerializeField, Tooltip("Centro de la activación. Si está vacío usa Camera.main.")] private Camera targetCamera;

        [Header("Activación (desde la cámara)")]
        [SerializeField, Min(0f), Tooltip("Cristales del recorrido más cerca que esto tienen GameObject. Nunca menor que la " +
            "semidiagonal visible + 1.")]
        private float activationRadius = 20f;
        [SerializeField, Min(0f), Tooltip("Distancia extra antes de desactivar.")] private float hysteresis = 4f;
        [SerializeField, Min(0f), Tooltip("Cristales soltados más lejos que esto de la cámara vuelven al pool.")]
        private float despawnDistance = 30f;
        [SerializeField, Min(0)] private int initialPoolSize = 24;

        [Header("Sonido al recoger")]
        [SerializeField, Tooltip("Si falta, se agrega uno en Awake.")] private AudioSource audioSource;
        [SerializeField, Tooltip("Vacío = se genera uno corto ascendente por código.")] private AudioClip collectClip;
        [SerializeField, Range(0f, 1f)] private float collectVolume = 0.45f;

        private const float ViewMargin = 1f;     // entre el cristal y el borde visible al activarse
        private const int PlacementAttempts = 12;

        private struct RouteCrystal
        {
            public Vector2 Position;
            public bool Collected;
        }

        private RouteCrystal[] route = new RouteCrystal[0];
        private AmmoPickup[] instances = new AmmoPickup[0];
        private readonly List<int> active = new List<int>();
        private readonly List<AmmoPickup> drops = new List<AmmoPickup>();
        private ObjectPool<AmmoPickup> pool;

        private GameConfig.PickupSettings Settings => config.Pickups;

        public int RouteCount => route.Length;
        public int ActiveCount => active.Count + drops.Count;

        private void Awake()
        {
            if (levelManager == null || cometField == null || config == null || playerAmmo == null || pickupPrefab == null)
            {
                Debug.LogError($"{nameof(PickupSpawner)}: faltan referencias (LevelManager, campo de cometas, GameConfig, munición o prefab).", this);
                enabled = false;
                return;
            }

            if (targetCamera == null) targetCamera = Camera.main;
            pool = new ObjectPool<AmmoPickup>(pickupPrefab, initialPoolSize, new GameObject("Pickups").transform);
            if (collectClip == null) collectClip = CreateCollectClip();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }
        }

        // OnEnable corre antes que todos los Start: el primer Generated del campo (en su Start) ya llega acá.
        private void OnEnable()
        {
            if (pool == null) return;
            cometField.Generated += Generate;
            if (gameManager != null) gameManager.Restarted += OnRestarted;
            Asteroid.Destroyed += OnAsteroidDestroyed;
            EnemyShip.Destroyed += OnEnemyDestroyed;
            AmmoPickup.Collected += OnCollected;
        }

        private void OnDisable()
        {
            if (pool == null) return;
            cometField.Generated -= Generate;
            if (gameManager != null) gameManager.Restarted -= OnRestarted;
            Asteroid.Destroyed -= OnAsteroidDestroyed;
            EnemyShip.Destroyed -= OnEnemyDestroyed;
            AmmoPickup.Collected -= OnCollected;
        }

        // El nivel arranca de nuevo en A: vuelven los cristales recogidos del recorrido y se descartan los soltados.
        private void OnRestarted()
        {
            ReleaseAll();
            for (int i = 0; i < route.Length; i++) route[i].Collected = false;
        }

        private void ReleaseAll()
        {
            pool.ReleaseAll();
            active.Clear();
            drops.Clear();
            Array.Clear(instances, 0, instances.Length);
        }

        private void Update()
        {
            Vector2 center = targetCamera.transform.position;
            float activate = Mathf.Max(activationRadius, CameraView.HalfDiagonal(targetCamera) + ViewMargin);
            float deactivate = activate + hysteresis;
            float deactivateSqr = deactivate * deactivate;
            float activateSqr = activate * activate;

            for (int i = active.Count - 1; i >= 0; i--)
            {
                int index = active[i];
                AmmoPickup pickup = instances[index];
                // Volvió al pool por su cuenta (recogido) o la instancia ya es otro cristal.
                bool collected = !pickup.gameObject.activeSelf || pickup.RouteIndex != index;
                if (collected) route[index].Collected = true;
                else if ((pickup.Position - center).sqrMagnitude <= deactivateSqr) continue;
                else pool.Release(pickup);

                instances[index] = null;
                active[i] = active[active.Count - 1];
                active.RemoveAt(active.Count - 1);
            }

            float despawnSqr = despawnDistance * despawnDistance;
            for (int i = drops.Count - 1; i >= 0; i--)
            {
                AmmoPickup drop = drops[i];
                bool gone = !drop.gameObject.activeSelf || drop.RouteIndex >= 0;
                if (!gone && (drop.Position - center).sqrMagnitude <= despawnSqr) continue;
                if (!gone) drop.Despawn();
                drops[i] = drops[drops.Count - 1];
                drops.RemoveAt(drops.Count - 1);
            }

            // Pocos cristales por nivel (decenas): recorrerlos todos es más barato que mantener una grilla.
            for (int index = 0; index < route.Length; index++)
            {
                if (route[index].Collected || instances[index] != null) continue;
                if ((route[index].Position - center).sqrMagnitude > activateSqr) continue;

                AmmoPickup pickup = pool.Get(route[index].Position, Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
                pickup.Launch(pool, Settings, playerAmmo, Vector2.zero, index);
                instances[index] = pickup;
                active.Add(index);
            }
        }

        // Grupos del recorrido en coordenadas locales de A→B: x a lo ancho (derecha), y a lo largo (0 en A).
        private void Generate()
        {
            ReleaseAll();

            LevelDefinition level = levelManager.Level;
            if (level == null) return;
            System.Random random = level.CreateRandom(RandomStream.Pickups);

            Vector2 origin = levelManager.StartPoint;
            Vector2 forward = level.Direction;
            Vector2 right = new Vector2(forward.y, -forward.x);
            float start = Settings.startOffset;
            float end = level.Distance - level.ArrivalClearRadius;
            int groups = end > start ? Mathf.RoundToInt((end - start) / Settings.groupSpacing * Settings.routeDensity) : 0;
            float slot = groups > 0 ? (end - start) / groups : 0f;
            // Cada cristal ocupa su radio más lo que deriva; el grupo, además, su reparto.
            float clearance = Settings.groupSpread + pickupPrefab.PickupRadius + Settings.wanderRadius + Settings.cometClearance;

            var positions = new List<Vector2>();
            for (int g = 0; g < groups; g++)
            {
                for (int attempt = 0; attempt < PlacementAttempts; attempt++)
                {
                    float y = start + slot * (g + AsteroidShape.Range(random, 0.15f, 0.85f));
                    float x = AsteroidShape.Range(random, -Settings.lateralSpread, Settings.lateralSpread);
                    Vector2 center = origin + right * x + forward * y;
                    if (!cometField.IsClear(center, clearance)) continue;

                    // Repartidos en ángulos parejos alrededor del centro: nunca se encimen.
                    int size = random.Next(Settings.groupSize.x, Settings.groupSize.y + 1);
                    float baseAngle = AsteroidShape.Range(random, 0f, 2f * Mathf.PI);
                    for (int i = 0; i < size; i++)
                    {
                        float angle = baseAngle + 2f * Mathf.PI * (i + AsteroidShape.Range(random, -0.15f, 0.15f)) / size;
                        float distance = Settings.groupSpread * AsteroidShape.Range(random, 0.5f, 1f);
                        positions.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance);
                    }
                    break;
                }
            }

            route = new RouteCrystal[positions.Count];
            instances = new AmmoPickup[positions.Count];
            for (int i = 0; i < positions.Count; i++) route[i].Position = positions[i];
        }

        // Solo lo destruido por un disparo suelta cristales: chocarlo con la nave no recarga.
        private void OnAsteroidDestroyed(AsteroidDestroyed destroyed)
        {
            if (!destroyed.Rammed) Drop(destroyed.Position, destroyed.Velocity, Settings.smallCometDropChance, Settings.smallCometDropCount);
        }

        private void OnEnemyDestroyed(EnemyDestroyed destroyed)
        {
            if (!destroyed.Rammed) Drop(destroyed.Position, destroyed.Velocity, Settings.enemyDropChance, Settings.enemyDropCount);
        }

        private void Drop(Vector2 position, Vector2 velocity, float chance, int count)
        {
            if (gameManager != null && gameManager.State != GameState.Playing) return;
            if (count <= 0 || Random.value >= chance) return;

            Vector2 inherited = velocity * Settings.dropInheritVelocity;
            float baseAngle = Random.Range(0f, 2f * Mathf.PI);
            for (int i = 0; i < count; i++)
            {
                float angle = baseAngle + 2f * Mathf.PI * i / count;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                AmmoPickup pickup = pool.Get(position + direction * 0.15f, Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
                pickup.Launch(pool, Settings, playerAmmo, inherited + direction * Settings.dropSpeed);
                // La misma instancia puede volver como drop en el mismo frame en que expiró: sin repetirla en la lista.
                if (!drops.Contains(pickup)) drops.Add(pickup);
            }
        }

        private void OnCollected(PickupCollected collected)
        {
            if (collectClip != null) audioSource.PlayOneShot(collectClip, collectVolume);
        }

        // Placeholder: barrido ascendente de ~140 ms (700 → 1540 Hz) con un armónico, ataque corto y caída suave.
        private static AudioClip CreateCollectClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.14f;
            int length = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[length];
            double phase = 0.0;
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / sampleRate;
                float k = t / duration;
                float frequency = 700f * Mathf.Pow(2.2f, k);
                phase += 2.0 * Math.PI * frequency / sampleRate;
                float envelope = Mathf.Min(1f, t / 0.005f) * Mathf.Pow(1f - k, 1.5f);
                float tone = Mathf.Sin((float)phase) * 0.7f + Mathf.Sin((float)(2.0 * phase)) * 0.2f;
                samples[i] = Mathf.Clamp(tone * envelope * 0.8f, -1f, 1f);
            }
            AudioClip clip = AudioClip.Create("AmmoPickupChime", length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.72f, 0f, 0.8f);
            foreach (RouteCrystal crystal in route)
                if (!crystal.Collected) Gizmos.DrawWireSphere(crystal.Position, 0.3f);
        }
    }
}
