using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RetroRendering.Editor
{
    [CustomEditor(typeof(RetroPostProcessFeature)), CanEditMultipleObjects]
    public sealed class RetroPostProcessFeatureEditor : UnityEditor.Editor
    {
        SerializedProperty settings;
        SerializedProperty postShader;
        SerializedProperty injectionPoint;
        static readonly GUIContent[] EventLabels =
        {
            new GUIContent("透明物体渲染后"),
            new GUIContent("内置后处理前"),
            new GUIContent("内置后处理后"),
            new GUIContent("场景渲染结束时"),
            new GUIContent("升采样前（推荐）")
        };
        static readonly int[] EventValues =
        {
            (int)RenderPassEvent.AfterRenderingTransparents,
            (int)RenderPassEvent.BeforeRenderingPostProcessing,
            (int)RenderPassEvent.AfterRenderingPostProcessing,
            (int)RenderPassEvent.AfterRendering,
            (int)RenderPassEvent.BeforeUpscaling
        };

        void OnEnable()
        {
            settings = serializedObject.FindProperty("settings");
            postShader = serializedObject.FindProperty("postShader");
            injectionPoint = serializedObject.FindProperty("injectionPoint");
        }

        public override void OnInspectorGUI()
        {
            // URP's parent Inspector may already have a pending change to the feature's enable toggle.
            serializedObject.UpdateIfRequiredOrScript();
            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Clamp(EditorGUIUtility.currentViewWidth * 0.36f, 95f, 150f);
            try
            {
                EditorGUILayout.HelpBox("控制画面氛围。渲染分辨率和 Point 放大请在 Renderer 的「Low Resolution Rendering」中设置。", MessageType.None);

                Section("执行时机");
                EditorGUILayout.IntPopup(injectionPoint, EventLabels, EventValues,
                    new GUIContent("执行事件", "选择后处理执行阶段。推荐升采样前：在内部渲染分辨率下执行，然后 Point 放大到输出分辨率。"));
                if (!injectionPoint.hasMultipleDifferentValues && injectionPoint.intValue == (int)RenderPassEvent.BeforeUpscaling)
                    EditorGUILayout.HelpBox("场景渲染 → 后处理 → 升采样 → 屏幕 UI。未启用降分时，此事件仍在最终输出前执行。", MessageType.None);

                Section("作用范围");
                Field("applyToRenderTextures", "应用于渲染纹理", "同时处理输出到 RenderTexture 的游戏相机，包括离屏截图。");
                Field("applyToSceneView", "在场景视图中预览", "在编辑器 Scene 视图显示后处理效果，不改变场景视图的渲染分辨率。");

                Section("调色");
                Field("exposure", "曝光（档）", "每增加 1 档，线性亮度加倍；0 保持原始曝光。");
                Field("contrast", "对比度", "1 保持原始对比度；提高后明暗差异更明显。");
                Field("saturation", "饱和度", "0 为灰度，1 保持原始饱和度，超过 1 增强颜色。");
                Field("tint", "染色颜色", "与画面相乘的颜色。浅暖色可营造旧纸氛围，白色不偏色。");
                Field("tintStrength", "染色强度", "0 不染色，1 完全应用染色颜色。");

                Section("暗角");
                Field("vignette", "暗角强度", "压暗画面边缘；0 关闭暗角。");
                Field("vignetteSoftness", "暗角柔和度", "控制暗角向中心过渡的范围；越大，过渡越宽。");

                Section("色阶与抖动");
                Field("posterization", "色阶化强度", "将连续颜色混合为有限色阶；0 保留连续颜色，1 完全色阶化。");
                using (new EditorGUI.DisabledScope(IsZero("posterization")))
                {
                    Field("colorLevels", "每通道色阶数", "每个颜色通道的级数（2–256）。越少越有分层感；不是整幅画面的颜色总数。");
                    Field("dither", "有序抖动强度", "在色阶边界加入固定的 4×4 像素图案，减轻生硬色带；只在色阶化开启时生效。");
                }

                Section("色差");
                Field("chromaticOffset", "色差偏移（像素）", "范围 0–5。按内部渲染像素偏移红、蓝通道，越靠近边缘越明显；0 关闭。Point 采样下，较小偏移可能不明显。");

                Section("着色器");
                EditorGUILayout.PropertyField(postShader, new GUIContent("后处理着色器", "用于执行后处理的 Shader，默认使用项目内置的 RetroPost.shader。"));
                if (!postShader.hasMultipleDifferentValues && postShader.objectReferenceValue == null)
                    EditorGUILayout.HelpBox("尚未指定后处理着色器，效果不会执行。请指定 RetroPost.shader。", MessageType.Warning);

                serializedObject.ApplyModifiedProperties();
            }
            finally
            {
                EditorGUIUtility.labelWidth = previousLabelWidth;
            }
        }

        void Field(string name, string label, string tooltip)
        {
            // Preserve Range / ColorUsage drawers, mixed values, Undo and serialized asset editing.
            EditorGUILayout.PropertyField(settings.FindPropertyRelative(name), new GUIContent(label, tooltip));
        }

        bool IsZero(string name)
        {
            var property = settings.FindPropertyRelative(name);
            return !property.hasMultipleDifferentValues && property.floatValue <= 0;
        }

        static void Section(string title)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }
    }
}
