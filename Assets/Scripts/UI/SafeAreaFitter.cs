using UnityEngine;

namespace NavesShooter.UI
{
    // Ajusta los anchors de este RectTransform al Screen.safeArea (notch, barra inferior del iPhone).
    // Va en un hijo a pantalla completa de un Canvas Screen Space Overlay; los controles cuelgan de él.
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform rectTransform;
        private Rect appliedSafeArea;
        private Vector2Int appliedScreenSize;

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
            Apply();
        }

        // El safe area puede cambiar en runtime (rotación, Device Simulator, cambio de resolución).
        private void Update()
        {
            if (Screen.safeArea != appliedSafeArea || Screen.width != appliedScreenSize.x || Screen.height != appliedScreenSize.y)
                Apply();
        }

        private void Apply()
        {
            Rect safeArea = Screen.safeArea;
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            appliedSafeArea = safeArea;
            appliedScreenSize = screenSize;
            if (screenSize.x <= 0 || screenSize.y <= 0) return;

            rectTransform.anchorMin = new Vector2(safeArea.xMin / screenSize.x, safeArea.yMin / screenSize.y);
            rectTransform.anchorMax = new Vector2(safeArea.xMax / screenSize.x, safeArea.yMax / screenSize.y);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}
