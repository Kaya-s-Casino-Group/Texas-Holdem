using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace RetroRendering.Editor
{
    public static class UICameraStackSetup
    {
        [MenuItem("工具/渲染/UI Camera/捕获运行画面")]
        public static void Capture()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            System.IO.Directory.CreateDirectory("Library/UICameraStackValidation");
            ScreenCapture.CaptureScreenshot("Library/UICameraStackValidation/game.png");
        }
        [MenuItem("工具/渲染/UI Camera/接入当前场景")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Configure UI cameras in Edit Mode.");
            var scene = SceneManager.GetActiveScene();
            var cameras = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).ToArray();
            var candidates = cameras.Where(c => c.CompareTag("MainCamera") &&
                c.GetUniversalAdditionalCameraData().renderType == CameraRenderType.Base).ToArray();
            if (candidates.Length != 1) throw new InvalidOperationException("Expected one MainCamera Base in the active scene.");
            var source = candidates[0];
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/UIRenderer.asset");
            if (pipeline == null || renderer == null) throw new InvalidOperationException("URP or UIRenderer is missing.");
            var list = new SerializedObject(pipeline).FindProperty("m_RendererDataList");
            int rendererIndex = -1;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == renderer) rendererIndex = i;
            if (rendererIndex < 0) throw new InvalidOperationException("Register UIRenderer in the active URP asset first.");
            var existing = cameras.Select(c => c.GetComponent<UICameraStack>()).Where(c => c != null && c.SceneCamera == source).ToArray();
            if (existing.Length > 1) throw new InvalidOperationException("Multiple UI overlays reference this Base camera.");
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Configure scene UI Camera Stack");
            var stack = existing.SingleOrDefault();
            if (stack == null)
            {
                var go = new GameObject("UI Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(go, scene);
                Undo.RegisterCreatedObjectUndo(go, "Create UI Camera");
                Undo.SetTransformParent(go.transform, source.transform, "Parent UI Camera");
                stack = Undo.AddComponent<UICameraStack>(go);
            }
            var camera = stack.UICamera;
            var data = camera.GetUniversalAdditionalCameraData();
            Undo.RecordObjects(new UnityEngine.Object[] { source, source.GetUniversalAdditionalCameraData(), stack, camera, data, camera.transform }, "Bind UI Camera");
            data.SetRenderer(rendererIndex);
            var serializedData = new SerializedObject(data);
            serializedData.FindProperty("m_ClearDepth").boolValue = true;
            serializedData.ApplyModifiedProperties();
            stack.Configure(source);
            foreach (var canvas in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Canvas>(true)).Where(c => c.isRootCanvas))
            {
                Undo.RecordObject(canvas, "Bind scene Canvas");
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
                    canvas.planeDistance = Mathf.Clamp(1f, camera.nearClipPlane + .01f, camera.farClipPlane - .01f);
                foreach (var child in canvas.GetComponentsInChildren<Transform>(true))
                {
                    Undo.RecordObject(child.gameObject, "Set UI layer");
                    child.gameObject.layer = LayerMask.NameToLayer("UI");
                }
                EditorUtility.SetDirty(canvas);
            }
            foreach (var obj in new UnityEngine.Object[] { source, source.GetUniversalAdditionalCameraData(), stack, camera, data }) EditorUtility.SetDirty(obj);
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(group);
            Debug.Log("UI Camera configured: " + source.name + " → " + camera.name + "; native resolution; renderer index " + rendererIndex);
        }
    }
}
