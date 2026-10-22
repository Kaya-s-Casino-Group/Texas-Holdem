using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>Overlapping PSD layers pass clicks through their transparent pixels.</summary>
    public sealed class BattleArtworkRaycastFilter : MonoBehaviour, ICanvasRaycastFilter
    {
        private Transform surface;
        private Texture2D texture;
        private Camera viewCamera;
        public void Configure(Transform plane, Texture2D pixels, Camera camera)
        { surface = plane; texture = pixels; viewCamera = camera; }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (surface == null || texture == null || viewCamera == null) return true;
            var ray = viewCamera.ScreenPointToRay(screenPoint);
            var plane = new Plane(surface.forward, surface.position);
            if (!plane.Raycast(ray, out float distance)) return false;
            var local = surface.InverseTransformPoint(ray.GetPoint(distance));
            float u = local.x + .5f, v = local.y + .5f;
            return u >= 0 && u <= 1 && v >= 0 && v <= 1 && texture.GetPixelBilinear(u, v).a > .1f;
        }
    }
}
