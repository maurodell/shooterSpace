using System.Collections.Generic;
using UnityEngine;

namespace NavesShooter.Core
{
    public enum RadarContactType { Target, Enemy, Obstacle }

    // Contactos que muestra el radar del HUD. Vive en Core para que los sistemas de juego se registren sin conocer
    // a la UI: el radar lee `Contacts` cada frame. La posición sale del transform más un offset local (p. ej. el
    // centro de la zona de acople de la nave nodriza). Los objetos de pool se registran en OnEnable y se dan de baja
    // en OnDisable (ver RadarContact); los contactos inactivos o destruidos no se dibujan.
    public static class RadarRegistry
    {
        public readonly struct Contact
        {
            public readonly Transform Target;
            public readonly RadarContactType Type;
            public readonly Color Color;
            public readonly Vector2 LocalOffset;

            public Contact(Transform target, RadarContactType type, Color color, Vector2 localOffset)
            {
                Target = target;
                Type = type;
                Color = color;
                LocalOffset = localOffset;
            }

            public bool IsVisible => Target != null && Target.gameObject.activeInHierarchy;
            public Vector2 Position => Target.TransformPoint(LocalOffset);
        }

        private static readonly List<Contact> contacts = new List<Contact>();

        public static IReadOnlyList<Contact> Contacts => contacts;

        // Registrar dos veces el mismo transform actualiza su tipo, color y offset.
        public static void Register(Transform target, RadarContactType type, Color color, Vector2 localOffset = default)
        {
            if (target == null) return;
            var contact = new Contact(target, type, color, localOffset);
            int index = IndexOf(target);
            if (index >= 0) contacts[index] = contact;
            else contacts.Add(contact);
        }

        public static void Unregister(Transform target)
        {
            int index = IndexOf(target);
            if (index >= 0) contacts.RemoveAt(index);
        }

        private static int IndexOf(Transform target)
        {
            for (int i = 0; i < contacts.Count; i++)
                if (contacts[i].Target == target) return i;
            return -1;
        }

        // Sin recarga de dominio la lista sobreviviría entre sesiones de Play con transforms destruidos.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => contacts.Clear();
    }
}
