using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RetroRendering.Editor
{
    public static class HeightFogSetup
    {
        public const string RendererPath = "Assets/Settings/MainDeferredRenderer.asset";
        public const string FogShaderPath = "Assets/Rendering/Shaders/HeightFog.shader";
        public const string LightShaderPath = "Assets/Rendering/Shaders/VolumetricLight.shader";

        [MenuItem("工具/渲染/高度雾与体积光/启用默认三维噪声")]
        public static void EnableDefaultNoise()
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            var fog = data.rendererFeatures.OfType<HeightFogFeature>().Single();
            var texture = AssetDatabase.LoadAssetAtPath<Texture3D>(VolumeNoiseGenerator.DefaultPath);
            if (texture == null) throw new InvalidOperationException("请先生成默认 32³ 噪声纹理。");
            Undo.RecordObject(fog, "Enable 3D Fog Noise");
            if (fog.settings.noiseTexture == null) fog.settings.noiseTexture = texture;
            fog.settings.useNoiseTexture = true;
            EditorUtility.SetDirty(fog); AssetDatabase.SaveAssetIfDirty(fog);
            Debug.Log("已启用高度雾的动态 3D 噪声，体积光共用相同密度与步进；其他雾和灯光参数保持当前设置。");
        }

        [MenuItem("工具/渲染/高度雾与体积光/安装效果")]
        public static void Install()
        {
            if (Application.isPlaying) throw new InvalidOperationException("请退出 Play 后安装高度雾。");
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            var fogShader = AssetDatabase.LoadAssetAtPath<Shader>(FogShaderPath);
            var lightShader = AssetDatabase.LoadAssetAtPath<Shader>(LightShaderPath);
            if (data == null || fogShader == null || lightShader == null)
                throw new InvalidOperationException("高度雾 Shader 或主 Renderer 尚未导入。");
            T GetOrCreate<T>(string label) where T : ScriptableRendererFeature
            {
                var existing = data.rendererFeatures.OfType<T>().FirstOrDefault();
                if (existing != null) return existing;
                var item = ScriptableObject.CreateInstance<T>(); item.name = label;
                AssetDatabase.AddObjectToAsset(item, data);
                return item;
            }
            var fog = GetOrCreate<HeightFogFeature>("高度雾");
            var light = GetOrCreate<VolumetricLightFeature>("体积光");
            fog.FogShader = fogShader; light.ScatteringShader = lightShader; light.heightFog = fog;
            fog.SetActive(true); light.SetActive(true);
            data.rendererFeatures.Remove(fog); data.rendererFeatures.Remove(light);
            int index = data.rendererFeatures.FindIndex(item => item is BloomFeature || item is RetroPostProcessFeature);
            if (index < 0) index = data.rendererFeatures.Count;
            data.rendererFeatures.Insert(index, fog); data.rendererFeatures.Insert(index + 1, light);
            var serialized = new SerializedObject(data);
            var map = serialized.FindProperty("m_RendererFeatureMap");
            map.arraySize = data.rendererFeatures.Count;
            for (int i = 0; i < map.arraySize; ++i)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(data.rendererFeatures[i], out string guid, out long localId);
                map.GetArrayElementAtIndex(i).longValue = localId;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            fog.Create(); light.Create();
            EditorUtility.SetDirty(fog); EditorUtility.SetDirty(light); data.SetDirty(); EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            Debug.Log("高度雾与体积光已安装：SSAO → 高度雾 → 体积光 → Bloom（如已安装）→ 复古后处理 → Point 放大 → UI。");
        }
    }

    [CustomEditor(typeof(HeightFogFeature))]
    public sealed class HeightFogFeatureEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("高度雾独立控制密度、雾色和透射衰减。基础高度以下保持完整密度；最大高度处减为零。关闭地面裁切时忽略 Ground Height。", MessageType.Info);
            DrawDefaultInspector();
            var fog = (HeightFogFeature)target;
            if (fog.settings.heightFalloff != HeightFogFalloff.Linear)
                EditorGUILayout.HelpBox("Height Falloff 选择高度分布。Custom 使用 Height Curve：横轴为归一化高度，纵轴为密度倍率，端点固定 1→0。曲线由雾和体积光共用，基础高度以下保持完整密度。", MessageType.Info);
            if (fog.settings.useNoiseTexture)
            {
                EditorGUILayout.HelpBox("3D 纹理读取 R 通道，强制 Repeat + 线性采样。Noise World Size 是世界空间平铺尺寸，Noise Velocity 是移动速度。纹理模式用 Noise Steps 同时积分雾与体积光；无纹理或强度为零时回退解析高度雾。", MessageType.Info);
                if (fog.settings.noiseTexture == null)
                    EditorGUILayout.HelpBox("尚未指定 Texture3D，当前仍使用解析高度雾。", MessageType.Warning);
            }
            if (!((HeightFogFeature)target).settings.IsValid)
                EditorGUILayout.HelpBox("当前密度、距离或高度范围使雾停用；依赖它的体积光也会停用。", MessageType.Warning);
        }
    }

    [CustomEditor(typeof(VolumetricLightFeature))]
    public sealed class VolumetricLightFeatureEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("依赖同一 Renderer 中排在前面的高度雾。主光使用本 Feature 参数；额外灯光只有挂载并启用 Volumetric Light 才参与体积光。高度雾启用 3D 纹理时共用其 Noise Steps 和固定中点采样，本处 Steps / Jitter 仅用于解析高度雾模式。", MessageType.Info);
            DrawDefaultInspector();
            var light = (VolumetricLightFeature)target;
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GetAssetPath(target));
            if (light.heightFog == null || !light.heightFog.isActive || (data != null &&
                (data.rendererFeatures.IndexOf(light.heightFog) < 0 || data.rendererFeatures.IndexOf(light.heightFog) >= data.rendererFeatures.IndexOf(light))))
                EditorGUILayout.HelpBox("请引用同一 Renderer 中启用且排在本 Feature 前面的高度雾。可执行 Install Height Fog and Volumetric Light 修复主 Renderer 的依赖与顺序。", MessageType.Warning);
        }
    }
}
