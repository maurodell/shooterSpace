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

        // Dificultad: acá van las curvas en función de LevelManager.Progress (0 en A, 1 en B).

        public float Distance => distance;
        public int Seed => seed;

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
    }

    public enum RandomStream { Direction }
}
