using System;
using NavesShooter.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace NavesShooter.Pickups
{
    // Cristal de munición recogido, tal como estaba al tocarlo. `Velocity` es la de la nave que lo recogió.
    public readonly struct PickupCollected
    {
        public readonly Vector2 Position;
        public readonly Vector2 Velocity;
        public readonly int Amount;

        public PickupCollected(Vector2 position, Vector2 velocity, int amount)
        {
            Position = position;
            Velocity = velocity;
            Amount = amount;
        }
    }

    // Cristal de munición: hexágono alargado con contorno neón ámbar, una faceta interior y un halo, todo generado por
    // código. Deriva despacio en una dirección al azar, gira sobre sí mismo y pulsa en brillo para que se vea sobre el
    // fondo oscuro. Dentro de `magnetRadius` de la nave se acelera hacia ella; al tocarla suma `ammoAmount` con Ammo.Add,
    // vuelve al pool y avisa por `Collected` (chispas, sonido). Con la munición llena no se atrae ni se recoge: sigue
    // flotando. Los del recorrido (`RouteIndex` ≥ 0) derivan sin alejarse más de `wanderRadius` de su punto y no
    // expiran; los soltados (-1) salen despedidos, frenan hasta la deriva y desaparecen a los `lifetime` segundos,
    // parpadeando los últimos `blinkDuration`. Valores de balance en la sección Pickups del GameConfig, que le pasa el
    // PickupSpawner en `Launch`. Layer `Pickup`: el trigger solo detecta al jugador.
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(LineRenderer))]
    public class AmmoPickup : MonoBehaviour
    {
        // Se invoca después de devolver el cristal al pool.
        public static event Action<PickupCollected> Collected;

        [Header("Forma")]
        [SerializeField, Min(0.05f), Tooltip("Medio alto del cristal (unidades).")] private float halfHeight = 0.32f;
        [SerializeField, Min(0.05f), Tooltip("Medio ancho del cristal (unidades).")] private float halfWidth = 0.17f;
        [SerializeField, Range(0f, 1f), Tooltip("Altura de los hombros del hexágono, como fracción del medio alto.")]
        private float shoulder = 0.45f;
        [SerializeField, Range(0.1f, 0.95f), Tooltip("Escala de la faceta interior.")] private float innerScale = 0.55f;
        [SerializeField, Min(0.05f), Tooltip("Radio del trigger de recolección (un poco más grande que el dibujo).")]
        private float pickupRadius = 0.4f;

        [Header("Render")]
        [SerializeField] private Color neonColor = new Color(1f, 0.72f, 0f);
        [SerializeField] private float lineWidth = 0.05f;
        [SerializeField, Range(0f, 1f), Tooltip("Brillo de la faceta interior respecto del contorno.")] private float innerBrightness = 0.55f;
        [SerializeField] private float innerWidth = 0.03f;
        [SerializeField] private float haloWidth = 0.22f;
        [SerializeField, Range(0f, 1f)] private float haloAlpha = 0.2f;

        [Header("Pulso y parpadeo")]
        [SerializeField, Min(0f), Tooltip("Pulsos de brillo por segundo.")] private float pulseRate = 1.4f;
        [SerializeField, Range(0f, 1f), Tooltip("Brillo mínimo del pulso.")] private float pulseMin = 0.5f;
        [SerializeField, Min(0f), Tooltip("Parpadeos por segundo al final de la vida (el doble en el último segundo).")]
        private float blinkRate = 4f;

        private const string InnerName = "Inner";
        private const string HaloName = "Halo";
        private const float WanderTurn = 60f;   // desvío al azar al volver hacia el punto de origen (grados)

        private Rigidbody2D body;
        private CircleCollider2D trigger;
        private LineRenderer outline;
        private LineRenderer inner;
        private LineRenderer halo;
        private ObjectPool<AmmoPickup> pool;
        private GameConfig.PickupSettings settings;
        private Ammo ammo;
        private Vector2 drift;
        private Vector2 anchor;
        private float age;
        private float phase;
        private bool pendingFirstStep;
        private bool visible = true;

        // Índice en el recorrido del PickupSpawner, o -1 si lo soltó algo destruido.
        public int RouteIndex { get; private set; } = -1;
        public Vector2 Position => body.position;
        public float PickupRadius => pickupRadius;
        private bool Expires => RouteIndex < 0;

        private void Reset()
        {
            var rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        private void Awake()
        {
            CacheComponents();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            trigger.isTrigger = true;
            ApplyShape();
        }

        // Activación desde el pool, después de pool.Get (que ya lo ubicó). `velocity` es la inicial (los soltados salen
        // despedidos); después tiende a la deriva. `routeIndex` -1 = soltado: expira.
        public void Launch(ObjectPool<AmmoPickup> owner, GameConfig.PickupSettings pickupSettings, Ammo playerAmmo,
            Vector2 velocity, int routeIndex = -1)
        {
            pool = owner;
            settings = pickupSettings;
            ammo = playerAmmo;
            RouteIndex = routeIndex;
            anchor = transform.position;
            age = 0f;
            phase = Random.value;

            float angle = Random.Range(0f, 2f * Mathf.PI);
            drift = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(settings.driftSpeedRange.x, settings.driftSpeedRange.y);

            // Mismo truco que Asteroid: sin interpolar hasta el primer paso, para no dibujarse un frame en la pose vieja.
            body.interpolation = RigidbodyInterpolation2D.None;
            pendingFirstStep = true;
            body.position = transform.position;
            body.rotation = transform.eulerAngles.z;
            body.linearVelocity = velocity;
            float spin = Random.Range(settings.spinRange.x, settings.spinRange.y);
            body.angularVelocity = Random.value < 0.5f ? spin : -spin;

            SetVisible(true);
            ApplyPulse();
        }

        private void FixedUpdate()
        {
            if (pendingFirstStep)
            {
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                pendingFirstStep = false;
            }
            if (settings == null) return;

            float dt = Time.fixedDeltaTime;
            Vector2 position = body.position;

            // Del recorrido: al salirse del círculo de deriva, vuelve hacia su punto con un desvío al azar.
            if (!Expires)
            {
                Vector2 fromAnchor = position - anchor;
                if (fromAnchor.sqrMagnitude > settings.wanderRadius * settings.wanderRadius && Vector2.Dot(drift, fromAnchor) > 0f)
                    drift = Quaternion.Euler(0f, 0f, Random.Range(-WanderTurn, WanderTurn)) * (-fromAnchor.normalized * drift.magnitude);
            }

            Vector2 velocity = body.linearVelocity;
            if (CanCollect())
            {
                Vector2 toShip = (Vector2)ammo.transform.position - position;
                float distance = toShip.magnitude;
                if (distance < settings.magnetRadius && distance > 0.0001f)
                {
                    body.linearVelocity = Vector2.MoveTowards(velocity, toShip / distance * settings.magnetSpeed, settings.magnetAcceleration * dt);
                    return;
                }
            }
            body.linearVelocity = Vector2.MoveTowards(velocity, drift, settings.driftDeceleration * dt);
        }

        private void Update()
        {
            if (settings == null) return;

            if (Expires)
            {
                age += Time.deltaTime;
                float remaining = settings.lifetime - age;
                if (remaining <= 0f)
                {
                    Despawn();
                    return;
                }
                float rate = remaining < 1f ? 2f * blinkRate : blinkRate;
                SetVisible(remaining > settings.blinkDuration || Mathf.Repeat(remaining * rate, 1f) > 0.35f);
            }

            if (visible) ApplyPulse();
        }

        private void OnTriggerEnter2D(Collider2D other) => TryCollect(other);

        // Con la munición llena no se recoge al entrar: si se gasta mientras sigue encima, se recoge en este.
        private void OnTriggerStay2D(Collider2D other) => TryCollect(other);

        private void TryCollect(Collider2D other)
        {
            if (!gameObject.activeSelf || !CanCollect()) return;
            Rigidbody2D shipBody = other.attachedRigidbody;
            if (shipBody == null || shipBody.transform != ammo.transform) return;

            int added = ammo.Add(settings.ammoAmount);
            var info = new PickupCollected(body.position, shipBody.linearVelocity, added);
            Despawn();
            Collected?.Invoke(info);
        }

        // Con la nave muerta (inactiva) o la munición llena, ni imán ni recolección.
        private bool CanCollect() => ammo != null && ammo.isActiveAndEnabled && !ammo.IsFull;

        public void Despawn()
        {
            if (pool != null) pool.Release(this);
            else gameObject.SetActive(false);
        }

        // Sin recarga de dominio al entrar en Play, los estáticos sobreviven entre sesiones.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Collected = null;

        private void ApplyPulse()
        {
            float k = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * (Time.time * pulseRate + phase));
            float brightness = Mathf.Lerp(pulseMin, 1f, k);
            SetColor(outline, neonColor * brightness, 1f);
            SetColor(inner, neonColor * (innerBrightness * brightness), 1f);
            SetColor(halo, neonColor, haloAlpha * Mathf.Lerp(0.4f, 1f, k));
        }

        private static void SetColor(LineRenderer line, Color color, float alpha)
        {
            color.a = alpha;
            line.startColor = color;
            line.endColor = color;
        }

        private void SetVisible(bool value)
        {
            if (visible == value) return;
            visible = value;
            outline.enabled = value;
            inner.enabled = value;
            halo.enabled = value;
        }

        [ContextMenu("Aplicar forma")]
        private void ApplyShape()
        {
            CacheComponents();
            trigger.radius = pickupRadius;

            float h = halfHeight, w = halfWidth, s = halfHeight * shoulder;
            SetPoints(outline, h, w, s, 1f);
            SetPoints(halo, h, w, s, 1f);
            // Faceta interior: rombo (sin hombros) a escala.
            SetPoints(inner, h, w, 0f, innerScale);

            Style(outline, lineWidth, outline.sortingOrder);
            Style(inner, innerWidth, outline.sortingOrder);
            Style(halo, haloWidth, outline.sortingOrder - 1);
            visible = true;
            outline.enabled = inner.enabled = halo.enabled = true;
            ApplyPulse();
        }

        // Hexágono alargado (punta arriba y abajo); con `shoulderY` 0 queda un rombo de 4 vértices.
        private static void SetPoints(LineRenderer line, float h, float w, float shoulderY, float scale)
        {
            if (shoulderY <= 0f)
            {
                line.positionCount = 4;
                line.SetPosition(0, new Vector3(0f, h) * scale);
                line.SetPosition(1, new Vector3(w, 0f) * scale);
                line.SetPosition(2, new Vector3(0f, -h) * scale);
                line.SetPosition(3, new Vector3(-w, 0f) * scale);
                return;
            }
            line.positionCount = 6;
            line.SetPosition(0, new Vector3(0f, h) * scale);
            line.SetPosition(1, new Vector3(w, shoulderY) * scale);
            line.SetPosition(2, new Vector3(w, -shoulderY) * scale);
            line.SetPosition(3, new Vector3(0f, -h) * scale);
            line.SetPosition(4, new Vector3(-w, -shoulderY) * scale);
            line.SetPosition(5, new Vector3(-w, shoulderY) * scale);
        }

        private void Style(LineRenderer line, float width, int sortingOrder)
        {
            line.useWorldSpace = false;
            line.loop = true;
            line.widthMultiplier = width;
            line.numCornerVertices = 2;
            line.alignment = LineAlignment.View;
            line.sortingOrder = sortingOrder;
            if (line != outline) line.sharedMaterial = outline.sharedMaterial;
        }

        private void CacheComponents()
        {
            body = GetComponent<Rigidbody2D>();
            trigger = GetComponent<CircleCollider2D>();
            outline = GetComponent<LineRenderer>();
            inner = Child(InnerName);
            halo = Child(HaloName);
        }

        private LineRenderer Child(string childName)
        {
            Transform child = transform.Find(childName);
            if (child == null)
            {
                child = new GameObject(childName, typeof(LineRenderer)).transform;
                child.SetParent(transform, false);
                child.gameObject.layer = gameObject.layer;
            }
            return child.GetComponent<LineRenderer>();
        }
    }
}
