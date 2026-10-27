using System;
using UnityEngine;

namespace NavesShooter.Core
{
    // Valores de balance del juego en un solo asset (Settings/GameConfig.asset): nave, arma, cometas chicos y grandes,
    // enemigos, spawners y presupuesto de amenaza. Cada sistema tiene un campo `config` y lee su sección en vivo, así
    // un cambio en Play se nota al instante (y queda guardado en el asset: no se revierte al salir de Play). Los objetos
    // de pool (cometas chicos, enemigos) reciben su sección del spawner en `Launch`. Lo que es por nivel (distancia,
    // dirección, curvas de dificultad según el progreso) va en cada LevelDefinition; lo visual (formas, colores,
    // anchos de línea) y lo técnico (tamaños de pool) sigue en cada componente.
    [CreateAssetMenu(fileName = "GameConfig", menuName = "NavesShooter/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Serializable]
        public class ShipSettings
        {
            [Header("Movimiento")]
            [Tooltip("Velocidad en reposo, sin avance (unidades/s).")] public float idleSpeed = 0.6f;
            [Tooltip("Velocidad con el avance apretado (unidades/s).")] public float thrustSpeed = 4f;
            [Tooltip("Unidades/s² al acelerar con el avance.")] public float linearAcceleration = 6f;
            [Tooltip("Unidades/s² al soltar el avance y volver a la velocidad de reposo.")] public float linearDeceleration = 3f;
            [Tooltip("Grados por segundo.")] public float maxTurnSpeed = 200f;
            [Tooltip("Grados/s² al aumentar el giro.")] public float angularAcceleration = 900f;
            [Tooltip("Grados/s² al soltar o invertir el giro.")] public float angularDeceleration = 1200f;
            [Range(0f, 0.9f), Tooltip("Magnitud mínima del joystick para avanzar y girar. Dentro de ella la nave sigue recta.")]
            public float deadZone = 0.2f;
            [Range(0f, 0.5f), Tooltip("Cuánto se angosta la nave en X al girar a velocidad máxima.")] public float bankAmount = 0.25f;
            [Min(0f), Tooltip("Piloto automático (acople): velocidad hacia el punto por unidad de distancia (1/s).")]
            public float autopilotGain = 3f;

            [Header("Vida y choques")]
            [Min(1)] public int maxHealth = 3;
            [Min(0f), Tooltip("Segundos sin recibir daño después de un golpe que no mata.")] public float invulnerabilityOnHit = 1f;
            [Min(0f), Tooltip("Segundos sin recibir daño (y atravesando lo peligroso) después de reaparecer.")]
            public float respawnInvulnerability = 2f;
            [Min(0f), Tooltip("Velocidad con que un choque empuja la nave lejos de lo que tocó (unidades/s).")]
            public float knockbackSpeed = 6f;
            [Min(0f), Tooltip("Unidades/s² a las que se disipa ese empuje.")] public float knockbackDecay = 10f;
        }

        [Serializable]
        public class WeaponSettings
        {
            [Min(0.1f), Tooltip("Disparos por segundo.")] public float fireRate = 8f;
            [Tooltip("Velocidad del proyectil relativa a la nave (unidades/s).")] public float projectileSpeed = 14f;
            [Min(0.05f), Tooltip("Segundos hasta que el proyectil vuelve al pool si no choca (alcance = velocidad · vida).")]
            public float projectileLifetime = 1.2f;
        }

        [Serializable]
        public class AsteroidSettings
        {
            [Min(0.05f), Tooltip("Radio máximo del contorno de un cometa chico (unidades).")] public float radius = 0.4f;
            [Tooltip("Velocidad lineal mínima (x) y máxima (y), en unidades/s, antes del multiplicador de dificultad.")]
            public Vector2 speedRange = new Vector2(1.8f, 2.8f);
            [Tooltip("Giro mínimo (x) y máximo (y) en valor absoluto, en grados/s. El sentido es aleatorio.")]
            public Vector2 spinRange = new Vector2(50f, 120f);
            [Min(0), Tooltip("Puntos por cometa chico destruido.")] public int points = 100;
        }

        [Serializable]
        public class AsteroidSpawnerSettings
        {
            [Min(0f), Tooltip("Radio interior del anillo de spawn desde la cámara. Si queda dentro de la vista se agranda lo justo.")]
            public float spawnRadius = 12.5f;
            [Min(0f), Tooltip("Ancho del anillo de spawn.")] public float spawnRingWidth = 3f;
            [Min(0f), Tooltip("Más lejos que esto (desde la cámara) el cometa vuelve al pool. Nunca menor que el borde del anillo.")]
            public float despawnDistance = 22f;
            [Range(0f, 1f), Tooltip("Fracción de cometas que se dirige a la zona del jugador.")] public float aimedFraction = 0.5f;
            [Min(0f), Tooltip("Radio del área alrededor del jugador a la que apuntan los dirigidos (unidades).")]
            public float aimSpread = 3f;
        }

        [Serializable]
        public class LargeCometSettings
        {
            [Tooltip("Radio mínimo (x) y máximo (y) de un cometa grande.")] public Vector2 radiusRange = new Vector2(1.2f, 2.8f);
            [Min(0f), Tooltip("Pasillo mínimo entre los bordes de dos cometas (la nave mide ~0.65 de ancho).")] public float minGap = 2.5f;
            [Min(0.1f), Tooltip("Separación mínima entre centros con densidad 0.")] public float sparseSpacing = 14f;
            [Min(0.1f), Tooltip("Separación mínima entre centros con densidad 1.")] public float denseSpacing = 6.5f;
            [Min(0f), Tooltip("Ancho de la franja con cometas a cada lado de la recta A→B (unidades).")] public float halfWidth = 20f;
            [Min(0f), Tooltip("Radio alrededor de A sin cometas.")] public float startClearance = 8f;
            [Min(0f), Tooltip("Espacio libre mínimo entre un cometa y la zona de acople o el casco de la nave nodriza.")]
            public float targetClearance = 5f;
            [Tooltip("Giro visual mínimo (x) y máximo (y) en valor absoluto, en grados/s.")] public Vector2 spinRange = new Vector2(2f, 7f);
        }

        [Serializable]
        public class EnemySettings
        {
            [Header("Movimiento")]
            [Tooltip("Velocidad al acercarse (unidades/s). Menor que la del jugador, para que pueda escaparle.")]
            public float approachSpeed = 3.2f;
            [Tooltip("Velocidad mientras ataca.")] public float attackSpeed = 1.8f;
            [Tooltip("Velocidad al alejarse para reposicionarse.")] public float repositionSpeed = 4.2f;
            [Tooltip("Unidades/s² al cambiar de velocidad.")] public float linearAcceleration = 4f;
            [Tooltip("Grados por segundo.")] public float maxTurnSpeed = 110f;
            [Tooltip("Grados/s² al acelerar o frenar el giro.")] public float angularAcceleration = 400f;

            [Header("Distancias al jugador")]
            [Min(0f), Tooltip("Más cerca que esto empieza a atacar.")] public float attackRange = 7.5f;
            [Min(0f), Tooltip("Atacando, más lejos que esto vuelve a acercarse.")] public float disengageRange = 10f;
            [Min(0f), Tooltip("Más cerca que esto se aleja para reposicionarse.")] public float tooClose = 3f;
            [Min(0f), Tooltip("Reposicionándose, a esta distancia vuelve a acercarse.")] public float repositionDistance = 8f;
            [Min(0f), Tooltip("Segundos máximos reposicionándose.")] public float repositionMaxTime = 2.5f;
            [Range(0f, 90f), Tooltip("Desvío de la huida respecto de alejarse en línea recta del jugador (grados).")]
            public float repositionAngle = 50f;

            [Header("Disparo")]
            [Min(1), Tooltip("Disparos por ráfaga.")] public int burstCount = 3;
            [Min(0.01f), Tooltip("Segundos entre disparos de una ráfaga.")] public float burstInterval = 0.15f;
            [Min(0f), Tooltip("Segundos entre ráfagas (se divide por la cadencia de la dificultad).")] public float burstCooldown = 2.2f;
            [Min(0f), Tooltip("Segundos desde que empieza a atacar hasta la primera ráfaga.")] public float firstBurstDelay = 0.8f;
            [Range(0f, 90f), Tooltip("Solo empieza una ráfaga si el jugador está a menos de este ángulo del frente.")]
            public float fireCone = 20f;
            [Range(0f, 30f), Tooltip("Desvío al azar de cada disparo, en grados a cada lado (se multiplica por la dificultad).")]
            public float inaccuracy = 6f;
            [Tooltip("Velocidad del proyectil relativa a la nave (unidades/s).")] public float projectileSpeed = 6f;
            [Min(0.05f)] public float projectileLifetime = 2.5f;

            [Header("Esquive")]
            [Min(0f), Tooltip("Largo del circle cast hacia adelante.")] public float lookAhead = 4f;
            [Min(0.01f), Tooltip("Radio del circle cast (un poco más que la nave).")] public float avoidRadius = 0.6f;

            [Header("Vida y puntaje")]
            [Min(1)] public int maxHealth = 3;
            [Min(0), Tooltip("Puntos al destruirla.")] public int points = 250;
        }

        [Serializable]
        public class EnemySpawnerSettings
        {
            [Min(0f), Tooltip("Segundos mínimos hasta la primera oleada, al empezar y al reiniciar.")] public float initialDelay = 4f;
            [Min(0f), Tooltip("Radio interior del anillo de spawn desde la cámara. Si queda dentro de la vista se agranda lo justo.")]
            public float spawnRadius = 12.5f;
            [Min(0f)] public float spawnRingWidth = 3f;
            [Min(0f), Tooltip("Más lejos que esto el enemigo vuelve al pool. Nunca menor que el borde exterior del anillo.")]
            public float despawnDistance = 30f;
            [Range(0f, 1f), Tooltip("Probabilidad de aparecer por delante del jugador (hacia B).")] public float forwardChance = 0.8f;
            [Range(0f, 180f), Tooltip("Desvío máximo respecto de la dirección hacia B, en grados a cada lado.")]
            public float forwardSpread = 60f;
            [Min(0f), Tooltip("Radio libre que necesita el punto de aparición.")] public float spawnClearance = 1.5f;
        }

        [Serializable]
        public class ThreatSettings
        {
            [Header("Costo de cada amenaza")]
            [Min(0)] public int smallCometCost = 1;
            [Min(0)] public int enemyCost = 3;

            [Header("Incremento por vuelta a los niveles")]
            [Min(0f), Tooltip("Fracción extra por vuelta de cometas chicos, tamaño de oleada y presupuesto.")]
            public float countPerCycle = 0.25f;
            [Min(0f), Tooltip("Fracción extra por vuelta de velocidad de cometas y cadencia enemiga; también divide " +
                "los intervalos entre apariciones y oleadas, y el desvío de los disparos.")]
            public float aggressionPerCycle = 0.15f;
            [Min(0f), Tooltip("Densidad de cometas grandes sumada por vuelta (0–1).")] public float densityPerCycle = 0.08f;
            [Min(0), Tooltip("Vueltas a partir de las cuales la dificultad deja de subir.")] public int maxCycles = 4;
        }

        [SerializeField] private ShipSettings ship = new ShipSettings();
        [SerializeField] private WeaponSettings weapon = new WeaponSettings();
        [SerializeField] private AsteroidSettings asteroids = new AsteroidSettings();
        [SerializeField] private AsteroidSpawnerSettings asteroidSpawner = new AsteroidSpawnerSettings();
        [SerializeField] private LargeCometSettings largeComets = new LargeCometSettings();
        [SerializeField] private EnemySettings enemies = new EnemySettings();
        [SerializeField] private EnemySpawnerSettings enemySpawner = new EnemySpawnerSettings();
        [SerializeField] private ThreatSettings threat = new ThreatSettings();

        public ShipSettings Ship => ship;
        public WeaponSettings Weapon => weapon;
        public AsteroidSettings Asteroids => asteroids;
        public AsteroidSpawnerSettings AsteroidSpawner => asteroidSpawner;
        public LargeCometSettings LargeComets => largeComets;
        public EnemySettings Enemies => enemies;
        public EnemySpawnerSettings EnemySpawner => enemySpawner;
        public ThreatSettings Threat => threat;

        private void OnValidate()
        {
            asteroids.speedRange.y = Mathf.Max(asteroids.speedRange.y, asteroids.speedRange.x);
            asteroids.spinRange.y = Mathf.Max(asteroids.spinRange.y, asteroids.spinRange.x);
            largeComets.radiusRange.y = Mathf.Max(largeComets.radiusRange.y, largeComets.radiusRange.x);
            largeComets.spinRange.y = Mathf.Max(largeComets.spinRange.y, largeComets.spinRange.x);
            enemies.disengageRange = Mathf.Max(enemies.disengageRange, enemies.attackRange);
        }
    }
}
