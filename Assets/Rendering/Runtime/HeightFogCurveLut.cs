using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace RetroRendering
{
    public enum HeightFogFalloff
    {
        [InspectorName("Linear / 线性")] Linear,
        [InspectorName("Smooth / 平滑")] Smooth,
        [InspectorName("Ground Hugging / 贴地浓雾")] GroundHugging,
        [InspectorName("High Fill / 高处缓降")] HighFill,
        [InspectorName("Custom / 自定义曲线")] Custom
    }

    /// <summary>Piecewise-linear density and its matching cumulative integral; rebuilt only on edits.</summary>
    public sealed class HeightFogCurveLut : IDisposable
    {
        public const int Segments = 256;
        public Texture2D Texture { get; private set; }
        HeightFogFalloff cachedMode;
        Keyframe[] cachedKeys;
        WrapMode cachedPre, cachedPost;
        bool cachedNull;

        public static float Evaluate(HeightFogFalloff mode, AnimationCurve curve, float t)
        {
            if (t <= 0) return 1;
            if (t >= 1) return 0;
            float value;
            switch (mode)
            {
                case HeightFogFalloff.Smooth: value = 1 - t * t * (3 - 2 * t); break;
                case HeightFogFalloff.GroundHugging: value = (1 - t) * (1 - t) * (1 - t); break;
                case HeightFogFalloff.HighFill: value = 1 - t * t * t; break;
                case HeightFogFalloff.Custom: value = curve != null && curve.length > 0 ? curve.Evaluate(t) : 1 - t; break;
                default: value = 1 - t; break;
            }
            return float.IsNaN(value) || float.IsInfinity(value) ? 1 - t : Mathf.Clamp01(value);
        }

        public static Vector2[] Bake(HeightFogFalloff mode, AnimationCurve curve)
        {
            var values = new Vector2[Segments + 1];
            values[0] = new Vector2(1, 0);
            for (int i = 1; i <= Segments; i++)
            {
                float density = Evaluate(mode, curve, i / (float)Segments);
                values[i] = new Vector2(density, values[i - 1].y + (values[i - 1].x + density) * (.5f / Segments));
            }
            return values;
        }

        public bool Update(HeightFogFalloff mode, AnimationCurve curve)
        {
            if (Texture != null && mode == cachedMode && (mode != HeightFogFalloff.Custom || Matches(curve))) return false;
            if (Texture == null)
                Texture = new Texture2D(Segments + 1, 1, TextureFormat.RGFloat, false, true)
                { name = "Height Fog Curve (Density + Integral)", hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point };
            Texture.SetPixelData(Bake(mode, curve), 0); Texture.Apply(false, false);
            cachedMode = mode; cachedNull = curve == null;
            cachedKeys = curve?.keys;
            if (curve != null) { cachedPre = curve.preWrapMode; cachedPost = curve.postWrapMode; }
            return true;
        }

        bool Matches(AnimationCurve curve)
        {
            if (curve == null) return cachedNull;
            if (cachedNull || cachedKeys == null || curve.length != cachedKeys.Length ||
                curve.preWrapMode != cachedPre || curve.postWrapMode != cachedPost) return false;
            // Indexer avoids allocating a fresh keys array every frame; include weighted tangent edits.
            for (int i = 0; i < cachedKeys.Length; i++)
            {
                var a = curve[i]; var b = cachedKeys[i];
                if (a.time != b.time || a.value != b.value || a.inTangent != b.inTangent || a.outTangent != b.outTangent ||
                    a.inWeight != b.inWeight || a.outWeight != b.outWeight || a.weightedMode != b.weightedMode) return false;
            }
            return true;
        }

        public void Dispose() { CoreUtils.Destroy(Texture); Texture = null; cachedKeys = null; }
    }
}
