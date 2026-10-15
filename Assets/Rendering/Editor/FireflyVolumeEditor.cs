using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RetroRendering.Editor
{
    [CustomEditor(typeof(FireflyVolume))]
    public sealed class FireflyVolumeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var volume = (FireflyVolume)target;
            EditorGUILayout.HelpBox($"已生成 {volume.SpawnedCount}/{volume.count} 只。实体球写入深度，受场景遮挡；Mesh 包围盒避障（含无 Collider 模型）。修改区域参数会重新分布。", MessageType.Info);
            if (volume.bodyMesh == null || volume.bodyMaterial == null)
                if (GUILayout.Button("配置默认发光球资源")) { Undo.RecordObject(volume, "Configure Fireflies"); FireflySetup.Configure(volume); }
            if (GUILayout.Button("重新生成 / 刷新障碍物")) volume.Regenerate();
            var camera = Camera.main;
            if (camera != null && !camera.allowHDR)
            {
                EditorGUILayout.HelpBox("主相机 HDR 未开启，亮点会被钳制，Bloom 无法获得完整 HDR 亮度。", MessageType.Warning);
                if (GUILayout.Button("开启主相机 HDR")) { Undo.RecordObject(camera, "Enable HDR"); camera.allowHDR = true; EditorUtility.SetDirty(camera); }
            }
        }
    }

    [InitializeOnLoad]
    static class FireflyEditorPreview
    {
        static double nextFrame;
        static FireflyEditorPreview() { EditorApplication.update += Update; }
        static void Update()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.timeSinceStartup < nextFrame) return;
            if (!FireflyVolume.ActiveVolumes.Any(v => v != null && v.isActiveAndEnabled && v.previewInEditor && v.gameObject.scene.IsValid())) return;
            nextFrame = EditorApplication.timeSinceStartup + 1.0 / 30;
            EditorApplication.QueuePlayerLoopUpdate(); SceneView.RepaintAll();
        }
    }

    public static class FireflySetup
    {
        public const string MaterialPath = "Assets/Rendering/Materials/FireflyGlow.mat";
        public const string PrefabPath = "Assets/Rendering/Prefabs/FireflyVolume.prefab";
        public static void Configure(FireflyVolume volume)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                Directory.CreateDirectory("Assets/Rendering/Materials"); AssetDatabase.Refresh();
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "FireflyGlow" };
                material.SetColor("_BaseColor", Color.white);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            try { volume.bodyMesh = sphere.GetComponent<MeshFilter>().sharedMesh; }
            finally { UnityEngine.Object.DestroyImmediate(sphere); }
            volume.bodyMaterial = material; EditorUtility.SetDirty(volume);
        }

        [MenuItem("Tools/Rendering/Create Firefly Volume")]
        public static void Create()
        {
            var go = new GameObject("Firefly Volume"); go.SetActive(false);
            Undo.RegisterCreatedObjectUndo(go, "Create Firefly Volume");
            var volume = go.AddComponent<FireflyVolume>(); Configure(volume);
            if (!File.Exists(PrefabPath))
            {
                Directory.CreateDirectory("Assets/Rendering/Prefabs"); AssetDatabase.Refresh();
                // Save a clean source without any transient preview children.
                go.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            }
            var camera = Camera.main;
            if (camera != null)
            {
                go.transform.position = camera.ViewportToWorldPoint(new Vector3(.5f, .5f, 2.8f));
                if (!camera.allowHDR)
                {
                    Undo.RecordObject(camera, "Enable HDR for Fireflies"); camera.allowHDR = true;
                    EditorUtility.SetDirty(camera);
                }
            }
            go.SetActive(true); volume.Regenerate();
            Selection.activeGameObject = go;
            Debug.Log($"已创建萤火虫区域，生成 {volume.SpawnedCount}/{volume.count} 只；主相机已启用 HDR。场景保持未保存，区域可在 Inspector 调整。", volume);
        }
    }
}
