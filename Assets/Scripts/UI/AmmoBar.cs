using NavesShooter.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NavesShooter.UI
{
    // Barra de munición del HUD: contorno neón ámbar (NeonShapeGraphic Rectangle), relleno Image Filled horizontal con
    // un sprite blanco generado por código y divisiones cada `shotsPerDivision` disparos. Solo escucha los eventos de
    // Ammo; el Update anima hacia el último valor recibido sin consultar la munición: el relleno sigue al objetivo con un
    // lerp exponencial, la recarga (de al menos `flashMinAmount`, así la regeneración pasiva de a 1 no destella) prende
    // `flash` y escala `bar` en un pulso, por debajo de `lowFraction` el relleno parpadea lento y en 0 el contorno pasa
    // a `emptyColor`, parpadea y aparece `emptyLabel`. Parpadeos y destello con CanvasRenderer.SetAlpha (sin
    // reconstruir mallas). Tiempo sin escalar: las animaciones terminan aunque el juego se congele.
    public class AmmoBar : MonoBehaviour
    {
        [SerializeField] private Ammo ammo;

        [Header("Partes")]
        [SerializeField, Tooltip("Se escala en el pulso de recarga (pivot al centro).")] private RectTransform bar;
        [SerializeField] private NeonShapeGraphic outline;
        [SerializeField] private Image fill;
        [SerializeField, Tooltip("Contenedor de las divisiones, del mismo tamaño que el relleno.")] private RectTransform dividers;
        [SerializeField, Tooltip("Image blanca sobre la barra.")] private Graphic flash;
        [SerializeField, Tooltip("Texto \"SIN CARGA\", activo solo en 0.")] private GameObject emptyLabel;

        [Header("Colores")]
        [SerializeField] private Color color = new Color(1f, 0.72f, 0f);
        [SerializeField] private Color emptyColor = new Color(1f, 0.2f, 0.85f);
        [SerializeField, Range(0f, 1f), Tooltip("Opacidad percibida del relleno.")] private float fillAlpha = 0.8f;
        [SerializeField] private Color dividerColor = new Color(0.02f, 0.02f, 0.04f, 0.85f);
        [SerializeField, Min(0f)] private float dividerWidth = 3f;
        [SerializeField, Min(1)] private int shotsPerDivision = 10;

        [Header("Animación")]
        [SerializeField, Min(0f), Tooltip("Rapidez con que el relleno alcanza el valor (1/s).")] private float fillSharpness = 18f;
        [SerializeField, Min(1), Tooltip("Recarga mínima que destella.")] private int flashMinAmount = 2;
        [SerializeField, Range(0f, 1f), Tooltip("Opacidad percibida del destello al empezar.")] private float flashAlpha = 0.85f;
        [SerializeField, Min(0.01f)] private float flashDuration = 0.2f;
        [SerializeField, Min(1f)] private float pulseScale = 1.05f;
        [SerializeField, Min(0.01f)] private float pulseDuration = 0.15f;
        [SerializeField, Range(0f, 1f), Tooltip("Por debajo de esta fracción el relleno parpadea.")] private float lowFraction = 0.2f;
        [SerializeField, Min(0f), Tooltip("Parpadeos por segundo con poca munición.")] private float lowBlinkRate = 1.2f;
        [SerializeField, Range(0f, 1f), Tooltip("Opacidad percibida mínima del parpadeo, relativa al relleno.")] private float lowBlinkMin = 0.25f;
        [SerializeField, Min(0f), Tooltip("Parpadeos por segundo del contorno en 0.")] private float emptyBlinkRate = 2.5f;
        [SerializeField, Range(0f, 1f)] private float emptyBlinkMin = 0.2f;

        private static Sprite whiteSprite;

        private float targetFill;
        private float displayFill;
        private bool low;
        private bool empty;
        private float flashTime = -1f;
        private float pulseTime = -1f;
        private float blinkTime;
        private int builtDivisions = -1;

        private void Awake()
        {
            if (fill != null)
            {
                if (fill.sprite == null) fill.sprite = WhiteSprite();
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = (int)Image.OriginHorizontal.Left;
                fill.color = WithAlpha(color, Linear(fillAlpha));
            }
            // Guardado con alfa 0 para que no tape la barra en el Editor; el destello lo anima con SetAlpha.
            if (flash != null)
            {
                flash.color = WithAlpha(flash.color, 1f);
                flash.canvasRenderer.SetAlpha(0f);
            }
        }

        // En Start y no en OnEnable: el Awake de Ammo (que fija la munición inicial) ya corrió.
        private void Start()
        {
            if (ammo == null) return;
            SetValue(ammo.Current, ammo.Max);
            displayFill = targetFill;
            ApplyFill();
        }

        private void OnEnable()
        {
            if (ammo == null) return;
            ammo.OnAmmoChanged += SetValue;
            ammo.OnAmmoRefilled += OnRefilled;
        }

        private void OnDisable()
        {
            if (ammo == null) return;
            ammo.OnAmmoChanged -= SetValue;
            ammo.OnAmmoRefilled -= OnRefilled;
        }

        private void SetValue(int current, int max)
        {
            targetFill = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
            BuildDividers(max);

            bool wasLow = low;
            low = current > 0 && targetFill < lowFraction;
            if (low && !wasLow) blinkTime = 0f;
            if (!low && fill != null) fill.canvasRenderer.SetAlpha(1f);

            bool isEmpty = current <= 0;
            if (isEmpty == empty) return;
            empty = isEmpty;
            blinkTime = 0f;
            if (outline != null)
            {
                outline.color = empty ? emptyColor : color;
                outline.canvasRenderer.SetAlpha(1f);
            }
            if (emptyLabel != null) emptyLabel.SetActive(empty);
        }

        private void OnRefilled(int amount)
        {
            if (amount < flashMinAmount) return;
            flashTime = 0f;
            pulseTime = 0f;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (!Mathf.Approximately(displayFill, targetFill))
            {
                displayFill = Mathf.Lerp(displayFill, targetFill, 1f - Mathf.Exp(-fillSharpness * dt));
                if (Mathf.Abs(displayFill - targetFill) < 0.001f) displayFill = targetFill;
                ApplyFill();
            }

            if (low || empty)
            {
                blinkTime += dt;
                if (low && fill != null)
                    fill.canvasRenderer.SetAlpha(Linear(Blink(lowBlinkRate, lowBlinkMin)));
                if (empty && outline != null)
                    outline.canvasRenderer.SetAlpha(Linear(Blink(emptyBlinkRate, emptyBlinkMin)));
            }

            if (flashTime >= 0f && flash != null)
            {
                flashTime += dt;
                float k = Mathf.Clamp01(flashTime / flashDuration);
                flash.canvasRenderer.SetAlpha(Linear(flashAlpha * (1f - k) * (1f - k)));
                if (k >= 1f) flashTime = -1f;
            }

            if (pulseTime >= 0f && bar != null)
            {
                pulseTime += dt;
                float k = Mathf.Clamp01(pulseTime / pulseDuration);
                bar.localScale = Vector3.one * (1f + (pulseScale - 1f) * Mathf.Sin(Mathf.PI * k));
                if (k >= 1f) pulseTime = -1f;
            }
        }

        private void ApplyFill()
        {
            if (fill != null) fill.fillAmount = displayFill;
        }

        // Opacidad percibida entre `min` y 1, empezando en 1.
        private float Blink(float rate, float min) =>
            Mathf.Lerp(min, 1f, 0.5f + 0.5f * Mathf.Cos(blinkTime * rate * 2f * Mathf.PI));

        // Una división cada `shotsPerDivision` disparos; solo se rearma si cambia la cantidad.
        private void BuildDividers(int max)
        {
            if (dividers == null || ammo == null) return;
            int perDivision = shotsPerDivision * Mathf.Max(1, ammo.CostPerShot);
            int count = max > 0 ? (max - 1) / perDivision : 0;
            if (count == builtDivisions) return;
            builtDivisions = count;

            for (int i = dividers.childCount; i < count; i++)
            {
                var line = new GameObject("Divider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                line.transform.SetParent(dividers, false);
                line.GetComponent<Image>().raycastTarget = false;
            }
            for (int i = 0; i < dividers.childCount; i++)
            {
                var line = (RectTransform)dividers.GetChild(i);
                bool used = i < count;
                line.gameObject.SetActive(used);
                if (!used) continue;
                float x = (float)(i + 1) * perDivision / max;
                line.anchorMin = new Vector2(x, 0f);
                line.anchorMax = new Vector2(x, 1f);
                line.pivot = new Vector2(0.5f, 0.5f);
                line.anchoredPosition = Vector2.zero;
                line.sizeDelta = new Vector2(dividerWidth, 0f);
                line.GetComponent<Image>().color = dividerColor;
            }
        }

        private static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        // El proyecto está en espacio lineal: el blending de UI es lineal (ver TouchControlFeedback.perceptualAlpha).
        private static float Linear(float alpha) =>
            QualitySettings.activeColorSpace == ColorSpace.Linear ? Mathf.GammaToLinearSpace(alpha) : alpha;

        // Image Filled necesita un sprite: uno blanco de 4×4, creado una vez.
        private static Sprite WhiteSprite()
        {
            if (whiteSprite != null) return whiteSprite;
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "AmmoBarWhite", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f);
            whiteSprite.name = "AmmoBarWhite";
            return whiteSprite;
        }
    }
}
