using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using RetroRendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class GargoyleVATSceneValidation
{
    [MenuItem("工具/场景美术/石像鬼/顶点动画/验证场景并截图")]
    public static void Capture()
    {
        var filter = UnityEngine.Object.FindObjectsOfType<MeshFilter>(true).Single(x=>x.name=="DragonGargoyle" && x.gameObject.scene.IsValid());
        var renderer = filter.GetComponent<MeshRenderer>(); var player = filter.GetComponent<RigidVATPlayer>();
        if (filter.sharedMesh != AssetDatabase.LoadAssetAtPath<Mesh>(GargoyleVATImport.MeshPath) ||
            !renderer.sharedMaterials.SequenceEqual(new[]{AssetDatabase.LoadAssetAtPath<Material>(GargoyleVATImport.SurfacePath)}) || player == null)
            throw new InvalidOperationException("Current statue has not been replaced with the prepared VAT assets.");
        var t=filter.transform; var originalProgress=player.Progress;
        var preparedMesh=filter.sharedMesh; var preparedMaterials=renderer.sharedMaterials;
        var scene=filter.gameObject.scene; bool wasDirty=scene.isDirty;
        var report=new JObject { ["scene"]=scene.path, ["mesh"]=AssetDatabase.GetAssetPath(filter.sharedMesh),
            ["materialSlots"]=renderer.sharedMaterials.Length, ["subMeshes"]=filter.sharedMesh.subMeshCount,
            ["worldPosition"]=JArray.FromObject(new[]{t.position.x,t.position.y,t.position.z}),
            ["worldScale"]=JArray.FromObject(new[]{t.lossyScale.x,t.lossyScale.y,t.lossyScale.z}),
            ["localRotation"]=JArray.FromObject(new[]{t.localRotation.x,t.localRotation.y,t.localRotation.z,t.localRotation.w}),
            ["rendererLocalBounds"]=renderer.localBounds.ToString("F6"), ["durationSeconds"]=(109f/30f),
            ["playOnStart"]=false };
        var go=new GameObject("Gargoyle VAT validation camera") {hideFlags=HideFlags.HideAndDontSave};
        var camera=go.AddComponent<Camera>(); var source=Camera.main;
        if (source!=null) camera.CopyFrom(source);
        camera.enabled=false; camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(.16f,.18f,.21f,1); camera.allowHDR=false;
        camera.orthographic=true; camera.orthographicSize=.175f; camera.aspect=1;
        camera.nearClipPlane=.01f; camera.farClipPlane=10;
        camera.cullingMask=~(1<<LayerMask.NameToLayer("UI"));
        var additional=go.AddComponent<UniversalAdditionalCameraData>();
        additional.SetRenderer(0); additional.renderType=CameraRenderType.Base;
        additional.renderPostProcessing=false; additional.renderShadows=true;
        additional.antialiasing=AntialiasingMode.None;
        var center=t.TransformPoint(new Vector3(.10f,.40f,0));
        go.transform.position=center+new Vector3(.45f,.27f,-.60f);
        go.transform.LookAt(center);
        var rt=new RenderTexture(768,768,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        rt.Create(); var previous=RenderTexture.active;
        var texture=new Texture2D(768,768,TextureFormat.RGB24,false);
        Directory.CreateDirectory("Library/GargoyleVATValidation");
        try
        {
            foreach (var sample in new[]{("original",0f),("rest",0f),("mid",50.25f/109),("collapsed",1f)})
            {
                if (sample.Item1=="original")
                {
                    var original=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Creatures/DragonGargoyle/DragonGargoyle.fbx");
                    filter.sharedMesh=original.GetComponentInChildren<MeshFilter>().sharedMesh;
                    renderer.sharedMaterials=new[]{AssetDatabase.LoadAssetAtPath<Material>(GargoyleVATImport.OriginalMaterial)};
                }
                else {filter.sharedMesh=preparedMesh;renderer.sharedMaterials=preparedMaterials;}
                player.SetProgress(sample.Item2);
                var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
                if (Mathf.Abs(block.GetFloat("_VATProgress")-sample.Item2)>1e-6f)
                    throw new InvalidOperationException("Renderer VAT timeline did not update.");
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest {destination=rt});
                RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,768,768),0,0);texture.Apply();
                File.WriteAllBytes("Library/GargoyleVATValidation/"+sample.Item1+".png",texture.EncodeToPNG());
            }
            report["captures"]=new JArray("rest.png","mid.png","collapsed.png");
            File.WriteAllText("Library/GargoyleVATValidation/scene.json",report.ToString());
            Debug.Log("Gargoyle VAT scene validation/captures complete: " + report);
        }
        finally
        {
            filter.sharedMesh=preparedMesh; renderer.sharedMaterials=preparedMaterials; player.SetProgress(originalProgress); RenderTexture.active=previous;
            rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(go);
            SceneView.RepaintAll();
        }
        if (scene.isDirty!=wasDirty) throw new InvalidOperationException("Capture changed the scene dirty state.");
    }
}
