using CardPresentation;
using UnityEngine;

namespace CardInteraction
{
    /// <summary>Ray tests against rendered card bounds in mesh space; no Collider or physics-world dependency.</summary>
    public static class CardZoneRaycaster
    {
        public static bool TryHit(CardZoneView zone, Ray ray, string hoveredViewId, out CardInstanceView hit)
        {
            hit = null;
            if (zone == null || !zone.isActiveAndEnabled || !Finite(ray.origin) || !Finite(ray.direction) || ray.direction.sqrMagnitude < 0.000001f)
                return false;
            ray = new Ray(ray.origin, ray.direction.normalized);
            float nearest = float.PositiveInfinity;
            foreach (var view in zone.Views)
            {
                if (view == null || !view.isActiveAndEnabled || view.IsEntering || view.Surface == null) continue;
                var surface = view.Surface.transform;
                var renderer = view.Surface.GetComponent<MeshRenderer>();
                var mesh = view.Surface.GetComponent<MeshFilter>().sharedMesh;
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || mesh == null) continue;
                float distance = Intersect(surface, mesh.bounds, ray, Vector3.zero);
                // Retain the hovered card's resting footprint as well as its raised footprint.
                // Otherwise +Z lift can move the card out from under a stationary pointer and flicker.
                if (view.ViewId == hoveredViewId && view.Data.Interaction.IsRaised)
                    distance = Mathf.Min(distance, Intersect(surface, mesh.bounds, ray, zone.transform.TransformVector(view.InteractionOffset)));
                // Later cards win equal-depth overlap, consistent with zone stacking order.
                if (float.IsPositiveInfinity(distance) || distance > nearest + 0.000001f) continue;
                nearest = distance; hit = view;
            }
            return hit != null;
        }

        private static float Intersect(Transform surface, Bounds bounds, Ray ray, Vector3 displacedOrigin)
        {
            var local = new Ray(surface.InverseTransformPoint(ray.origin + displacedOrigin), surface.InverseTransformVector(ray.direction));
            if (!bounds.IntersectRay(local, out float distance)) return float.PositiveInfinity;
            var point = surface.TransformPoint(local.GetPoint(distance)) - displacedOrigin;
            float forward = Vector3.Dot(point - ray.origin, ray.direction);
            return forward >= 0 ? forward : float.PositiveInfinity;
        }

        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
