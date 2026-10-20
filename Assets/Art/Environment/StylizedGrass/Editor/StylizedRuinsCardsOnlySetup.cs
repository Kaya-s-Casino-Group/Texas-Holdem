using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StylizedGrass.Editor
{
    public static class StylizedRuinsCardsOnlySetup
    {
        const string Root="Assets/Art/Environment/StylizedRuins";
        const string Output="ArtSource/StylizedGrass";
        [MenuItem("Tools/Scene Art/StylizedRuins/Use Cards Only")]
        public static void Apply()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
            var camera=Camera.main;if(!camera)throw new InvalidOperationException("Main camera required.");
            var shader=Shader.Find("StylizedRuins/Backdrop");
            if(!shader || ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Backdrop shader error.");
            string path=Root+"/Textures/T_StylizedRuins_GroundCard.png";
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture=true;importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=true;
            importer.wrapMode=TextureWrapMode.Clamp;importer.mipmapEnabled=true;importer.filterMode=FilterMode.Trilinear;
            importer.maxTextureSize=4096;importer.npotScale=TextureImporterNPOTScale.None;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            string materialPath=Root+"/Materials/PaintedScene/M_StylizedRuins_GroundCard.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(!material){material=new Material(shader){name="M_StylizedRuins_GroundCard"};AssetDatabase.CreateAsset(material,materialPath);}
            material.SetTexture("_BaseMap",texture);material.SetFloat("_PaintContrast",1);material.SetFloat("_ShadowLift",0);
            material.SetFloat("_ZWrite",0);material.SetFloat("_TopFadeStart",.40f);material.SetFloat("_TopFadeEnd",.54f);
            material.enableInstancing=true;material.renderQueue=-1;EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);
            var ground=GameObject.Find("StylizedRuins Ground Card");
            if(!ground)
            {
                ground=new GameObject("StylizedRuins Ground Card");Undo.RegisterCreatedObjectUndo(ground,"Create perspective ground card");
                float depth=10,viewHeight=2*depth*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f);
                float width=viewHeight*Mathf.Max(camera.aspect,16f/9f),height=width*texture.height/texture.width;
                ground.transform.SetPositionAndRotation(camera.transform.position+camera.transform.forward*depth+camera.transform.up*((height-viewHeight)*.5f-viewHeight*.12f),camera.transform.rotation);
                ground.transform.localScale=new Vector3(width,height,1);
                ground.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/BackdropQuad.asset");
                var r=ground.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
                r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;
            }
            // Remove the superseded live grass/terrain controls, without deleting reusable project assets.
            foreach(var manager in UnityEngine.Object.FindObjectsOfType<StylizedGrassManager>())Undo.DestroyObjectImmediate(manager.gameObject);
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Root+"/Rendering/StylizedRuinsForwardRenderer.asset");
            if(renderer)
            {
                foreach(var feature in renderer.rendererFeatures.ToArray())
                {
                    if(feature is StylizedRuinsGroundDepthFeature){renderer.rendererFeatures.Remove(feature);UnityEngine.Object.DestroyImmediate(feature,true);}
                }
                var so=new SerializedObject(renderer);so.FindProperty("m_RendererFeatureMap").ClearArray();so.ApplyModifiedPropertiesWithoutUndo();
                renderer.SetDirty();EditorUtility.SetDirty(renderer);AssetDatabase.SaveAssetIfDirty(renderer);
            }
            EditorSceneManager.MarkSceneDirty(ground.scene);EditorSceneManager.SaveScene(ground.scene);
            Selection.activeGameObject=ground;SceneView.RepaintAll();Validate();Capture();
        }
        [MenuItem("Tools/Scene Art/StylizedRuins/Capture Cards Only")]
        public static void Capture(){StylizedGrassSetup.Capture(Camera.main,Output+"/StylizedRuins_CardsOnly.png");}
        [MenuItem("Tools/Scene Art/StylizedRuins/Fit Ground Card Horizon")]
        public static void FitHorizon()
        {
            var camera=Camera.main;var ground=GameObject.Find("StylizedRuins Ground Card").transform;
            float depth=Vector3.Dot(ground.position-camera.transform.position,camera.transform.forward);
            float viewHeight=2*depth*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f);
            Undo.RecordObject(ground,"Fit painted ground horizon");
            float target=(ground.lossyScale.y-viewHeight)*.5f-viewHeight*.12f;
            float current=Vector3.Dot(ground.position-camera.transform.position,camera.transform.up);
            ground.position+=camera.transform.up*(target-current);
            var right=GameObject.Find("StylizedRuins Foreground/ForegroundRight").transform;
            depth=Vector3.Dot(right.position-camera.transform.position,camera.transform.forward);
            float viewWidth=2*depth*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f)*16/9;
            current=Vector3.Dot(right.position-camera.transform.position,camera.transform.right);
            Undo.RecordObject(right,"Frame right foreground wall");right.position+=camera.transform.right*((.86f-.5f)*viewWidth-current);
            EditorSceneManager.MarkSceneDirty(ground.gameObject.scene);EditorSceneManager.SaveScene(ground.gameObject.scene);Capture();
        }
        [MenuItem("Tools/Scene Art/StylizedRuins/Validate Cards Only")]
        public static void Validate()
        {
            if(UnityEngine.Object.FindObjectOfType<StylizedGrassManager>() || UnityEngine.Object.FindObjectOfType<StylizedGrassField>() || UnityEngine.Object.FindObjectOfType<StylizedRuinsGroundBlend>())throw new InvalidOperationException("Live grass or terrain configuration remains.");
            int cards=0,triangles=0;
            foreach(var r in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())
            {
                if(!r.sharedMaterial || r.sharedMaterial.shader.name!="StylizedRuins/Backdrop")continue; // Preserve user-added test actors.
                var mesh=r.GetComponent<MeshFilter>().sharedMesh;triangles+=(int)mesh.GetIndexCount(0)/3;cards++;
            }
            var ground=GameObject.Find("StylizedRuins Ground Card");var tex=ground.GetComponent<MeshRenderer>().sharedMaterial.mainTexture;
            float ratio=ground.transform.lossyScale.x/ground.transform.lossyScale.y;
            if(ground.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_ProjectiveUV")>.5f)StylizedRuinsFlatGroundSetup.Validate();
            else if(Mathf.Abs(ratio-(float)tex.width/tex.height)>.0001f)throw new InvalidOperationException("Ground card is stretched.");
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Root+"/Rendering/StylizedRuinsForwardRenderer.asset");
            if(renderer.rendererFeatures.Count!=0)throw new InvalidOperationException("Ground renderer features remain.");
            File.WriteAllText(Output+"/cards-only-validation.json",$"{{\"cards\":{cards},\"triangles\":{triangles},\"groundAspectPreserved\":true,\"grassAndTerrainControllers\":0,\"rendererFeatures\":0}}");
            Debug.Log($"Cards-only scene verified: {cards} cards / {triangles} triangles, original ground image aspect, no grass or terrain controllers.");
        }
    }
}
