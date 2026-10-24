using UnityEngine;

namespace NavesShooter.Core
{
    // Vibración del dispositivo. En el Editor y en plataformas sin vibración no hace nada.
    public static class Haptics
    {
#if UNITY_IOS && !UNITY_EDITOR
        // Plugins/iOS/NavesHaptics.mm
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void NavesHaptics_ImpactHeavy();
#endif

        // En Android vibra `milliseconds`; en iOS es un golpe corto del Taptic Engine (UIImpactFeedbackGenerator,
        // estilo Heavy), de duración fija.
        public static void Vibrate(long milliseconds)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                {
                    if (vibrator == null) return;
                    using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                    {
                        if (version.GetStatic<int>("SDK_INT") >= 26)
                        {
                            using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                            using (var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, -1))
                                vibrator.Call("vibrate", effect);
                        }
                        else
                        {
                            vibrator.Call("vibrate", milliseconds);
                        }
                    }
                }
            }
            catch (System.Exception)
            {
                // Sin acceso al servicio nativo: vibración por defecto de Unity (más larga). Esta referencia a
                // Handheld.Vibrate es además la que hace que Unity agregue el permiso VIBRATE al manifest.
                Handheld.Vibrate();
            }
#elif UNITY_IOS && !UNITY_EDITOR
            NavesHaptics_ImpactHeavy();
#endif
        }
    }
}
