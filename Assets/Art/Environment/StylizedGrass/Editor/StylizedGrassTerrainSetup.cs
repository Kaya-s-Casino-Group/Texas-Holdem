using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace StylizedGrass.Editor
{
    public static class StylizedGrassTerrainSetup
    {
        const string Root="Assets/Art/Environment/StylizedGrass";
        const string Output="ArtSource/StylizedGrass";

        [MenuItem("Tools/Scene Art/StylizedGrass/Restore Opaque Grass and Shape Terrain")]
        public static void Restore()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Exit Play mode first.");
            var manager=UnityEngine.Object.FindObjectOfType<StylizedGrassManager>();
            if(!manager || !manager.field || !manager.groundRenderer)throw new InvalidOperationException("Open the existing StylizedGrass scene.");
            var shader=Shader.Find("StylizedGrass/Opaque Grass");
            if(!shader || ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Opaque grass shader must compile first.");
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Source/Models/SM_StylizedGrass.fbx");
            var filters=source.GetComponentsInChildren<MeshFilter>().OrderByDescending(f=>f.sharedMesh.GetIndexCount(0)).ToArray();
            if(filters.Length<2)throw new InvalidOperationException("Both original grass mesh variants are required.");
            Undo.RecordObject(manager,"Restore original grass");Undo.RecordObject(manager.field,"Restore original grass");
            manager.nearMesh=StylizedGrassSetup.BakeMesh(filters[0],"StylizedGrassNear",true);
            manager.farMesh=StylizedGrassSetup.BakeMesh(filters[filters.Length-1],"StylizedGrassFar",true);
            // Restore the grass values recorded immediately before the Blender experiment.
            // Existing ground material overrides and main-camera LOD settings are intentionally not reassigned.
            manager.seed=7477;manager.areaSize=new Vector2(22,24);manager.spacing=.08f;manager.heightRange=new Vector2(.1f,.23f);
            manager.drawGrass=true;manager.castNearShadows=false;
            manager.lodging=.337f;manager.lodgingDirection=165;manager.lodgingVariation=.778f;manager.lodgingPatchSize=3.5f;
            manager.windDirection=new Vector2(1,.35f);manager.windStrength=.065f;manager.windSpeed=.7f;manager.windScale=1.1f;
            manager.rootColor=Hex("342133");manager.tipColor=Hex("812D44");manager.dryColor=Hex("794650");
            manager.colorVariation=.35f;manager.grassSmoothness=.08f;manager.rootOcclusion=.32f;manager.normalUp=.7f;manager.grassFill=.12f;
            var material=manager.grassMaterial;
            Undo.RecordObject(material,"Restore opaque grass material");
            var clean=new Material(shader){name=material.name};
            EditorUtility.CopySerialized(clean,material);UnityEngine.Object.DestroyImmediate(clean);
            material.enableInstancing=true;material.renderQueue=-1;
            material.SetColor("_RootColor",manager.rootColor);material.SetColor("_TipColor",manager.tipColor);material.SetColor("_DryColor",manager.dryColor);
            material.SetVector("_WindDirection",new Vector4(1,0,.35f,0));
            material.SetFloat("_WindStrength",manager.windStrength);material.SetFloat("_WindSpeed",manager.windSpeed);material.SetFloat("_WindScale",manager.windScale);
            material.SetFloat("_Lodging",manager.lodging);material.SetFloat("_LodgingDirection",manager.lodgingDirection);
            material.SetFloat("_LodgingVariation",manager.lodgingVariation);material.SetFloat("_LodgingPatchSize",manager.lodgingPatchSize);
            material.SetFloat("_ColorVariation",manager.colorVariation);material.SetFloat("_Smoothness",manager.grassSmoothness);
            material.SetFloat("_RootOcclusion",manager.rootOcclusion);material.SetFloat("_NormalUp",manager.normalUp);material.SetFloat("_Fill",manager.grassFill);
            material.SetFloat("_WindTime",-1);material.SetFloat("_Cull",0);
            EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);
            AssetDatabase.SaveAssetIfDirty(manager.nearMesh);AssetDatabase.SaveAssetIfDirty(manager.farMesh);
            RebuildTerrain(manager);
            manager.Apply();manager.field.Rebuild();
            EditorUtility.SetDirty(manager);EditorUtility.SetDirty(manager.field);
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);EditorSceneManager.SaveScene(manager.gameObject.scene);
            Selection.activeGameObject=manager.gameObject;SceneView.RepaintAll();
            Debug.Log("Restored original opaque 144/48-triangle instanced grass, retaining manager and main-camera LOD. Reference terrain rebuilt; ground material and renderer settings retained.");
        }
        static Color Hex(string value){ColorUtility.TryParseHtmlString("#"+value,out var c);return c;}

        [MenuItem("Tools/Scene Art/StylizedGrass/Rebuild Reference Terrain")]
        public static void RebuildReferenceTerrain()
        {
            var manager=UnityEngine.Object.FindObjectOfType<StylizedGrassManager>();
            if(!manager)throw new InvalidOperationException("Open the StylizedGrass scene.");
            RebuildTerrain(manager);manager.field.Rebuild();
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);EditorSceneManager.SaveScene(manager.gameObject.scene);
            SceneView.RepaintAll();
        }
        static void RebuildTerrain(StylizedGrassManager manager)
        {
            var filter=manager.groundRenderer.GetComponent<MeshFilter>();
            var collider=manager.groundRenderer.GetComponent<MeshCollider>();
            var old=filter.sharedMesh;
            float width=old.bounds.size.x,depth=old.bounds.size.z;
            var mesh=StylizedGrassSetup.GroundMesh(width,depth,180,196,manager.terrainData);
            filter.sharedMesh=StylizedGrassSetup.SaveMesh(mesh,"StylizedGrassGround");
            if(collider){collider.sharedMesh=null;collider.sharedMesh=filter.sharedMesh;EditorUtility.SetDirty(collider);}
            EditorUtility.SetDirty(filter);AssetDatabase.SaveAssetIfDirty(filter.sharedMesh);
        }

        [MenuItem("Tools/Scene Art/StylizedGrass/Validate Reference Terrain")]
        public static void ValidateTerrain()
        {
            Directory.CreateDirectory(Output);
            var manager=UnityEngine.Object.FindObjectOfType<StylizedGrassManager>();
            var mesh=manager.groundRenderer.GetComponent<MeshFilter>().sharedMesh;
            var collider=manager.groundRenderer.GetComponent<MeshCollider>();
            float maxSurfaceError=0;
            manager.Apply();
            foreach(var p in mesh.vertices)maxSurfaceError=Mathf.Max(maxSurfaceError,Mathf.Abs(p.y-manager.field.SurfaceHeight(p.x,p.z)));
            if(maxSurfaceError>.0001f || collider.sharedMesh!=mesh)throw new InvalidOperationException("Terrain collision or grass sampling does not match the terrain mesh.");
            File.WriteAllText(Output+"/terrain-validation.json",JsonUtility.ToJson(new TerrainReport{vertices=mesh.vertexCount,triangles=(int)mesh.GetIndexCount(0)/3,
                minHeight=mesh.bounds.min.y,maxHeight=mesh.bounds.max.y,maxSurfaceError=maxSurfaceError,colliderMatches=true},true));
            var original=manager.LodCamera;
            var go=new GameObject("Terrain Review Camera"){hideFlags=HideFlags.HideAndDontSave};
            try
            {
                var camera=go.AddComponent<Camera>();camera.CopyFrom(original);camera.enabled=false;
                int renderer=new SerializedObject(original.GetUniversalAdditionalCameraData()).FindProperty("m_RendererIndex").intValue;
                camera.GetUniversalAdditionalCameraData().SetRenderer(renderer);
                camera.transform.position=new Vector3(13,13,-20);camera.transform.LookAt(new Vector3(-.8f,2.8f,1.5f));
                camera.fieldOfView=48;
                StylizedGrassSetup.Capture(camera,Output+"/StylizedGrass_Terrain.png");
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
            StylizedGrassSetup.Capture(original,Output+"/StylizedGrass_Overview.png");
            Debug.Log($"Reference terrain verified: {mesh.vertexCount} vertices, height {mesh.bounds.min.y:F2}–{mesh.bounds.max.y:F2}, matching collider and grass surface.");
        }
        [Serializable] sealed class TerrainReport
        {
            public int vertices,triangles;public float minHeight,maxHeight,maxSurfaceError;public bool colliderMatches;
        }
    }
}
