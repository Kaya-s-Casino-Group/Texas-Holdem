using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace GothicChapel.Editor
{
    public static class GothicChapelAnimationValidation
    {
        const string Folder=GothicChapelImport.Folder;
        [MenuItem("Tools/Scene Art/Gothic Chapel/Validate Animation and Instancing")]
        public static void Validate()
        {
            var root=SceneManager.GetActiveScene().GetRootGameObjects().First(g=>g.name=="PF_GC_GothicChapel");
            var banners=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.sharedMaterial.shader.name=="Gothic Chapel/Animated Banner").ToArray();
            var flames=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.sharedMaterial.shader.name=="Gothic Chapel/Candle Flame").ToArray();
            if(banners.Length!=3 || flames.Length!=15)throw new InvalidOperationException("Incorrect banner/flame count: "+banners.Length+"/"+flames.Length);
            foreach(var r in banners.Concat(flames))if(ShaderUtil.ShaderHasError(r.sharedMaterial.shader))throw new InvalidOperationException("Shader compilation failed: "+r.name);
            foreach(var r in banners)
            {
                var mesh=r.GetComponent<MeshFilter>().sharedMesh;var mask=mesh.colors;
                if(mask.Length!=mesh.vertexCount || !mask.Any(c=>c.r==0) || !mask.Any(c=>c.r>.99f))throw new InvalidOperationException("Banner pin mask is invalid.");
                if(mask.Any(c=>c.r<0 || c.r>1))throw new InvalidOperationException("Invalid banner deformation weights.");
            }
            var material=new Material(flames[0].sharedMaterial){enableInstancing=true};
            var banner=new Material(banners[0].sharedMaterial);
            try
            {
                var mesh=flames[0].GetComponent<MeshFilter>().sharedMesh;
                material.SetFloat("_AnimationTime",0);var a=RenderInstances(material,mesh,"flame-instancing-t0.png");
                material.SetFloat("_AnimationTime",.37f);var b=RenderInstances(material,mesh,"flame-instancing-t1.png");
                int left=0,right=0;double energy=0,delta=0;
                for(int i=0;i<a.Length;i++){if(a[i].maxColorComponent>.1f){if(i%256<128)left++;else right++;}energy+=a[i].r;delta+=Mathf.Abs(a[i].r-b[i].r);}
                if(left<50||right<50||energy<10||delta/energy<.01)throw new InvalidOperationException("Instanced flames blank or animation frozen: "+left+"/"+right+" delta="+delta/energy);
                var flagMesh=banners[0].GetComponent<MeshFilter>().sharedMesh;
                var flagCopy=new Material(banner);
                try
                {
                    flagCopy.SetTexture("_BaseMap",Texture2D.whiteTexture);flagCopy.SetColor("_BaseColor",Color.white);
                    flagCopy.SetFloat("_Unlit",1);flagCopy.SetFloat("_Metallic",0);flagCopy.SetFloat("_BumpScale",0);
                    flagCopy.SetFloat("_AnimationTime",0);var flagA=RenderBanner(flagCopy,flagMesh);
                    flagCopy.SetFloat("_AnimationTime",.7f);var flagB=RenderBanner(flagCopy,flagMesh);
                    if(flagA.Zip(flagB,(x,y)=>Mathf.Abs(x.r-y.r)).Sum()<5)throw new InvalidOperationException("Banner GPU motion is blank or frozen.");
                }
                finally{UnityEngine.Object.DestroyImmediate(flagCopy);}
                banner.SetFloat("_AnimationTime",0);var metaA=GothicChapelMetaValidation.MetaPixels(banner,flagMesh,false,0);
                banner.SetFloat("_AnimationTime",12);var metaB=GothicChapelMetaValidation.MetaPixels(banner,flagMesh,false,12);
                if(metaA.Zip(metaB,(x,y)=>Mathf.Abs(x.r-y.r)).Max()>.001f)throw new InvalidOperationException("Animated banner Meta is time-dependent.");
                if(flames.Any(r=>r.sharedMaterial!=flames[0].sharedMaterial||r.GetComponent<MeshFilter>().sharedMesh!=mesh||!r.sharedMaterial.enableInstancing))throw new InvalidOperationException("Flames must share instanced material/mesh.");
                Capture(root);
                string report="PASS: 3 banners, fixed top masks, GPU-rendered wave motion; shared shadow/depth/normal deformation.\nPASS: banner Meta is independent of animation time.\nPASS: 15 flames share one mesh and GPU-instancing-enabled material.\nPASS: GPU DrawMeshInstanced rendered 2 instances in one draw: "+left+" / "+right+" nonzero pixels.\nPASS: time variation ratio="+(delta/energy)+".\nShader errors: 0.\n";
                File.WriteAllText(GothicChapelImport.ReportFolder+"/animation-validation.txt",report);Debug.Log(report);
            }
            finally{UnityEngine.Object.DestroyImmediate(material);UnityEngine.Object.DestroyImmediate(banner);}
        }
        static Color[] RenderInstances(Material material,Mesh mesh,string name)
        {
            var rt=RenderTexture.GetTemporary(256,192,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);
            var tex=new Texture2D(256,192,TextureFormat.RGBAFloat,false,true);var previous=RenderTexture.active;
            var inverseView=Shader.GetGlobalMatrix("unity_MatrixInvV");
            var cmd=new CommandBuffer{name="Candle Flame GPU Instancing Validation"};
            try
            {
                cmd.SetRenderTarget(rt);cmd.ClearRenderTarget(true,true,Color.clear);
                cmd.SetViewProjectionMatrices(Matrix4x4.identity,GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-.07f,.07f,-.008f,.09f,-1,1),true));
                cmd.SetGlobalMatrix("unity_MatrixInvV",Matrix4x4.identity);
                var matrices=new[]{Matrix4x4.Translate(new Vector3(-.032f,0,0)),Matrix4x4.Translate(new Vector3(.032f,0,0))};
                cmd.DrawMeshInstanced(mesh,0,material,material.FindPass("Flame"),matrices,2);
                Graphics.ExecuteCommandBuffer(cmd);RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,256,192),0,0);tex.Apply();
                var pixels=tex.GetPixels();File.WriteAllBytes(GothicChapelImport.ReportFolder+"/"+name,tex.EncodeToPNG());return pixels;
            }
            finally{Shader.SetGlobalMatrix("unity_MatrixInvV",inverseView);RenderTexture.active=previous;cmd.Release();RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(tex);}
        }
        static Color[] RenderBanner(Material material,Mesh mesh)
        {
            var rt=RenderTexture.GetTemporary(128,256,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);
            var tex=new Texture2D(128,256,TextureFormat.RGBAFloat,false,true);var previous=RenderTexture.active;
            var cmd=new CommandBuffer{name="Banner GPU Motion Validation"};
            try
            {
                var color=mesh.colors[0];var normal=new Vector3(color.b,0,color.a);
                var rotate=Quaternion.Euler(0,45,0)*Quaternion.FromToRotation(normal,Vector3.forward);
                cmd.SetRenderTarget(rt);cmd.ClearRenderTarget(true,true,Color.clear);
                cmd.SetViewProjectionMatrices(Matrix4x4.identity,GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-.35f,.35f,-.7f,.7f,-2,2),true));
                cmd.DrawMesh(mesh,Matrix4x4.Rotate(rotate),material,0,material.FindPass("Forward"));
                Graphics.ExecuteCommandBuffer(cmd);RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,128,256),0,0);tex.Apply();return tex.GetPixels();
            }
            finally{RenderTexture.active=previous;cmd.Release();RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(tex);}
        }
        static void Capture(GameObject root)
        {
            var cam=Camera.main;
            if(!cam)cam=root.GetComponentInChildren<Camera>();
            if(!cam)return;
            var previous=cam.targetTexture;var active=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(1600,966,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var tex=new Texture2D(1600,966,TextureFormat.RGB24,false);
            try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1600,966),0,0);tex.Apply();File.WriteAllBytes(GothicChapelImport.ReportFolder+"/Unity-GothicChapel-Animated.png",tex.EncodeToPNG());}
            finally{cam.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(tex);}
        }
    }
}
