using System;
using NavesShooter.Core;
using UnityEngine;

namespace NavesShooter.Background
{
    // Planetas de fondo (esferas 3D lejanas) para la cámara en perspectiva. Cada uno se ubica según el recorrido del
    // nivel: queda centrado en pantalla, corrido `screenOffset`, cuando la cámara pasa por `progress` del camino A→B.
    // `screenOffset` está en unidades del plano del gameplay (lo que se ve en z = 0) y se escala con la profundidad; el
    // radio igual (`apparentRadius`), así el tamaño en pantalla no depende de la z. Por la perspectiva, un planeta a z
    // 800 se mueve en pantalla ~2% de lo que se mueve la cámara. Se reubican al cargar cada nivel (LevelLoaded).
    // Giran despacio sobre su eje. El orden de dibujo va de `sortingOrder` (el más lejano) en adelante: con el 2D
    // Renderer el orden lo dan sorting layer y order, no la profundidad.
    public class PlanetField : MonoBehaviour
    {
        [Serializable]
        public class Planet
        {
            public Transform transform;
            [Tooltip("Profundidad (z del mundo).")] public float depth = 500f;
            [Tooltip("Progreso del recorrido A→B (0–1) en el que queda en su lugar de la pantalla.")]
            [Range(0f, 1f)] public float progress = 0.5f;
            [Tooltip("Desplazamiento desde el centro de la pantalla en ese momento, en unidades del plano del gameplay.")]
            public Vector2 screenOffset = new Vector2(3f, 4f);
            [Tooltip("Radio visto en el plano del gameplay, en unidades.")]
            [Min(0.01f)] public float apparentRadius = 2f;
            [Tooltip("Giro sobre su eje, en grados/s.")] public float spin = 3f;
            [Tooltip("Inclinación del eje de giro, en grados.")] public Vector2 tilt = new Vector2(20f, 0f);
        }

        [SerializeField] private Camera targetCamera;
        [SerializeField] private LevelManager levelManager;
        [SerializeField] private Planet[] planets = Array.Empty<Planet>();
        [SerializeField, Tooltip("Orden de dibujo del planeta más lejano; cada uno más cercano suma 1.")]
        private int sortingOrder = -115;

        private float[] spinAngles;

        private void Start()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null || levelManager == null)
            {
                Debug.LogError("PlanetField: falta asignar la cámara o el LevelManager.", this);
                enabled = false;
                return;
            }
            spinAngles = new float[planets.Length];
            levelManager.LevelLoaded += Place;
            ApplySortingOrder();
            Place();
        }

        private void OnDestroy()
        {
            if (levelManager != null) levelManager.LevelLoaded -= Place;
        }

        private void Update()
        {
            for (int i = 0; i < planets.Length; i++)
            {
                Planet planet = planets[i];
                if (planet.transform == null) continue;
                spinAngles[i] += planet.spin * Time.deltaTime;
                planet.transform.rotation = Quaternion.Euler(planet.tilt.x, planet.tilt.y, 0f) * Quaternion.Euler(0f, spinAngles[i], 0f);
            }
        }

        private void Place()
        {
            Vector2 start = levelManager.StartPoint;
            Vector2 target = levelManager.TargetPoint;
            // Distancia de la cámara al plano del gameplay: ahí las medidas son las que se ven.
            float gameplayDistance = Mathf.Max(0.01f, -targetCamera.transform.position.z);

            foreach (Planet planet in planets)
            {
                if (planet.transform == null) continue;
                float scale = (planet.depth - targetCamera.transform.position.z) / gameplayDistance;
                Vector2 camera = Vector2.Lerp(start, target, planet.progress);
                Vector2 position = camera + planet.screenOffset * scale;
                planet.transform.position = new Vector3(position.x, position.y, planet.depth);
                // La esfera primitiva mide 1 de diámetro.
                planet.transform.localScale = Vector3.one * (2f * planet.apparentRadius * scale);
            }
        }

        // Más lejano, más atrás: cada planeta suma 1 por cada uno que tenga más lejos.
        private void ApplySortingOrder()
        {
            for (int i = 0; i < planets.Length; i++)
            {
                if (planets[i].transform == null) continue;
                int farther = 0;
                for (int j = 0; j < planets.Length; j++)
                {
                    if (planets[j].depth > planets[i].depth) farther++;
                }
                var planetRenderer = planets[i].transform.GetComponent<Renderer>();
                if (planetRenderer != null) planetRenderer.sortingOrder = sortingOrder + farther;
            }
        }
    }
}
