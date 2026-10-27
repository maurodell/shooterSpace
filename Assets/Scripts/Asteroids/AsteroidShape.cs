using System.Collections.Generic;
using UnityEngine;

namespace NavesShooter.Asteroids
{
    // Contorno irregular compartido por los cometas chicos y los grandes. Recibe el generador para que los grandes
    // salgan siempre iguales a partir de su semilla de forma.
    public static class AsteroidShape
    {
        // Generador xorshift32 reiniciable con una semilla: los cometas grandes rearman su forma en cada activación sin
        // alocar un System.Random nuevo (no se puede volver a sembrar). Suficiente para formas, no para estadística.
        public sealed class ShapeRandom
        {
            private uint state;

            public ShapeRandom(int seed) => Reset(seed);

            public void Reset(int seed)
            {
                // Mezcla la semilla para que semillas consecutivas no den secuencias parecidas; xorshift no admite estado 0.
                state = unchecked((uint)seed * 2654435761u) ^ 0x9E3779B9u;
                if (state == 0) state = 0x9E3779B9u;
                NextUInt();
            }

            public uint NextUInt()
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return state;
            }

            // [0, 1).
            public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

            // [min, max).
            public int Next(int min, int max) => max <= min ? min : min + (int)(NextUInt() % (uint)(max - min));
        }

        // Polígono de `minVertices` a `maxVertices` vértices repartidos alrededor del centro, cada uno con un desvío
        // angular y un radio al azar. El desvío nunca llega a media separación, así los vértices no se cruzan de orden
        // y el contorno no se corta a sí mismo.
        public static void Build(List<Vector2> points, ShapeRandom random, float radius, int minVertices, int maxVertices,
            float minRadiusFactor, float angleJitter)
        {
            int count = random.Next(minVertices, Mathf.Max(minVertices, maxVertices) + 1);
            float step = 2f * Mathf.PI / count;

            points.Clear();
            for (int i = 0; i < count; i++)
            {
                float angle = (i + Range(random, -angleJitter, angleJitter)) * step;
                float r = radius * Range(random, minRadiusFactor, 1f);
                points.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r);
            }
        }

        // Mismo camino para el collider y el contorno.
        public static void Apply(List<Vector2> points, PolygonCollider2D polygon, LineRenderer outline)
        {
            polygon.pathCount = 1;
            polygon.SetPath(0, points);

            outline.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++) outline.SetPosition(i, points[i]);
        }

        public static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

        public static float Range(ShapeRandom random, float min, float max) => min + random.NextFloat() * (max - min);
    }
}
