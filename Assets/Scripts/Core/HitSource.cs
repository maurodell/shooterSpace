namespace NavesShooter.Core
{
    // Origen del golpe en curso, para que los eventos de destrucción (AsteroidDestroyed, EnemyDestroyed) informen si lo
    // destruido fue chocado por la nave y no por un disparo, sin cambiar IDamageable. Sirve porque todo el camino es
    // sincrónico dentro de TakeHit (en el enemigo: Health.TakeDamage → Died → EnemyShip.Explode → evento).
    public static class HitSource
    {
        public static bool Ramming { get; private set; }

        // Golpe por choque contra la nave del jugador.
        public static void Ram(IDamageable target)
        {
            Ramming = true;
            try
            {
                target.TakeHit();
            }
            finally
            {
                Ramming = false;
            }
        }
    }
}
