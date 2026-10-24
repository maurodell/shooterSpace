using System;
using UnityEngine;

namespace NavesShooter.Core
{
    // Puntos de vida reutilizables (jugador, enemigos). Un impacto (`TakeHit`, de IDamageable) quita un punto. Tras
    // cada daño que no mata queda `invulnerabilityOnHit` segundos sin recibir más. La invulnerabilidad se mide con
    // Time.time: en pausa (timeScale 0) no corre. Quien la usa resetea con `Restore` al reaparecer o salir del pool.
    // Los efectos (parpadeo, vibración, explosión) los decide cada dueño escuchando los eventos.
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1)] private int maxHealth = 3;
        [SerializeField, Min(0f), Tooltip("Segundos sin recibir daño después de un golpe que no mata.")]
        private float invulnerabilityOnHit;

        // Después de restar los puntos; también se invoca en el golpe que mata, antes de `Died`.
        public event Action<Health> Damaged;
        public event Action<Health> Died;
        // Cualquier cambio de `Current` (daño o `Restore`), para la UI.
        public event Action<Health> Changed;

        private float invulnerableUntil;

        public int Max => maxHealth;
        public int Current { get; private set; }
        public bool IsDead => Current <= 0;
        public bool IsInvulnerable => Time.time < invulnerableUntil;

        private void Awake()
        {
            Current = maxHealth;
        }

        public void TakeHit() => TakeDamage(1);

        // Devuelve si el daño se aplicó (no estaba muerto ni invulnerable).
        public bool TakeDamage(int amount)
        {
            if (amount <= 0 || IsDead || IsInvulnerable) return false;

            Current = Mathf.Max(Current - amount, 0);
            if (!IsDead) SetInvulnerable(invulnerabilityOnHit);
            Changed?.Invoke(this);
            Damaged?.Invoke(this);
            if (IsDead) Died?.Invoke(this);
            return true;
        }

        // Vida completa y sin invulnerabilidad.
        public void Restore()
        {
            Current = maxHealth;
            invulnerableUntil = 0f;
            Changed?.Invoke(this);
        }

        // Extiende la invulnerabilidad a por lo menos `seconds` desde ahora.
        public void SetInvulnerable(float seconds)
        {
            invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + seconds);
        }
    }
}
