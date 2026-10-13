using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using RetroRendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class GargoyleCrackValidation
{
    [MenuItem("Tools/Art Assets/Dragon Gargoyle Cracks/Validate Rendering")]
    public static void Validate()
    {
        var filter=UnityEngine.Object.FindObjectsOfType<MeshFilter>(true).Single(x=>x.name=="DragonGargoyle" && x.gameObject.scene.IsValid());
        var renderer=filter.GetComponent<MeshRenderer>();var materials=renderer.sharedMaterials;
        var originalBlock=new MaterialPropertyBlock();renderer.GetPropertyBlock(originalBlock);
        var scene=filter.gameObject.scene;bool dirty=scene.isDirty;
        var report=new JObject { ["material"]=AssetDatabase.GetAssetPath(materials[0]),
            ["active"]=renderer.gameObject.activeInHierarchy,["enabled"]=renderer.enabled,["forceOff"]=renderer.forceRenderingOff,
            ["materialCrackProgress"]=materials[0].GetFloat("_CrackProgress"),
            ["blockHasCrackProgress"]=originalBlock.HasFloat(Shader.PropertyToID("_CrackProgress")),
            ["blockCrackProgress"]=originalBlock.GetFloat("_CrackProgress"),
            ["blockVATProgress"]=originalBlock.GetFloat("_VATProgress") };
        var mat=new Material(materials[0]);renderer.sharedMaterials=new[]{mat};
        var go=new GameObject("Crack validation camera") {hideFlags=HideFlags.HideAndDontSave};
        var camera=go.AddComponent<Camera>();if(Camera.main!=null)camera.CopyFrom(Camera.main);
        camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.16f,.18f,.21f,1);
        camera.allowHDR=false;camera.orthographic=true;camera.orthographicSize=.175f;camera.aspect=1;camera.nearClipPlane=.01f;camera.farClipPlane=10;
        camera.cullingMask=~(1<<LayerMask.NameToLayer("UI"));
        var additional=go.AddComponent<UniversalAdditionalCameraData>();additional.SetRenderer(0);additional.renderPostProcessing=false;additional.renderShadows=true;
        var center=filter.transform.TransformPoint(new Vector3(.10f,.40f,0));go.transform.position=center+new Vector3(.45f,.27f,-.60f);go.transform.LookAt(center);
        var rt=new RenderTexture(768,768,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);rt.Create();
        var readback=new Texture2D(768,768,TextureFormat.RGB24,false);var active=RenderTexture.active;
        Directory.CreateDirectory("Library/GargoyleCrackValidation");
        try
        {
            foreach(float vat in new[]{0f,50.25f/109})
            {
                var block=new MaterialPropertyBlock();block.SetFloat("_VATProgress",vat);renderer.SetPropertyBlock(block);
                Color32[] baseline=null;
                foreach(float cracks in new[]{0f,1f})
                {
                    mat.SetFloat("_CrackProgress",cracks);
                    RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest {destination=rt});
                    RenderTexture.active=rt;readback.ReadPixels(new Rect(0,0,768,768),0,0);readback.Apply();
                    var pixels=readback.GetPixels32();
                    string name=(vat==0?"rest":"mid")+"_"+(cracks==0?"intact":"cracked");
                    File.WriteAllBytes("Library/GargoyleCrackValidation/"+name+".png",readback.EncodeToPNG());
                    if(baseline==null)baseline=pixels;
                    else
                    {
                        int changed=0;for(int i=0;i<pixels.Length;++i)
                            if(Math.Abs(pixels[i].r-baseline[i].r)+Math.Abs(pixels[i].g-baseline[i].g)+Math.Abs(pixels[i].b-baseline[i].b)>9)++changed;
                        report[vat==0?"restChangedPixels":"midChangedPixels"]=changed;
                    }
                }
            }
            var diagnostic=new Material(Shader.Find("Hidden/Validation/GargoyleCracks"));diagnostic.CopyPropertiesFromMaterial(materials[0]);
            var raw=new RenderTexture(768,768,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);raw.Create();
            var rawRead=new Texture2D(768,768,TextureFormat.RGBAFloat,false,true);
            var freshMesh=UnityEngine.Object.Instantiate(filter.sharedMesh);
            try
            {
                foreach(float cracks in new[]{0f,1f,2f})
                {
                    diagnostic.SetFloat("_CrackProgress",Mathf.Min(cracks,1));diagnostic.SetFloat("_VATProgress",0);
                    var cmd=new CommandBuffer();cmd.SetRenderTarget(raw);cmd.ClearRenderTarget(true,true,Color.clear);
                    cmd.SetViewProjectionMatrices(camera.worldToCameraMatrix,GL.GetGPUProjectionMatrix(camera.projectionMatrix,true));
                    cmd.DrawMesh(cracks==2?freshMesh:filter.sharedMesh,filter.transform.localToWorldMatrix,diagnostic,0,0);Graphics.ExecuteCommandBuffer(cmd);cmd.Release();
                    RenderTexture.active=raw;rawRead.ReadPixels(new Rect(0,0,768,768),0,0);rawRead.Apply();
                    var samples=rawRead.GetPixels().Where(c=>c.a>.5f).ToArray();
                    report[cracks==2?"freshMesh":("raw"+cracks)]=new JObject { ["fragments"]=samples.Length,["exterior"]=samples.Count(c=>c.g<.5f),
                        ["maxMask"]=samples.Length==0?0:samples.Max(c=>c.r),["maskPixels"]=samples.Count(c=>c.r>.05f),
                        ["progressRead"]=samples.Length==0?0:samples.Max(c=>c.b) };
                }
            }
            finally {UnityEngine.Object.DestroyImmediate(freshMesh);raw.Release();UnityEngine.Object.DestroyImmediate(raw);UnityEngine.Object.DestroyImmediate(rawRead);UnityEngine.Object.DestroyImmediate(diagnostic);}
            File.WriteAllText("Library/GargoyleCrackValidation/report.json",report.ToString());
            if ((int)report["restChangedPixels"]<100 || (int)report["midChangedPixels"]<100 ||
                (int)report["raw1"]["exterior"]==0 || (float)report["raw1"]["maxMask"]<.2f ||
                (int)report["raw1"]["exterior"]!=(int)report["freshMesh"]["exterior"] ||
                (int)report["raw1"]["maskPixels"]!=(int)report["freshMesh"]["maskPixels"])
                throw new Exception("Crack rendering or live GPU surface stream failed; inspect Library/GargoyleCrackValidation/report.json.");
            Debug.Log("Crack rendering regression passed: "+report);
        }
        finally
        {
            renderer.sharedMaterials=materials;renderer.SetPropertyBlock(originalBlock);RenderTexture.active=active;
            rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(readback);UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(mat);
            SceneView.RepaintAll();
        }
        if(scene.isDirty!=dirty)throw new Exception("Crack validation changed scene dirty state.");
    }
}
