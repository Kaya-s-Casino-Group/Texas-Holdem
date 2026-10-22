using UnityEngine;

namespace CardPresentation
{
    /// <summary>Shared discard motion for hand removals and non-scoring cards in a played hand.</summary>
    public static class CardDiscardMotion
    {
        public static float Travel(float progress)
        {
            float t = Mathf.Clamp01(progress);
            return t * t * t;
        }

        public static float Flip(float progress) => Mathf.SmoothStep(0, 1, Mathf.Clamp01(progress));

        public static Vector3 Position(Vector3 start, Vector3 destination, Vector3 lift, float progress)
        {
            float arc = Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI);
            return Vector3.Lerp(start, destination, Travel(progress)) + lift * (arc * arc);
        }
    }
}
