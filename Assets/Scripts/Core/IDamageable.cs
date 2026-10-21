namespace NavesShooter.Core
{
    // Algo que reacciona a un impacto (por ahora, de un proyectil). Quien golpea no necesita conocer el tipo concreto:
    // cada implementación decide qué hace con el golpe (romperse, perder vida, ignorarlo).
    public interface IDamageable
    {
        void TakeHit();
    }
}
