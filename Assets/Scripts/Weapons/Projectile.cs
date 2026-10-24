using System;
using NavesShooter.Core;
using UnityEngine;

namespace NavesShooter.Weapons
{
    // Proyectil con contorno neón. Rigidbody2D kinematic movido por velocidad y collider trigger: no empuja nada, solo
    // detecta. Vuelve a su pool al cumplir el tiempo de vida o al tocar cualquier collider (la matriz de colisiones
    // de layers decide con qué puede chocar); si lo tocado es `IDamageable`, antes le avisa del impacto. Si no lo es
    // (cometas grandes, casco de la nave nodriza), avisa por `Blocked` para el destello.
    [RequireComponent(typeof(Rigidbody2D), typeof(PolygonCollider2D), typeof(LineRenderer))]
    public class Projectile : MonoBehaviour
    {
        [Header("Forma")]
        [SerializeField, Tooltip("Vértices locales del contorno y del collider. La punta mira a +Y.")]
        private Vector2[] shape =
        {
            new Vector2(0f, 0.16f),
            new Vector2(0.05f, 0f),
            new Vector2(0f, -0.16f),
            new Vector2(-0.05f, 0f),
        };
        [SerializeField] private float lineWidth = 0.04f;
        [SerializeField] private Color neonColor = new Color(1f, 0.9f, 0.15f);

        // Impacto contra algo indestructible: punto aproximado del impacto y velocidad del proyectil.
        public static event Action<Vector2, Vector2> Blocked;

        private Rigidbody2D body;
        private ObjectPool<Projectile> pool;
        private float timeLeft;

        private void Reset()
        {
            var rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            GetComponent<PolygonCollider2D>().isTrigger = true;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            GetComponent<PolygonCollider2D>().isTrigger = true;
            ApplyShape();
        }

        // Activación desde el pool: resetea todo el estado de la instancia reutilizada. Llamar después de pool.Get.
        public void Launch(ObjectPool<Projectile> owner, Vector2 velocity, float lifetime)
        {
            pool = owner;
            timeLeft = lifetime;
            body.position = transform.position;
            body.rotation = transform.eulerAngles.z;
            body.linearVelocity = velocity;
            body.angularVelocity = 0f;
        }

        private void Update()
        {
            timeLeft -= Time.deltaTime;
            if (timeLeft <= 0f) Despawn();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Si tocó dos colliders en el mismo paso, el primero ya lo devolvió al pool: no golpea dos veces.
            if (!gameObject.activeSelf) return;
            if (other.TryGetComponent(out IDamageable target)) target.TakeHit();
            else Blocked?.Invoke(body.position - body.linearVelocity * (0.5f * Time.fixedDeltaTime), body.linearVelocity);
            Despawn();
        }

        // Sin recarga de dominio al entrar en Play, los estáticos sobreviven entre sesiones.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Blocked = null;

        private void Despawn()
        {
            if (pool != null) pool.Release(this);
            else gameObject.SetActive(false);
        }

        // Copia `shape` al collider y al LineRenderer. Se llama en Awake; desde el menú contextual para previsualizar en el Editor.
        [ContextMenu("Aplicar forma")]
        public void ApplyShape()
        {
            var polygon = GetComponent<PolygonCollider2D>();
            polygon.pathCount = 1;
            polygon.SetPath(0, shape);

            var outline = GetComponent<LineRenderer>();
            outline.useWorldSpace = false;
            outline.loop = true;
            outline.positionCount = shape.Length;
            for (int i = 0; i < shape.Length; i++) outline.SetPosition(i, shape[i]);
            outline.widthMultiplier = lineWidth;
            outline.startColor = neonColor;
            outline.endColor = neonColor;
            outline.numCornerVertices = 2;
            outline.alignment = LineAlignment.View;
        }
    }
}
