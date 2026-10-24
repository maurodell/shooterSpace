using System.Collections.Generic;
using NavesShooter.Core;
using UnityEngine;

namespace NavesShooter.Asteroids
{
    // Campo de cometas grandes a lo largo del recorrido de A a B. Se genera una sola vez al empezar, con la semilla del
    // nivel (siempre el mismo campo para el mismo nivel), sobre una franja de `halfWidth` a cada lado de la recta A→B.
    // Distribución Poisson disk (Bridson): ningún par de centros a menos de `spacing`, y además ningún par de bordes a
    // menos de `minGap`, así siempre quedan pasillos navegables. El radio de cada cometa se elige después de ver a sus
    // vecinos: cerca de otro cometa, de A o de la nave nodriza sale más chico, o el candidato se descarta.
    // Todos los cometas son datos; solo los que están a menos de `activationRadius` de la cámara tienen un GameObject
    // del pool. Se buscan con una grilla espacial, y se desactivan recién a `activationRadius + hysteresis` para que no
    // parpadeen en el borde.
    public class LargeCometField : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private LevelManager levelManager;
        [SerializeField] private LargeComet cometPrefab;
        [SerializeField, Tooltip("Centro de la activación. Si está vacío usa Camera.main.")] private Camera targetCamera;

        [Header("Zona")]
        [SerializeField, Min(0f), Tooltip("Ancho de la franja con cometas a cada lado de la recta A→B (unidades).")]
        private float halfWidth = 20f;
        [SerializeField, Min(0f), Tooltip("Cuánto se extiende el campo detrás de A.")] private float behindStart = 12f;
        [SerializeField, Min(0f), Tooltip("Cuánto se extiende el campo más allá de B.")] private float beyondTarget = 20f;
        [SerializeField, Min(0f), Tooltip("Radio alrededor de A sin cometas.")] private float startClearance = 8f;
        [SerializeField, Min(0f), Tooltip("Espacio libre mínimo entre un cometa y la zona de acople o el casco de la nave nodriza.")]
        private float targetClearance = 5f;

        [Header("Distribución")]
        [SerializeField, Tooltip("Radio mínimo (x) y máximo (y) de un cometa.")] private Vector2 radiusRange = new Vector2(1.2f, 2.8f);
        [SerializeField, Min(0f), Tooltip("Pasillo mínimo entre los bordes de dos cometas (la nave mide ~0.65 de ancho).")]
        private float minGap = 2.5f;
        [SerializeField, Min(0.1f), Tooltip("Separación mínima entre centros con densidad 0 (LevelDefinition.CometDensity).")]
        private float sparseSpacing = 14f;
        [SerializeField, Min(0.1f), Tooltip("Separación mínima entre centros con densidad 1.")]
        private float denseSpacing = 6.5f;
        [SerializeField, Range(1, 60), Tooltip("Candidatos que se prueban alrededor de cada cometa (k de Bridson). Más = más compacto.")]
        private int candidatesPerPoint = 20;
        [SerializeField, Tooltip("Giro visual mínimo (x) y máximo (y) en valor absoluto, en grados/s.")]
        private Vector2 spinRange = new Vector2(2f, 7f);

        [Header("Activación (desde la cámara)")]
        [SerializeField, Min(0f), Tooltip("Cometas más cerca que esto tienen GameObject. Cubre el despawn de los cometas chicos " +
            "para que reboten contra los grandes también fuera de la vista. Nunca menor que la semidiagonal visible + radio máximo.")]
        private float activationRadius = 24f;
        [SerializeField, Min(0f), Tooltip("Distancia extra antes de desactivar.")] private float hysteresis = 4f;
        [SerializeField, Min(0)] private int initialPoolSize = 48;

        private const float CellSize = 8f;       // grilla espacial para la activación (unidades)
        private const float ViewMargin = 1f;     // entre el cometa y el borde visible al activarse
        private const int SeedAttempts = 50;

        private LargeCometData[] comets = new LargeCometData[0];
        private LargeComet[] instances = new LargeComet[0];
        private readonly List<int> active = new List<int>();
        private readonly Dictionary<Vector2Int, List<int>> cells = new Dictionary<Vector2Int, List<int>>();
        private ObjectPool<LargeComet> pool;

        public int Count => comets.Length;
        public int ActiveCount => active.Count;

        private void Awake()
        {
            if (levelManager == null || cometPrefab == null)
            {
                Debug.LogError($"{nameof(LargeCometField)}: faltan referencias (LevelManager o prefab).", this);
                enabled = false;
                return;
            }

            if (targetCamera == null) targetCamera = Camera.main;
            pool = new ObjectPool<LargeComet>(cometPrefab, initialPoolSize, new GameObject("LargeComets").transform);
        }

        // En Start: el LevelManager ya ubicó la nave nodriza en su Awake.
        private void Start()
        {
            if (levelManager.Level == null) return;
            Generate();
        }

        private void Update()
        {
            Vector2 center = targetCamera.transform.position;
            float activate = EffectiveActivationRadius(targetCamera);
            float deactivate = activate + hysteresis;
            float deactivateSqr = deactivate * deactivate;

            for (int i = active.Count - 1; i >= 0; i--)
            {
                int index = active[i];
                if ((comets[index].Position - center).sqrMagnitude <= deactivateSqr) continue;
                pool.Release(instances[index]);
                instances[index] = null;
                active[i] = active[active.Count - 1];
                active.RemoveAt(active.Count - 1);
            }

            float activateSqr = activate * activate;
            Vector2Int min = Cell(center - Vector2.one * activate);
            Vector2Int max = Cell(center + Vector2.one * activate);
            for (int x = min.x; x <= max.x; x++)
            {
                for (int y = min.y; y <= max.y; y++)
                {
                    if (!cells.TryGetValue(new Vector2Int(x, y), out List<int> cell)) continue;
                    foreach (int index in cell)
                    {
                        if (instances[index] != null || (comets[index].Position - center).sqrMagnitude > activateSqr) continue;
                        LargeComet comet = pool.Get(comets[index].Position, Quaternion.identity);
                        comet.Activate(comets[index]);
                        instances[index] = comet;
                        active.Add(index);
                    }
                }
            }
        }

        // Bridson con restricciones extra. Trabaja en coordenadas locales de la franja: x a lo ancho (derecha de A→B),
        // y a lo largo (0 en A, `Distance` en B).
        private void Generate()
        {
            LevelDefinition level = levelManager.Level;
            System.Random random = level.CreateRandom(RandomStream.LargeComets);
            float spacing = Mathf.Lerp(sparseSpacing, denseSpacing, level.CometDensity);

            Vector2 origin = levelManager.StartPoint;
            Vector2 forward = level.Direction;
            Vector2 right = new Vector2(forward.y, -forward.x);
            float minY = -behindStart;
            float maxY = level.Distance + beyondTarget;

            // Exclusiones (en locales): A, la zona de acople y el casco de la nave nodriza.
            var exclusions = new List<Vector3> { new Vector3(0f, 0f, startClearance) };
            var mothership = levelManager.Mothership;
            if (mothership != null)
            {
                exclusions.Add(ToLocal(mothership.DockPosition, origin, right, forward, mothership.DockRadius + targetClearance));
                exclusions.Add(ToLocal(mothership.transform.position, origin, right, forward, mothership.HullRadius + targetClearance));
            }

            // Con celdas de lado spacing/√2 cabe a lo sumo un centro por celda.
            float cellSize = spacing / Mathf.Sqrt(2f);
            int columns = Mathf.Max(1, Mathf.CeilToInt(2f * halfWidth / cellSize));
            int rows = Mathf.Max(1, Mathf.CeilToInt((maxY - minY) / cellSize));
            var grid = new int[columns * rows];
            for (int i = 0; i < grid.Length; i++) grid[i] = -1;
            int reachCells = Mathf.CeilToInt(Mathf.Max(spacing, 2f * radiusRange.y + minGap) / cellSize);

            var positions = new List<Vector2>();
            var radii = new List<float>();
            var open = new List<int>();

            bool TryAdd(Vector2 p)
            {
                if (p.x < -halfWidth || p.x >= halfWidth || p.y < minY || p.y >= maxY) return false;

                float allowed = radiusRange.y;
                foreach (Vector3 exclusion in exclusions)
                    allowed = Mathf.Min(allowed, Vector2.Distance(p, exclusion) - exclusion.z);

                int cx = Mathf.Min(columns - 1, (int)((p.x + halfWidth) / cellSize));
                int cy = Mathf.Min(rows - 1, (int)((p.y - minY) / cellSize));
                for (int y = Mathf.Max(0, cy - reachCells); y <= Mathf.Min(rows - 1, cy + reachCells); y++)
                {
                    for (int x = Mathf.Max(0, cx - reachCells); x <= Mathf.Min(columns - 1, cx + reachCells); x++)
                    {
                        int other = grid[y * columns + x];
                        if (other < 0) continue;
                        float distance = Vector2.Distance(p, positions[other]);
                        if (distance < spacing) return false;
                        allowed = Mathf.Min(allowed, distance - radii[other] - minGap);
                    }
                }

                if (allowed < radiusRange.x) return false;

                grid[cy * columns + cx] = positions.Count;
                open.Add(positions.Count);
                positions.Add(p);
                radii.Add(AsteroidShape.Range(random, radiusRange.x, allowed));
                return true;
            }

            for (int attempt = 0; attempt < SeedAttempts; attempt++)
            {
                var seed = new Vector2(AsteroidShape.Range(random, -halfWidth, halfWidth),
                    AsteroidShape.Range(random, minY, maxY));
                if (TryAdd(seed)) break;
            }

            // Cada punto abierto prueba candidatos en el anillo [spacing, 2·spacing]; si ninguno entra, se cierra.
            while (open.Count > 0)
            {
                int pick = random.Next(open.Count);
                Vector2 from = positions[open[pick]];
                bool added = false;
                for (int k = 0; k < candidatesPerPoint && !added; k++)
                {
                    float angle = AsteroidShape.Range(random, 0f, 2f * Mathf.PI);
                    float distance = AsteroidShape.Range(random, spacing, 2f * spacing);
                    added = TryAdd(from + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance);
                }
                if (added) continue;
                open[pick] = open[open.Count - 1];
                open.RemoveAt(open.Count - 1);
            }

            comets = new LargeCometData[positions.Count];
            instances = new LargeComet[positions.Count];
            cells.Clear();
            for (int i = 0; i < positions.Count; i++)
            {
                float spin = AsteroidShape.Range(random, spinRange.x, spinRange.y);
                comets[i] = new LargeCometData
                {
                    Position = origin + right * positions[i].x + forward * positions[i].y,
                    Radius = radii[i],
                    BaseAngle = AsteroidShape.Range(random, 0f, 360f),
                    Spin = random.NextDouble() < 0.5 ? spin : -spin,
                    ShapeSeed = random.Next(),
                };

                Vector2Int key = Cell(comets[i].Position);
                if (!cells.TryGetValue(key, out List<int> cell)) cells.Add(key, cell = new List<int>());
                cell.Add(i);
            }
        }

        // (x local, y local, radio libre).
        private static Vector3 ToLocal(Vector2 world, Vector2 origin, Vector2 right, Vector2 forward, float clearRadius)
        {
            Vector2 offset = world - origin;
            return new Vector3(Vector2.Dot(offset, right), Vector2.Dot(offset, forward), clearRadius);
        }

        private static Vector2Int Cell(Vector2 position) =>
            new Vector2Int(Mathf.FloorToInt(position.x / CellSize), Mathf.FloorToInt(position.y / CellSize));

        private float EffectiveActivationRadius(Camera cam)
        {
            float halfHeight = cam.orthographicSize;
            float halfViewWidth = halfHeight * cam.aspect;
            float viewHalfDiagonal = Mathf.Sqrt(halfViewWidth * halfViewWidth + halfHeight * halfHeight);
            return Mathf.Max(activationRadius, viewHalfDiagonal + radiusRange.y + ViewMargin);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.55f, 0.45f, 1f, 0.6f);
            foreach (LargeCometData comet in comets) Gizmos.DrawWireSphere(comet.Position, comet.Radius);

            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null) return;
            Vector3 center = cam.transform.position;
            center.z = 0f;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(center, EffectiveActivationRadius(cam));
        }
    }
}
