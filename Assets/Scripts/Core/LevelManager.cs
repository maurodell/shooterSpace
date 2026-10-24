using NavesShooter.Level;
using NavesShooter.Player;
using UnityEngine;

namespace NavesShooter.Core
{
    // Arma el nivel a partir de `level`: la nave arranca en A (`startPoint`) mirando hacia B, y la nave nodriza queda
    // con su zona de acople en B, a `Distance` de A en la dirección del nivel, con la proa hacia A. Al entrar en la
    // zona de acople completa el nivel en el GameManager. Al reiniciar (muerte o nivel completado) la nave reaparece
    // en A: ShipLife conserva el punto de spawn que se le fija acá.
    // Corre antes que el resto para que la nave ya esté en A cuando CameraFollow se centra en su Start.
    [DefaultExecutionOrder(-100)]
    public class LevelManager : MonoBehaviour
    {
        [SerializeField] private LevelDefinition level;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private ShipLife ship;
        [SerializeField] private Mothership mothership;
        [SerializeField, Tooltip("Punto de inicio (A) en el mundo.")] private Vector2 startPoint = Vector2.zero;

        public LevelDefinition Level => level;
        public Mothership Mothership => mothership;
        public Vector2 StartPoint => startPoint;
        public Vector2 TargetPoint { get; private set; }
        public float TotalDistance => level != null ? level.Distance : 0f;

        // Distancia de la nave a B (con la nave muerta, desde donde murió).
        public float DistanceToTarget => ship != null ? Vector2.Distance(ship.transform.position, TargetPoint) : TotalDistance;

        // 0 en A, 1 en B. Alejarse de B más que la distancia total no baja de 0.
        public float Progress => TotalDistance > 0f ? Mathf.Clamp01(1f - DistanceToTarget / TotalDistance) : 0f;

        private void Awake()
        {
            if (level == null || ship == null || mothership == null)
            {
                Debug.LogError($"{nameof(LevelManager)}: faltan referencias (nivel, nave o nave nodriza).", this);
                enabled = false;
                return;
            }

            float angle = level.DirectionAngle;
            TargetPoint = startPoint + level.Direction * level.Distance;
            mothership.Place(TargetPoint, angle + 180f);
            ship.PlaceAtSpawn(startPoint, angle);
        }

        private void OnEnable()
        {
            if (mothership != null) mothership.LevelCompleted += OnLevelCompleted;
        }

        private void OnDisable()
        {
            if (mothership != null) mothership.LevelCompleted -= OnLevelCompleted;
        }

        private void OnLevelCompleted()
        {
            if (gameManager != null) gameManager.CompleteLevel();
        }

        private void OnDrawGizmosSelected()
        {
            if (level == null) return;
            Vector2 target = startPoint + level.Direction * level.Distance;
            Gizmos.color = Color.green;
            Gizmos.DrawLine(startPoint, target);
            Gizmos.DrawWireSphere(startPoint, 0.5f);
        }
    }
}
