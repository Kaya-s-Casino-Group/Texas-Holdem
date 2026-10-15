using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RetroRendering.Editor
{
    public static class BloomSetup
    {
        public const string RendererPath = "Assets/Settings/MainDeferredRenderer.asset";
        public const string ShaderPath = "Assets/Rendering/Shaders/Bloom.shader";

        [MenuItem("Tools/Rendering/Install Bloom")]
        public static void Install()
        {
            if (Application.isPlaying) throw new InvalidOperationException("请退出 Play 后安装 Bloom。");
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (data == null || shader == null) throw new InvalidOperationException("Bloom Shader 或主 Renderer 尚未导入。");
            var bloom = data.rendererFeatures.OfType<BloomFeature>().FirstOrDefault();
            if (bloom == null)
            {
                bloom = ScriptableObject.CreateInstance<BloomFeature>(); bloom.name = "Bloom";
                AssetDatabase.AddObjectToAsset(bloom, data);
            }
            bloom.BloomShader = shader;
            data.rendererFeatures.Remove(bloom);
            int index = data.rendererFeatures.FindIndex(item => item is RetroPostProcessFeature);
            data.rendererFeatures.Insert(index < 0 ? data.rendererFeatures.Count : index, bloom);
            var volumetric = data.rendererFeatures.OfType<VolumetricLightFeature>().FirstOrDefault();
            if (volumetric != null) { volumetric.name = "体积光"; EditorUtility.SetDirty(volumetric); }
            var serialized = new SerializedObject(data);
            var map = serialized.FindProperty("m_RendererFeatureMap"); map.arraySize = data.rendererFeatures.Count;
            for (int i = 0; i < map.arraySize; ++i)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(data.rendererFeatures[i], out string guid, out long localId);
                map.GetArrayElementAtIndex(i).longValue = localId;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo(); bloom.Create();
            EditorUtility.SetDirty(bloom); data.SetDirty(); EditorUtility.SetDirty(data); AssetDatabase.SaveAssetIfDirty(data);
            Debug.Log("Bloom 已安装：体积光 → Bloom → 复古后处理 → Point 放大 → 原生 UI。");
        }
    }

    [CustomEditor(typeof(BloomFeature))]
    public sealed class BloomFeatureEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("常规 HDR Bloom：柔和阈值提取高光，多级模糊后叠加光晕。使用场景内部尺寸，在复古调色、Point 放大和 UI 之前执行；Intensity 为 0 时跳过。", MessageType.Info);
            DrawDefaultInspector();
        }
    }
}
