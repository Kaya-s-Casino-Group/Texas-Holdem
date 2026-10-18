// GPU Meta checks adapted from ForestRuinsAssetValidation.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace GothicChapel.Editor
{
    public static class GothicChapelMetaValidation
    {
        [MenuItem("Tools/Scene Art/Gothic Chapel/Validate Meta Pixels")]
        public static void ValidateMetaPixels()
        {
            var shader = Shader.Find(GothicChapelImport.ShaderName);
            var material = new Material(shader);
            var mesh = new Mesh();
            var texture = new Texture2D(2,1,TextureFormat.RGBA32,false,true) {filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            mesh.vertices = new[] {new Vector3(0,0,0),new Vector3(1,0,0),new Vector3(1,1,0),new Vector3(0,1,0)};
            mesh.triangles = new[] {0,1,2,0,2,3};
            mesh.uv = new[] {Vector2.zero,Vector2.right,Vector2.one,Vector2.up}; mesh.uv2 = mesh.uv;
            mesh.colors = Enumerable.Repeat(new Color(.5f,1,1,1),4).ToArray();
            mesh.RecalculateNormals();mesh.RecalculateTangents();
            try
            {
                material.SetFloat("_Metallic",0);
                material.SetColor("_BaseColor", new Color(.5f,.3f,.15f,1));
                material.SetFloat("_VertexTone",1);material.SetFloat("_WindStrength",.08f);
                material.SetFloat("_Unlit",.88f);material.SetFloat("_BakeEmission",0);
                Color[] a = MetaPixels(material,mesh,false,0);
                Color[] b = MetaPixels(material,mesh,false,123);
                if (a.Average(c=>c.r) < .2f || a.Zip(b,(x,y)=>Mathf.Abs(x.r-y.r)+Mathf.Abs(x.g-y.g)+Mathf.Abs(x.b-y.b)).Max() > .001f)
                    throw new InvalidOperationException("Meta albedo is blank or changes with wind.");
                material.SetFloat("_Unlit",0);
                Color[] noFill = MetaPixels(material,mesh,false,0);
                if (a.Zip(noFill,(x,y)=>Mathf.Abs(x.r-y.r)).Max() > .001f) throw new InvalidOperationException("Paint fill contaminated baked albedo.");
                material.SetFloat("_Unlit",1);
                Color[] emission = MetaPixels(material,mesh,true,0);
                if (emission.Max(c=>Mathf.Max(c.r,Mathf.Max(c.g,c.b))) > .001f) throw new InvalidOperationException("Artistic fill emitted baked light.");
                material.SetFloat("_BakeEmission",1);
                Color[] physical = MetaPixels(material,mesh,true,0);
                if (physical.Average(c=>c.r)<.2f) throw new InvalidOperationException("Explicit baked emission is broken.");
                material.SetFloat("_BakeEmission",0);
                texture.SetPixels(new[] {Color.white,new Color(1,1,1,0)});texture.Apply();
                material.SetTexture("_BaseMap",texture);material.SetFloat("_Cutoff",.38f);
                Color[] cutout = MetaPixels(material,mesh,false,0);
                int filled=cutout.Count(c=>c.r>.1f);
                if (filled<1800 || filled>2300) throw new InvalidOperationException("Meta alpha clipping failed: "+filled);
                File.WriteAllText(GothicChapelImport.ReportFolder+"/meta_pixel_validation.txt", "PASS: GPU Meta albedo is nonzero and identical across wind times.\nPASS: PaintFill does not change bake albedo or create baked emission.\nPASS: explicit physical emission works.\nPASS: alpha-clipped Meta covers "+filled+" / 4096 pixels for a half-transparent test texture.\n");
                Debug.Log("Gothic Chapel Meta pixel tests passed.");
            }
            finally {UnityEngine.Object.DestroyImmediate(material);UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(texture);}
        }
        internal static Color[] MetaPixels(Material material,Mesh mesh,bool emission,float time)
        {
            var target=RenderTexture.GetTemporary(64,64,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);
            var read=new Texture2D(64,64,TextureFormat.RGBAFloat,false,true);
            var previous=RenderTexture.active;
            var names=new[]{"unity_MetaVertexControl","unity_MetaFragmentControl","unity_LightmapST","unity_DynamicLightmapST","_Time"};
            var old=names.Select(Shader.GetGlobalVector).ToArray();
            var floatNames=new[]{"unity_OneOverOutputBoost","unity_MaxOutputValue","unity_UseLinearSpace"};
            var oldFloats=floatNames.Select(Shader.GetGlobalFloat).ToArray();
            var command=new CommandBuffer {name="Gothic Chapel Meta Pixel Validation"};
            try
            {
                command.SetRenderTarget(target);command.ClearRenderTarget(false,true,Color.clear);
                command.SetViewProjectionMatrices(Matrix4x4.identity,GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(0,1,0,1,-1,1),true));
                command.SetGlobalVector(names[0],new Vector4(1,0,0,0));
                command.SetGlobalVector(names[1],emission?new Vector4(0,1,0,0):new Vector4(1,0,0,0));
                command.SetGlobalVector(names[2],new Vector4(1,1,0,0));command.SetGlobalVector(names[3],new Vector4(1,1,0,0));
                command.SetGlobalVector(names[4],new Vector4(time/20,time,time*2,time*3));
                command.SetGlobalFloat(floatNames[0],1);command.SetGlobalFloat(floatNames[1],16);command.SetGlobalFloat(floatNames[2],1);
                command.DrawMesh(mesh,Matrix4x4.identity,material,0,material.FindPass("Meta"));
                Graphics.ExecuteCommandBuffer(command);
                RenderTexture.active=target;read.ReadPixels(new Rect(0,0,64,64),0,0);read.Apply();return read.GetPixels();
            }
            finally
            {
                for(int i=0;i<names.Length;i++)Shader.SetGlobalVector(names[i],old[i]);
                for(int i=0;i<floatNames.Length;i++)Shader.SetGlobalFloat(floatNames[i],oldFloats[i]);
                RenderTexture.active=previous;command.Release();RenderTexture.ReleaseTemporary(target);UnityEngine.Object.DestroyImmediate(read);
            }
        }
    }
}
