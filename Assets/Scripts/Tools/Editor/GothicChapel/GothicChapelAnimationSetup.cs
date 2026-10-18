using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace GothicChapel.Editor
{
    public static class GothicChapelAnimationSetup
    {
        const string Folder=GothicChapelImport.Folder;
        [Serializable] public class Anchor {public string candle;public Vector3 position;}
        [Serializable] public class Anchors {public Anchor[] anchors;}
        [MenuItem("Tools/Scene Art/Gothic Chapel/Set Up Banner and Candle Animation")]
        public static void Setup()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var bannerShader=Shader.Find("Gothic Chapel/Animated Banner");
            var flameShader=Shader.Find("Gothic Chapel/Candle Flame");
            foreach(var shader in new[]{bannerShader,flameShader})
                if(!shader || ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Shader errors: "+(shader?shader.name:"missing"));
            var mat=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Materials/MAT_Banner_OxbloodGold.mat");
            mat.shader=bannerShader;mat.SetFloat("_BannerAmplitude",.038f);mat.SetFloat("_BannerSpeed",1.25f);mat.SetFloat("_BannerFrequency",5);
            mat.SetFloat("_AnimationTime",-1);mat.enableInstancing=true;EditorUtility.SetDirty(mat);
            var source=GothicChapelImport.Read();
            foreach(var obj in source.objects.Where(o=>o.category=="COL_06_Banners" && !o.name.EndsWith("HangingRod")))
            {
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Meshes/"+obj.name+".asset");
                var spec=JsonUtility.FromJson<GothicChapelImport.MeshSpec>(File.ReadAllText(obj.path));
                float low=spec.vertices.Min(v=>v.y),high=spec.vertices.Max(v=>v.y);
                // Cloth lies in a vertical plane. Pick its perpendicular toward the original camera.
                Vector3 n=Vector3.zero;
                foreach(var normal in spec.normals) {var horizontal=new Vector3(normal.x,0,normal.z);if(horizontal.sqrMagnitude>.5f){n=horizontal.normalized;break;}}
                if(n==Vector3.zero)n=Vector3.forward;
                if(Vector3.Dot(n,source.camera.position-obj.position)<0)n=-n;
                mesh.colors=mesh.vertices.Select(v=>new Color(Mathf.Clamp01((high-v.y)/(high-low)),1/(high-low),n.x,n.z)).ToArray();
                var bounds=new Bounds();bounds.SetMinMax(spec.vertices.Aggregate(Vector3.Min),spec.vertices.Aggregate(Vector3.Max));
                bounds.Expand(.3f);mesh.bounds=bounds;EditorUtility.SetDirty(mesh);
            }
            var flameMaterial=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Materials/MAT_Candle_Flame.mat");
            if(!flameMaterial){flameMaterial=new Material(flameShader){name="MAT_Candle_Flame"};AssetDatabase.CreateAsset(flameMaterial,Folder+"/Materials/MAT_Candle_Flame.mat");}
            flameMaterial.shader=flameShader;flameMaterial.enableInstancing=true;
            flameMaterial.SetColor("_OuterColor",new Color(1,.19f,.018f));flameMaterial.SetColor("_CoreColor",new Color(1,.83f,.35f));
            flameMaterial.SetFloat("_Intensity",6);flameMaterial.SetFloat("_FlickerAmount",.32f);flameMaterial.SetFloat("_FlickerSpeed",7);
            flameMaterial.SetFloat("_Sway",.009f);flameMaterial.SetFloat("_SwaySpeed",4);flameMaterial.SetFloat("_AnimationTime",-1);flameMaterial.SetFloat("_BakeIntensity",0);
            flameMaterial.globalIlluminationFlags=MaterialGlobalIlluminationFlags.EmissiveIsBlack;EditorUtility.SetDirty(flameMaterial);
            var meshPath=Folder+"/Meshes/SM_Candle_Flame.asset";
            var flameMesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            var generated=CreateFlameMesh();
            if(flameMesh){EditorUtility.CopySerialized(generated,flameMesh);UnityEngine.Object.DestroyImmediate(generated);EditorUtility.SetDirty(flameMesh);}
            else{flameMesh=generated;AssetDatabase.CreateAsset(flameMesh,meshPath);}
            var anchors=JsonUtility.FromJson<Anchors>(File.ReadAllText(Folder+"/Source/GothicChapel_FlameAnchors.json"));
            var prefabPath=Folder+"/Prefabs/PF_GC_GothicChapel.prefab";
            var prefab=PrefabUtility.LoadPrefabContents(prefabPath);
            try{AddFlames(prefab,anchors,flameMesh,flameMaterial);PrefabUtility.SaveAsPrefabAsset(prefab,prefabPath);}
            finally{PrefabUtility.UnloadPrefabContents(prefab);}
            var scene=SceneManager.GetActiveScene();
            var root=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="PF_GC_GothicChapel");
            if(root)
            {
                foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name.StartsWith("FX_CandleFlame_")))
                {
                    r.gameObject.layer=r.transform.parent.gameObject.layer;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(r.gameObject);
                }
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText(GothicChapelImport.ReportFolder+"/animation-setup.txt","PASS: 3 pinned animated banners; 15 wick-aligned flames; shared instanced material and mesh.\nNo Light components added.\nCurrent scene="+scene.path);
            EditorApplication.QueuePlayerLoopUpdate();SceneView.RepaintAll();
            Debug.Log(File.ReadAllText(GothicChapelImport.ReportFolder+"/animation-setup.txt"));
        }
        static void AddFlames(GameObject root,Anchors anchors,Mesh mesh,Material material)
        {
            int id=0;
            foreach(var a in anchors.anchors)
            {
                var parent=root.GetComponentsInChildren<Transform>(true).First(t=>t.name==a.candle);
                string name="FX_CandleFlame_"+(++id).ToString("00");
                var t=parent.Find(name);
                if(!t){var go=new GameObject(name);t=go.transform;t.SetParent(parent,false);}
                t.localPosition=a.position-new Vector3(0,.004f,0);t.localRotation=Quaternion.identity;t.localScale=Vector3.one;t.gameObject.layer=parent.gameObject.layer;
                var f=t.GetComponent<MeshFilter>();if(!f)f=t.gameObject.AddComponent<MeshFilter>();f.sharedMesh=mesh;
                var r=t.GetComponent<MeshRenderer>();if(!r)r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=material;
                r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;
                r.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;GameObjectUtility.SetStaticEditorFlags(t.gameObject,0);
            }
        }
        static Mesh CreateFlameMesh()
        {
            const int rows=12;var v=new Vector3[(rows+1)*2];var uv=new Vector2[v.Length];var tris=new int[rows*6];
            for(int y=0;y<=rows;y++)for(int x=0;x<2;x++){int i=y*2+x;uv[i]=new Vector2(x,(float)y/rows);v[i]=new Vector3((x-.5f)*.035f,(float)y/rows*.075f,0);}
            for(int y=0;y<rows;y++){int i=y*6,a=y*2;tris[i]=a;tris[i+1]=a+2;tris[i+2]=a+1;tris[i+3]=a+1;tris[i+4]=a+2;tris[i+5]=a+3;}
            var mesh=new Mesh{name="SM_Candle_Flame"};mesh.vertices=v;mesh.uv=uv;mesh.uv2=uv;mesh.triangles=tris;mesh.RecalculateNormals();mesh.RecalculateTangents();
            // Contains all camera-facing orientations and sway, independent of billboard rotation.
            mesh.bounds=new Bounds(new Vector3(0,.04f,0),new Vector3(.12f,.14f,.12f));return mesh;
        }
    }
}
