using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace StylizedGrass.Editor
{
    public static class StylizedRuinsForegroundSetup
    {
        const string Root="Assets/Art/Environment/StylizedRuins";
        [MenuItem("Tools/Scene Art/StylizedRuins/Fix Ground Aspect and Add Foreground")]
        public static void Apply()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
            var blend=UnityEngine.Object.FindObjectOfType<StylizedRuinsGroundBlend>();
            if(!blend || !blend.referenceCamera)throw new InvalidOperationException("Open the painted ruins scene first.");
            Undo.RecordObject(blend,"Fix ground painting proportions");
            blend.paintingZoom=1;blend.paintingOffset=Vector2.zero;blend.CaptureProjection();
            EditorUtility.SetDirty(blend);EditorUtility.SetDirty(blend.groundMaterial);AssetDatabase.SaveAssetIfDirty(blend.groundMaterial);
            var root=GameObject.Find("StylizedRuins Foreground");
            if(!root){root=new GameObject("StylizedRuins Foreground");Undo.RegisterCreatedObjectUndo(root,"Add foreground framing");}
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/BackdropQuad.asset");
            var shader=Shader.Find("StylizedRuins/Backdrop");
            if(!shader || ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Backdrop shader must compile first.");
            Add(root.transform,blend.referenceCamera,mesh,shader,"ForegroundLeft",4.5f,new Vector2(.1f,.47f),.88f);
            Add(root.transform,blend.referenceCamera,mesh,shader,"ForegroundRight",4.2f,new Vector2(.98f,.49f),1.06f);
            Add(root.transform,blend.referenceCamera,mesh,shader,"ForegroundRubble",3.8f,new Vector2(.5f,.13f),.60f);
            EditorSceneManager.MarkSceneDirty(root.scene);EditorSceneManager.SaveScene(root.scene);
            Selection.activeGameObject=root;SceneView.RepaintAll();Validate();
            StylizedRuinsPaintedSceneSetup.Capture();
        }
        static void Add(Transform root,Camera camera,Mesh mesh,Shader shader,string name,float depth,Vector2 center,float screenHeight)
        {
            string path=Root+"/Textures/T_StylizedRuins_"+name+".png";
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(!importer)throw new InvalidOperationException("Missing foreground: "+path);
            importer.sRGBTexture=true;importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=true;
            importer.wrapMode=TextureWrapMode.Clamp;importer.mipmapEnabled=true;importer.filterMode=FilterMode.Trilinear;
            importer.maxTextureSize=4096;importer.npotScale=TextureImporterNPOTScale.None;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            string matPath=Root+"/Materials/PaintedScene/M_StylizedRuins_"+name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if(!mat)
            {
                mat=new Material(shader){name="M_StylizedRuins_"+name};
                mat.SetTexture("_BaseMap",texture);mat.SetFloat("_ZWrite",1);mat.SetFloat("_Cutoff",.5f);
                mat.SetFloat("_PaintContrast",1);mat.SetFloat("_ShadowLift",0);mat.enableInstancing=true;mat.renderQueue=-1;
                AssetDatabase.CreateAsset(mat,matPath);
            }
            // Re-running the command preserves hand-positioned existing foreground cards.
            var existing=root.Find(name);if(existing)return;
            var go=new GameObject(name);Undo.RegisterCreatedObjectUndo(go,"Add foreground card");go.transform.SetParent(root,false);
            float viewHeight=2*depth*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f);
            float height=viewHeight*screenHeight,width=height*texture.width/texture.height;
            go.transform.SetPositionAndRotation(camera.transform.position+camera.transform.forward*depth
                +camera.transform.right*((center.x-.5f)*viewHeight*16/9)+camera.transform.up*((center.y-.5f)*viewHeight),camera.transform.rotation);
            go.transform.localScale=new Vector3(width,height,1);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=mat;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            // Prefab uses an unscaled unit-height card retaining the source image aspect.
            var prefab=UnityEngine.Object.Instantiate(go);prefab.name="PF_StylizedRuins_"+name;
            prefab.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);prefab.transform.localScale=new Vector3((float)texture.width/texture.height,1,1);
            PrefabUtility.SaveAsPrefabAsset(prefab,Root+"/Prefabs/"+prefab.name+".prefab");UnityEngine.Object.DestroyImmediate(prefab);
        }
        [MenuItem("Tools/Scene Art/StylizedRuins/Validate Painting Proportions")]
        public static void Validate()
        {
            var blend=UnityEngine.Object.FindObjectOfType<StylizedRuinsGroundBlend>();blend.Apply();
            var m=blend.groundMaterial;var image=m.GetTexture("_GroundPainting");
            var crop=m.GetVector("_PaintingCrop");var projection=m.GetVector("_PaintingProjection");
            float ratio=crop.x*image.width/(projection.y*crop.y*image.height);
            if(Mathf.Abs(ratio-1)>.0001f)throw new InvalidOperationException("Ground painting pixels are not uniformly scaled.");
            var root=GameObject.Find("StylizedRuins Foreground");int count=0;
            foreach(var r in root.GetComponentsInChildren<MeshRenderer>())
            {
                var t=r.sharedMaterial.mainTexture;
                float actual=r.transform.lossyScale.x/r.transform.lossyScale.y;
                if(Mathf.Abs(actual-(float)t.width/t.height)>.0001f)throw new InvalidOperationException("Foreground proportions changed: "+r.name);
                if(r.sharedMaterial.renderQueue!=3000 || r.sortingOrder!=0 || r.sharedMaterial.GetFloat("_ZWrite")<.5f)throw new InvalidOperationException("Foreground depth settings invalid.");
                count++;
            }
            File.WriteAllText("ArtSource/StylizedGrass/foreground-validation.json",$"{{\"groundPixelScaleRatio\":{ratio.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"proportionalForegroundCards\":{count},\"depthWrites\":true}}");
            Debug.Log("Ground image now uses uniform pixel scale; all foreground cards retain source aspect and depth occlusion.");
        }
    }
}
