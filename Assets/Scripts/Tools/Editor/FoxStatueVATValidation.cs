using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using RetroRendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Validate the exported prefab in a temporary preview scene. No gameplay scene
// placement or settings are needed to verify its native vertex streams/rendering.
public static class FoxStatueVATValidation
{
    const string Output="Library/FoxStatueValidation";
    const int Layer=31;

    [MenuItem("工具/场景美术/狐狸雕像/重导出检查/开始")]
    public static void BeginReexportProbe()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Begin the reexport probe in edit mode.");
        var preview=EditorSceneManager.NewPreviewScene();
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(FoxStatueAssetImport.Stem+"_VAT.prefab");
        var statue=(GameObject)PrefabUtility.InstantiatePrefab(prefab,preview);
        statue.layer=Layer;statue.transform.localScale=Vector3.one*.22f;
        Light(preview,"Reexport key",new Vector3(43,-34,0),1.1f);
        Light(preview,"Reexport fill",new Vector3(25,135,0),.5f);
        statue.GetComponent<RigidVATPlayer>().SetProgress(.35f);
        var material=statue.GetComponent<MeshRenderer>().sharedMaterial;
        var before=new JObject { ["objectId"]=statue.GetInstanceID(),["meshId"]=statue.GetComponent<MeshFilter>().sharedMesh.GetInstanceID(),
            ["meshGuid"]=AssetDatabase.AssetPathToGUID(FoxStatueAssetImport.Stem+"_Unity.asset"),
            ["materialGuid"]=AssetDatabase.AssetPathToGUID(FoxStatueAssetImport.Stem+"_Surface.mat"),
            ["prefabGuid"]=AssetDatabase.AssetPathToGUID(FoxStatueAssetImport.Stem+"_VAT.prefab"),
            ["originalCrackProgress"]=material.GetFloat("_CrackProgress") };
        material.SetFloat("_CrackProgress",.37f);EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"/reexport-baseline.json",before.ToString());
        Debug.Log("Fox reexport probe ready: existing mesh resident on GPU, progress .35, crack tuning .37.");
    }

    [MenuItem("工具/场景美术/狐狸雕像/重导出检查/完成")]
    public static void FinishReexportProbe()
    {
        var before=JObject.Parse(File.ReadAllText(Output+"/reexport-baseline.json"));
        var statue=EditorUtility.InstanceIDToObject((int)before["objectId"]) as GameObject;
        if(statue==null)throw new InvalidOperationException("Reexport probe instance no longer exists; begin it again.");
        var material=statue.GetComponent<MeshRenderer>().sharedMaterial;
        var preview=statue.scene;
        try
        {
            bool stable=statue.GetComponent<MeshFilter>().sharedMesh.GetInstanceID()==(int)before["meshId"] &&
                AssetDatabase.AssetPathToGUID(FoxStatueAssetImport.Stem+"_Unity.asset")== (string)before["meshGuid"] &&
                AssetDatabase.AssetPathToGUID(FoxStatueAssetImport.Stem+"_Surface.mat")== (string)before["materialGuid"] &&
                AssetDatabase.AssetPathToGUID(FoxStatueAssetImport.Stem+"_VAT.prefab")== (string)before["prefabGuid"];
            if(!stable || Mathf.Abs(statue.GetComponent<RigidVATPlayer>().Progress-.35f)>1e-6f || Mathf.Abs(material.GetFloat("_CrackProgress")-.37f)>1e-6f)
                throw new InvalidOperationException("Reexport changed GUIDs, mesh identity, playback or crack tuning.");
            var cracks=GargoyleCrackValidation.Validate(statue.GetComponent<MeshFilter>(),Output+"/ReexportCracks",new Vector3(0,.4f,0),1<<Layer);
            var report=new JObject { ["guidsPreserved"]=true,["liveMeshIdentityPreserved"]=true,["playbackProgressPreserved"]=.35,
                ["crackTuningPreserved"]=.37,["nativeGPURendering"]=cracks };
            File.WriteAllText(Output+"/reexport-unity.json",report.ToString());
            Debug.Log("Fox reexport and resident GPU validation passed: "+report);
        }
        finally
        {
            material.SetFloat("_CrackProgress",(float)before["originalCrackProgress"]);
            EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    [MenuItem("工具/场景美术/狐狸雕像/验证渲染并截图")]
    public static void Validate()
    {
        var active=SceneManager.GetActiveScene();bool dirty=active.isDirty;
        var roots=active.GetRootGameObjects().Select(o=>o.GetInstanceID()).ToArray();
        var preview=EditorSceneManager.NewPreviewScene();
        try
        {
            string prefabPath=FoxStatueAssetImport.Stem+"_VAT.prefab";
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if(prefab==null)throw new InvalidOperationException("Configure the VAT prefab first.");
            var statue=(GameObject)PrefabUtility.InstantiatePrefab(prefab,preview);
            statue.layer=Layer;statue.transform.localScale=Vector3.one*.22f;
            Light(preview,"Fox key",new Vector3(43,-34,0),1.1f);
            Light(preview,"Fox fill",new Vector3(25,135,0),.5f);
            var filter=statue.GetComponent<MeshFilter>();var renderer=statue.GetComponent<MeshRenderer>();
            var player=statue.GetComponent<RigidVATPlayer>();
            var cracks=GargoyleCrackValidation.Validate(filter,Output+"/Cracks",new Vector3(0,.4f,0),1<<Layer);
            Directory.CreateDirectory(Output);
            var report=new JObject { ["prefab"]=prefabPath,["singleMaterial"]=renderer.sharedMaterials.Length==1,
                ["mesh"]=AssetDatabase.GetAssetPath(filter.sharedMesh),["durationSeconds"]=(float)new SerializedObject(player).FindProperty("durationSeconds").floatValue,
                ["cracks"]=cracks };
            if (!(bool)report["singleMaterial"])throw new InvalidOperationException("Expected single-material prefab.");
            var original=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(FoxStatueAssetImport.Folder+"/FoxStatue.fbx"),preview);
            original.transform.localScale=Vector3.one*.22f;
            foreach(var t in original.GetComponentsInChildren<Transform>())t.gameObject.layer=Layer;
            original.SetActive(false);
            Capture(preview,statue,original,player,report);
            player.Play();
            if(!player.IsPlaying || player.Progress!=0)throw new InvalidOperationException("Play did not start at rest.");
            player.Pause();if(player.IsPlaying)throw new InvalidOperationException("Pause failed.");
            player.SetProgress(2);if(player.Progress!=1)throw new InvalidOperationException("End clamp failed.");
            player.ResetPose();if(player.Progress!=0 || player.IsPlaying)throw new InvalidOperationException("Reset failed.");
            report["playerControls"]="Play/Pause/SetProgress/end clamp/ResetPose passed";
            report["currentScenePreserved"]=active==SceneManager.GetActiveScene() && active.isDirty==dirty &&
                roots.SequenceEqual(active.GetRootGameObjects().Select(o=>o.GetInstanceID()));
            File.WriteAllText(Output+"/rendering.json",report.ToString());
            if(!(bool)report["currentScenePreserved"])throw new InvalidOperationException("Validation changed the user's scene.");
            Debug.Log("Fox VAT rendering/captures passed: "+report);
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    static void Light(Scene scene,string name,Vector3 angles,float intensity)
    {
        var obj=EditorUtility.CreateGameObjectWithHideFlags(name,HideFlags.HideAndDontSave);
        SceneManager.MoveGameObjectToScene(obj,scene);obj.transform.rotation=Quaternion.Euler(angles);
        var light=obj.AddComponent<Light>();light.type=LightType.Directional;light.intensity=intensity;
        light.cullingMask=1<<Layer;light.shadows=LightShadows.Soft;
    }

    static void Capture(Scene scene,GameObject statue,GameObject original,RigidVATPlayer player,JObject report)
    {
        var go=EditorUtility.CreateGameObjectWithHideFlags("Fox capture",HideFlags.HideAndDontSave);
        SceneManager.MoveGameObjectToScene(go,scene);
        var camera=go.AddComponent<Camera>();camera.enabled=false;camera.scene=scene;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.16f,.18f,.21f,1);
        camera.cullingMask=1<<Layer;camera.orthographic=true;camera.orthographicSize=.19f;camera.aspect=1;
        camera.nearClipPlane=.01f;camera.farClipPlane=10;camera.allowHDR=false;
        var additional=go.AddComponent<UniversalAdditionalCameraData>();additional.SetRenderer(0);
        additional.renderPostProcessing=false;additional.renderShadows=true;
        var center=new Vector3(0,.095f,0);go.transform.position=center+new Vector3(.45f,.27f,-.60f);go.transform.LookAt(center);
        var rt=new RenderTexture(768,768,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);rt.Create();
        var image=new Texture2D(768,768,TextureFormat.RGB24,false);var active=RenderTexture.active;
        var samples=new JArray();Color32[] first=null,last=null;
        try
        {
            foreach(var sample in new[]{("original",0f),("rest",0f),("mid",50.25f/109),("collapsed",1f)})
            {
                bool source=sample.Item1=="original";original.SetActive(source);statue.SetActive(!source);
                player.SetProgress(sample.Item2);
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
                RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,768,768),0,0);image.Apply();
                File.WriteAllBytes(Output+"/"+sample.Item1+".png",image.EncodeToPNG());
                if(sample.Item1=="rest")first=image.GetPixels32();if(sample.Item1=="collapsed")last=image.GetPixels32();
                samples.Add(sample.Item1+".png");
            }
            int changed=Enumerable.Range(0,first.Length).Count(i=>Math.Abs(first[i].r-last[i].r)+Math.Abs(first[i].g-last[i].g)+Math.Abs(first[i].b-last[i].b)>9);
            report["collapseChangedPixels"]=changed;report["captures"]=samples;
            if(changed<500)throw new InvalidOperationException("Collapse did not change the rendered statue.");
        }
        finally
        {
            RenderTexture.active=active;rt.Release();UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(go);
            statue.SetActive(true);original.SetActive(false);player.ResetPose();
        }
    }
}
