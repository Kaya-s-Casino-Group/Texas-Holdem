using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RetroRendering.Editor
{
    public static class LowResolutionSSAOSetup
    {
        public const string RendererPath = "Assets/Settings/MainDeferredRenderer.asset";
        public const string ShaderPath = "Assets/Rendering/Shaders/LowResolutionSSAO.shader";

        [MenuItem("Tools/Rendering/Install Low Resolution SSAO")]
        public static void Install()
        {
            if (Application.isPlaying) throw new InvalidOperationException("请退出 Play 后安装 SSAO。");
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (data == null || shader == null) throw new InvalidOperationException("SSAO Shader 或主 Renderer 尚未导入。");
            var feature = data.rendererFeatures.OfType<LowResolutionSSAOFeature>().FirstOrDefault();
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<LowResolutionSSAOFeature>();
                feature.name = "低分辨率 SSAO";
                AssetDatabase.AddObjectToAsset(feature,data);
            }
            feature.AOShader = shader; feature.SetActive(true); feature.Create();
            data.rendererFeatures.Remove(feature);
            int retroIndex = data.rendererFeatures.FindIndex(item => item is HeightFogFeature ||
                item is VolumetricLightFeature || item is BloomFeature || item is RetroPostProcessFeature);
            data.rendererFeatures.Insert(retroIndex < 0 ? 0 : retroIndex,feature);
            var serialized = new SerializedObject(data);
            var map = serialized.FindProperty("m_RendererFeatureMap");
            map.arraySize = data.rendererFeatures.Count;
            for (int i = 0; i < map.arraySize; ++i)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(data.rendererFeatures[i],out string guid,out long localId);
                map.GetArrayElementAtIndex(i).longValue = localId;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(feature); data.SetDirty(); EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            Debug.Log("低分辨率 SSAO 已安装：BeforeUpscaling，位于复古后处理之前。");
        }
    }
}
