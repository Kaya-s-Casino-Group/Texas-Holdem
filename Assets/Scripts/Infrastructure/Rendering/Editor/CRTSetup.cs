using System;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RetroRendering.Editor
{
    public static class CRTSetup
    {
        public const string RendererPath = "Assets/Settings/UIRenderer.asset";
        [MenuItem("工具/渲染/老电视 CRT/当前场景 Overlay UI 改由 UI Camera 绘制")]
        public static void RouteCurrentOverlayUI()
        {
            if (Application.isPlaying) throw new InvalidOperationException("请退出 Play 后修改场景 Canvas。");
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var stack = roots.SelectMany(root => root.GetComponentsInChildren<UICameraStack>(true)).Single(item => item.SceneCamera != null);
            var camera = stack.UICamera;
            var canvases = roots.SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
                .Where(canvas => canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Overlay UI 改由 UI Camera 绘制");
            foreach (var canvas in canvases)
            {
                Undo.RecordObject(canvas,"设置 UI Canvas 相机");
                canvas.worldCamera = camera; canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.planeDistance = Mathf.Clamp(1,camera.nearClipPlane+.001f,camera.farClipPlane-.001f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(canvas);
                foreach (var child in canvas.GetComponentsInChildren<Transform>(true))
                {
                    if (child.gameObject.layer == LayerMask.NameToLayer("UI")) continue;
                    Undo.RecordObject(child.gameObject,"设置 UI Layer");
                    child.gameObject.layer = LayerMask.NameToLayer("UI");
                    PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);
                }
                EditorUtility.SetDirty(canvas);
            }
            Canvas.ForceUpdateCanvases();
            foreach (var canvas in canvases)
                if (canvas.renderMode != RenderMode.ScreenSpaceCamera || canvas.worldCamera != camera)
                    throw new InvalidOperationException("Canvas 未保持 UI Camera 绘制: "+canvas.name);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("保存场景失败。");
            Debug.Log("已保存 "+scene.name+"："+string.Join("、",canvases.Select(canvas => canvas.name))+" → "+camera.name+"，Screen Space - Camera，原排序保留。");
        }

        [MenuItem("工具/渲染/老电视 CRT/安装到最终 UI Renderer")]
        public static void Install()
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            var feature = data.rendererFeatures.OfType<CRTFeature>().FirstOrDefault();
            if (!feature)
            {
                feature = ScriptableObject.CreateInstance<CRTFeature>(); feature.name = "老电视 CRT（最终输出）";
                AssetDatabase.AddObjectToAsset(feature,data); data.rendererFeatures.Add(feature);
            }
            feature.CRTShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/GameAssets/Rendering/Shaders/CRT.shader");
            var serialized = new SerializedObject(data);
            var map = serialized.FindProperty("m_RendererFeatureMap"); map.arraySize = data.rendererFeatures.Count;
            for (int i=0;i<map.arraySize;i++)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(data.rendererFeatures[i],out string guid,out long id);
                map.GetArrayElementAtIndex(i).longValue = id;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            feature.Create(); EditorUtility.SetDirty(feature); data.SetDirty(); EditorUtility.SetDirty(data); AssetDatabase.SaveAssetIfDirty(data);
            Debug.Log("CRT installed on UI Renderer: scene → native UI → CRT → display.");
        }
    }
    [CustomEditor(typeof(CRTFeature)),CanEditMultipleObjects]
    public sealed class CRTFeatureEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.UpdateIfRequiredOrScript();
            EditorGUILayout.HelpBox("整条相机栈合成后执行 CRT。安装在最后一台相机使用的 Renderer 上；当前管线为 UI Renderer。场景、描边和 UI 一起进入显像管屏幕。",MessageType.None);
            var settings = serializedObject.FindProperty("settings");
            void Field(string name,string label) => EditorGUILayout.PropertyField(settings.FindPropertyRelative(name),new GUIContent(label));
            Field("crtEnabled","启用老电视 CRT");
            Field("crtIntensity","总强度");
            Field("crtCurvature","屏幕曲率");
            Field("crtScanlinePitch","扫描线间距（屏幕像素）");
            Field("crtScanlineStrength","扫描线强度");
            Field("crtMaskStrength","RGB 荧光栅格");
            Field("crtEdgeSoftness","屏幕边缘柔化");
            Field("applyToRenderTextures","应用于渲染纹理");
            Field("applyToSceneView","在 Scene 视图预览");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("crtShader"),new GUIContent("CRT 着色器"));
            serializedObject.ApplyModifiedProperties();
        }
    }
}
