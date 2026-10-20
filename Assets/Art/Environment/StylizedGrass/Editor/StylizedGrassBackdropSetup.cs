using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace StylizedGrass.Editor
{
    /// <summary>Painted world-space layers composed for the fixed main camera.</summary>
    public static class StylizedGrassBackdropSetup
    {
        const string Root="Assets/Art/Environment/StylizedRuins";
        const string Output="ArtSource/StylizedGrass";

        [MenuItem("Tools/Scene Art/StylizedGrass/Build Reference Backdrop")]
        public static void Build()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Exit Play mode first.");
            var manager=UnityEngine.Object.FindObjectOfType<StylizedGrassManager>();
            if(!manager || !manager.LodCamera)throw new InvalidOperationException("Open the existing grass scene with its main camera.");
            var camera=manager.LodCamera;
            var shader=Shader.Find("StylizedRuins/Backdrop");
            if(!shader || ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Backdrop shader must compile first.");
            foreach(string name in new[]{"Sky","Mountains","Castle","RuinsFront","RuinsRear"})
            {
                string path=Root+"/Textures/T_StylizedRuins_"+name+".png";
                var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                if(!importer)throw new InvalidOperationException("Missing texture: "+path);
                importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;
                importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=name!="Sky";
                importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Clamp;
                importer.filterMode=FilterMode.Bilinear;importer.maxTextureSize=2048;
                importer.npotScale=TextureImporterNPOTScale.None;
                importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            var root=GameObject.Find("StylizedRuins Backdrop");
            if(!root){root=new GameObject("StylizedRuins Backdrop");Undo.RegisterCreatedObjectUndo(root,"Create reference backdrop");}
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/BackdropQuad.asset");
            if(!mesh)
            {
                mesh=new Mesh{name="BackdropQuad"};
                mesh.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)};
                mesh.uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)};
                mesh.triangles=new[]{0,2,1,0,3,2};mesh.RecalculateNormals();mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh,Root+"/Meshes/BackdropQuad.asset");
            }
            // Rectangles use normalized 16:9 camera composition, with overscan for the wider editor Game view.
            Layer(root.transform,camera,mesh,shader,"Sky",65,new Rect(-.2f,.35f,1.4f,.75f),1);
            Layer(root.transform,camera,mesh,shader,"Mountains",53,new Rect(-.2f,.38f,1.4f,.65f),1);
            Layer(root.transform,camera,mesh,shader,"Castle",43,new Rect(.41f,.49f,.43f,.5f),1);
            Layer(root.transform,camera,mesh,shader,"RuinsRear",36,new Rect(-.2f,.33f,1.4f,.75f),1);
            Layer(root.transform,camera,mesh,shader,"RuinsFront",31,new Rect(-.2f,.33f,1.4f,.75f),1);
            var legacy=root.transform.Find("Ruins");if(legacy)legacy.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(root.scene);EditorSceneManager.SaveScene(root.scene);
            Selection.activeGameObject=root;SceneView.RepaintAll();
            Capture();
        }
        static void Layer(Transform parent,Camera camera,Mesh mesh,Shader shader,string name,float depth,Rect rect,float brightness)
        {
            var child=parent.Find(name);
            if(!child){var go=new GameObject(name);Undo.RegisterCreatedObjectUndo(go,"Create painted layer");child=go.transform;child.SetParent(parent,false);}
            Undo.RecordObject(child,"Compose painted layer");
            float viewHeight=2*depth*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f),viewWidth=viewHeight*16/9;
            child.position=camera.transform.position+camera.transform.forward*depth
                +camera.transform.right*((rect.center.x-.5f)*viewWidth)+camera.transform.up*((rect.center.y-.5f)*viewHeight);
            child.rotation=camera.transform.rotation;child.localScale=new Vector3(rect.width*viewWidth,rect.height*viewHeight,1);
            var filter=child.GetComponent<MeshFilter>();if(!filter)filter=child.gameObject.AddComponent<MeshFilter>();filter.sharedMesh=mesh;
            var renderer=child.GetComponent<MeshRenderer>();if(!renderer)renderer=child.gameObject.AddComponent<MeshRenderer>();
            string path=Root+"/Materials/M_StylizedRuins_"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
            material.shader=shader;material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/T_StylizedRuins_"+name+".png"));
            material.SetColor("_BaseColor",Color.white);material.SetFloat("_Exposure",brightness);material.renderQueue=-1;material.enableInstancing=true;
            material.SetFloat("_PaintContrast",.72f);material.SetFloat("_ShadowLift",name=="Mountains"?.16f:.19f);
            material.SetFloat("_ZWrite",name=="Castle" || name=="RuinsFront"?1:0);material.SetFloat("_Cutoff",.5f);
            if(name=="RuinsRear")
            {
                material.SetColor("_BaseColor",new Color(.65f,.32f,.42f,.38f));
                material.SetFloat("_ShadowLift",.03f);material.SetFloat("_Exposure",.7f);
            }
            EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);
            renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            renderer.allowOcclusionWhenDynamic=false;
            var collider=child.GetComponent<Collider>();if(collider)UnityEngine.Object.DestroyImmediate(collider);
        }
        [MenuItem("Tools/Scene Art/StylizedGrass/Capture Reference Backdrop")]
        public static void Capture()
        {
            var manager=UnityEngine.Object.FindObjectOfType<StylizedGrassManager>();
            Directory.CreateDirectory(Output);
            var shader=Shader.Find("StylizedRuins/Backdrop");
            if(!shader || ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Backdrop shader error.");
            StylizedGrassSetup.Capture(manager.LodCamera,Output+"/StylizedGrass_Backdrop.png");
            Debug.Log("Reference backdrop: shared default transparent queue, depth-writing solid cards and distance-sorted soft layers. Main camera, grass manager, ground material and render settings retained.");
        }
    }
}
