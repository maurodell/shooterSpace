using System;
using System.Collections.Generic;
using NavesShooter.Core;
using TMPro;
using UnityEngine;

namespace NavesShooter.UI
{
    // Radar circular del HUD con la nave del jugador en el centro. Dibuja los contactos de RadarRegistry: dentro de
    // `range` a escala en su posición real; más lejos, en el borde del círculo en su dirección. Orientado al mundo
    // (norte arriba, el ícono de la nave gira); con `rotateWithShip` el frente de la nave queda arriba y gira el resto.
    // Muestra también la distancia restante a B y el porcentaje de progreso del nivel.
    // Va en un RectTransform cuadrado: el círculo se inscribe en él y los marcadores son hijos centrados.
    public class Radar : MonoBehaviour
    {
        [Serializable]
        private struct ContactStyle
        {
            public RadarContactType type;
            public NeonShapeGraphic.Shape shape;
            [Min(0f)] public float size;
        }

        [SerializeField] private LevelManager levelManager;
        [SerializeField, Tooltip("Centro del radar (la nave del jugador).")] private Transform ship;
        [SerializeField, Min(0.1f), Tooltip("Distancia en unidades de mundo que cubre el radio del radar.")]
        private float range = 40f;
        [SerializeField, Tooltip("Rotar el radar con la nave (su frente arriba) en vez de mantener el norte arriba.")]
        private bool rotateWithShip;

        [Header("Visual")]
        [SerializeField, Tooltip("Ícono de la nave, apuntando hacia arriba en reposo.")] private RectTransform shipIcon;
        [SerializeField, Tooltip("Marcador inactivo que se clona por contacto (hijo centrado del radar).")]
        private NeonShapeGraphic markerTemplate;
        [SerializeField, Min(0f), Tooltip("Distancia del borde del círculo a la que quedan los contactos fuera de alcance.")]
        private float edgeInset = 18f;
        [SerializeField, Tooltip("Contornos del radar a los que se aplica `ringAlpha`.")] private NeonShapeGraphic[] rings;
        [SerializeField, Range(0f, 1f), Tooltip("Opacidad percibida de los contornos (convertida a lineal, como en TouchControlFeedback).")]
        private float ringAlpha = 0.35f;
        [SerializeField] private ContactStyle[] styles =
        {
            new ContactStyle { type = RadarContactType.Target, shape = NeonShapeGraphic.Shape.Diamond, size = 36f },
            new ContactStyle { type = RadarContactType.Enemy, shape = NeonShapeGraphic.Shape.Triangle, size = 26f },
            new ContactStyle { type = RadarContactType.Obstacle, shape = NeonShapeGraphic.Shape.Circle, size = 22f },
        };

        [Header("Textos")]
        [SerializeField] private TMP_Text distanceLabel;
        [SerializeField] private TMP_Text progressLabel;

        private readonly List<NeonShapeGraphic> markers = new List<NeonShapeGraphic>();
        private RectTransform rectTransform;
        private int shownDistance = -1;
        private int shownProgress = -1;

        public float Range
        {
            get => range;
            set => range = Mathf.Max(value, 0.1f);
        }

        public bool RotateWithShip
        {
            get => rotateWithShip;
            set => rotateWithShip = value;
        }

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
            if (markerTemplate != null) markerTemplate.gameObject.SetActive(false);

            float alpha = ringAlpha;
            if (QualitySettings.activeColorSpace == ColorSpace.Linear) alpha = Mathf.GammaToLinearSpace(alpha);
            if (rings == null) return;
            foreach (NeonShapeGraphic ring in rings)
            {
                if (ring == null) continue;
                Color color = ring.color;
                color.a = alpha;
                ring.color = color;
            }
        }

        private void LateUpdate()
        {
            UpdateLabels();
            if (ship == null) return;

            float heading = ship.eulerAngles.z;
            Quaternion toRadar = Quaternion.Euler(0f, 0f, rotateWithShip ? -heading : 0f);
            if (shipIcon != null) shipIcon.localRotation = Quaternion.Euler(0f, 0f, rotateWithShip ? 0f : heading);

            Rect rect = rectTransform.rect;
            float radius = Mathf.Max(Mathf.Min(rect.width, rect.height) * 0.5f - edgeInset, 0f);
            float scale = radius / range;
            Vector2 origin = ship.position;

            int used = 0;
            IReadOnlyList<RadarRegistry.Contact> contacts = RadarRegistry.Contacts;
            for (int i = 0; i < contacts.Count; i++)
            {
                RadarRegistry.Contact contact = contacts[i];
                if (!contact.IsVisible) continue;
                NeonShapeGraphic marker = GetMarker(used++);
                if (marker == null) break;

                Vector2 local = (Vector2)(toRadar * (contact.Position - origin)) * scale;
                if (local.sqrMagnitude > radius * radius) local = local.normalized * radius;

                ContactStyle style = StyleFor(contact.Type);
                marker.ShapeType = style.shape;
                marker.color = contact.Color;
                var markerRect = marker.rectTransform;
                var size = new Vector2(style.size, style.size);
                if (markerRect.sizeDelta != size) markerRect.sizeDelta = size;
                markerRect.anchoredPosition = local;
                if (!marker.gameObject.activeSelf) marker.gameObject.SetActive(true);
            }

            for (int i = used; i < markers.Count; i++)
                if (markers[i].gameObject.activeSelf) markers[i].gameObject.SetActive(false);
        }

        private void UpdateLabels()
        {
            if (levelManager == null) return;

            int distance = Mathf.CeilToInt(levelManager.DistanceToTarget);
            if (distanceLabel != null && distance != shownDistance)
            {
                shownDistance = distance;
                distanceLabel.SetText("{0}", distance);
            }

            int progress = Mathf.FloorToInt(levelManager.Progress * 100f);
            if (progressLabel != null && progress != shownProgress)
            {
                shownProgress = progress;
                progressLabel.SetText("{0}%", progress);
            }
        }

        private NeonShapeGraphic GetMarker(int index)
        {
            if (index < markers.Count) return markers[index];
            if (markerTemplate == null) return null;
            NeonShapeGraphic marker = Instantiate(markerTemplate, markerTemplate.transform.parent);
            marker.name = $"Marker_{index}";
            markers.Add(marker);
            return marker;
        }

        private ContactStyle StyleFor(RadarContactType type)
        {
            foreach (ContactStyle style in styles)
                if (style.type == type) return style;
            return new ContactStyle { type = type, shape = NeonShapeGraphic.Shape.Circle, size = 24f };
        }
    }
}
