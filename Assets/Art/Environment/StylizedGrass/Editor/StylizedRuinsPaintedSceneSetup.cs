using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StylizedGrass.Editor
{
    public static class StylizedRuinsPaintedSceneSetup
    {
        const string Root="Assets/Art/Environment/StylizedRuins";
        const string Output="ArtSource/StylizedGrass";
        const string RendererPath=Root+"/Rendering/StylizedRuinsForwardRenderer.asset";
        [MenuItem("Tools/Scene Art/StylizedRuins/Apply Painted Ground Transition")]
        public static void Apply()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
            var manager=UnityEngine.Object.FindObjectOfType<StylizedGrassManager>();
            if(!manager || !manager.groundRenderer || !manager.LodCamera)throw new InvalidOperationException("Open the StylizedGrass scene first.");
            var shader=Shader.Find("StylizedRuins/Painted Ground Transition");
            if(!shader || ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Ground shader must compile first.");
            Directory.CreateDirectory(Output);
            var camera=manager.LodCamera;
            var data=camera.GetUniversalAdditionalCameraData();
            var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if(!pipeline)throw new InvalidOperationException("URP is required.");
            var pipelineSO=new SerializedObject(pipeline);
            var list=pipelineSO.FindProperty("m_RendererDataList");
            int oldIndex=new SerializedObject(data).FindProperty("m_RendererIndex").intValue;
            if(oldIndex<0)oldIndex=pipelineSO.FindProperty("m_DefaultRendererIndex").intValue;
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if(!renderer)
            {
                StylizedGrassSetup.Capture(camera,Output+"/StylizedRuins_BeforeGroundTransition.png");
                var source=list.GetArrayElementAtIndex(oldIndex).objectReferenceValue as UniversalRendererData;
                if(!source)throw new InvalidOperationException("An existing UniversalRendererData is required.");
                renderer=UnityEngine.Object.Instantiate(source);renderer.name="StylizedRuinsForwardRenderer";
                renderer.rendererFeatures.Clear();renderer.renderingMode=RenderingMode.Forward;
                renderer.lowResolution.enabled=false;
                AssetDatabase.CreateAsset(renderer,RendererPath);
                var rendererSO=new SerializedObject(renderer);
                rendererSO.FindProperty("m_RendererFeatureMap").ClearArray();
                rendererSO.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(renderer);AssetDatabase.SaveAssetIfDirty(renderer);
            }
            EnsureDepthFeature(renderer);
            int index=-1;
            for(int i=0;i<list.arraySize;i++)if(list.GetArrayElementAtIndex(i).objectReferenceValue==renderer)index=i;
            if(index<0)
            {
                Undo.RecordObject(pipeline,"Register independent ruins renderer");
                index=list.arraySize;list.InsertArrayElementAtIndex(index);list.GetArrayElementAtIndex(index).objectReferenceValue=renderer;
                pipelineSO.ApplyModifiedProperties();AssetDatabase.SaveAssetIfDirty(pipeline);
            }
            Undo.RecordObject(data,"Use independent painted scene renderer");data.SetRenderer(index);
            Undo.RecordObject(camera,"Sort painted ground by view depth");camera.transparencySortMode=TransparencySortMode.Orthographic;

            string texturePath=Root+"/Textures/T_StylizedRuins_GroundPainting.png";
            var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.sRGBTexture=true;importer.alphaSource=TextureImporterAlphaSource.None;importer.mipmapEnabled=true;
            importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Trilinear;importer.maxTextureSize=4096;
            importer.npotScale=TextureImporterNPOTScale.None;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            string materialPath=Root+"/Materials/M_StylizedRuins_PaintedGround.mat";
            var ground=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(!ground){ground=new Material(shader){name="M_StylizedRuins_PaintedGround"};AssetDatabase.CreateAsset(ground,materialPath);}
            ground.SetTexture("_GroundPainting",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            ground.enableInstancing=true;ground.renderQueue=-1;
            Undo.RecordObject(manager,"Use painted terrain material");Undo.RecordObject(manager.groundRenderer,"Use painted terrain material");
            manager.groundMaterial=ground;manager.Apply();
            var blend=manager.GetComponent<StylizedRuinsGroundBlend>();
            if(!blend)blend=Undo.AddComponent<StylizedRuinsGroundBlend>(manager.gameObject);
            Undo.RecordObject(blend,"Configure fixed camera ground painting");
            blend.referenceCamera=camera;blend.groundMaterial=ground;blend.CaptureProjection();
            // Keep source materials available; the new renderer uses its own calibration without the old retro grade.
            if(!AssetDatabase.IsValidFolder(Root+"/Materials/PaintedScene"))AssetDatabase.CreateFolder(Root+"/Materials","PaintedScene");
            foreach(var r in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())
            {
                var original=r.sharedMaterial;
                if(!original || original.shader.name!="StylizedRuins/Backdrop")continue;
                string path=Root+"/Materials/PaintedScene/"+original.name+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!material)
                {
                    material=new Material(original){name=original.name};
                    material.SetFloat("_PaintContrast",1);material.SetFloat("_ShadowLift",0);
                    AssetDatabase.CreateAsset(material,path);
                }
                Undo.RecordObject(r,"Use painted scene color calibration");r.sharedMaterial=material;
            }
            EditorUtility.SetDirty(blend);EditorUtility.SetDirty(manager);EditorUtility.SetDirty(ground);AssetDatabase.SaveAssetIfDirty(ground);
            EditorUtility.SetDirty(data);EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);EditorSceneManager.SaveScene(manager.gameObject.scene);
            Selection.activeGameObject=manager.gameObject;SceneView.RepaintAll();Capture();
            Debug.Log("Painted ground transition applied. Independent native-resolution Forward renderer; surface-only atmosphere; no background blur. Existing terrain mesh, collider and grass LOD retained.");
        }
        [MenuItem("Tools/Scene Art/StylizedRuins/Capture Painted Scene")]
        public static void Capture()
        {
            var blend=UnityEngine.Object.FindObjectOfType<StylizedRuinsGroundBlend>();blend.Apply();
            StylizedGrassSetup.Capture(blend.referenceCamera,Output+"/StylizedRuins_PaintedTransition.png");
        }
        static void EnsureDepthFeature(UniversalRendererData renderer)
        {
            if(renderer.rendererFeatures.OfType<StylizedRuinsGroundDepthFeature>().Any())return;
            var feature=ScriptableObject.CreateInstance<StylizedRuinsGroundDepthFeature>();feature.name="Painted Ground Solid Depth";
            AssetDatabase.AddObjectToAsset(feature,renderer);renderer.rendererFeatures.Add(feature);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature,out string guid,out long id);
            var so=new SerializedObject(renderer);var map=so.FindProperty("m_RendererFeatureMap");
            map.arraySize=renderer.rendererFeatures.Count;map.GetArrayElementAtIndex(map.arraySize-1).longValue=id;
            so.ApplyModifiedPropertiesWithoutUndo();renderer.SetDirty();EditorUtility.SetDirty(renderer);AssetDatabase.SaveAssetIfDirty(renderer);
        }
        [MenuItem("Tools/Scene Art/StylizedRuins/Finalize Painted Ground Depth")]
        public static void FinalizeDepth()
        {
            EnsureDepthFeature(AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath));Capture();
        }
        [MenuItem("Tools/Scene Art/StylizedRuins/Validate Background Fog Exclusion")]
        public static void Validate()
        {
            var blend=UnityEngine.Object.FindObjectOfType<StylizedRuinsGroundBlend>();
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if(!blend || !renderer || renderer.renderingMode!=RenderingMode.Forward || renderer.lowResolution.enabled || renderer.rendererFeatures.Count!=1 || !(renderer.rendererFeatures[0] is StylizedRuinsGroundDepthFeature))
                throw new InvalidOperationException("Independent renderer configuration mismatch.");
            if(ShaderUtil.ShaderHasError(blend.groundMaterial.shader))throw new InvalidOperationException("Ground shader error.");
            float original=blend.fogStrength;
            try
            {
                blend.fogStrength=0;blend.Apply();
                var clear=StylizedGrassSetup.Capture(blend.referenceCamera,Output+"/StylizedRuins_FogOff.png");
                blend.fogStrength=1;blend.Apply();
                var fogged=StylizedGrassSetup.Capture(blend.referenceCamera,Output+"/StylizedRuins_FogMax.png");
                int backgroundChanged=0,groundChanged=0;
                for(int y=0;y<900;y++)for(int x=0;x<1600;x++)
                {
                    var a=clear[y*1600+x];var b=fogged[y*1600+x];
                    int delta=Math.Abs(a.r-b.r)+Math.Abs(a.g-b.g)+Math.Abs(a.b-b.b);
                    if(y>=675 && delta>1)backgroundChanged++;
                    if(y<500 && delta>3)groundChanged++;
                }
                if(backgroundChanged!=0 || groundChanged<1000)throw new InvalidOperationException($"Fog exclusion failed: background={backgroundChanged}, ground={groundChanged}.");
                File.WriteAllText(Output+"/painted-ground-validation.json",$"{{\"forward\":true,\"nativeResolution\":true,\"fullscreenFogOrBlur\":false,\"backgroundPixelsChanged\":{backgroundChanged},\"groundPixelsChanged\":{groundChanged}}}");
                Debug.Log($"Surface-only fog verified: 0 changed upper-background pixels, {groundChanged} changed ground pixels at maximum strength.");
            }
            finally{blend.fogStrength=original;blend.Apply();Capture();}
        }
        [MenuItem("Tools/Scene Art/StylizedRuins/Validate Painted Ground Occlusion")]
        public static void ValidateOcclusion()
        {
            var blend=UnityEngine.Object.FindObjectOfType<StylizedRuinsGroundBlend>();
            var root=new GameObject("Painted Ground Depth Validation"){hideFlags=HideFlags.HideAndDontSave};
            Material ground=null,card=null;Texture2D blue=null;
            try
            {
                var cameraGo=new GameObject("Validation Camera");cameraGo.transform.SetParent(root.transform);
                var camera=cameraGo.AddComponent<Camera>();camera.CopyFrom(blend.referenceCamera);camera.enabled=false;
                int index=new SerializedObject(blend.referenceCamera.GetUniversalAdditionalCameraData()).FindProperty("m_RendererIndex").intValue;
                camera.GetUniversalAdditionalCameraData().SetRenderer(index);
                camera.transform.SetPositionAndRotation(new Vector3(1000,1000,1000),Quaternion.identity);
                camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
                blue=new Texture2D(1,1,TextureFormat.RGBA32,false);blue.SetPixel(0,0,Color.blue);blue.Apply();
                ground=new Material(Shader.Find("StylizedRuins/Painted Ground Transition"));
                ground.SetTexture("_GroundPainting",blue);ground.SetVector("_PaintingOrigin",camera.transform.position);
                ground.SetVector("_PaintingProjection",new Vector4(Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f),16f/9f,1,1));
                ground.SetVector("_GroundBlend",new Vector4(6,10,0,0));ground.SetVector("_GroundFog",new Vector4(0,10,0,0));
                card=new Material(Shader.Find("StylizedRuins/Backdrop"));card.SetColor("_BaseColor",Color.red);card.SetFloat("_ZWrite",1);
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/BackdropQuad.asset");
                GameObject Quad(Material material,float depth,int sorting)
                {
                    var go=new GameObject("Depth Sample");go.layer=31;go.transform.SetParent(root.transform);
                    go.transform.position=camera.transform.position+Vector3.forward*depth;go.transform.localScale=new Vector3(3,3,1);
                    go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.sortingOrder=sorting;
                    return go;
                }
                var terrain=Quad(ground,5,0);var red=Quad(card,7,100);
                Color Center()=>StylizedGrassSetup.Capture(camera,Path.Combine(Path.GetTempPath(),"PaintedGround-DepthCheck.png"))[450*1600+800];
                var behind=Center();if(behind.b<.9f || behind.r>.05f)throw new InvalidOperationException("A later-drawn card behind solid terrain remained visible.");
                red.transform.position=camera.transform.position+Vector3.forward*3;
                var near=Center();if(near.r<.9f || near.b>.05f)throw new InvalidOperationException("An object in front of the terrain was hidden.");
                terrain.transform.position=camera.transform.position+Vector3.forward*8;
                red.transform.position=camera.transform.position+Vector3.forward*9;red.GetComponent<MeshRenderer>().sortingOrder=-100;
                var edge=Center();if(edge.r<.25f || edge.b<.25f)throw new InvalidOperationException("Soft terrain edge did not blend with the background.");
                File.WriteAllText(Output+"/painted-ground-depth-validation.json","{\"solidGroundOccludesLaterCard\":true,\"nearObjectVisible\":true,\"softEdgeBlends\":true}");
                Debug.Log("Painted ground depth passed: solid core occlusion, near object visibility, and transparent edge blending.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                if(ground)UnityEngine.Object.DestroyImmediate(ground);if(card)UnityEngine.Object.DestroyImmediate(card);if(blue)UnityEngine.Object.DestroyImmediate(blue);
            }
        }
    }
}
