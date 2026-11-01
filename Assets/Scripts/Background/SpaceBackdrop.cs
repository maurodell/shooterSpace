using UnityEngine;
using UnityEngine.Rendering;

namespace NavesShooter.Background
{
    // Fondo lejano en lugar de skybox (el 2D Renderer no dibuja skybox): un quad hijo de la cámara a `distance`, que
    // cubre todo el campo visible, con una textura generada una sola vez en Start: degradé vertical oscuro y nebulosas
    // tenues de ruido Perlin en dos tonos neón. Como la cámara nunca rota, se ve igual que un skybox: no se mueve con el
    // recorrido. El far clip de la cámara tiene que pasar `distance`. Si crece el campo visible (resolución, fov), se
    // reescala; con el zoom hacia adentro del salto alcanza el mismo tamaño.
    public class SpaceBackdrop : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField, Tooltip("Material unlit con textura y color de vértice (Starfield.mat); se usa una copia.")]
        private Material material;
        [SerializeField, Min(1f), Tooltip("Distancia a la cámara (menor que su far clip).")]
        private float distance = 1100f;
        [SerializeField, Min(1f), Tooltip("Tamaño extra respecto del campo visible.")]
        private float overscan = 1.1f;
        [SerializeField] private Vector2Int textureSize = new Vector2Int(128, 256);
        [SerializeField, Tooltip("Color del borde inferior.")] private Color bottomColor = new Color(0.02f, 0.015f, 0.05f);
        [SerializeField, Tooltip("Color del borde superior.")] private Color topColor = new Color(0.005f, 0.02f, 0.045f);
        [SerializeField] private Color nebulaColorA = new Color(0.05f, 0.18f, 0.25f);
        [SerializeField] private Color nebulaColorB = new Color(0.22f, 0.04f, 0.2f);
        [SerializeField, Range(0f, 1f), Tooltip("Intensidad de las nebulosas.")] private float nebulaStrength = 0.6f;
        [SerializeField, Min(0.01f), Tooltip("Escala del ruido: más alto, nubes más chicas.")] private float nebulaScale = 2.5f;
        [SerializeField] private int sortingOrder = -200;
        [SerializeField] private int seed = 7;

        private Transform quad;
        private Material instance;
        private Texture2D texture;
        private Vector2 appliedHalfExtents;

        private void Start()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null || material == null)
            {
                Debug.LogError("SpaceBackdrop: falta asignar la cámara o el material.", this);
                enabled = false;
                return;
            }

            texture = BuildTexture();
            instance = new Material(material) { name = "SpaceBackdrop", mainTexture = texture };

            var go = new GameObject("Backdrop");
            quad = go.transform;
            quad.SetParent(targetCamera.transform, false);
            quad.localPosition = new Vector3(0f, 0f, distance);
            go.AddComponent<MeshFilter>().sharedMesh = BuildQuad();
            var quadRenderer = go.AddComponent<MeshRenderer>();
            quadRenderer.sharedMaterial = instance;
            quadRenderer.sortingOrder = sortingOrder;
            quadRenderer.shadowCastingMode = ShadowCastingMode.Off;
            quadRenderer.receiveShadows = false;
            quadRenderer.lightProbeUsage = LightProbeUsage.Off;
            quadRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            Resize();
        }

        private void OnDestroy()
        {
            if (instance != null) Destroy(instance);
            if (texture != null) Destroy(texture);
        }

        private void LateUpdate()
        {
            Vector2 needed = HalfExtents();
            if (needed.x > appliedHalfExtents.x || needed.y > appliedHalfExtents.y) Resize();
        }

        private Vector2 HalfExtents()
        {
            float halfHeight = targetCamera.orthographic
                ? targetCamera.orthographicSize
                : distance * Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            return new Vector2(halfHeight * targetCamera.aspect, halfHeight);
        }

        private void Resize()
        {
            appliedHalfExtents = HalfExtents();
            quad.localScale = new Vector3(appliedHalfExtents.x * 2f * overscan, appliedHalfExtents.y * 2f * overscan, 1f);
        }

        // Quad de 1×1 mirando a la cámara (normal -Z).
        private static Mesh BuildQuad()
        {
            var mesh = new Mesh { name = "Backdrop" };
            mesh.SetVertices(new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            });
            mesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) });
            mesh.SetColors(new[] { Color.white, Color.white, Color.white, Color.white });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private Texture2D BuildTexture()
        {
            int w = Mathf.Max(2, textureSize.x);
            int h = Mathf.Max(2, textureSize.y);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "SpaceBackdrop",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            var rnd = new System.Random(seed);
            var offsetA = new Vector2((float)rnd.NextDouble() * 100f, (float)rnd.NextDouble() * 100f);
            var offsetB = new Vector2((float)rnd.NextDouble() * 100f, (float)rnd.NextDouble() * 100f);
            float aspect = (float)w / h;
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = (float)y / (h - 1);
                Color gradient = Color.Lerp(bottomColor, topColor, v);
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / (w - 1) * aspect;
                    float a = Nebula(u * nebulaScale + offsetA.x, v * nebulaScale + offsetA.y);
                    float b = Nebula(u * nebulaScale + offsetB.x, v * nebulaScale + offsetB.y);
                    Color c = gradient + (nebulaColorA * a + nebulaColorB * b) * nebulaStrength;
                    c.a = 1f;
                    pixels[y * w + x] = c;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        // Dos octavas de Perlin con umbral suave: nubes sueltas y fondo limpio entre ellas.
        private static float Nebula(float x, float y)
        {
            float n = Mathf.PerlinNoise(x, y) * 0.65f + Mathf.PerlinNoise(x * 2.3f + 17f, y * 2.3f + 5f) * 0.35f;
            float t = Mathf.InverseLerp(0.45f, 0.85f, n);
            return t * t;
        }
    }
}
