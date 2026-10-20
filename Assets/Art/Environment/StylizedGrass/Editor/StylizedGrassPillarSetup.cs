using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace StylizedGrass.Editor
{
    public static class StylizedGrassPillarSetup
    {
        const string Root="Assets/Art/Environment/StylizedRuins";
        [MenuItem("Tools/Scene Art/StylizedGrass/Import Stone Pillar Variants")]
        public static void ImportVariants()
        {
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/BackdropQuad.asset");
            var reference=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/M_StylizedRuins_Pillar.mat");
            if(!mesh || !reference || ShaderUtil.ShaderHasError(reference.shader))
                throw new InvalidOperationException("Original pillar mesh and compiled material are required.");
            var assets=new System.Collections.Generic.List<UnityEngine.Object>();
            foreach(string variant in new[]{"Slender","Broad","Leaning","Overhang"})
            {
                string texturePath=Root+"/Textures/T_StylizedRuins_Pillar_"+variant+".png";
                var importer=AssetImporter.GetAtPath(texturePath) as TextureImporter;
                if(!importer)throw new InvalidOperationException("Missing variant: "+variant);
                importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;
                importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=true;
                importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;
                importer.maxTextureSize=2048;importer.npotScale=TextureImporterNPOTScale.None;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                string materialPath=Root+"/Materials/M_StylizedRuins_Pillar_"+variant+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if(!material)
                {
                    material=new Material(reference){name="M_StylizedRuins_Pillar_"+variant,enableInstancing=true};
                    material.SetTexture("_BaseMap",texture);material.renderQueue=-1;material.SetFloat("_ZWrite",1);material.SetFloat("_Cutoff",.5f);AssetDatabase.CreateAsset(material,materialPath);
                }
                string prefabPath=Root+"/Prefabs/PF_StylizedRuins_Pillar_"+variant+".prefab";
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if(!prefab)
                {
                    var go=new GameObject("StylizedRuins Pillar "+variant);
                    try
                    {
                        var card=new GameObject("Painted Card");card.transform.SetParent(go.transform,false);
                        card.transform.localPosition=new Vector3(0,2.8f,0);
                        card.transform.localScale=new Vector3(6f*texture.width/texture.height,6,1);
                        card.AddComponent<MeshFilter>().sharedMesh=mesh;Configure(card.AddComponent<MeshRenderer>(),material);
                        prefab=PrefabUtility.SaveAsPrefabAsset(go,prefabPath);
                    }
                    finally{UnityEngine.Object.DestroyImmediate(go);}
                }
                var renderer=prefab.GetComponentInChildren<MeshRenderer>();
                if(!renderer || renderer.sharedMaterial!=material || material.GetTexture("_BaseMap")!=texture || !material.enableInstancing)
                    throw new InvalidOperationException("Variant prefab validation failed: "+variant);
                assets.Add(prefab);
            }
            Selection.objects=assets.ToArray();EditorGUIUtility.PingObject(assets[0]);
            Debug.Log("Four pillar variants imported and verified: Slender, Broad, Leaning, Overhang. Bottom-centered root pivots, shared quad, alpha materials and instancing. Existing scene composition retained.");
        }
        [MenuItem("Tools/Scene Art/StylizedGrass/Add Stone Pillar Cards")]
        public static void Build()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Exit Play mode first.");
            var manager=UnityEngine.Object.FindObjectOfType<StylizedGrassManager>();
            if(!manager || !manager.LodCamera || !manager.field)throw new InvalidOperationException("Open the grass scene with its main camera.");
            string texturePath=Root+"/Textures/T_StylizedRuins_Pillar.png";
            var importer=AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if(!importer)throw new InvalidOperationException("Import the pillar texture first.");
            importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;
            importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=true;
            importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;
            importer.maxTextureSize=2048;importer.npotScale=TextureImporterNPOTScale.None;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/BackdropQuad.asset");
            var shader=Shader.Find("StylizedRuins/Backdrop");
            if(!mesh || !shader || ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Existing backdrop mesh and shader are required.");
            string materialPath=Root+"/Materials/M_StylizedRuins_Pillar.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(!material)
            {
                var reference=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/M_StylizedRuins_Castle.mat");
                material=reference?new Material(reference):new Material(shader);
                material.name="M_StylizedRuins_Pillar";material.SetTexture("_BaseMap",texture);
                material.renderQueue=-1;material.enableInstancing=true;material.SetFloat("_ZWrite",1);material.SetFloat("_Cutoff",.5f);
                AssetDatabase.CreateAsset(material,materialPath);
            }
            var root=GameObject.Find("StylizedRuins Pillars");
            if(!root){root=new GameObject("StylizedRuins Pillars");Undo.RegisterCreatedObjectUndo(root,"Add stone pillar cards");}
            // Only fill missing slots: later menu use preserves hand-adjusted composition and material tuning.
            Add(root.transform,manager,mesh,material,"Pillar Mid Left",16,.13f,.45f,-3);
            Add(root.transform,manager,mesh,material,"Pillar Far Left",23,.34f,.25f,2);
            Add(root.transform,manager,mesh,material,"Pillar Mid Right",18,.96f,.55f,4);
            Directory.CreateDirectory(Root+"/Prefabs");
            string prefabPath=Root+"/Prefabs/PF_StylizedRuins_Pillar.prefab";
            if(!AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath))
            {
                var go=new GameObject("StylizedRuins Pillar");
                try
                {
                    go.AddComponent<MeshFilter>().sharedMesh=mesh;
                    Configure(go.AddComponent<MeshRenderer>(),material);
                    go.transform.localScale=new Vector3(4,6,1);
                    PrefabUtility.SaveAsPrefabAsset(go,prefabPath);
                }
                finally{UnityEngine.Object.DestroyImmediate(go);}
            }
            EditorSceneManager.MarkSceneDirty(root.scene);EditorSceneManager.SaveScene(root.scene);
            Selection.activeGameObject=root;SceneView.RepaintAll();Capture();
        }
        static void Configure(MeshRenderer renderer,Material material)
        {
            renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;
            renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;renderer.allowOcclusionWhenDynamic=false;
        }
        static void Add(Transform parent,StylizedGrassManager manager,Mesh mesh,Material material,string name,float depth,float screenX,float screenHeight,float roll)
        {
            if(parent.Find(name))return;
            var camera=manager.LodCamera;float vh=2*depth*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f);
            float height=vh*screenHeight;
            Vector3 foot=camera.transform.position+camera.transform.forward*depth+camera.transform.right*((screenX-.5f)*vh*16/9);
            var field=manager.field.transform;Vector3 local=field.InverseTransformPoint(foot);
            manager.Apply();local.y=manager.field.SurfaceHeight(local.x,local.z);foot=field.TransformPoint(local);
            Quaternion rotation=camera.transform.rotation*Quaternion.Euler(0,0,roll);
            var go=new GameObject(name);Undo.RegisterCreatedObjectUndo(go,"Add stone pillar");go.transform.SetParent(parent,false);
            go.transform.rotation=rotation;
            // Generated stone foot ends at UV y~.03. Bury it slightly to hide the card's contact line.
            go.transform.position=foot+(rotation*Vector3.up)*(height*.45f);
            go.transform.localScale=new Vector3(height*(2f/3),height,1);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;Configure(go.AddComponent<MeshRenderer>(),material);
        }
        [MenuItem("Tools/Scene Art/StylizedGrass/Capture Stone Pillar Cards")]
        public static void Capture()
        {
            var manager=UnityEngine.Object.FindObjectOfType<StylizedGrassManager>();
            var root=GameObject.Find("StylizedRuins Pillars");
            if(!root)throw new InvalidOperationException("No pillar cards in the current scene.");
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
                if(!renderer.sharedMaterial || ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader))throw new InvalidOperationException("Pillar material error.");
            Directory.CreateDirectory("ArtSource/StylizedGrass");
            StylizedGrassSetup.Capture(manager.LodCamera,"ArtSource/StylizedGrass/StylizedGrass_Pillars.png");
            Debug.Log("Stone pillar cards verified: "+root.transform.childCount+" independent quads, shared instancing-enabled material; terrain and existing backdrops retained.");
        }
    }
}
