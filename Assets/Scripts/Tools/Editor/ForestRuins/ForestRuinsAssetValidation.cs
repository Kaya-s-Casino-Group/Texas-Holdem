using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ForestRuins.Editor
{
    public static class ForestRuinsAssetValidation
    {
        const string Output = "Blender/ForestRuins_UnityExport";
        [MenuItem("工具/场景美术/森林遗迹/导出 Unity 资源包")]
        public static void ExportPackage()
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.ExportPackage(ForestRuinsAssetImport.Folder, Output + "/ForestRuins_UnityReady.unitypackage", ExportPackageOptions.Recurse);
            Debug.Log("Forest Ruins standalone asset folder exported as a Unity package.");
        }
        [MenuItem("工具/场景美术/森林遗迹/捕获导入预览")]
        public static void CapturePreview()
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            bool wasDirty = active.isDirty;
            int[] rootsBefore = active.GetRootGameObjects().Select(o => o.GetInstanceID()).ToArray();
            var manifest = ForestRuinsAssetImport.ReadManifest();
            var preview = new PreviewRenderUtility();
            if (!UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(preview.camera.scene))
            { preview.Cleanup(); throw new InvalidOperationException("Preview camera is not isolated."); }
            Texture2D result = null;
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ForestRuinsAssetImport.Folder + "/Prefabs/PF_FR_EnvironmentLayout.prefab");
                var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview.camera.scene);
                var nodes = root.GetComponentsInChildren<Transform>(true);
                Vector3 pos = nodes.First(t => t.name == "REF_FR_CameraPosition").position;
                Vector3 forward = nodes.First(t => t.name == "REF_FR_CameraForward").position - pos;
                Vector3 up = nodes.First(t => t.name == "REF_FR_CameraUp").position - pos;
                var camera = preview.camera;
                camera.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(forward, up));
                camera.fieldOfView = manifest.cameraVerticalFov;
                camera.nearClipPlane = .05f; camera.farClipPlane = 150;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.035f,.045f,.04f);
                preview.ambientColor = new Color(.15f,.17f,.18f);
                preview.lights[0].intensity = 1.15f;
                preview.lights[0].transform.rotation = Quaternion.LookRotation((forward + new Vector3(.35f,-.9f,0)).normalized);
                preview.lights[1].intensity = .35f;
                preview.lights[1].transform.rotation = Quaternion.LookRotation((-forward + new Vector3(-.3f,-.7f,0)).normalized);
                preview.BeginStaticPreview(new Rect(0,0,1500,Mathf.RoundToInt(1500/manifest.cameraAspect)));
                preview.Render(true);
                result = preview.EndStaticPreview();
                Directory.CreateDirectory(Output);File.WriteAllBytes(Output + "/ForestRuins_UnityPreview.png", result.EncodeToPNG());
                File.WriteAllText(Output + "/unity_camera_reference.txt", "Position: " + pos + "\nForward: " + forward + "\nUp: " + up);
                Debug.Log("Forest Ruins Unity preview captured.");
            }
            finally { if (result != null) UnityEngine.Object.DestroyImmediate(result); preview.Cleanup(); }
            if (active.isDirty != wasDirty || !active.GetRootGameObjects().Select(o => o.GetInstanceID()).SequenceEqual(rootsBefore))
                throw new InvalidOperationException("Preview changed the user's scene.");
        }

        [MenuItem("工具/场景美术/森林遗迹/验证元数据通道像素")]
        public static void ValidateMetaPixels()
        {
            var shader = Shader.Find(ForestRuinsAssetImport.ShaderName);
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
                material.SetColor("_BaseColor", new Color(.5f,.3f,.15f,1));
                material.SetFloat("_VertexTone",1);material.SetFloat("_WindStrength",.08f);
                material.SetFloat("_PaintFill",.88f);material.SetFloat("_BakeEmission",0);
                Color[] a = MetaPixels(material,mesh,false,0);
                Color[] b = MetaPixels(material,mesh,false,123);
                if (a.Average(c=>c.r) < .2f || a.Zip(b,(x,y)=>Mathf.Abs(x.r-y.r)+Mathf.Abs(x.g-y.g)+Mathf.Abs(x.b-y.b)).Max() > .001f)
                    throw new InvalidOperationException("Meta albedo is blank or changes with wind.");
                material.SetFloat("_PaintFill",0);
                Color[] noFill = MetaPixels(material,mesh,false,0);
                if (a.Zip(noFill,(x,y)=>Mathf.Abs(x.r-y.r)).Max() > .001f) throw new InvalidOperationException("Paint fill contaminated baked albedo.");
                material.SetFloat("_PaintFill",1);
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
                File.WriteAllText(Output+"/meta_pixel_validation.txt", "PASS: GPU Meta albedo is nonzero and identical across wind times.\nPASS: PaintFill does not change bake albedo or create baked emission.\nPASS: explicit physical emission works.\nPASS: alpha-clipped Meta covers "+filled+" / 4096 pixels for a half-transparent test texture.\n");
                Debug.Log("Forest Ruins Meta pixel tests passed.");
            }
            finally {UnityEngine.Object.DestroyImmediate(material);UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(texture);}
        }
        static Color[] MetaPixels(Material material,Mesh mesh,bool emission,float time)
        {
            var target=RenderTexture.GetTemporary(64,64,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);
            var read=new Texture2D(64,64,TextureFormat.RGBAFloat,false,true);
            var previous=RenderTexture.active;
            var names=new[]{"unity_MetaVertexControl","unity_MetaFragmentControl","unity_LightmapST","unity_DynamicLightmapST","_Time"};
            var old=names.Select(Shader.GetGlobalVector).ToArray();
            var floatNames=new[]{"unity_OneOverOutputBoost","unity_MaxOutputValue","unity_UseLinearSpace"};
            var oldFloats=floatNames.Select(Shader.GetGlobalFloat).ToArray();
            var command=new CommandBuffer {name="Forest Ruins Meta Pixel Validation"};
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
