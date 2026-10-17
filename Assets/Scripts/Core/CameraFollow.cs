using UnityEngine;

namespace NavesShooter.Core
{
    // Sigue al objetivo con SmoothDamp y un adelanto en su dirección de avance (target.up).
    // Nunca toca la rotación: la cámara mantiene siempre la misma orientación.
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField, Tooltip("Tiempo aproximado en segundos para alcanzar la posición deseada.")]
        private float smoothTime = 0.2f;
        [SerializeField, Tooltip("Unidades de adelanto en la dirección en la que avanza el objetivo.")]
        private float lookAhead = 2.5f;

        private Vector3 velocity;

        private void Start()
        {
            if (target != null) transform.position = DesiredPosition();
        }

        private void LateUpdate()
        {
            if (target == null) return;
            transform.position = Vector3.SmoothDamp(transform.position, DesiredPosition(), ref velocity, smoothTime);
        }

        private Vector3 DesiredPosition()
        {
            Vector3 desired = target.position + target.up * lookAhead;
            desired.z = transform.position.z;
            return desired;
        }
    }
}
