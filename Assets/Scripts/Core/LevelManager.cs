using System;
using NavesShooter.Level;
using NavesShooter.Player;
using UnityEngine;

namespace NavesShooter.Core
{
    // Arma el nivel actual de `levels`: la nave arranca en A (`startPoint`) mirando hacia B, y la nave nodriza queda
    // con su zona de acople en B, a `Distance` de A en la dirección del nivel, con la proa hacia A. Al entrar en la
    // zona de acople empieza el salto (GameManager.BeginJump) y se guarda el nivel siguiente como alcanzado; al
    // terminar el resumen, `AdvanceLevel` lo carga y reinicia la partida. Después del último nivel vuelve al primero
    // con un ciclo más (`Cycle`), que el DifficultyDirector usa para endurecer las curvas.
    // Al morir, el GameManager reinicia el nivel actual: ShipLife conserva el punto de spawn (A) que se le fija acá.
    // El nivel alcanzado (índice y ciclo) se guarda en PlayerPrefs, así al abrir el juego se continúa desde ahí.
    // Corre antes que el resto para que la nave ya esté en A cuando CameraFollow se centra en su Start.
    [DefaultExecutionOrder(-100)]
    public class LevelManager : MonoBehaviour
    {
        private const string LevelIndexKey = "NavesShooter.LevelIndex";
        private const string CycleKey = "NavesShooter.LevelCycle";

        [SerializeField, Tooltip("Niveles en orden. Después del último se vuelve al primero con la dificultad incrementada.")]
        private LevelDefinition[] levels;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private ShipLife ship;
        [SerializeField] private Mothership mothership;
        [SerializeField, Tooltip("Punto de inicio (A) en el mundo.")] private Vector2 startPoint = Vector2.zero;
        [SerializeField, Tooltip("Continuar desde el nivel guardado en PlayerPrefs. Apagado: siempre arranca en el primero.")]
        private bool loadSavedProgress = true;

        // Después de cambiar de nivel (nave nodriza y punto A ya ubicados) y antes del reinicio del GameManager.
        public event Action LevelLoaded;

        private bool advancePending;
        private int nextIndex;
        private int nextCycle;

        public LevelDefinition Level { get; private set; }
        public int LevelIndex { get; private set; }
        // Vueltas completas a la lista de niveles: 0 la primera vez.
        public int Cycle { get; private set; }
        // Número de nivel para mostrar (1, 2, 3...), contando los ciclos.
        public int LevelNumber => Cycle * Mathf.Max(1, levels.Length) + LevelIndex + 1;
        public Mothership Mothership => mothership;
        public Vector2 StartPoint => startPoint;
        public Vector2 TargetPoint { get; private set; }
        public float TotalDistance => Level != null ? Level.Distance : 0f;

        // Distancia de la nave a B (con la nave muerta, desde donde murió).
        public float DistanceToTarget => ship != null ? Vector2.Distance(ship.transform.position, TargetPoint) : TotalDistance;

        // 0 en A, 1 en B. Alejarse de B más que la distancia total no baja de 0.
        public float Progress => TotalDistance > 0f ? Mathf.Clamp01(1f - DistanceToTarget / TotalDistance) : 0f;

        private void Awake()
        {
            if (levels == null || levels.Length == 0 || Array.IndexOf(levels, null) >= 0 || ship == null || mothership == null)
            {
                Debug.LogError($"{nameof(LevelManager)}: faltan referencias (niveles, nave o nave nodriza).", this);
                enabled = false;
                return;
            }

            int index = 0, cycle = 0;
            if (loadSavedProgress)
            {
                index = Mathf.Clamp(PlayerPrefs.GetInt(LevelIndexKey, 0), 0, levels.Length - 1);
                cycle = Mathf.Max(0, PlayerPrefs.GetInt(CycleKey, 0));
            }
            Load(index, cycle);
        }

        private void OnEnable()
        {
            if (mothership != null) mothership.Docked += OnDocked;
        }

        private void OnDisable()
        {
            if (mothership != null) mothership.Docked -= OnDocked;
        }

        // Llegó a la zona de acople: el nivel siguiente ya cuenta como alcanzado (si se cierra el juego durante el
        // salto o el resumen, se continúa desde ahí).
        private void OnDocked()
        {
            if (gameManager == null || gameManager.State != GameState.Playing) return;

            nextIndex = LevelIndex + 1;
            nextCycle = Cycle;
            if (nextIndex >= levels.Length)
            {
                nextIndex = 0;
                nextCycle++;
            }
            advancePending = true;
            SaveProgress(nextIndex, nextCycle);
            gameManager.BeginJump();
        }

        // Desde el resumen: carga el nivel alcanzado y reinicia la partida en su punto A.
        public void AdvanceLevel()
        {
            if (!advancePending || gameManager == null || gameManager.State != GameState.LevelCompleted) return;
            advancePending = false;
            Load(nextIndex, nextCycle);
            LevelLoaded?.Invoke();
            gameManager.Restart();
        }

        private void Load(int index, int cycle)
        {
            LevelIndex = index;
            Cycle = cycle;
            Level = levels[index];

            float angle = Level.DirectionAngle;
            TargetPoint = startPoint + Level.Direction * Level.Distance;
            mothership.Place(TargetPoint, angle + 180f);
            ship.PlaceAtSpawn(startPoint, angle);
        }

        private static void SaveProgress(int index, int cycle)
        {
            PlayerPrefs.SetInt(LevelIndexKey, index);
            PlayerPrefs.SetInt(CycleKey, cycle);
            PlayerPrefs.Save();
        }

        [ContextMenu("Borrar progreso guardado")]
        private void ClearSavedProgress()
        {
            PlayerPrefs.DeleteKey(LevelIndexKey);
            PlayerPrefs.DeleteKey(CycleKey);
            PlayerPrefs.Save();
        }

        private void OnDrawGizmosSelected()
        {
            LevelDefinition level = Level != null ? Level : levels != null && levels.Length > 0 ? levels[0] : null;
            if (level == null) return;
            Vector2 target = startPoint + level.Direction * level.Distance;
            Gizmos.color = Color.green;
            Gizmos.DrawLine(startPoint, target);
            Gizmos.DrawWireSphere(startPoint, 0.5f);
        }
    }
}
