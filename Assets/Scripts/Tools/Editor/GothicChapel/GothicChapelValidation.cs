using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace GothicChapel.Editor
{
    public static class GothicChapelValidation
    {
        [Serializable] public class UVData { public string name; public Vector3[] vertices; public Vector2[] uv0, uv1; public int[] triangles; }
        [MenuItem("Tools/Scene Art/Gothic Chapel/Export UV Validation Data")]
        public static void ExportUV()
        {
            string folder=GothicChapelImport.ReportFolder+"/UV";Directory.CreateDirectory(folder);
            foreach(var obj in GothicChapelImport.Read().objects)
            {
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(GothicChapelImport.Folder+"/Meshes/"+obj.name+".asset");
                var data=new UVData {name=obj.name,vertices=mesh.vertices,uv0=mesh.uv,uv1=mesh.uv2,triangles=mesh.triangles};
                File.WriteAllText(folder+"/"+obj.name+".json",JsonUtility.ToJson(data));
            }
            Debug.Log("Gothic Chapel: UV validation data exported.");
        }
        [MenuItem("Tools/Scene Art/Gothic Chapel/Validate Placed Scene and Capture")]
        public static void Capture()
        {
            var scene=SceneManager.GetActiveScene();var root=scene.GetRootGameObjects().First(o=>o.name=="PF_GC_GothicChapel");
            var manifest=GothicChapelImport.Read();var nodes=root.GetComponentsInChildren<Transform>(true);
            float positionError=0;
            foreach(var spec in manifest.objects)
            {
                var t=nodes.First(n=>n.name==spec.name);positionError=Mathf.Max(positionError,Vector3.Distance(t.position,spec.position));
                var renderer=t.GetComponent<MeshRenderer>();
                if(renderer.sharedMaterials.Any(m=>!m||ShaderUtil.ShaderHasError(m.shader)))throw new InvalidOperationException("Invalid material on "+t.name);
                if(t.GetComponent<MeshFilter>().sharedMesh.uv2.Length==0)throw new InvalidOperationException("No UV1 on "+t.name);
            }
            if(positionError>1e-4f)throw new InvalidOperationException("Layout drift: "+positionError);
            if(root.GetComponentsInChildren<Light>(true).Length!=0)throw new InvalidOperationException("Lights must not be imported.");
            var camera=root.GetComponentInChildren<Camera>();
            var target=RenderTexture.GetTemporary(1600,966,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var previous=camera.targetTexture;var active=RenderTexture.active;
            var image=new Texture2D(1600,966,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                image.ReadPixels(new Rect(0,0,1600,966),0,0);image.Apply();
                File.WriteAllBytes(GothicChapelImport.ReportFolder+"/Unity-GothicChapel.png",image.EncodeToPNG());
            }
            finally {camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);UnityEngine.Object.DestroyImmediate(image);}
            File.WriteAllText(GothicChapelImport.ReportFolder+"/scene-validation.txt","PASS: layout max position error="+positionError+" m\nPASS: "+manifest.objects.Length+" mesh renderers and UV1\nPASS: no imported Light components\nCamera vertical FOV="+camera.fieldOfView+"\nCurrent scene="+scene.path);
            Debug.Log("Gothic Chapel scene validation and camera capture complete.");
        }
    }
}
