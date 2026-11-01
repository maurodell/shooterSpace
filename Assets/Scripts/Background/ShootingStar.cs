using NavesShooter.Core;
using UnityEngine;

namespace NavesShooter.Background
{
    // Estrella fugaz de fondo: avanza en línea recta dejando un TrailRenderer, y al terminar su vuelo deja de emitir y
    // espera a que la estela se desvanezca antes de volver al pool. `Launch` es la activación del pool: limpia la estela
    // (si no, la primera franja une la posición vieja con la nueva, cruzando la pantalla) y resetea timers.
    [RequireComponent(typeof(TrailRenderer))]
    public class ShootingStar : MonoBehaviour
    {
        private TrailRenderer trail;
        private ObjectPool<ShootingStar> pool;
        private Vector3 velocity;
        private float flightRemaining;
        private float fadeRemaining;

        private void Awake()
        {
            trail = GetComponent<TrailRenderer>();
        }

        // `widthScale` agranda la estela para que a la profundidad del lanzamiento se vea del ancho configurado.
        public void Launch(ObjectPool<ShootingStar> pool, Vector3 velocity, float flightTime, float widthScale, float baseWidth)
        {
            this.pool = pool;
            this.velocity = velocity;
            flightRemaining = flightTime;
            fadeRemaining = trail.time;
            trail.widthMultiplier = baseWidth * widthScale;
            trail.Clear();
            trail.emitting = true;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            transform.position += velocity * dt;

            if (flightRemaining > 0f)
            {
                flightRemaining -= dt;
                if (flightRemaining <= 0f) trail.emitting = false;
                return;
            }

            fadeRemaining -= dt;
            if (fadeRemaining <= 0f) pool.Release(this);
        }
    }
}
