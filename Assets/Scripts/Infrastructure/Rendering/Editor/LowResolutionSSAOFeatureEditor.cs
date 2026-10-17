using UnityEditor;
using UnityEngine;

namespace RetroRendering.Editor
{
    [CustomEditor(typeof(LowResolutionSSAOFeature)), CanEditMultipleObjects]
    public sealed class LowResolutionSSAOFeatureEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.UpdateIfRequiredOrScript();
            EditorGUILayout.HelpBox("在 Renderer 内部尺寸下计算 SSAO，深度引导滤波后于升采样前合成。请在 Feature 列表中放到复古后处理之前；原生 UI 不受影响。", MessageType.None);
            var settings = serializedObject.FindProperty("settings");
            Field(settings, "intensity", "遮蔽强度", "0 跳过整个效果；对场景颜色做乘法遮蔽。");
            Field(settings, "radius", "采样半径（世界单位）", "跟随模型实际尺寸。本项目桌面默认 0.06 米；过大容易产生宽黑边。");
            Field(settings, "bias", "深度偏移（世界单位）", "减少平面自遮蔽。过大会丢失细小接触阴影。");
            Field(settings, "samples", "采样数", "低 / 中 / 高分别为 8 / 16 / 32 次深度采样。");
            Field(settings, "bilateralSharpness", "深度边缘锐度", "值越大越少跨深度边缘滤波，减轻轮廓漏黑。");
            Field(settings, "applyToRenderTextures", "应用于渲染纹理", "支持离屏 RenderTexture 和 Standard Render Request。");
            Field(settings, "applyToSceneView", "在场景视图中预览", "使用 Scene View 自身的内部渲染尺寸。");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("aoShader"), new GUIContent("SSAO 着色器"));
            EditorGUILayout.HelpBox("只处理 Base 场景相机，使用不透明深度重建法线。透明物体不贡献遮蔽，已混合的透明颜色也会被遮蔽；不支持 XR / Render Graph。", MessageType.None);
            serializedObject.ApplyModifiedProperties();
        }
        static void Field(SerializedProperty settings, string name, string label, string tip) =>
            EditorGUILayout.PropertyField(settings.FindPropertyRelative(name), new GUIContent(label, tip));
    }
}
