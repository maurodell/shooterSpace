using System.Collections.Generic;
using UnityEngine;

namespace NavesShooter.Core
{
    // Brillo creciente de un objeto dibujado con LineRenderers neón (los de sus hijos): con `Glow` > 0 los contornos se
    // aclaran hacia blanco y se engrosan, y cada uno suma halos (copias más anchas y transparentes, detrás). Con 0
    // vuelve al aspecto original. Los colores y anchos originales se toman al encender el brillo, así respeta lo que
    // haya armado cada dueño (ApplyShape). Un contorno apagado (parpadeo, zona de acople oculta) apaga sus halos.
    // Los halos se crean una sola vez, la primera vez que se enciende.
    public class NeonGlow : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f), Tooltip("Cuánto se aclara el contorno hacia blanco con Glow = 1.")]
        private float whiten = 0.7f;
        [SerializeField, Min(0f), Tooltip("Ancho extra del contorno con Glow = 1, en múltiplos del original.")]
        private float widthBoost = 0.8f;
        [SerializeField, Tooltip("Ancho de cada halo con Glow = 1, en múltiplos del contorno original.")]
        private float[] haloWidths = { 4f, 10f };
        [SerializeField, Tooltip("Alfa de cada halo con Glow = 1.")]
        private float[] haloAlphas = { 0.45f, 0.18f };

        private struct Entry
        {
            public LineRenderer line;
            public LineRenderer[] halos;
            public Color start;
            public Color end;
            public float width;
        }

        private readonly List<Entry> entries = new List<Entry>();
        private bool captured;
        private float glow;

        public float Glow
        {
            get => glow;
            set
            {
                value = Mathf.Max(0f, value);
                if (value <= 0f && glow <= 0f) return;
                if (!captured) Capture();
                glow = value;
                Apply();
            }
        }

        // Los contornos se apagan y prenden después de Glow (parpadeos): los halos siguen al contorno cada frame.
        private void LateUpdate()
        {
            if (glow <= 0f) return;
            foreach (Entry entry in entries)
            {
                bool shown = entry.line.enabled && entry.line.gameObject.activeInHierarchy;
                foreach (LineRenderer halo in entry.halos)
                    if (halo.enabled != shown) halo.enabled = shown;
            }
        }

        private void Capture()
        {
            captured = true;
            entries.Clear();
            foreach (LineRenderer line in GetComponentsInChildren<LineRenderer>(true))
            {
                var entry = new Entry
                {
                    line = line,
                    start = line.startColor,
                    end = line.endColor,
                    width = line.widthMultiplier,
                    halos = new LineRenderer[haloWidths.Length],
                };
                for (int i = 0; i < haloWidths.Length; i++) entry.halos[i] = CreateHalo(line, i);
                entries.Add(entry);
            }
        }

        // Copia del contorno como hijo (sigue su transform, también el banking), un orden de dibujo atrás.
        private static LineRenderer CreateHalo(LineRenderer line, int index)
        {
            var go = new GameObject("Halo_" + index, typeof(LineRenderer));
            go.layer = line.gameObject.layer;
            go.transform.SetParent(line.transform, false);
            var halo = go.GetComponent<LineRenderer>();
            halo.useWorldSpace = line.useWorldSpace;
            halo.loop = line.loop;
            halo.positionCount = line.positionCount;
            var points = new Vector3[line.positionCount];
            line.GetPositions(points);
            halo.SetPositions(points);
            halo.alignment = line.alignment;
            halo.numCornerVertices = Mathf.Max(line.numCornerVertices, 4);
            halo.numCapVertices = Mathf.Max(line.numCapVertices, 4);
            halo.sharedMaterial = line.sharedMaterial;
            halo.sortingLayerID = line.sortingLayerID;
            halo.sortingOrder = line.sortingOrder - 1 - index;
            halo.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            halo.receiveShadows = false;
            halo.enabled = false;
            return halo;
        }

        private void Apply()
        {
            float g = Mathf.Clamp01(glow);
            foreach (Entry entry in entries)
            {
                if (entry.line == null) continue;
                entry.line.startColor = Whiten(entry.start, g);
                entry.line.endColor = Whiten(entry.end, g);
                entry.line.widthMultiplier = entry.width * (1f + widthBoost * g);

                bool shown = g > 0f && entry.line.enabled && entry.line.gameObject.activeInHierarchy;
                for (int i = 0; i < entry.halos.Length; i++)
                {
                    LineRenderer halo = entry.halos[i];
                    halo.enabled = shown;
                    if (!shown) continue;
                    halo.widthMultiplier = entry.width * (1f + (haloWidths[i] - 1f) * g);
                    Color color = entry.start;
                    color.a = haloAlphas[i] * g;
                    halo.startColor = color;
                    color = entry.end;
                    color.a = haloAlphas[i] * g;
                    halo.endColor = color;
                }
            }
        }

        private Color Whiten(Color color, float g)
        {
            Color result = Color.Lerp(color, Color.white, whiten * g);
            result.a = color.a;
            return result;
        }
    }
}
