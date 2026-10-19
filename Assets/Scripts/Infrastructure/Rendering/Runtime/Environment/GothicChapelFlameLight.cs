using UnityEngine;

namespace RetroRendering
{
    /// <summary>Drives a chapel flame and its point light with the same flicker sample.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Light))]
    [AddComponentMenu("Rendering/Gothic Chapel Flame Light")]
    public sealed class GothicChapelFlameLight : MonoBehaviour
    {
        private static readonly int FlickerAmountId = Shader.PropertyToID("_FlickerAmount");
        private static readonly int FlickerSpeedId = Shader.PropertyToID("_FlickerSpeed");
        private static readonly int AnimationTimeId = Shader.PropertyToID("_AnimationTime");
        private static readonly int FlickerOverrideId = Shader.PropertyToID("_FlickerOverride");

        [SerializeField, Tooltip("使用 Gothic Chapel/Candle Flame 材质的火焰 Renderer。")]
        private Renderer flameRenderer;

        [SerializeField, Min(0), Tooltip("闪烁倍率为 1 时的点光强度。")]
        private float baseIntensity = 1f;

        [SerializeField, Min(0), Tooltip("烛火与点光的同步闪烁速度倍率；1 为材质原速，0 为暂停闪烁。")]
        private float speedMultiplier = 0.3f;

        private Light pointLight;
        private Renderer boundRenderer;
        private MaterialPropertyBlock properties;
        private float originalIntensity;
        private float originalOverride;

        private void Reset()
        {
            pointLight = GetComponent<Light>();
            baseIntensity = pointLight.intensity;
            flameRenderer = GetComponent<Renderer>();
            if (flameRenderer == null)
                flameRenderer = GetComponentInChildren<Renderer>();
        }

        private void OnEnable()
        {
            pointLight = GetComponent<Light>();
            originalIntensity = pointLight.intensity;
            properties = new MaterialPropertyBlock();
        }

        private void LateUpdate()
        {
            if (boundRenderer != flameRenderer)
            {
                ReleaseFlame();
                boundRenderer = flameRenderer;
                if (boundRenderer != null)
                {
                    boundRenderer.GetPropertyBlock(properties);
                    originalOverride = properties.HasFloat(FlickerOverrideId)
                        ? properties.GetFloat(FlickerOverrideId) : -1f;
                }
            }

            Material material = boundRenderer != null ? boundRenderer.sharedMaterial : null;
            if (pointLight.type != LightType.Point || material == null ||
                material.shader.name != "Gothic Chapel/Candle Flame")
            {
                ReleaseFlame();
                pointLight.intensity = originalIntensity;
                return;
            }

            boundRenderer.GetPropertyBlock(properties);
            float amount = ReadFloat(material, FlickerAmountId);
            float speed = ReadFloat(material, FlickerSpeedId) * Mathf.Max(0f, speedMultiplier);
            float animationTime = ReadFloat(material, AnimationTimeId);
            float time = animationTime >= 0f ? animationTime : Time.time;
            Vector3 position = boundRenderer.transform.position;
            float seed = Vector3.Dot(position, new Vector3(13.13f, 7.71f, 19.19f));
            float noise = 0.64f * Noise(time * speed + seed)
                + 0.36f * Noise(time * speed * 2.31f + seed * 3.7f);
            float flicker = Mathf.Max(0.15f, 1f + (noise - 0.5f) * 2f * amount);

            // Share the CPU sample: sin/hash precision differs between CPU and GPU.
            properties.SetFloat(FlickerOverrideId, flicker);
            boundRenderer.SetPropertyBlock(properties);
            pointLight.intensity = baseIntensity * flicker;
        }

        private float ReadFloat(Material material, int id)
        {
            return properties.HasFloat(id) ? properties.GetFloat(id) : material.GetFloat(id);
        }

        private static float Noise(float value)
        {
            float cell = Mathf.Floor(value);
            float fraction = value - cell;
            fraction = fraction * fraction * (3f - 2f * fraction);
            return Mathf.Lerp(Hash(cell), Hash(cell + 1f), fraction);
        }

        private static float Hash(float value)
        {
            float hash = Mathf.Sin(value * 127.1f) * 43758.5453f;
            return hash - Mathf.Floor(hash);
        }

        private void ReleaseFlame()
        {
            if (boundRenderer == null)
                return;

            boundRenderer.GetPropertyBlock(properties);
            properties.SetFloat(FlickerOverrideId, originalOverride);
            boundRenderer.SetPropertyBlock(properties);
            boundRenderer = null;
        }

        private void OnDisable()
        {
            ReleaseFlame();
            if (pointLight != null)
                pointLight.intensity = originalIntensity;
        }
    }
}
