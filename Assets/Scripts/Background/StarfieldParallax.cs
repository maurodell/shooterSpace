using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace NavesShooter.Background
{
    // Fondo de estrellas infinito con parallax. Cada capa es una sola mesh (un quad por estrella).
    // Cuando una estrella sale del área visible + margen se reubica del lado opuesto: no se crean ni
    // destruyen objetos, y la mesh solo se reescribe en los frames en que alguna estrella se recicla.
    // Orden de ejecución alto: su LateUpdate corre después del de la cámara (CameraFollow).
    [DefaultExecutionOrder(1000)]
    public class StarfieldParallax : MonoBehaviour
    {
        [Serializable]
        public class Layer
        {
            public string name = "Layer";
            [Min(1)] public int starCount = 100;
            [Tooltip("Lado del cuadradito de cada estrella, en unidades de mundo (mín, máx).")]
            public Vector2 sizeRange = new Vector2(0.03f, 0.06f);
            [Tooltip("Brillo/opacidad de cada estrella, 0..1 (mín, máx).")]
            public Vector2 brightnessRange = new Vector2(0.3f, 0.6f);
            [Tooltip("Cuánto se mueve la capa con el mundo: 0 = pegada a la cámara (muy lejos), 1 = igual que el mundo.")]
            [Range(0f, 1f)] public float parallax = 0.5f;
        }

        private class LayerState
        {
            public Layer config;
            public Transform transform;
            public Mesh mesh;
            public Vector2[] positions; // posición local de cada estrella dentro de la capa
            public float[] halfSizes;
            public Vector3[] vertices;
        }

        [SerializeField] private Camera targetCamera;
        [SerializeField, Tooltip("Material unlit con color de vértice (Sprite-Unlit-Default).")]
        private Material material;
        [SerializeField] private Color baseColor = Color.white;
        [SerializeField, Tooltip("Tono hacia el que se mezcla al azar cada estrella.")]
        private Color tintColor = new Color(0.6f, 0.9f, 1f);
        [SerializeField, Tooltip("Margen alrededor del área visible antes de reciclar, en unidades de mundo.")]
        private float margin = 1f;
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField, Tooltip("Orden de la primera capa (la más lejana); cada capa siguiente suma 1.")]
        private int sortingOrder = -100;
        [SerializeField] private int seed = 1234;
        [SerializeField, Tooltip("De la más lejana a la más cercana.")]
        private Layer[] layers =
        {
            new Layer { name = "Far",  starCount = 140, sizeRange = new Vector2(0.025f, 0.05f), brightnessRange = new Vector2(0.2f, 0.45f), parallax = 0.05f },
            new Layer { name = "Mid",  starCount = 70,  sizeRange = new Vector2(0.04f, 0.08f),  brightnessRange = new Vector2(0.35f, 0.7f), parallax = 0.3f },
            new Layer { name = "Near", starCount = 30,  sizeRange = new Vector2(0.06f, 0.12f),  brightnessRange = new Vector2(0.6f, 1f),    parallax = 0.9f },
        };

        private LayerState[] states;
        private Vector2 halfExtents;

        private void Start()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null || material == null)
            {
                Debug.LogError("StarfieldParallax: falta asignar la cámara o el material.", this);
                enabled = false;
                return;
            }

            states = new LayerState[layers.Length];
            for (int i = 0; i < layers.Length; i++) states[i] = CreateLayer(i);
            Rebuild();
        }

        private void OnDestroy()
        {
            if (states == null) return;
            foreach (var s in states)
            {
                if (s.mesh != null) Destroy(s.mesh);
            }
        }

        private void LateUpdate()
        {
            // Cambio de resolución u orthographicSize: redistribuir estrellas en el área nueva.
            if (CurrentHalfExtents() != halfExtents) Rebuild();

            Vector2 cam = targetCamera.transform.position;
            Vector2 area = halfExtents * 2f;
            float z = transform.position.z;

            foreach (var s in states)
            {
                float p = s.config.parallax;
                Vector2 layerPos = cam * (1f - p);
                s.transform.position = new Vector3(layerPos.x, layerPos.y, z);

                // Posición de cada estrella relativa a la cámara = local - cam * p.
                Vector2 shift = cam * p;
                bool dirty = false;
                for (int i = 0; i < s.positions.Length; i++)
                {
                    Vector2 local = s.positions[i];
                    Vector2 rel = local - shift;
                    bool moved = Wrap(ref local.x, rel.x, halfExtents.x, area.x);
                    moved |= Wrap(ref local.y, rel.y, halfExtents.y, area.y);
                    if (!moved) continue;

                    s.positions[i] = local;
                    WriteQuad(s, i);
                    dirty = true;
                }

                if (dirty)
                {
                    s.mesh.SetVertices(s.vertices);
                    s.mesh.RecalculateBounds();
                }
            }
        }

        // Si la estrella quedó fuera de [-half, half] respecto a la cámara, la corre un múltiplo del área
        // para que reaparezca del lado opuesto.
        private static bool Wrap(ref float local, float rel, float half, float size)
        {
            if (rel > half)
            {
                local -= size * Mathf.Ceil((rel - half) / size);
                return true;
            }
            if (rel < -half)
            {
                local += size * Mathf.Ceil((-half - rel) / size);
                return true;
            }
            return false;
        }

        private Vector2 CurrentHalfExtents()
        {
            float halfHeight = targetCamera.orthographicSize;
            return new Vector2(halfHeight * targetCamera.aspect + margin, halfHeight + margin);
        }

        private LayerState CreateLayer(int index)
        {
            var config = layers[index];
            var go = new GameObject(config.name);
            go.transform.SetParent(transform, false);

            var mesh = new Mesh { name = "Starfield " + config.name };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.sortingLayerName = sortingLayerName;
            meshRenderer.sortingOrder = sortingOrder + index;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            return new LayerState { config = config, transform = go.transform, mesh = mesh };
        }

        private void Rebuild()
        {
            halfExtents = CurrentHalfExtents();
            Vector2 cam = targetCamera.transform.position;
            for (int i = 0; i < states.Length; i++) Fill(states[i], i, cam);
        }

        private void Fill(LayerState s, int index, Vector2 cam)
        {
            var c = s.config;
            int n = c.starCount;
            var rnd = new System.Random(seed + index * 7919);

            s.positions = new Vector2[n];
            s.halfSizes = new float[n];
            s.vertices = new Vector3[n * 4];
            var colors = new Color32[n * 4];
            var uvs = new Vector2[n * 4];
            var triangles = new int[n * 6];

            Vector2 shift = cam * c.parallax;
            for (int i = 0; i < n; i++)
            {
                s.positions[i] = shift + new Vector2(
                    Mathf.Lerp(-halfExtents.x, halfExtents.x, Next(rnd)),
                    Mathf.Lerp(-halfExtents.y, halfExtents.y, Next(rnd)));

                // Al cuadrado: más estrellas chicas que grandes.
                float t = Next(rnd);
                s.halfSizes[i] = 0.5f * Mathf.Lerp(c.sizeRange.x, c.sizeRange.y, t * t);

                Color color = Color.Lerp(baseColor, tintColor, Next(rnd));
                color.a = Mathf.Lerp(c.brightnessRange.x, c.brightnessRange.y, Next(rnd));
                Color32 color32 = color;

                int v = i * 4;
                colors[v] = colors[v + 1] = colors[v + 2] = colors[v + 3] = color32;
                uvs[v] = new Vector2(0f, 0f);
                uvs[v + 1] = new Vector2(0f, 1f);
                uvs[v + 2] = new Vector2(1f, 1f);
                uvs[v + 3] = new Vector2(1f, 0f);

                int tri = i * 6;
                triangles[tri] = v;
                triangles[tri + 1] = v + 1;
                triangles[tri + 2] = v + 2;
                triangles[tri + 3] = v;
                triangles[tri + 4] = v + 2;
                triangles[tri + 5] = v + 3;

                WriteQuad(s, i);
            }

            s.mesh.Clear();
            s.mesh.SetVertices(s.vertices);
            s.mesh.SetColors(colors);
            s.mesh.SetUVs(0, uvs);
            s.mesh.SetTriangles(triangles, 0);
            s.mesh.RecalculateBounds();
        }

        private static void WriteQuad(LayerState s, int i)
        {
            Vector2 p = s.positions[i];
            float h = s.halfSizes[i];
            int v = i * 4;
            s.vertices[v] = new Vector3(p.x - h, p.y - h, 0f);
            s.vertices[v + 1] = new Vector3(p.x - h, p.y + h, 0f);
            s.vertices[v + 2] = new Vector3(p.x + h, p.y + h, 0f);
            s.vertices[v + 3] = new Vector3(p.x + h, p.y - h, 0f);
        }

        private static float Next(System.Random rnd)
        {
            return (float)rnd.NextDouble();
        }
    }
}
