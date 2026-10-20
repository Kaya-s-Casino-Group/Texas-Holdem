using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StylizedGrass.Editor
{
    public static class StylizedRuinsDepthSetup
    {
        const string Root="Assets/Art/Environment/StylizedRuins";
        [MenuItem("Tools/Scene Art/StylizedGrass/Split Ruins and Use Depth")]
        public static void Apply()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Exit Play mode first.");
            var manager=UnityEngine.Object.FindObjectOfType<StylizedGrassManager>();
            var root=GameObject.Find("StylizedRuins Backdrop");
            if(!manager || !manager.LodCamera || !root)throw new InvalidOperationException("Open the existing ruins scene.");
            var shader=Shader.Find("StylizedRuins/Backdrop");
            if(!shader || ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Compile the backdrop shader first.");
            var camera=manager.LodCamera;
            foreach(string name in new[]{"RuinsFront","RuinsRear"})
            {
                var importer=AssetImporter.GetAtPath(Root+"/Textures/T_StylizedRuins_"+name+".png") as TextureImporter;
                if(!importer)throw new InvalidOperationException("Missing split layer: "+name);
                importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;
                importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=true;
                importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;
                importer.maxTextureSize=2048;importer.npotScale=TextureImporterNPOTScale.None;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            }
            var front=root.transform.Find("RuinsFront");
            if(!front)
            {
                front=root.transform.Find("Ruins");
                if(!front)throw new InvalidOperationException("Existing Ruins layer not found.");
                Undo.RecordObject(front.gameObject,"Split ruins depth layers");front.name="RuinsFront";
            }
            var reference=front.GetComponent<MeshRenderer>().sharedMaterial;
            foreach(string name in new[]{"RuinsFront","RuinsRear"})
            {
                string path=Root+"/Materials/M_StylizedRuins_"+name+".mat";
                if(!AssetDatabase.LoadAssetAtPath<Material>(path))
                {
                    var material=new Material(reference){name="M_StylizedRuins_"+name};
                    material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/T_StylizedRuins_"+name+".png"));
                    if(name=="RuinsRear")
                    {
                        material.SetColor("_BaseColor",new Color(.65f,.32f,.42f,.38f));
                        material.SetFloat("_ShadowLift",.03f);material.SetFloat("_Exposure",.7f);
                    }
                    AssetDatabase.CreateAsset(material,path);
                }
            }
            var rear=root.transform.Find("RuinsRear");
            if(!rear)
            {
                var go=UnityEngine.Object.Instantiate(front.gameObject,root.transform);go.name="RuinsRear";rear=go.transform;
                Undo.RegisterCreatedObjectUndo(go,"Add distant ruins layer");
                float depth=Vector3.Dot(front.position-camera.transform.position,camera.transform.forward);
                if(depth<=.1f)throw new InvalidOperationException("The ruins must be in front of the camera.");
                float ratio=(depth+4)/depth;
                rear.position=camera.transform.position+(front.position-camera.transform.position)*ratio;
                rear.localScale=front.localScale*ratio;
            }
            foreach(var layer in new[]{front,rear})
            {
                var renderer=layer.GetComponent<MeshRenderer>();Undo.RecordObject(renderer,"Use split ruins material");
                renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/M_StylizedRuins_"+layer.name+".mat");
            }
            foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{Root+"/Materials"}))
            {
                var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if(material.shader!=shader)continue;
                Undo.RecordObject(material,"Use physical depth for painted cards");
                bool solid=material.name.Contains("Pillar") || material.name.EndsWith("Castle") || material.name.EndsWith("RuinsFront");
                material.renderQueue=-1;material.SetFloat("_ZWrite",solid?1:0);material.SetFloat("_Cutoff",.5f);
                material.enableInstancing=true;EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);
            }
            foreach(var renderer in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())
            {
                if(!renderer.sharedMaterial || renderer.sharedMaterial.shader!=shader)continue;
                Undo.RecordObject(renderer,"Clear painted-card sorting overrides");
                renderer.sortingLayerID=0;renderer.sortingOrder=0;renderer.rendererPriority=0;
            }
            Undo.RecordObject(camera,"Sort transparent layers by distance");
            camera.transparencySortMode=camera.orthographic?TransparencySortMode.Orthographic:TransparencySortMode.Perspective;
            EditorUtility.SetDirty(camera);
            EditorSceneManager.MarkSceneDirty(root.scene);EditorSceneManager.SaveScene(root.scene);
            Selection.activeGameObject=front.gameObject;SceneView.RepaintAll();
            StylizedGrassSetup.Capture(camera,"ArtSource/StylizedGrass/StylizedGrass_DepthSplit.png");
            Debug.Log("Ruins split into front cutout and rear atmospheric layer. All painted materials now use shader-default queue, zero sorting overrides, solid depth writes and standard distance sorting.");
        }

        [MenuItem("Tools/Scene Art/StylizedGrass/Validate Card Depth Sorting")]
        public static void Validate()
        {
            var shader=Shader.Find("StylizedRuins/Backdrop");
            if(!shader || ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Shader error.");
            foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{Root+"/Materials"}))
            {
                var m=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if(m.shader==shader && (m.renderQueue!=3000 || new SerializedObject(m).FindProperty("m_CustomRenderQueue").intValue!=-1))throw new InvalidOperationException("Fixed queue override remains: "+m.name);
            }
            var original=Camera.main;
            var root=new GameObject("Depth Sorting Validation"){hideFlags=HideFlags.HideAndDontSave};
            Material red=null,green=null;Texture2D transparent=null;
            try
            {
                var cameraGo=new GameObject("Depth Test Camera");cameraGo.transform.SetParent(root.transform);
                var camera=cameraGo.AddComponent<Camera>();camera.CopyFrom(original);camera.enabled=false;
                int index=new SerializedObject(original.GetUniversalAdditionalCameraData()).FindProperty("m_RendererIndex").intValue;
                camera.GetUniversalAdditionalCameraData().SetRenderer(index);
                camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
                camera.transparencySortMode=TransparencySortMode.Perspective;
                camera.transform.SetPositionAndRotation(new Vector3(1000,1000,1000),Quaternion.identity);
                red=new Material(shader);green=new Material(shader);
                red.SetColor("_BaseColor",new Color(1,.01f,.01f,1));green.SetColor("_BaseColor",new Color(.01f,1,.01f,1));
                red.SetFloat("_ZWrite",1);green.SetFloat("_ZWrite",1);
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/BackdropQuad.asset");
                GameObject Card(string name,Material material,float depth)
                {
                    var go=new GameObject(name);go.layer=31;go.transform.SetParent(root.transform);
                    go.transform.position=camera.transform.position+Vector3.forward*depth;go.transform.localScale=new Vector3(3,3,1);
                    go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;
                    r.shadowCastingMode=ShadowCastingMode.Off;return go;
                }
                var a=Card("Red",red,5);var b=Card("Green",green,8);
                Color Center()
                {
                    var pixels=StylizedGrassSetup.Capture(camera,Path.Combine(Path.GetTempPath(),"StylizedRuins-DepthCheck.png"));
                    return pixels[450*1600+800];
                }
                bool Red(Color c)=>c.r>c.g+.1f;
                bool Green(Color c)=>c.g>c.r+.1f;
                if(!Red(Center()))throw new InvalidOperationException("Near red solid did not occlude far green.");
                a.transform.position=camera.transform.position+Vector3.forward*9;
                if(!Green(Center()))throw new InvalidOperationException("Swapping depth did not swap solid visibility.");
                a.transform.position=camera.transform.position+Vector3.forward*5;
                transparent=new Texture2D(1,1,TextureFormat.RGBA32,false);transparent.SetPixel(0,0,Color.clear);transparent.Apply();
                red.SetTexture("_BaseMap",transparent);
                if(!Green(Center()))throw new InvalidOperationException("Transparent cutout incorrectly occluded the rear card.");
                red.SetTexture("_BaseMap",Texture2D.whiteTexture);
                red.SetFloat("_ZWrite",0);green.SetFloat("_ZWrite",0);
                red.SetColor("_BaseColor",new Color(1,.01f,.01f,.65f));green.SetColor("_BaseColor",new Color(.01f,1,.01f,.65f));
                if(!Red(Center()))throw new InvalidOperationException("Soft layers did not sort far to near.");
                a.transform.position=camera.transform.position+Vector3.forward*9;
                if(!Green(Center()))throw new InvalidOperationException("Soft layer ordering did not follow depth swap.");
                File.WriteAllText("ArtSource/StylizedGrass/depth-sorting-validation.json","{\"defaultQueues\":true,\"solidDepthSwap\":true,\"transparentHoles\":true,\"softDepthSwap\":true}");
                Debug.Log("Card depth validation passed: default queues, solid depth swap, transparent holes, and soft-layer depth swap, using the project renderer.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                if(red)UnityEngine.Object.DestroyImmediate(red);if(green)UnityEngine.Object.DestroyImmediate(green);
                if(transparent)UnityEngine.Object.DestroyImmediate(transparent);
            }
        }
    }
}
