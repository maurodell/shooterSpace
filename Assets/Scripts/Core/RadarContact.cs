using UnityEngine;

namespace NavesShooter.Core
{
    // Registra este objeto en el radar mientras está activo. Pensado para prefabs de pool (enemigos, obstáculos):
    // OnEnable/OnDisable acompañan cada Get/Release. Para registrar por código, usar RadarRegistry directamente.
    public class RadarContact : MonoBehaviour
    {
        [SerializeField] private RadarContactType type = RadarContactType.Enemy;
        [SerializeField] private Color color = new Color(1f, 0.2f, 0.85f);
        [SerializeField, Tooltip("Punto que marca el radar, en coordenadas locales.")] private Vector2 localOffset;

        private void OnEnable() => RadarRegistry.Register(transform, type, color, localOffset);

        private void OnDisable() => RadarRegistry.Unregister(transform);
    }
}
