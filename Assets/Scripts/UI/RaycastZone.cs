using UnityEngine;
using UnityEngine.UI;

namespace NavesShooter.UI
{
    // Graphic invisible que solo sirve como raycast target: recibe toques en todo su rect sin dibujar nada.
    [RequireComponent(typeof(CanvasRenderer))]
    public class RaycastZone : Graphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
        }
    }
}
