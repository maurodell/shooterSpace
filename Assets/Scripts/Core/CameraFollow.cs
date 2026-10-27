using UnityEngine;

namespace NavesShooter.Core
{
    // Cámara con zona muerta: el objetivo se mueve libre dentro de un rectángulo interior del safe area y la cámara
    // solo se desplaza cuando sale de él, lo justo para devolverlo al borde, suavizado con SmoothDamp. Un límite duro
    // exterior corrige al instante si el objetivo llega a acercarse demasiado al borde de la pantalla.
    // Nunca toca la rotación. Pensada para cámara ortográfica. `ShakeOffset` (sacudida del salto) se suma a la posición
    // final sin afectar el seguimiento: se descuenta al empezar el frame siguiente. Con un foco (`SetFocus`, el final del
    // nivel) deja de seguir al objetivo y se centra suavizada en ese punto hasta `ClearFocus` o el reinicio.
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        // Márgenes por lado, como fracción del ancho/alto del safe area.
        [System.Serializable]
        public struct Margins
        {
            [Range(0f, 0.5f)] public float left;
            [Range(0f, 0.5f)] public float right;
            [Range(0f, 0.5f)] public float top;
            [Range(0f, 0.5f)] public float bottom;
        }

        [SerializeField] private Transform target;
        [SerializeField, Tooltip("Dentro de este rectángulo el objetivo se mueve sin que la cámara se mueva. " +
            "Abajo es más grande para que la nave no quede bajo el joystick y el botón de disparo.")]
        private Margins deadZone = new Margins { left = 0.15f, right = 0.15f, top = 0.15f, bottom = 0.3f };
        [SerializeField, Tooltip("El objetivo nunca queda fuera de este rectángulo: si llega, la cámara corrige al instante.")]
        private Margins hardLimit = new Margins { left = 0.05f, right = 0.05f, top = 0.05f, bottom = 0.05f };
        [SerializeField, Tooltip("Segundos aproximados para que la cámara alcance la posición que deja al objetivo en el borde de la zona.")]
        private float smoothTime = 0.15f;
        [SerializeField, Tooltip("Adelanta el destino del suavizado según la velocidad con que se corre la zona, para que a velocidad " +
            "constante el objetivo quede en el borde de la zona y no ≈ velocidad × smoothTime más afuera.")]
        private bool compensateLag = true;
        [SerializeField, Min(0.01f), Tooltip("Segundos aproximados para centrarse en un foco (SetFocus).")]
        private float focusSmoothTime = 0.6f;
        [SerializeField, Tooltip("Al reiniciar la partida, la cámara salta a centrar el objetivo en vez de viajar suavizada.")]
        private GameManager gameManager;

        private Camera cam;
        private Vector3 velocity;
        // Posición (xy) a la que se suaviza la cámara. Solo se corre cuando el objetivo sale de la zona muerta medida
        // desde ella; así, con el objetivo adentro, la cámara se asienta sin rebotes.
        private Vector2 goal;
        private Vector2 goalVelocity;
        private bool snapPending;
        private Vector2 appliedShake;
        private bool hasFocus;
        private Vector2 focus;

        // Desplazamiento extra de la cámara (sacudida). Lo maneja quien la produce; 0 = sin sacudida.
        public Vector2 ShakeOffset { get; set; }

        // Zonas relativas al centro de la cámara, en unidades de mundo.
        private Rect deadZoneRect;
        private Rect hardRect;

        // Parámetros con los que se calcularon las zonas; si alguno cambia, se recalculan.
        private Rect appliedSafeArea;
        private Vector2Int appliedScreenSize;
        private float appliedOrthographicSize;
        private float appliedAspect;

        private void Awake()
        {
            cam = GetComponent<Camera>();
        }

        private void Start()
        {
            goal = transform.position;
            if (target == null) return;
            UpdateZonesIfNeeded();
            goal = GoalFor(target.position);
            transform.position = new Vector3(goal.x, goal.y, transform.position.z);
        }

        private void OnEnable()
        {
            if (gameManager != null) gameManager.Restarted += RequestSnap;
        }

        private void OnDisable()
        {
            if (gameManager != null) gameManager.Restarted -= RequestSnap;
        }

        // El salto se hace en LateUpdate, cuando el objetivo ya está en su lugar nuevo.
        private void RequestSnap()
        {
            snapPending = true;
            hasFocus = false;
        }

        public void SetFocus(Vector2 point)
        {
            hasFocus = true;
            focus = point;
        }

        public void ClearFocus() => hasFocus = false;

        private void OnValidate()
        {
            appliedScreenSize = default; // márgenes editados en Play: recalcular las zonas
        }

        private void LateUpdate()
        {
            if (target == null) return;
            UpdateZonesIfNeeded();
            transform.position -= (Vector3)appliedShake;
            appliedShake = Vector2.zero;

            Vector2 targetPosition = target.position;
            if (snapPending)
            {
                snapPending = false;
                goal = targetPosition;
                goalVelocity = Vector2.zero;
                velocity = Vector3.zero;
                transform.position = new Vector3(goal.x, goal.y, transform.position.z);
                ApplyShake();
                return;
            }

            if (hasFocus)
            {
                transform.position = Vector3.SmoothDamp(transform.position, new Vector3(focus.x, focus.y, transform.position.z), ref velocity, focusSmoothTime);
                // Al soltar el foco, la zona muerta sigue desde donde quedó la cámara.
                goal = transform.position;
                goalVelocity = Vector2.zero;
                ApplyShake();
                return;
            }

            Vector2 previousGoal = goal;
            goal = GoalFor(targetPosition);
            goalVelocity = Time.deltaTime > 0f ? (goal - previousGoal) / Time.deltaTime : Vector2.zero;

            // SmoothDamp persigue un destino en movimiento con un retraso ≈ velocidad × smoothTime: se adelanta ese tanto.
            Vector2 aim = compensateLag ? goal + goalVelocity * smoothTime : goal;
            Vector3 position = Vector3.SmoothDamp(transform.position, new Vector3(aim.x, aim.y, transform.position.z), ref velocity, smoothTime);

            // Límite duro: si el suavizado deja al objetivo fuera, la cámara salta lo justo, sin suavizado.
            Vector2 offset = targetPosition - (Vector2)position;
            Vector2 clamped = Clamp(offset, hardRect);
            if (clamped != offset)
            {
                position.x = targetPosition.x - clamped.x;
                position.y = targetPosition.y - clamped.y;
            }

            transform.position = position;
            ApplyShake();
        }

        private void ApplyShake()
        {
            appliedShake = ShakeOffset;
            transform.position += (Vector3)appliedShake;
        }

        // Corre la meta lo justo para que el objetivo quede dentro de la zona muerta medida desde ella.
        private Vector2 GoalFor(Vector2 targetPosition)
        {
            return targetPosition - Clamp(targetPosition - goal, deadZoneRect);
        }

        private void UpdateZonesIfNeeded()
        {
            Rect safeArea = Screen.safeArea;
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            if (safeArea == appliedSafeArea && screenSize == appliedScreenSize
                && cam.orthographicSize == appliedOrthographicSize && cam.aspect == appliedAspect)
                return;

            appliedSafeArea = safeArea;
            appliedScreenSize = screenSize;
            appliedOrthographicSize = cam.orthographicSize;
            appliedAspect = cam.aspect;

            Rect safeViewport = SafeAreaViewport(cam, safeArea);
            hardRect = ToCameraRect(cam, safeViewport, hardLimit);
            // La zona muerta nunca se extiende más allá del límite duro.
            Rect dead = ToCameraRect(cam, safeViewport, deadZone);
            deadZoneRect = Rect.MinMaxRect(
                Mathf.Max(dead.xMin, hardRect.xMin), Mathf.Max(dead.yMin, hardRect.yMin),
                Mathf.Min(dead.xMax, hardRect.xMax), Mathf.Min(dead.yMax, hardRect.yMax));
        }

        // Safe area (en píxeles de pantalla) llevado a coordenadas de viewport de la cámara (0..1).
        private static Rect SafeAreaViewport(Camera camera, Rect safeArea)
        {
            Rect pixels = camera.pixelRect;
            if (pixels.width <= 0f || pixels.height <= 0f) return new Rect(0f, 0f, 1f, 1f);
            return Rect.MinMaxRect(
                Mathf.Clamp01((safeArea.xMin - pixels.xMin) / pixels.width),
                Mathf.Clamp01((safeArea.yMin - pixels.yMin) / pixels.height),
                Mathf.Clamp01((safeArea.xMax - pixels.xMin) / pixels.width),
                Mathf.Clamp01((safeArea.yMax - pixels.yMin) / pixels.height));
        }

        // Aplica los márgenes al rect de viewport y lo pasa a unidades de mundo relativas al centro de la cámara.
        private static Rect ToCameraRect(Camera camera, Rect viewport, Margins margins)
        {
            float xMin = viewport.xMin + margins.left * viewport.width;
            float xMax = viewport.xMax - margins.right * viewport.width;
            float yMin = viewport.yMin + margins.bottom * viewport.height;
            float yMax = viewport.yMax - margins.top * viewport.height;
            // Márgenes que se cruzan: el rect colapsa a una línea en el punto medio.
            if (xMin > xMax) xMin = xMax = (xMin + xMax) * 0.5f;
            if (yMin > yMax) yMin = yMax = (yMin + yMax) * 0.5f;

            float halfHeight = camera.orthographicSize;
            float halfWidth = halfHeight * camera.aspect;
            return Rect.MinMaxRect(
                (xMin - 0.5f) * 2f * halfWidth, (yMin - 0.5f) * 2f * halfHeight,
                (xMax - 0.5f) * 2f * halfWidth, (yMax - 0.5f) * 2f * halfHeight);
        }

        private static Vector2 Clamp(Vector2 point, Rect rect)
        {
            return new Vector2(Mathf.Clamp(point.x, rect.xMin, rect.xMax), Mathf.Clamp(point.y, rect.yMin, rect.yMax));
        }

        // Zona muerta (verde) y límite duro (rojo) alrededor de la cámara. Fuera de Play no hay safe area confiable
        // (Screen en OnDrawGizmos es la vista Scene), así que se usa el viewport completo.
        private void OnDrawGizmos()
        {
            Camera camera = cam != null ? cam : GetComponent<Camera>();
            if (camera == null) return;

            Rect dead, hard;
            if (Application.isPlaying && cam != null)
            {
                dead = deadZoneRect;
                hard = hardRect;
            }
            else
            {
                var fullViewport = new Rect(0f, 0f, 1f, 1f);
                dead = ToCameraRect(camera, fullViewport, deadZone);
                hard = ToCameraRect(camera, fullViewport, hardLimit);
            }

            Vector3 center = transform.position;
            center.z = 0f;
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(center + (Vector3)hard.center, hard.size);
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(center + (Vector3)dead.center, dead.size);
        }
    }
}
