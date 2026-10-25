using UnityEngine;

namespace NavesShooter.Core
{
    public enum ThreatKind { SmallComet, Enemy }

    // Traduce el progreso hacia la nave nodriza (LevelManager.Progress) en los parámetros de dificultad, con las curvas
    // de la LevelDefinition: cantidad y velocidad de cometas chicos, tamaño de oleada, intervalos y agresividad enemiga,
    // y la densidad de cometas grandes a lo largo del recorrido (esa se lee por posición, no por progreso actual).
    // Además lleva el presupuesto de amenaza: cada amenaza activa cerca del jugador cuesta (`smallCometCost`, `enemyCost`)
    // y los spawners solo crean algo si entra en `Budget`. Los spawners informan cuántas tienen activas (todas están cerca:
    // las lejanas vuelven al pool) y cuántas tienen comprometidas sin crear todavía (los enemigos que faltan de la oleada
    // en curso): lo comprometido de un tipo no lo puede gastar otro, así los cometas no le quitan lugar a la oleada.
    // Corre antes que los spawners para que lean los valores del frame actual.
    [DefaultExecutionOrder(-50)]
    public class DifficultyDirector : MonoBehaviour
    {
        [SerializeField] private LevelManager levelManager;

        [Header("Costo de cada amenaza")]
        [SerializeField, Min(0)] private int smallCometCost = 1;
        [SerializeField, Min(0)] private int enemyCost = 3;

        private readonly int[] activeCounts = new int[2];
        private readonly int[] reservedCounts = new int[2];
        private LevelDefinition level;

        public float Progress { get; private set; }
        public int SmallCometCount { get; private set; }
        public float SmallCometSpeed { get; private set; } = 1f;
        public int MaxEnemies { get; private set; }
        public float EnemySpawnInterval { get; private set; } = 1f;
        public float WaveBreak { get; private set; }
        public float EnemyFireRate { get; private set; } = 1f;
        public float EnemyInaccuracy { get; private set; } = 1f;
        public int Budget { get; private set; }

        public float ArrivalClearRadius => level != null ? level.ArrivalClearRadius : 0f;

        // Costo de todo lo activo.
        public int UsedBudget => activeCounts[0] * Cost(ThreatKind.SmallComet) + activeCounts[1] * Cost(ThreatKind.Enemy);

        public int ActiveCount(ThreatKind kind) => activeCounts[(int)kind];
        public int ReservedCount(ThreatKind kind) => reservedCounts[(int)kind];

        public int Cost(ThreatKind kind) => kind == ThreatKind.SmallComet ? smallCometCost : enemyCost;

        private void Awake()
        {
            if (levelManager == null || levelManager.Level == null)
            {
                Debug.LogError($"{nameof(DifficultyDirector)}: falta el LevelManager o su nivel.", this);
                enabled = false;
                return;
            }

            level = levelManager.Level;
            Evaluate();
        }

        private void Update() => Evaluate();

        private void Evaluate()
        {
            float t = Progress = levelManager.Progress;
            SmallCometCount = Mathf.Max(0, Mathf.RoundToInt(level.SmallCometCount.Evaluate(t)));
            SmallCometSpeed = Mathf.Max(0.05f, level.SmallCometSpeed.Evaluate(t));
            MaxEnemies = Mathf.Max(0, Mathf.RoundToInt(level.MaxEnemies.Evaluate(t)));
            EnemySpawnInterval = Mathf.Max(0.1f, level.EnemySpawnInterval.Evaluate(t));
            WaveBreak = Mathf.Max(0f, level.WaveBreak.Evaluate(t));
            EnemyFireRate = Mathf.Max(0.05f, level.EnemyFireRate.Evaluate(t));
            EnemyInaccuracy = Mathf.Max(0f, level.EnemyInaccuracy.Evaluate(t));
            Budget = Mathf.Max(0, Mathf.RoundToInt(level.ThreatBudget.Evaluate(t)));
        }

        // Densidad de cometas grandes (0–1) en el punto del recorrido con ese progreso. Sin nivel, la del medio.
        public float LargeCometDensityAt(float progress) =>
            level != null ? Mathf.Clamp01(level.LargeCometDensity.Evaluate(Mathf.Clamp01(progress))) : 0.5f;

        // Los spawners lo llaman al cambiar su cantidad activa (y después de crear cada amenaza, para que el próximo
        // pedido del mismo frame ya la cuente).
        public void ReportActive(ThreatKind kind, int count) => activeCounts[(int)kind] = Mathf.Max(0, count);

        // Amenazas de ese tipo que el spawner ya decidió crear pero todavía no aparecieron.
        public void ReportReserved(ThreatKind kind, int count) => reservedCounts[(int)kind] = Mathf.Max(0, count);

        // Si una amenaza más de ese tipo entra en el presupuesto, sin tocar lo comprometido por los otros tipos.
        public bool CanAfford(ThreatKind kind)
        {
            int reservedByOthers = 0;
            for (int i = 0; i < reservedCounts.Length; i++)
            {
                if (i != (int)kind) reservedByOthers += reservedCounts[i] * Cost((ThreatKind)i);
            }
            return UsedBudget + reservedByOthers + Cost(kind) <= Budget;
        }
    }
}
