using NavesShooter.Asteroids;
using NavesShooter.Background;
using NavesShooter.Core;
using NavesShooter.Enemies;
using NavesShooter.Player;
using UnityEngine;

namespace NavesShooter.Level
{
    // Final del nivel, del acople al resumen. Empieza cuando el GameManager pasa a Jumping (LevelManager, al entrar en la
    // zona de acople):
    // 1. Acople (`alignDuration`): los cometas chicos y los enemigos activos explotan, los controles táctiles se
    //    desvanecen, la nave, con piloto automático, va al centro de la zona de acople mirando a la nave nodriza, y la
    //    cámara se centra en las dos naves.
    // 2. Carga (`chargeDuration`): las dos naves brillan cada vez más (NeonGlow) y aparece "SALTO A LA LUZ".
    // 3. Salto (`jumpDuration`): hiperespacio en el fondo (StarfieldParallax.Warp, en la dirección del nivel), zoom
    //    hacia adentro con sacudida creciente y, al final, un destello blanco que cubre la pantalla. En el pico las
    //    dos naves desaparecen y el GameManager pasa a LevelCompleted (resumen, juego congelado).
    // Durante el resumen el destello se desvanece y el fondo sigue en hiperespacio a `cruiseWarp` (tiempo sin escalar).
    // Al reiniciar (nivel siguiente) todo vuelve a su estado normal y el hiperespacio se frena en `exitDuration`.
    public class LevelEndSequence : MonoBehaviour
    {
        private enum Phase { None, Align, Charge, Jump, Summary, Exit }

        [Header("Referencias")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private LevelManager levelManager;
        [SerializeField] private ShipController ship;
        [SerializeField, Tooltip("Brillo de la nave del jugador.")] private NeonGlow shipGlow;
        [SerializeField, Tooltip("Brillo de la nave nodriza.")] private NeonGlow mothershipGlow;
        [SerializeField] private AsteroidSpawner asteroidSpawner;
        [SerializeField] private EnemySpawner enemySpawner;
        [SerializeField] private StarfieldParallax starfield;
        [SerializeField] private CameraFollow cameraFollow;
        [SerializeField, Tooltip("Texto \"SALTO A LA LUZ\" (CanvasGroup del texto).")] private CanvasGroup jumpText;
        [SerializeField, Tooltip("Destello blanco a pantalla completa.")] private CanvasGroup flash;
        [SerializeField, Tooltip("Controles táctiles: se desvanecen durante el final del nivel.")] private CanvasGroup touchControls;

        [Header("Tiempos (s)")]
        [SerializeField, Min(0.01f)] private float alignDuration = 1f;
        [SerializeField, Min(0.01f)] private float chargeDuration = 1.5f;
        [SerializeField, Min(0.01f)] private float jumpDuration = 1.5f;
        [SerializeField, Min(0.01f), Tooltip("Desvanecimiento del destello sobre el resumen.")] private float flashFadeOut = 0.6f;
        [SerializeField, Min(0.01f), Tooltip("Frenado del hiperespacio al empezar el nivel siguiente.")] private float exitDuration = 1.2f;
        [SerializeField, Min(0.01f), Tooltip("Fundido de los controles táctiles.")] private float controlsFade = 0.3f;

        [Header("Salto")]
        [SerializeField, Range(0.5f, 1f), Tooltip("orthographicSize al final del salto, en fracción del normal.")]
        private float zoom = 0.88f;
        [SerializeField, Min(0f), Tooltip("Amplitud máxima de la sacudida de cámara (unidades).")] private float shakeAmplitude = 0.3f;
        [SerializeField, Min(0f), Tooltip("Frecuencia de la sacudida.")] private float shakeFrequency = 22f;
        [SerializeField, Range(0f, 1f), Tooltip("Fracción del salto en la que empieza el destello.")] private float flashStart = 0.6f;
        [SerializeField, Range(0f, 1f), Tooltip("Hiperespacio durante el resumen.")] private float cruiseWarp = 0.5f;

        [Header("Texto")]
        [SerializeField, Min(0f), Tooltip("Pulsos por segundo del texto.")] private float textPulseRate = 2.5f;
        [SerializeField, Min(0f), Tooltip("Escala extra del texto en el pulso.")] private float textPulseScale = 0.06f;

        private Phase phase;
        private float time;
        private float exitStartWarp;
        private float baseOrthographicSize;
        private Camera cam;

        private void Awake()
        {
            if (cameraFollow != null)
            {
                cam = cameraFollow.GetComponent<Camera>();
                baseOrthographicSize = cam.orthographicSize;
            }
            SetText(0f, 1f);
            SetFlash(0f);
        }

        private void OnEnable()
        {
            if (gameManager == null) return;
            gameManager.StateChanged += OnStateChanged;
            gameManager.Restarted += OnRestarted;
        }

        private void OnDisable()
        {
            if (gameManager == null) return;
            gameManager.StateChanged -= OnStateChanged;
            gameManager.Restarted -= OnRestarted;
        }

        private void OnStateChanged(GameState state)
        {
            if (state == GameState.Jumping) Begin();
        }

        private void Begin()
        {
            Mothership mothership = levelManager.Mothership;
            if (asteroidSpawner != null) asteroidSpawner.ExplodeAll();
            if (enemySpawner != null) enemySpawner.ExplodeAll();
            mothership.DockVisible = false;

            // Mirando a la nave nodriza: la dirección del nivel (A→B).
            LevelDefinition level = levelManager.Level;
            ship.StartAutopilot(mothership.DockPosition, level.DirectionAngle);
            if (starfield != null) starfield.WarpDirection = level.Direction;

            // Centro del conjunto: de la zona de acople a la popa de la nave nodriza.
            Vector2 center = mothership.transform.position;
            Vector2 bow = (mothership.DockPosition - center).normalized;
            if (cameraFollow != null) cameraFollow.SetFocus((mothership.DockPosition + center - bow * mothership.HullRadius) * 0.5f);

            phase = Phase.Align;
            time = 0f;
        }

        private void Update()
        {
            UpdateControls();
            if (phase == Phase.None) return;

            bool unscaled = phase == Phase.Summary || phase == Phase.Exit;
            time += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;

            switch (phase)
            {
                case Phase.Align:
                    if (time >= alignDuration) Next(Phase.Charge);
                    break;

                case Phase.Charge:
                {
                    float k = Mathf.Clamp01(time / chargeDuration);
                    SetGlow(k * k);
                    SetText(Mathf.Clamp01(time / 0.3f), Pulse());
                    if (k >= 1f) Next(Phase.Jump);
                    break;
                }

                case Phase.Jump:
                {
                    float k = Mathf.Clamp01(time / jumpDuration);
                    SetGlow(1f);
                    SetText(1f - Mathf.Clamp01(k / 0.4f), Pulse());
                    if (starfield != null) starfield.Warp = k * k;
                    if (cam != null) cam.orthographicSize = baseOrthographicSize * Mathf.Lerp(1f, zoom, Mathf.SmoothStep(0f, 1f, k));
                    SetShake(shakeAmplitude * k);
                    SetFlash(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(flashStart, 1f, k)));
                    if (k >= 1f) Jumped();
                    break;
                }

                case Phase.Summary:
                {
                    float k = Mathf.Clamp01(time / flashFadeOut);
                    SetFlash(1f - k);
                    if (starfield != null) starfield.Warp = Mathf.Lerp(1f, cruiseWarp, k);
                    break;
                }

                case Phase.Exit:
                {
                    float k = Mathf.Clamp01(time / exitDuration);
                    if (starfield != null) starfield.Warp = exitStartWarp * (1f - k) * (1f - k);
                    if (k >= 1f) phase = Phase.None;
                    break;
                }
            }
        }

        // Pico del salto: las naves ya se fueron, el destello cubre todo y empieza el resumen.
        private void Jumped()
        {
            SetShake(0f);
            SetFlash(1f);
            SetText(0f, 1f);
            ship.gameObject.SetActive(false);
            levelManager.Mothership.gameObject.SetActive(false);
            Next(Phase.Summary);
            gameManager.CompleteLevel();
        }

        // Muerte o nivel siguiente: todo a su estado normal. Si venía del resumen, el hiperespacio se frena de a poco.
        private void OnRestarted()
        {
            bool fromSummary = phase == Phase.Summary;
            exitStartWarp = starfield != null ? starfield.Warp : 0f;
            phase = fromSummary && exitStartWarp > 0f ? Phase.Exit : Phase.None;
            time = 0f;
            if (!fromSummary && starfield != null) starfield.Warp = 0f;

            Mothership mothership = levelManager.Mothership;
            mothership.gameObject.SetActive(true);
            if (cameraFollow != null) cameraFollow.ClearFocus();
            mothership.DockVisible = true;
            SetGlow(0f);
            SetShake(0f);
            SetFlash(0f);
            SetText(0f, 1f);
            if (cam != null) cam.orthographicSize = baseOrthographicSize;
        }

        private void Next(Phase next)
        {
            phase = next;
            time = 0f;
        }

        // Fuera del gameplay normal los controles no responden: se desvanecen y vuelven al reiniciar.
        private void UpdateControls()
        {
            if (touchControls == null) return;
            bool hidden = phase == Phase.Align || phase == Phase.Charge || phase == Phase.Jump || phase == Phase.Summary;
            touchControls.alpha = Mathf.MoveTowards(touchControls.alpha, hidden ? 0f : 1f, Time.unscaledDeltaTime / controlsFade);
        }

        private float Pulse() => 1f + textPulseScale * Mathf.Sin(time * textPulseRate * 2f * Mathf.PI);

        private void SetGlow(float value)
        {
            if (shipGlow != null) shipGlow.Glow = value;
            if (mothershipGlow != null) mothershipGlow.Glow = value;
        }

        private void SetShake(float amplitude)
        {
            if (cameraFollow == null) return;
            if (amplitude <= 0f)
            {
                cameraFollow.ShakeOffset = Vector2.zero;
                return;
            }
            float t = Time.time * shakeFrequency;
            cameraFollow.ShakeOffset = new Vector2(Mathf.PerlinNoise(t, 0.37f) - 0.5f, Mathf.PerlinNoise(0.71f, t) - 0.5f) * (2f * amplitude);
        }

        private void SetText(float alpha, float scale)
        {
            if (jumpText == null) return;
            jumpText.alpha = alpha;
            jumpText.transform.localScale = new Vector3(scale, scale, 1f);
            bool shown = alpha > 0f;
            if (jumpText.gameObject.activeSelf != shown) jumpText.gameObject.SetActive(shown);
        }

        private void SetFlash(float alpha)
        {
            if (flash == null) return;
            flash.alpha = alpha;
            bool shown = alpha > 0f;
            if (flash.gameObject.activeSelf != shown) flash.gameObject.SetActive(shown);
        }
    }
}
