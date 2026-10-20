using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace StylizedGrass.Editor
{
    [CustomEditor(typeof(StylizedGrassManager))]
    public sealed class StylizedGrassManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var manager=(StylizedGrassManager)target;
            EditorGUILayout.HelpBox("统一调整草、草皮、倒伏与风。LOD 只跟随主相机；参数保存在场景中，不改写共享材质资产。",MessageType.Info);
            EditorGUI.BeginChangeCheck();DrawDefaultInspector();
            if(EditorGUI.EndChangeCheck())
            {
                manager.Apply();SceneView.RepaintAll();EditorApplication.QueuePlayerLoopUpdate();
            }
            EditorGUILayout.Space();
            if(GUILayout.Button("打开草地地形编辑器"))StylizedGrassTerrainEditor.Open(manager);
            if(GUILayout.Button("读取现有材质参数"))
            {
                Undo.RecordObject(manager,"Read grass materials");manager.ReadCurrentSettings();manager.Apply();
                EditorUtility.SetDirty(manager);EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            }
            if(GUILayout.Button("重建草分布")){manager.Apply();if(manager.field)manager.field.Rebuild();SceneView.RepaintAll();}
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("主相机最近一次渲染",EditorStyles.boldLabel);
            EditorGUILayout.LabelField("LOD 相机",manager.LodCamera?manager.LodCamera.name:"未找到主相机，保留完整草");
            if(manager.field)
            {
                var s=manager.field.MainCameraStatistics;
                EditorGUILayout.LabelField("近 / 中 / 远实例",$"{s.nearInstances} / {s.midInstances} / {s.farInstances}");
                EditorGUILayout.LabelField("实例 / Instanced Draw",$"{s.instances} / {s.draws}");
                EditorGUILayout.LabelField("草三角形（单次绘制）",$"{s.triangles:N0} / 完整模型 {s.fullDetailTriangles:N0}");
            }
        }
    }
    [CustomEditor(typeof(StylizedGrassField))]
    public sealed class StylizedGrassFieldEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var field=(StylizedGrassField)target;
            if(field.manager && field.manager.isActiveAndEnabled)
            {
                EditorGUILayout.HelpBox("此草场由父级 StylizedGrass Manager 统一管理。",MessageType.Info);
                if(GUILayout.Button("选择管理器"))Selection.activeGameObject=field.manager.gameObject;
                using(new EditorGUI.DisabledScope(true))DrawDefaultInspector();
            }
            else DrawDefaultInspector();
        }
    }
    public static class StylizedGrassManagement
    {
        const string Output="ArtSource/StylizedGrass";
        [MenuItem("Tools/Scene Art/StylizedGrass/Install Manager on Root")]
        public static void Install()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Exit Play mode before attaching the manager.");
            var field=UnityEngine.Object.FindObjectOfType<StylizedGrassField>();
            if(!field)throw new InvalidOperationException("Open the StylizedGrass scene first.");
            var root=field.transform.parent?field.transform.parent.gameObject:field.gameObject;
            var manager=root.GetComponent<StylizedGrassManager>();
            if(!manager)
            {
                manager=Undo.AddComponent<StylizedGrassManager>(root);
                manager.field=field;manager.ReadCurrentSettings();
            }
            manager.Apply();EditorUtility.SetDirty(manager);EditorUtility.SetDirty(field);
            EditorSceneManager.MarkSceneDirty(root.scene);EditorSceneManager.SaveScene(root.scene);
            Selection.activeGameObject=root;EditorGUIUtility.PingObject(root);SceneView.RepaintAll();
            Debug.Log("StylizedGrass Manager installed; existing scene appearance imported and main-camera LOD enabled.");
        }
        [Serializable] sealed class LodReport
        {
            public StylizedGrassField.RenderStatistics fullDetail, lod, midRange, distant;
            public bool secondaryCameraKeptMainLod, secondaryCameraKeptMainTopology, mainStatisticsPreserved, midRangeTopologyVerified, materialAssetsUnchanged;
            public int managerControlChangedPixels;
            public float midRangeCameraDistance;
        }
        [MenuItem("Tools/Scene Art/StylizedGrass/Validate Main Camera LOD")]
        public static void ValidateLod()
        {
            var manager=UnityEngine.Object.FindObjectOfType<StylizedGrassManager>();
            if(!manager || !manager.field || !manager.LodCamera)throw new InvalidOperationException("Install the manager and assign a main camera first.");
            var camera=manager.LodCamera;var field=manager.field;
            var position=camera.transform.position;var rotation=camera.transform.rotation;
            bool enabled=manager.lodEnabled;float time=field.material.GetFloat("_WindTime"),lodging=manager.lodging;
            string grassPath=AssetDatabase.GetAssetPath(manager.grassMaterial),groundPath=AssetDatabase.GetAssetPath(manager.groundMaterial);
            byte[] grassBytes=File.ReadAllBytes(grassPath),groundBytes=File.ReadAllBytes(groundPath);
            GameObject probeObject=null;
            var report=new LodReport();Directory.CreateDirectory(Output);
            try
            {
                field.material.SetFloat("_WindTime",0);
                manager.lodEnabled=false;manager.Apply();
                var original=StylizedGrassSetup.Capture(camera,Output+"/StylizedGrass_FullDetail.png");report.fullDetail=field.MainCameraStatistics;
                manager.lodging=lodging>.5f?0:1;manager.Apply();
                var changed=StylizedGrassSetup.Capture(camera,Output+"/Validation_ManagerControls.png");
                for(int i=0;i<original.Length;i++)
                    if(Math.Abs(original[i].r-changed[i].r)+Math.Abs(original[i].g-changed[i].g)+Math.Abs(original[i].b-changed[i].b)>12)report.managerControlChangedPixels++;
                if(report.managerControlChangedPixels<1000)throw new InvalidOperationException("Root manager appearance controls did not affect the rendered grass.");
                manager.lodging=lodging;
                manager.lodEnabled=true;manager.Apply();
                StylizedGrassSetup.Capture(camera,Output+"/StylizedGrass_LOD.png");report.lod=field.MainCameraStatistics;
                if(report.lod.instances>=report.fullDetail.instances || report.lod.triangles>=report.fullDetail.triangles)
                    throw new InvalidOperationException("The test view did not reduce instances and triangles.");
                probeObject=new GameObject("LOD Validation Secondary Camera"){hideFlags=HideFlags.HideAndDontSave};
                var probe=probeObject.AddComponent<Camera>();probe.CopyFrom(camera);probe.enabled=false;
                probe.transform.SetPositionAndRotation(position+Vector3.right*70,rotation);
                int index=new SerializedObject(camera.GetUniversalAdditionalCameraData()).FindProperty("m_RendererIndex").intValue;
                probe.GetUniversalAdditionalCameraData().SetRenderer(index);
                StylizedGrassSetup.Capture(probe,Output+"/Validation_SecondaryCamera.png");
                report.secondaryCameraKeptMainLod=field.LastRenderStatistics.lodSignature==report.lod.lodSignature;
                report.secondaryCameraKeptMainTopology=report.secondaryCameraKeptMainLod;
                report.mainStatisticsPreserved=field.MainCameraStatistics.instances==report.lod.instances && field.MainCameraStatistics.camera==camera.name;
                if(!report.secondaryCameraKeptMainLod || !report.secondaryCameraKeptMainTopology || !report.mainStatisticsPreserved)
                    throw new InvalidOperationException("A secondary render camera changed main-camera LOD or statistics.");
                Vector3 patchCenter=field.transform.TransformPoint(new Vector3(0,field.SurfaceHeight(0,0),0));
                report.midRangeCameraDistance=manager.midDistance+field.size.magnitude*.5f+1;
                camera.transform.position=patchCenter-(rotation*Vector3.forward)*report.midRangeCameraDistance;
                camera.transform.LookAt(patchCenter);
                StylizedGrassSetup.Capture(camera,Output+"/Validation_MidRange.png");report.midRange=field.MainCameraStatistics;
                report.midRangeTopologyVerified=report.midRange.draws>0 && report.midRange.nearInstances==0 &&
                    report.midRange.triangles==report.midRange.instances*(int)field.farMesh.GetIndexCount(0)/3;
                if(!report.midRangeTopologyVerified)throw new InvalidOperationException("Far-camera probe did not use the simplified grass mesh.");
                camera.transform.SetPositionAndRotation(position,rotation);
                camera.transform.position=position-camera.transform.forward*(manager.maxDistance+field.size.magnitude);
                StylizedGrassSetup.Capture(camera,Output+"/Validation_DistantGround.png");report.distant=field.MainCameraStatistics;
                if(report.distant.instances!=0)throw new InvalidOperationException("Grass beyond the configured main-camera range was not culled.");
                report.materialAssetsUnchanged=Equal(grassBytes,File.ReadAllBytes(grassPath)) && Equal(groundBytes,File.ReadAllBytes(groundPath));
                if(!report.materialAssetsUnchanged)throw new InvalidOperationException("Scene-local manager controls changed a shared material asset.");
                File.WriteAllText(Output+"/lod-validation.json",JsonUtility.ToJson(report,true));
                Debug.Log($"Main-camera LOD verified: {report.fullDetail.instances} -> {report.lod.instances} grass instances; {report.fullDetail.triangles} -> {report.lod.triangles} triangles. " +
                    $"Far probe: {report.midRange.instances} grass tufts. Secondary camera kept LOD; distant grass culled.");
            }
            finally
            {
                manager.lodEnabled=enabled;manager.lodging=lodging;field.material.SetFloat("_WindTime",time);
                camera.transform.SetPositionAndRotation(position,rotation);manager.Apply();
                if(probeObject)UnityEngine.Object.DestroyImmediate(probeObject);
                StylizedGrassSetup.Capture(camera,Output+"/StylizedGrass_Overview.png");
                Selection.activeGameObject=manager.gameObject;
            }
        }
        static bool Equal(byte[] a,byte[] b)
        {
            if(a.Length!=b.Length)return false;
            for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;
            return true;
        }
    }
}
