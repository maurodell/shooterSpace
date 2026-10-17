using UnityEngine;

namespace NavesShooter.Core
{
    // Configuración global de runtime para mobile. Va en un GameObject de la escena Game.
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private int targetFrameRate = 60;

        private void Awake()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = targetFrameRate;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
    }
}
