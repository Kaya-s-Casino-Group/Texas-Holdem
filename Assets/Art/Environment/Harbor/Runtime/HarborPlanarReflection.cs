using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HarborEnvironment
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class HarborPlanarReflection : MonoBehaviour
    {
        [Range(128, 2048)] public int resolution = 512;
        public float waterHeight = -.12f;
        [Tooltip("Only these layers are rendered into the water reflection.")]
        public LayerMask reflectionLayers = ~(1 << 4);
        Camera reflectionCamera;
        RenderTexture texture;
        static bool rendering;
        static readonly int ReflectionTexture = Shader.PropertyToID("_HarborPlanarReflection");
        static readonly int ReflectionVP = Shader.PropertyToID("_HarborReflectionVP");
        static readonly int HasReflection = Shader.PropertyToID("_HarborHasReflection");

        void OnEnable() => RenderPipelineManager.beginCameraRendering += Reflect;
        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= Reflect;
            Shader.SetGlobalFloat(HasReflection, 0);
            if (reflectionCamera) DestroyOwned(reflectionCamera.gameObject);
            if (texture) { texture.Release(); DestroyOwned(texture); }
        }
        static void DestroyOwned(Object obj)
        { if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj); }
        void Reflect(ScriptableRenderContext context, Camera source)
        {
            if (rendering || !isActiveAndEnabled || source.cameraType == CameraType.Reflection ||
                source.cameraType == CameraType.Preview || source == reflectionCamera ||
                (source.TryGetComponent<UniversalAdditionalCameraData>(out var sourceData) && sourceData.renderType == CameraRenderType.Overlay)) return;
            if (!reflectionCamera)
            {
                var go = new GameObject("Harbor Planar Reflection") { hideFlags = HideFlags.HideAndDontSave };
                reflectionCamera = go.AddComponent<Camera>(); reflectionCamera.enabled = false;
                var data = go.AddComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = false; data.requiresColorOption = CameraOverrideOption.Off;
                data.requiresDepthOption = CameraOverrideOption.Off;
                data.renderShadows = false;
            }
            if (!texture || texture.width != resolution)
            {
                if (texture) { texture.Release(); DestroyOwned(texture); }
                texture = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.ARGBHalf)
                { name = "Harbor Planar Reflection", hideFlags = HideFlags.HideAndDontSave };
                texture.Create();
            }
            reflectionCamera.CopyFrom(source); reflectionCamera.enabled = false;
            reflectionCamera.cameraType = CameraType.Reflection;
            reflectionCamera.cullingMask = source.cullingMask & reflectionLayers.value & ~(1 << 4);
            reflectionCamera.targetTexture = texture; reflectionCamera.allowMSAA = false;
            var reflection = Matrix4x4.identity; reflection.m11 = -1; reflection.m13 = 2 * waterHeight;
            var position = source.transform.position; position.y = 2 * waterHeight - position.y;
            reflectionCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(
                reflection.MultiplyVector(source.transform.forward), reflection.MultiplyVector(source.transform.up)));
            reflectionCamera.worldToCameraMatrix = source.worldToCameraMatrix * reflection;
            var planePosition = new Vector3(0, waterHeight + .015f, 0);
            var view = reflectionCamera.worldToCameraMatrix;
            var point = view.MultiplyPoint(planePosition); var normal = view.MultiplyVector(Vector3.up).normalized;
            reflectionCamera.projectionMatrix = source.CalculateObliqueMatrix(new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(point, normal)));
            bool oldInvert = GL.invertCulling;
            rendering = true;
            try
            {
                GL.invertCulling = !oldInvert;
                UniversalRenderPipeline.RenderSingleCamera(context, reflectionCamera);
                Shader.SetGlobalTexture(ReflectionTexture, texture);
                Shader.SetGlobalMatrix(ReflectionVP, GL.GetGPUProjectionMatrix(reflectionCamera.projectionMatrix, true) * reflectionCamera.worldToCameraMatrix);
                Shader.SetGlobalFloat(HasReflection, 1);
            }
            finally { GL.invertCulling = oldInvert; rendering = false; }
        }
    }
}
