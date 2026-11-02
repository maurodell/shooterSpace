using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NavesShooter.UI
{
    // Contorno neón generado por código (sin sprites): un trazo de `thickness` con un halo `glow` que se desvanece
    // a ambos lados. Sin relleno. La figura se inscribe en el rect; el triángulo apunta hacia arriba y el rombo tiene
    // vértices arriba, abajo y a los costados. Con `stretch` círculo, triángulo y rombo se estiran para ocupar el rect
    // (elipse, rombo alargado); el rectángulo y el cristal (hexágono alargado del AmmoPickup) siempre lo ocupan.
    [RequireComponent(typeof(CanvasRenderer))]
    public class NeonShapeGraphic : MaskableGraphic, ICanvasRaycastFilter
    {
        public enum Shape { Circle, Triangle, Diamond, Rectangle, Crystal }

        [SerializeField] private Shape shape = Shape.Circle;
        [SerializeField, Min(0f), Tooltip("Ancho del trazo en unidades del Canvas.")] private float thickness = 6f;
        [SerializeField, Min(0f), Tooltip("Ancho del halo a cada lado del trazo.")] private float glow = 8f;
        [SerializeField, Range(8, 128), Tooltip("Segmentos del círculo.")] private int segments = 64;
        [SerializeField, Tooltip("Círculo, triángulo y rombo ocupan el rect en vez de inscribirse en su círculo.")]
        private bool stretch;
        [SerializeField, Range(0f, 1f), Tooltip("Cristal: altura de los hombros del hexágono, como fracción del medio alto.")]
        private float shoulder = 0.45f;
        [SerializeField, Tooltip("Si es raycast target, solo cuenta el disco inscripto en el rect, no las esquinas.")]
        private bool circularHitArea = true;

        private static readonly List<Vector2> Points = new List<Vector2>();

        public Shape ShapeType
        {
            get => shape;
            set
            {
                if (shape == value) return;
                shape = value;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float halfThickness = thickness * 0.5f;
            float inset = halfThickness + glow;
            Vector2 extents = new Vector2(rect.width * 0.5f - inset, rect.height * 0.5f - inset);
            if (extents.x <= 0f || extents.y <= 0f) return;
            if (!stretch && shape != Shape.Rectangle && shape != Shape.Crystal)
                extents.x = extents.y = Mathf.Min(extents.x, extents.y);

            // Antihorario desde arriba, para que la normal de cada lado (e.y, -e.x) apunte hacia afuera.
            Points.Clear();
            switch (shape)
            {
                case Shape.Rectangle:
                    AddPoint(rect, extents, 1f, 1f);
                    AddPoint(rect, extents, -1f, 1f);
                    AddPoint(rect, extents, -1f, -1f);
                    AddPoint(rect, extents, 1f, -1f);
                    break;
                case Shape.Crystal:
                    AddPoint(rect, extents, 0f, 1f);
                    AddPoint(rect, extents, -1f, shoulder);
                    AddPoint(rect, extents, -1f, -shoulder);
                    AddPoint(rect, extents, 0f, -1f);
                    AddPoint(rect, extents, 1f, -shoulder);
                    AddPoint(rect, extents, 1f, shoulder);
                    break;
                default:
                    int sides = shape == Shape.Circle ? segments : shape == Shape.Diamond ? 4 : 3;
                    for (int i = 0; i < sides; i++)
                    {
                        float angle = Mathf.PI * 0.5f + i * 2f * Mathf.PI / sides;
                        AddPoint(rect, extents, Mathf.Cos(angle), Mathf.Sin(angle));
                    }
                    break;
            }
            int count = Points.Count;

            Color32 solid = color;
            Color32 clear = solid;
            clear.a = 0;
            bool hasGlow = glow > 0f;
            int rings = hasGlow ? 4 : 2;

            for (int i = 0; i < count; i++)
            {
                Vector2 point = Points[i];
                Vector2 prevNormal = OutwardNormal(Points[(i + count - 1) % count], point);
                Vector2 nextNormal = OutwardNormal(point, Points[(i + 1) % count]);
                // Unión en inglete: misma distancia perpendicular a ambos lados del vértice.
                Vector2 miter = (prevNormal + nextNormal).normalized;
                float miterScale = 1f / Mathf.Max(Vector2.Dot(miter, nextNormal), 0.1f);

                if (hasGlow) vh.AddVert(point - miter * ((halfThickness + glow) * miterScale), clear, Vector4.zero);
                vh.AddVert(point - miter * (halfThickness * miterScale), solid, Vector4.zero);
                vh.AddVert(point + miter * (halfThickness * miterScale), solid, Vector4.zero);
                if (hasGlow) vh.AddVert(point + miter * ((halfThickness + glow) * miterScale), clear, Vector4.zero);
            }

            for (int i = 0; i < count; i++)
            {
                int a = i * rings;
                int b = (i + 1) % count * rings;
                for (int k = 0; k < rings - 1; k++)
                {
                    vh.AddTriangle(a + k, a + k + 1, b + k + 1);
                    vh.AddTriangle(b + k + 1, b + k, a + k);
                }
            }
        }

        private static void AddPoint(Rect rect, Vector2 extents, float x, float y) =>
            Points.Add(rect.center + new Vector2(x * extents.x, y * extents.y));

        private static Vector2 OutwardNormal(Vector2 from, Vector2 to)
        {
            Vector2 edge = to - from;
            return new Vector2(edge.y, -edge.x).normalized;
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!circularHitArea) return true;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out Vector2 local))
                return false;
            Rect rect = rectTransform.rect;
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            return (local - rect.center).sqrMagnitude <= radius * radius;
        }
    }
}
