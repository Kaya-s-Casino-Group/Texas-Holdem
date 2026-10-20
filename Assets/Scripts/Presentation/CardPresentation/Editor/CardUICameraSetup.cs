#if UNITY_EDITOR
using System;
using System.Linq;
using RetroRendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using System.IO;

namespace CardPresentation.Editor
{
    public static class CardUICameraSetup
    {
        public const string RendererPath = "Assets/Settings/BattleForwardRenderer.asset";
        public const string CameraName = "UI Camera";

        public static UniversalRendererData EnsureRenderer()
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (data != null) return data;
            data = ScriptableObject.CreateInstance<UniversalRendererData>(); data.name = "BattleForwardRenderer";
            var serialized = new SerializedObject(data);
            serialized.FindProperty("m_RendererFeatureMap").arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(data, RendererPath); data.SetDirty(); AssetDatabase.SaveAssetIfDirty(data);
            return data;
        }

        private static int RegisterRenderer(UniversalRendererData renderer)
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null) throw new InvalidOperationException("UI Camera Stack requires the active URP pipeline.");
            var serialized = new SerializedObject(pipeline); var list = serialized.FindProperty("m_RendererDataList");
            for (int i = 0; i < list.arraySize; ++i)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == renderer) return i;
            Undo.RecordObject(pipeline, "注册 UI Renderer");
            int index = list.arraySize++; list.GetArrayElementAtIndex(index).objectReferenceValue = renderer;
            serialized.ApplyModifiedProperties(); EditorUtility.SetDirty(pipeline); AssetDatabase.SaveAssetIfDirty(pipeline);
            return index;
        }

        public static Camera Register(Scene scene, Camera sceneCamera = null)
        {
            if (sceneCamera == null || sceneCamera.gameObject.scene != scene)
            {
                sceneCamera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                    .FirstOrDefault(camera => camera.CompareTag("MainCamera"));
            }
            // Data-only preview scenes do not require a camera. Do not bind them to another scene's Main Camera.
            if (sceneCamera == null) return null;
            if (sceneCamera.GetUniversalAdditionalCameraData().renderType != CameraRenderType.Base)
                throw new InvalidOperationException("请选择场景的 Base 摄像机。");
            int index = RegisterRenderer(EnsureRenderer());
            var rig = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<UICameraStack>(true))
                .FirstOrDefault(item => item.SceneCamera == sceneCamera);
            if (rig == null)
            {
                var root = new GameObject(CameraName); SceneManager.MoveGameObjectToScene(root, scene);
                root.transform.SetParent(sceneCamera.transform, false);
                Undo.RegisterCreatedObjectUndo(root, "创建 UI Camera");
                Undo.AddComponent<Camera>(root); Undo.AddComponent<UniversalAdditionalCameraData>(root);
                rig = Undo.AddComponent<UICameraStack>(root);
            }
            var uiCamera = rig.UICamera; var baseData = sceneCamera.GetUniversalAdditionalCameraData();
            Undo.RecordObjects(new UnityEngine.Object[] { rig, uiCamera, uiCamera.transform, sceneCamera, baseData,
                uiCamera.GetUniversalAdditionalCameraData() }, "配置 UI Camera Stack");
            uiCamera.GetUniversalAdditionalCameraData().SetRenderer(index);
            var uiData = new SerializedObject(uiCamera.GetUniversalAdditionalCameraData());
            uiData.FindProperty("m_ClearDepth").boolValue = true;
            uiData.FindProperty("m_VolumeFrameworkUpdateModeOption").enumValueIndex = (int)VolumeFrameworkUpdateMode.ViaScripting;
            uiData.ApplyModifiedProperties();
            rig.Configure(sceneCamera);
            foreach (var item in new UnityEngine.Object[] { rig, uiCamera, uiCamera.GetUniversalAdditionalCameraData(), sceneCamera, baseData })
                EditorUtility.SetDirty(item);
            EditorSceneManager.MarkSceneDirty(scene); return uiCamera;
        }

        [MenuItem("工具/卡牌表现/截图/运行时界面相机叠加")]
        public static void CaptureInPlay()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("进入 Play 后捕获 UI Stack。");
            var rig = UnityEngine.Object.FindObjectsOfType<UICameraStack>().First(item => item.isActiveAndEnabled);
            Directory.CreateDirectory("Library/CardUICameraValidation");
            void Capture(ScriptableRenderContext context, Camera camera)
            {
                if (camera != rig.UICamera) return;
                RenderPipelineManager.endCameraRendering -= Capture;
                ScreenCapture.CaptureScreenshot("Library/CardUICameraValidation/game.png");
                Debug.Log("UI Camera Stack capture requested: Library/CardUICameraValidation/game.png");
            }
            RenderPipelineManager.endCameraRendering += Capture;
        }
    }
}
#endif
