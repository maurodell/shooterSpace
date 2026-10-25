using UnityEngine;

namespace NavesShooter.Core
{
    // Datos de un nivel: cuánto hay que viajar desde el inicio (A) hasta la zona de acople de la nave nodriza (B), en
    // qué dirección (siempre hacia arriba de la pantalla, con un desvío acotado), y la semilla de la generación
    // procedural. Toda la aleatoriedad del nivel sale de `seed` (vía CreateRandom), así el mismo asset genera siempre
    // el mismo nivel.
    [CreateAssetMenu(menuName = "NavesShooter/Level Definition", fileName = "Level_")]
    public class LevelDefinition : ScriptableObject
    {
        public enum DirectionMode { Fixed, Random }

        [Header("Recorrido")]
        [SerializeField, Min(1f), Tooltip("Distancia en línea recta de A a B (unidades).")]
        private float distance = 480f;
        [SerializeField, Tooltip("Fixed: B en `fixedAngle`. Random: ángulo al azar dentro de ±`randomAngleSpread` a partir de la semilla.")]
        private DirectionMode directionMode = DirectionMode.Fixed;
        [SerializeField, Range(-60f, 60f), Tooltip("Dirección de B respecto de A en grados: 0 = arriba, positivo = antihorario " +
            "(misma convención que Rigidbody2D.rotation). Acotada para que B quede siempre hacia arriba.")]
        private float fixedAngle;
        [SerializeField, Range(0f, 60f), Tooltip("Desvío máximo respecto de arriba en modo Random (grados).")]
        private float randomAngleSpread = 30f;

        [Header("Generación procedural")]
        [SerializeField] private int seed = 1;

        // Dificultad: todas las curvas van de 0 (A) a 1 (B) en X, igual que LevelManager.Progress. Las lee el
        // DifficultyDirector. Arranque tranquilo (primer ~15% casi sin enemigos y con pocos cometas), subida gradual y
        // el tramo final como el más intenso.
        [Header("Dificultad: obstáculos")]
        [SerializeField, Tooltip("Densidad de cometas grandes según el progreso del punto del recorrido: 0 = separación máxima " +
            "entre ellos, 1 = mínima (LargeCometField). Los pasillos navegables se respetan con cualquier valor.")]
        private AnimationCurve largeCometDensity = Curve((0f, 0.1f), (0.15f, 0.2f), (0.6f, 0.6f), (0.9f, 0.85f), (1f, 0.85f));
        [SerializeField, Min(0f), Tooltip("Radio alrededor de la zona de acople (B) sin cometas grandes: el acercamiento final queda despejado.")]
        private float arrivalClearRadius = 22f;
        [SerializeField, Tooltip("Cometas chicos activos alrededor del jugador (se redondea).")]
        private AnimationCurve smallCometCount = Curve((0f, 3f), (0.15f, 4f), (0.5f, 7f), (0.85f, 10f), (1f, 10f));
        [SerializeField, Tooltip("Multiplicador de la velocidad de los cometas chicos al aparecer.")]
        private AnimationCurve smallCometSpeed = Curve((0f, 0.8f), (0.5f, 1f), (1f, 1.3f));

        [Header("Dificultad: enemigos")]
        [SerializeField, Tooltip("Tamaño de cada oleada = enemigos simultáneos como máximo (se redondea).")]
        private AnimationCurve maxEnemies = Curve((0f, 0f), (0.12f, 0f), (0.2f, 1f), (0.45f, 2f), (0.75f, 3f), (0.9f, 4f), (1f, 4f));
        [SerializeField, Tooltip("Segundos entre apariciones dentro de una oleada.")]
        private AnimationCurve enemySpawnInterval = Curve((0f, 5f), (0.5f, 3f), (1f, 1.5f));
        [SerializeField, Tooltip("Segundos de calma después de eliminar (o perder de vista) a toda una oleada.")]
        private AnimationCurve waveBreak = Curve((0f, 8f), (0.5f, 6f), (1f, 4f));
        [SerializeField, Tooltip("Multiplicador de la cadencia de disparo enemiga (divide la espera entre ráfagas).")]
        private AnimationCurve enemyFireRate = Curve((0f, 0.7f), (0.5f, 1f), (1f, 1.5f));
        [SerializeField, Tooltip("Multiplicador del desvío de los disparos enemigos: menos = más precisos.")]
        private AnimationCurve enemyInaccuracy = Curve((0f, 1.6f), (0.5f, 1f), (1f, 0.5f));

        [Header("Dificultad: presupuesto de amenaza")]
        [SerializeField, Tooltip("Costo máximo de amenazas activas a la vez (cometa chico y enemigo tienen costo en el " +
            "DifficultyDirector). Por debajo de la suma de los máximos de cometas y enemigos, así la escena no se satura.")]
        private AnimationCurve threatBudget = Curve((0f, 4f), (0.15f, 6f), (0.5f, 12f), (0.85f, 18f), (1f, 19f));

        public float Distance => distance;
        public int Seed => seed;
        public float ArrivalClearRadius => arrivalClearRadius;
        public AnimationCurve LargeCometDensity => largeCometDensity;
        public AnimationCurve SmallCometCount => smallCometCount;
        public AnimationCurve SmallCometSpeed => smallCometSpeed;
        public AnimationCurve MaxEnemies => maxEnemies;
        public AnimationCurve EnemySpawnInterval => enemySpawnInterval;
        public AnimationCurve WaveBreak => waveBreak;
        public AnimationCurve EnemyFireRate => enemyFireRate;
        public AnimationCurve EnemyInaccuracy => enemyInaccuracy;
        public AnimationCurve ThreatBudget => threatBudget;

        // Ángulo de B respecto de A, con la convención de Rigidbody2D.rotation.
        public float DirectionAngle => directionMode == DirectionMode.Fixed
            ? fixedAngle
            : (float)(CreateRandom(RandomStream.Direction).NextDouble() * 2.0 - 1.0) * randomAngleSpread;

        public Vector2 Direction
        {
            get
            {
                float radians = DirectionAngle * Mathf.Deg2Rad;
                return new Vector2(-Mathf.Sin(radians), Mathf.Cos(radians));
            }
        }

        // Un generador por uso (dirección, cometas, enemigos...), para que agregar un sistema que consuma números al
        // azar no cambie lo que generan los demás.
        public System.Random CreateRandom(RandomStream stream) => new System.Random(unchecked(seed * 486187739 + (int)stream));

        // Curva por puntos (progreso, valor) con tangentes planas: transiciones suaves y mesetas en cada punto.
        private static AnimationCurve Curve(params (float time, float value)[] points)
        {
            var keys = new Keyframe[points.Length];
            for (int i = 0; i < points.Length; i++) keys[i] = new Keyframe(points[i].time, points[i].value);
            return new AnimationCurve(keys);
        }
    }

    public enum RandomStream { Direction, LargeComets }
}
