using UnityEngine;

namespace NavesShooter.Core
{
    // Área visible de una cámara sobre un plano z (el gameplay vive en z = 0). Sirve igual para cámara ortográfica
    // (orthographicSize, no depende del plano) y en perspectiva (distancia al plano · tan(fov/2)). La cámara nunca rota.
    public static class CameraView
    {
        // Medio ancho (x) y medio alto (y) visibles en el plano `planeZ`, en unidades de mundo.
        public static Vector2 HalfExtents(Camera cam, float planeZ = 0f)
        {
            float halfHeight = cam.orthographic
                ? cam.orthographicSize
                : Mathf.Abs(planeZ - cam.transform.position.z) * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            return new Vector2(halfHeight * cam.aspect, halfHeight);
        }

        // Semidiagonal del área visible en el plano: el círculo más chico centrado en la cámara que la contiene entera.
        public static float HalfDiagonal(Camera cam, float planeZ = 0f) => HalfExtents(cam, planeZ).magnitude;

        // Distancia al plano a la que una cámara en perspectiva con `fieldOfView` ve `halfHeight` de medio alto.
        public static float DistanceForHalfHeight(float halfHeight, float fieldOfView) =>
            halfHeight / Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
    }
}
