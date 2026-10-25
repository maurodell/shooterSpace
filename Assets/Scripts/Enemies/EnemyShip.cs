using System;
using NavesShooter.Core;
using NavesShooter.Weapons;
using UnityEngine;
using Random = UnityEngine.Random;

namespace NavesShooter.Enemies
{
    // Nave enemiga destruida, tal como estaba al explotar. No se emite cuando vuelve al pool por distancia o reinicio.
    public readonly struct EnemyDestroyed
    {
        public readonly Vector2 Position;
        public readonly Vector2 Velocity;
        public readonly int Points;

        public EnemyDestroyed(Vector2 position, Vector2 velocity, int points)
        {
            Position = position;
            Velocity = velocity;
            Points = points;
        }
    }

    // Enemigo básico "Cazador": flecha neón roja que, como el jugador, avanza siempre hacia donde apunta y gira con
    // velocidad angular limitada (con aceleración), así se lo puede esquivar cortándole el giro.
    // Estados: Approach (vuela hacia el jugador), Attack (dentro de `attackRange`, más lento, dispara ráfagas cortas
    // hacia donde apunta con algo de imprecisión, solo si el jugador está dentro de `fireCone`) y Reposition (si quedó a
    // menos de `tooClose`, se abre en diagonal alejándose y vuelve a entrar). Esquiva los obstáculos inmóviles (cometas
    // grandes, casco de la nave nodriza) con un circle cast hacia adelante; los cuerpos dinámicos (cometas chicos) no
    // cuentan como obstáculo. Vida en Health: parpadea al recibir un impacto y al morir vuelve al pool y avisa por
    // `Destroyed` (explosión, puntaje). Sale del pool con `Launch`, que resetea todo su estado.
    [RequireComponent(typeof(Rigidbody2D), typeof(PolygonCollider2D), typeof(LineRenderer))]
    [RequireComponent(typeof(Health))]
    public class EnemyShip : MonoBehaviour
    {
        public enum State { Approach, Attack, Reposition }

        // Se invoca después de devolver la nave al pool.
        public static event Action<EnemyDestroyed> Destroyed;

        [Header("Movimiento")]
        [SerializeField, Tooltip("Velocidad al acercarse (unidades/s). Menor que la del jugador, para que pueda escaparle.")]
        private float approachSpeed = 3.2f;
        [SerializeField, Tooltip("Velocidad mientras ataca.")] private float attackSpeed = 1.8f;
        [SerializeField, Tooltip("Velocidad al alejarse para reposicionarse.")] private float repositionSpeed = 4.2f;
        [SerializeField, Tooltip("Unidades/s² al cambiar de velocidad.")] private float linearAcceleration = 4f;
        [SerializeField, Tooltip("Grados por segundo.")] private float maxTurnSpeed = 110f;
        [SerializeField, Tooltip("Grados/s² al acelerar o frenar el giro.")] private float angularAcceleration = 400f;

        [Header("Distancias al jugador")]
        [SerializeField, Min(0f), Tooltip("Más cerca que esto empieza a atacar.")] private float attackRange = 7.5f;
        [SerializeField, Min(0f), Tooltip("Atacando, más lejos que esto vuelve a acercarse.")] private float disengageRange = 10f;
        [SerializeField, Min(0f), Tooltip("Más cerca que esto se aleja para reposicionarse.")] private float tooClose = 3f;
        [SerializeField, Min(0f), Tooltip("Reposicionándose, a esta distancia vuelve a acercarse.")] private float repositionDistance = 8f;
        [SerializeField, Min(0f), Tooltip("Segundos máximos reposicionándose.")] private float repositionMaxTime = 2.5f;
        [SerializeField, Range(0f, 90f), Tooltip("Desvío de la huida respecto de alejarse en línea recta del jugador (grados).")]
        private float repositionAngle = 50f;

        [Header("Disparo")]
        [SerializeField, Min(1), Tooltip("Disparos por ráfaga.")] private int burstCount = 3;
        [SerializeField, Min(0.01f), Tooltip("Segundos entre disparos de una ráfaga.")] private float burstInterval = 0.15f;
        [SerializeField, Min(0f), Tooltip("Segundos entre ráfagas.")] private float burstCooldown = 2.2f;
        [SerializeField, Min(0f), Tooltip("Segundos desde que empieza a atacar hasta la primera ráfaga.")] private float firstBurstDelay = 0.8f;
        [SerializeField, Range(0f, 90f), Tooltip("Solo empieza una ráfaga si el jugador está a menos de este ángulo del frente.")]
        private float fireCone = 20f;
        [SerializeField, Range(0f, 30f), Tooltip("Desvío al azar de cada disparo, en grados a cada lado.")] private float inaccuracy = 6f;
        [SerializeField, Tooltip("Velocidad del proyectil relativa a la nave (unidades/s).")] private float projectileSpeed = 6f;
        [SerializeField, Min(0.05f)] private float projectileLifetime = 2.5f;
        [SerializeField, Tooltip("Punto de salida en coordenadas locales (la punta).")] private Vector2 muzzleOffset = new Vector2(0f, 0.55f);

        [Header("Esquive")]
        [SerializeField, Tooltip("Layers con obstáculos a esquivar. Solo cuentan los cuerpos que no son dinámicos.")]
        private LayerMask avoidLayers;
        [SerializeField, Min(0f), Tooltip("Largo del circle cast hacia adelante.")] private float lookAhead = 4f;
        [SerializeField, Min(0.01f), Tooltip("Radio del circle cast (un poco más que la nave).")] private float avoidRadius = 0.6f;

        [Header("Daño")]
        [SerializeField, Min(0), Tooltip("Puntos al destruirla.")] private int points = 250;
        [SerializeField, Min(0f), Tooltip("Segundos de parpadeo al recibir un impacto.")] private float flashDuration = 0.25f;
        [SerializeField, Min(0.1f), Tooltip("Parpadeos por segundo.")] private float blinkRate = 20f;

        [Header("Forma")]
        [SerializeField, Tooltip("Vértices locales del contorno y del collider. La punta mira a +Y.")]
        private Vector2[] shape =
        {
            new Vector2(0f, 0.55f),
            new Vector2(0.45f, -0.4f),
            new Vector2(0f, -0.12f),
            new Vector2(-0.45f, -0.4f),
        };
        [SerializeField] private float lineWidth = 0.07f;
        [SerializeField] private Color neonColor = new Color(1f, 0.32f, 0.12f);

        private static readonly RaycastHit2D[] Hits = new RaycastHit2D[8];

        private Rigidbody2D body;
        private Health health;
        private LineRenderer outline;
        private ObjectPool<EnemyShip> pool;
        private ObjectPool<Projectile> projectiles;
        private Transform target;
        private GameManager gameManager;
        private float stateTime;
        private float speed;
        private float angularSpeed;
        private int burstLeft;
        private float fireTimer;
        private float flashTime;
        private Vector2 repositionDirection;
        private bool pendingFirstStep;
        private float fireRateScale = 1f;
        private float inaccuracyScale = 1f;

        public State CurrentState { get; private set; }

        private void Reset()
        {
            var rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
            outline = GetComponent<LineRenderer>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            health.Damaged += OnDamaged;
            health.Died += OnDied;
            ApplyShape();
        }

        private void OnDestroy()
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }

        // Activación desde el pool, después de pool.Get (que ya la ubicó mirando hacia el jugador). Agresividad según la
        // dificultad: `fireRate` divide la espera entre ráfagas y `inaccuracyMultiplier` escala el desvío de cada disparo.
        public void Launch(ObjectPool<EnemyShip> owner, ObjectPool<Projectile> projectilePool, Transform player, GameManager manager,
            float fireRate = 1f, float inaccuracyMultiplier = 1f)
        {
            pool = owner;
            projectiles = projectilePool;
            target = player;
            gameManager = manager;
            fireRateScale = Mathf.Max(fireRate, 0.05f);
            inaccuracyScale = Mathf.Max(inaccuracyMultiplier, 0f);

            health.Restore();
            CurrentState = State.Approach;
            stateTime = 0f;
            speed = approachSpeed;
            angularSpeed = 0f;
            burstLeft = 0;
            fireTimer = firstBurstDelay;
            flashTime = 0f;
            outline.enabled = true;

            // Mismo caso que Asteroid: sin interpolar hasta el primer paso, para no dibujarse entre la pose vieja y la nueva.
            body.interpolation = RigidbodyInterpolation2D.None;
            pendingFirstStep = true;
            body.position = transform.position;
            body.rotation = transform.eulerAngles.z;
            body.linearVelocity = (Vector2)transform.up * speed;
            body.angularVelocity = 0f;
        }

        private void FixedUpdate()
        {
            if (pendingFirstStep)
            {
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                pendingFirstStep = false;
            }

            Vector2 position = body.position;
            Vector2 forward = Forward(body.rotation);
            Vector2 desired = forward;
            float targetSpeed = approachSpeed;

            // Sin jugador (muerto) sigue de largo.
            if (HasTarget)
            {
                Vector2 toTarget = (Vector2)target.position - position;
                UpdateState(toTarget, forward);
                switch (CurrentState)
                {
                    case State.Approach: desired = toTarget; break;
                    case State.Attack: desired = toTarget; targetSpeed = attackSpeed; break;
                    case State.Reposition: desired = repositionDirection; targetSpeed = repositionSpeed; break;
                }
            }

            desired = Avoid(position, forward, desired, ref targetSpeed);

            speed = Mathf.MoveTowards(speed, targetSpeed, linearAcceleration * Time.fixedDeltaTime);
            angularSpeed = Mathf.MoveTowards(angularSpeed, TurnSpeedTowards(desired), angularAcceleration * Time.fixedDeltaTime);
            body.angularVelocity = angularSpeed;
            body.linearVelocity = forward * speed;
        }

        private bool HasTarget => target != null && target.gameObject.activeInHierarchy;

        private void UpdateState(Vector2 toTarget, Vector2 forward)
        {
            stateTime += Time.fixedDeltaTime;
            float distance = toTarget.magnitude;

            switch (CurrentState)
            {
                case State.Approach:
                    if (distance < tooClose) EnterReposition(toTarget, forward);
                    else if (distance < attackRange) Enter(State.Attack);
                    break;
                case State.Attack:
                    if (distance < tooClose) EnterReposition(toTarget, forward);
                    else if (distance > disengageRange) Enter(State.Approach);
                    break;
                case State.Reposition:
                    if (distance > repositionDistance || stateTime > repositionMaxTime) Enter(State.Approach);
                    break;
            }
        }

        private void Enter(State state)
        {
            CurrentState = state;
            stateTime = 0f;
            if (state != State.Attack) return;
            burstLeft = 0;
            fireTimer = Mathf.Max(fireTimer, firstBurstDelay);
        }

        // Huye en diagonal: alejarse del jugador desviado `repositionAngle`, hacia el lado más cercano a su rumbo
        // actual, así no tiene que dar media vuelta pasando por encima del jugador.
        private void EnterReposition(Vector2 toTarget, Vector2 forward)
        {
            Vector2 away = -toTarget.normalized;
            Vector2 left = Rotate(away, repositionAngle);
            Vector2 right = Rotate(away, -repositionAngle);
            repositionDirection = Vector2.Dot(left, forward) >= Vector2.Dot(right, forward) ? left : right;
            Enter(State.Reposition);
        }

        // Si hay un obstáculo inmóvil adelante, gira hacia el lado contrario a su centro y frena; cuanto más cerca, más fuerte.
        private Vector2 Avoid(Vector2 position, Vector2 forward, Vector2 desired, ref float targetSpeed)
        {
            if (avoidLayers.value == 0 || lookAhead <= 0f) return desired;

            var filter = new ContactFilter2D { useLayerMask = true, layerMask = avoidLayers, useTriggers = false };
            int count = Physics2D.CircleCast(position, avoidRadius, forward, filter, Hits, lookAhead);

            float nearest = float.MaxValue;
            Collider2D obstacle = null;
            for (int i = 0; i < count; i++)
            {
                Rigidbody2D other = Hits[i].rigidbody;
                if (other != null && other.bodyType == RigidbodyType2D.Dynamic) continue;
                if (Hits[i].distance >= nearest) continue;
                nearest = Hits[i].distance;
                obstacle = Hits[i].collider;
            }
            if (obstacle == null) return desired;

            Vector2 toObstacle = (Vector2)obstacle.bounds.center - position;
            // Obstáculo a la izquierda del rumbo (producto cruz positivo): girar a la derecha, y viceversa.
            bool obstacleOnLeft = forward.x * toObstacle.y - forward.y * toObstacle.x > 0f;
            Vector2 sideways = obstacleOnLeft ? new Vector2(forward.y, -forward.x) : new Vector2(-forward.y, forward.x);

            float urgency = 1f - nearest / lookAhead;
            targetSpeed *= Mathf.Lerp(1f, 0.5f, urgency);
            return Vector2.Lerp(desired.normalized, sideways, 0.5f + 0.5f * urgency);
        }

        // Igual que la nave del jugador: la velocidad angular que permite frenar justo en el ángulo objetivo.
        private float TurnSpeedTowards(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.0001f) return 0f;
            float targetAngle = Mathf.Atan2(-direction.x, direction.y) * Mathf.Rad2Deg;
            float delta = Mathf.DeltaAngle(body.rotation, targetAngle);
            float distance = Mathf.Abs(delta);
            float turnSpeed = Mathf.Min(maxTurnSpeed, Mathf.Sqrt(2f * angularAcceleration * distance), distance / Time.fixedDeltaTime);
            return Mathf.Sign(delta) * turnSpeed;
        }

        private void Update()
        {
            if (flashTime > 0f)
            {
                flashTime -= Time.deltaTime;
                outline.enabled = flashTime <= 0f || Mathf.Repeat(flashTime * blinkRate, 1f) >= 0.5f;
            }

            if (gameManager != null && gameManager.State != GameState.Playing) return;
            if (CurrentState != State.Attack || !HasTarget || projectiles == null) return;

            fireTimer -= Time.deltaTime;
            if (fireTimer > 0f) return;

            if (burstLeft == 0)
            {
                // Espera a tener al jugador delante para empezar la ráfaga.
                Vector2 toTarget = target.position - transform.position;
                if (Vector2.Angle(transform.up, toTarget) > fireCone)
                {
                    fireTimer = 0f;
                    return;
                }
                burstLeft = burstCount;
            }

            Fire();
            burstLeft--;
            fireTimer += burstLeft > 0 ? burstInterval : burstCooldown / fireRateScale;
        }

        private void Fire()
        {
            // Pose interpolada del transform (la que se ve), así el proyectil sale de la punta dibujada.
            float spread = inaccuracy * inaccuracyScale;
            Vector2 direction = Rotate(transform.up, Random.Range(-spread, spread));
            Vector2 origin = transform.TransformPoint(muzzleOffset);
            Vector2 velocity = direction * projectileSpeed + body.linearVelocity;

            float angle = Mathf.Atan2(-velocity.x, velocity.y) * Mathf.Rad2Deg;
            Projectile projectile = projectiles.Get(origin, Quaternion.Euler(0f, 0f, angle));
            projectile.Launch(projectiles, velocity, projectileLifetime);
        }

        private void OnDamaged(Health _)
        {
            if (!health.IsDead) flashTime = flashDuration;
        }

        private void OnDied(Health _)
        {
            var info = new EnemyDestroyed(body.position, body.linearVelocity, points);
            Despawn();
            Destroyed?.Invoke(info);
        }

        public void Despawn()
        {
            if (pool != null) pool.Release(this);
            else gameObject.SetActive(false);
        }

        // Sin recarga de dominio al entrar en Play, los estáticos sobreviven entre sesiones.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Destroyed = null;

        // Rotación 0 = +Y, positiva = antihoraria (convención de Rigidbody2D.rotation).
        private static Vector2 Forward(float rotation)
        {
            float radians = rotation * Mathf.Deg2Rad;
            return new Vector2(-Mathf.Sin(radians), Mathf.Cos(radians));
        }

        private static Vector2 Rotate(Vector2 v, float degrees) => (Vector2)(Quaternion.Euler(0f, 0f, degrees) * v);

        // Copia `shape` al collider y al LineRenderer. Se llama en Awake; desde el menú contextual para previsualizar en el Editor.
        [ContextMenu("Aplicar forma")]
        public void ApplyShape()
        {
            var polygon = GetComponent<PolygonCollider2D>();
            polygon.pathCount = 1;
            polygon.SetPath(0, shape);

            var line = GetComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = shape.Length;
            for (int i = 0; i < shape.Length; i++) line.SetPosition(i, shape[i]);
            line.widthMultiplier = lineWidth;
            line.startColor = neonColor;
            line.endColor = neonColor;
            line.numCornerVertices = 2;
            line.alignment = LineAlignment.View;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireSphere(transform.position, tooClose);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, transform.position + transform.up * lookAhead);
        }
    }
}
