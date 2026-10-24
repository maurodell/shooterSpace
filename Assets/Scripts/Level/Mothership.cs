using System;
using System.Collections.Generic;
using NavesShooter.Core;
using UnityEngine;

namespace NavesShooter.Level
{
    // Nave nodriza: destino del nivel (B). Estática. Casco hexagonal alargado con detalles internos, todo en contorno
    // neón verde (un LineRenderer hijo por trazo, generados por código), y delante de la proa (+Y local) una zona de
    // acople circular que pulsa. El casco es sólido (PolygonCollider2D); la zona de acople es un trigger que solo
    // registra `dockLayers`: cuando el centro de la nave del jugador entra en el círculo se dispara LevelCompleted.
    // Se registra en el radar como objetivo.
    [RequireComponent(typeof(PolygonCollider2D), typeof(CircleCollider2D))]
    public class Mothership : MonoBehaviour
    {
        [Header("Forma")]
        [SerializeField, Min(0.1f), Tooltip("Escala de toda la figura (casco, detalles y zona de acople).")]
        private float scale = 1f;
        [SerializeField, Min(0.1f), Tooltip("Radio de la zona de acople, antes de `scale`.")]
        private float dockRadius = 1.3f;
        [SerializeField, Min(0f), Tooltip("Separación entre la proa y el borde de la zona de acople, antes de `scale`.")]
        private float dockGap = 0.6f;

        [Header("Acople")]
        [SerializeField, Tooltip("Layers que completan el nivel al entrar en la zona de acople. Las demás la atraviesan sin contacto.")]
        private LayerMask dockLayers;

        [Header("Render")]
        [SerializeField] private Material lineMaterial;
        [SerializeField] private Color neonColor = new Color(0.3f, 1f, 0.45f);
        [SerializeField, Range(0f, 1f), Tooltip("Brillo de los detalles internos respecto del casco.")]
        private float detailBrightness = 0.6f;
        [SerializeField] private float hullWidth = 0.12f;
        [SerializeField] private float detailWidth = 0.06f;
        [SerializeField] private float dockWidth = 0.08f;
        [SerializeField, Min(0f), Tooltip("Pulsos por segundo de la zona de acople.")] private float dockPulseRate = 1.2f;
        [SerializeField, Range(0f, 1f), Tooltip("Alfa mínimo de la zona de acople durante el pulso.")] private float dockMinAlpha = 0.35f;

        private const float HullLength = 4f;     // media eslora (proa en +Y)
        private const float HullHalfWidth = 1.8f;
        private const float HullShoulder = 2.6f; // altura de los vértices laterales
        private const int DockSegments = 48;
        private const string LinePrefix = "Line_";

        private struct Stroke
        {
            public Vector2[] points;
            public bool loop;
            public bool dock;
            public bool detail;
        }

        public event Action LevelCompleted;

        private readonly List<LineRenderer> dockLines = new List<LineRenderer>();

        // Centro de la zona de acople en coordenadas locales (sin `scale`, que ya lo incluye).
        private Vector2 LocalDockCenter => new Vector2(0f, (HullLength + dockGap + dockRadius) * scale);

        public Vector2 DockPosition => transform.TransformPoint(LocalDockCenter);
        public float DockRadius => dockRadius * scale;

        private void Awake()
        {
            ApplyShape();
        }

        // El radar marca el centro de la zona de acople (B), no el del casco.
        private void OnEnable() => RadarRegistry.Register(transform, RadarContactType.Target, neonColor, LocalDockCenter);

        private void OnDisable() => RadarRegistry.Unregister(transform);

        private void Update()
        {
            if (dockLines.Count == 0) return;
            float pulse = 0.5f + 0.5f * Mathf.Cos(Time.time * dockPulseRate * 2f * Mathf.PI);
            Color color = neonColor;
            color.a = Mathf.Lerp(dockMinAlpha, 1f, pulse);
            foreach (LineRenderer line in dockLines)
            {
                line.startColor = color;
                line.endColor = color;
            }
        }

        // Ubica la nave nodriza con el centro de la zona de acople en `dockPosition` y la proa mirando hacia
        // `facingAngle` (grados, 0 = arriba, positivo = antihorario).
        public void Place(Vector2 dockPosition, float facingAngle)
        {
            Quaternion rotation = Quaternion.Euler(0f, 0f, facingAngle);
            Vector2 position = dockPosition - (Vector2)(rotation * LocalDockCenter);
            transform.SetPositionAndRotation(position, rotation);
        }

        // Solo llega desde la zona de acople: el casco no es trigger. Se exige que el centro de la nave esté dentro del
        // círculo, no que lo roce.
        private void OnTriggerStay2D(Collider2D other)
        {
            if ((dockLayers.value & (1 << other.gameObject.layer)) == 0) return;
            Vector2 position = other.attachedRigidbody != null ? other.attachedRigidbody.position : (Vector2)other.transform.position;
            float radius = DockRadius;
            if ((position - DockPosition).sqrMagnitude > radius * radius) return;
            LevelCompleted?.Invoke();
        }

        // Genera los trazos, los colliders y los LineRenderers hijos (los crea si faltan). Se llama en Awake; desde el
        // menú contextual para previsualizar en el Editor.
        [ContextMenu("Aplicar forma")]
        public void ApplyShape()
        {
            List<Stroke> strokes = BuildStrokes();

            var hull = GetComponent<PolygonCollider2D>();
            hull.isTrigger = false;
            hull.pathCount = 1;
            hull.SetPath(0, strokes[0].points);

            var dock = GetComponent<CircleCollider2D>();
            dock.isTrigger = true;
            dock.offset = LocalDockCenter;
            dock.radius = DockRadius;
            // Solo `dockLayers` generan contacto: proyectiles y asteroides pasan a través sin enterarse.
            dock.excludeLayers = ~dockLayers.value;

            dockLines.Clear();
            for (int i = 0; i < strokes.Count; i++)
            {
                Stroke stroke = strokes[i];
                LineRenderer line = GetOrCreateLine(i);
                line.useWorldSpace = false;
                line.loop = stroke.loop;
                line.positionCount = stroke.points.Length;
                for (int p = 0; p < stroke.points.Length; p++) line.SetPosition(p, stroke.points[p]);
                line.widthMultiplier = stroke.dock ? dockWidth : stroke.detail ? detailWidth : hullWidth;
                Color color = stroke.detail ? neonColor * detailBrightness : neonColor;
                color.a = 1f;
                line.startColor = color;
                line.endColor = color;
                line.numCornerVertices = 2;
                line.alignment = LineAlignment.View;
                if (lineMaterial != null) line.sharedMaterial = lineMaterial;
                if (stroke.dock) dockLines.Add(line);
            }

            // Trazos sobrantes de una forma anterior.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (!child.name.StartsWith(LinePrefix)) continue;
                if (int.TryParse(child.name.Substring(LinePrefix.Length), out int index) && index < strokes.Count) continue;
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }

        private LineRenderer GetOrCreateLine(int index)
        {
            string name = LinePrefix + index;
            Transform child = transform.Find(name);
            if (child == null)
            {
                child = new GameObject(name, typeof(LineRenderer)).transform;
                child.SetParent(transform, false);
                child.gameObject.layer = gameObject.layer;
            }
            return child.GetComponent<LineRenderer>();
        }

        // El primer trazo es el casco (también es el collider).
        private List<Stroke> BuildStrokes()
        {
            var strokes = new List<Stroke>();

            // Casco: hexágono alargado, proa en +Y.
            strokes.Add(Shape(true, false, false,
                new Vector2(0f, HullLength), new Vector2(HullHalfWidth, HullShoulder), new Vector2(HullHalfWidth, -HullShoulder),
                new Vector2(0f, -HullLength), new Vector2(-HullHalfWidth, -HullShoulder), new Vector2(-HullHalfWidth, HullShoulder)));

            // Cubierta interior: el mismo hexágono al 55%.
            const float inner = 0.55f;
            strokes.Add(Shape(true, false, true,
                new Vector2(0f, HullLength * inner), new Vector2(HullHalfWidth * inner, HullShoulder * inner),
                new Vector2(HullHalfWidth * inner, -HullShoulder * inner), new Vector2(0f, -HullLength * inner),
                new Vector2(-HullHalfWidth * inner, -HullShoulder * inner), new Vector2(-HullHalfWidth * inner, HullShoulder * inner)));

            // Puente: triángulo entre la cubierta y la proa.
            strokes.Add(Shape(true, false, true, new Vector2(0f, 3.4f), new Vector2(0.4f, 2.8f), new Vector2(-0.4f, 2.8f)));

            // Hangares laterales.
            strokes.Add(Shape(false, false, true, new Vector2(1.4f, 1.6f), new Vector2(1.4f, -1.6f)));
            strokes.Add(Shape(false, false, true, new Vector2(-1.4f, 1.6f), new Vector2(-1.4f, -1.6f)));

            // Quilla: de la cubierta a la popa.
            strokes.Add(Shape(false, false, true, new Vector2(0f, -HullLength * inner), new Vector2(0f, -HullLength)));

            // Motores: dos toberas que sobresalen de la popa.
            strokes.Add(Shape(true, false, false, new Vector2(0.45f, -3.1f), new Vector2(1.05f, -3.1f), new Vector2(1.05f, -4.5f), new Vector2(0.45f, -4.5f)));
            strokes.Add(Shape(true, false, false, new Vector2(-0.45f, -3.1f), new Vector2(-1.05f, -3.1f), new Vector2(-1.05f, -4.5f), new Vector2(-0.45f, -4.5f)));

            // Zona de acople: círculo delante de la proa y una flecha que apunta hacia el casco.
            Vector2 center = new Vector2(0f, HullLength + dockGap + dockRadius);
            var circle = new Vector2[DockSegments];
            for (int i = 0; i < DockSegments; i++)
            {
                float angle = i * 2f * Mathf.PI / DockSegments;
                circle[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dockRadius;
            }
            strokes.Add(Shape(true, true, false, circle));
            float chevron = dockRadius * 0.35f;
            strokes.Add(Shape(false, true, false,
                center + new Vector2(-chevron, chevron * 0.5f), center + new Vector2(0f, -chevron * 0.5f), center + new Vector2(chevron, chevron * 0.5f)));

            for (int s = 0; s < strokes.Count; s++)
                for (int i = 0; i < strokes[s].points.Length; i++)
                    strokes[s].points[i] *= scale;
            return strokes;
        }

        private static Stroke Shape(bool loop, bool dock, bool detail, params Vector2[] points)
        {
            return new Stroke { points = points, loop = loop, dock = dock, detail = detail };
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Vector3 center = DockPosition;
            const int segments = 32;
            for (int i = 0; i < segments; i++)
            {
                float a = i * 2f * Mathf.PI / segments;
                float b = (i + 1) * 2f * Mathf.PI / segments;
                Gizmos.DrawLine(center + new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * DockRadius,
                    center + new Vector3(Mathf.Cos(b), Mathf.Sin(b)) * DockRadius);
            }
        }
    }
}
