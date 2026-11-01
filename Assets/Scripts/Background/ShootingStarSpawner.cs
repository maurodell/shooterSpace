using NavesShooter.Core;
using UnityEngine;

namespace NavesShooter.Background
{
    // Lanza una estrella fugaz cada `intervalRange` segundos (al azar), en dirección al azar, a una profundidad al azar
    // de `depthRange` detrás del gameplay. Cada una cruza un punto del área visible en su z (sin el margen
    // `edgeInset`) a mitad de su vuelo, así siempre se ve pasar. Velocidad y ancho se definen como se ven en el plano
    // del gameplay y se escalan con la profundidad. Usa tiempo escalado: en pausa y en el resumen se detiene.
    // Pool bajo `ShootingStars` en la raíz.
    public class ShootingStarSpawner : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private ShootingStar prefab;
        [SerializeField, Min(1)] private int initialPoolSize = 3;
        [SerializeField, Tooltip("Segundos entre estrellas (mín, máx).")]
        private Vector2 intervalRange = new Vector2(3f, 8f);
        [SerializeField, Tooltip("Profundidad (z del mundo) mínima y máxima.")]
        private Vector2 depthRange = new Vector2(40f, 150f);
        [SerializeField, Tooltip("Velocidad vista en el plano del gameplay (mín, máx), en unidades/s.")]
        private Vector2 speedRange = new Vector2(18f, 28f);
        [SerializeField, Tooltip("Duración del vuelo (mín, máx), en segundos; después la estela se desvanece.")]
        private Vector2 flightTimeRange = new Vector2(0.5f, 0.9f);
        [SerializeField, Min(0f), Tooltip("Ancho de la estela visto en el plano del gameplay, en unidades.")]
        private float width = 0.08f;
        [SerializeField, Range(0f, 0.5f), Tooltip("Margen del área visible por el que no pasa el centro del vuelo, en fracción.")]
        private float edgeInset = 0.15f;

        private ObjectPool<ShootingStar> pool;
        private float timer;

        private void Start()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null || prefab == null)
            {
                Debug.LogError("ShootingStarSpawner: falta asignar la cámara o el prefab.", this);
                enabled = false;
                return;
            }
            pool = new ObjectPool<ShootingStar>(prefab, initialPoolSize, new GameObject("ShootingStars").transform);
            timer = Random.Range(intervalRange.x, intervalRange.y);
        }

        private void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = Random.Range(intervalRange.x, intervalRange.y);
            Launch();
        }

        private void Launch()
        {
            Vector3 cam = targetCamera.transform.position;
            float gameplayDistance = Mathf.Max(0.01f, -cam.z);
            float depth = Random.Range(depthRange.x, depthRange.y);
            float scale = (depth - cam.z) / gameplayDistance;
            Vector2 half = CameraView.HalfExtents(targetCamera, depth) * (1f - edgeInset);

            float angle = Random.Range(0f, 2f * Mathf.PI);
            var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            Vector3 velocity = direction * (Random.Range(speedRange.x, speedRange.y) * scale);
            float flightTime = Random.Range(flightTimeRange.x, flightTimeRange.y);

            // A mitad del vuelo pasa por `middle`.
            var middle = new Vector3(cam.x + Random.Range(-half.x, half.x), cam.y + Random.Range(-half.y, half.y), depth);
            Vector3 start = middle - velocity * (flightTime * 0.5f);

            ShootingStar star = pool.Get(start, Quaternion.identity);
            star.Launch(pool, velocity, flightTime, scale, width);
        }
    }
}
