using UnityEngine;

namespace NavesShooter.Core
{
    // Valores de balance de la munición (Settings/AmmoConfig.asset). Ammo los lee en vivo: editar en Play se nota al
    // instante y queda guardado en el asset.
    [CreateAssetMenu(fileName = "AmmoConfig", menuName = "NavesShooter/Ammo Config")]
    public class AmmoConfig : ScriptableObject
    {
        [Min(1)] public int maxAmmo = 40;
        [Min(1), Tooltip("Munición que gasta cada disparo.")] public int costPerShot = 1;
        [Min(0), Tooltip("Munición al empezar y al reaparecer (con tope en el máximo).")] public int initialAmmo = 40;

        [Header("Regeneración pasiva")]
        public bool regenEnabled = true;
        [Min(1), Tooltip("Munición que se recupera en cada tick.")] public int regenAmount = 1;
        [Min(0.05f), Tooltip("Segundos entre ticks.")] public float regenInterval = 2f;
        [Min(0f), Tooltip("Segundos sin intentar disparar antes de que empiece a regenerar (apretar disparo sin munición también cuenta).")]
        public float regenDelayAfterFire = 1.5f;
        [Range(0f, 1f), Tooltip("La regeneración se detiene al llegar a esta fracción del máximo.")]
        public float regenCapFraction = 0.15f;

        public int RegenCap => Mathf.Min(Mathf.FloorToInt(maxAmmo * regenCapFraction), maxAmmo);
    }
}
