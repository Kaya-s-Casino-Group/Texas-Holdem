using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace StylizedGrass.Editor
{
    public static class StylizedRuinsContactSetup
    {
        [MenuItem("Tools/Scene Art/StylizedRuins/Blend Ruins Into Flat Ground")]
        public static void Apply()
        {
            var front=GameObject.Find("StylizedRuins Backdrop/RuinsFront");
            if(!front)throw new InvalidOperationException("RuinsFront card required.");
            SeparateDepthLayers(Camera.main,front.transform);
            var r=front.GetComponent<MeshRenderer>();var m=r.sharedMaterial;
            Undo.RecordObject(m,"Blend ruins contact into floor");
            m.SetFloat("_GroundFadeHeight",3.5f);m.SetFloat("_GroundPlaneY",0);m.SetFloat("_GroundFadeBreakup",1.1f);
            // A soft foot must preserve its fractional alpha rather than clip it into a solid edge.
            m.SetFloat("_ZWrite",0);m.renderQueue=-1;EditorUtility.SetDirty(m);AssetDatabase.SaveAssetIfDirty(m);
            AddMidground(Camera.main);
            EditorSceneManager.MarkSceneDirty(front.scene);EditorSceneManager.SaveScene(front.scene);
            Selection.activeGameObject=front;StylizedRuinsCardsOnlySetup.Capture();
        }
        static void SeparateDepthLayers(Camera camera,Transform front)
        {
            float currentDepth=Vector3.Dot(front.position-camera.transform.position,camera.transform.forward);
            float ratio=40/Mathf.Max(.1f,currentDepth);
            var root=GameObject.Find("StylizedRuins Backdrop");
            float far=camera.farClipPlane;
            foreach(Transform layer in root.transform)
            {
                Undo.RecordObject(layer,"Separate distant cards from midground");
                layer.position=camera.transform.position+(layer.position-camera.transform.position)*ratio;
                layer.localScale*=ratio;
                var renderer=layer.GetComponent<MeshRenderer>();
                if(renderer)
                {
                    var bounds=renderer.bounds;var f=camera.transform.forward;
                    float extent=Mathf.Abs(f.x)*bounds.extents.x+Mathf.Abs(f.y)*bounds.extents.y+Mathf.Abs(f.z)*bounds.extents.z;
                    far=Mathf.Max(far,Vector3.Dot(bounds.center-camera.transform.position,f)+extent+5);
                }
            }
            Undo.RecordObject(camera,"Include distant painted layers");camera.farClipPlane=Mathf.Ceil(far);
        }
        static void AddMidground(Camera camera)
        {
            const string rootPath="Assets/Art/Environment/StylizedRuins";
            string texturePath=rootPath+"/Textures/T_StylizedRuins_MidgroundRubble.png";
            var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=true;importer.sRGBTexture=true;
            importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Trilinear;
            importer.maxTextureSize=4096;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.npotScale=TextureImporterNPOTScale.None;importer.SaveAndReimport();
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            string materialPath=rootPath+"/Materials/PaintedScene/M_StylizedRuins_MidgroundRubble.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(!material)
            {
                material=new Material(Shader.Find("StylizedRuins/Backdrop")){name="M_StylizedRuins_MidgroundRubble"};
                material.SetTexture("_BaseMap",texture);material.SetFloat("_ZWrite",1);material.SetFloat("_Cutoff",.5f);
                material.SetFloat("_GroundFadeHeight",.22f);material.SetFloat("_GroundFadeBreakup",.15f);
                material.enableInstancing=true;material.renderQueue=-1;AssetDatabase.CreateAsset(material,materialPath);
            }
            var root=GameObject.Find("StylizedRuins Midground");
            if(!root){root=new GameObject("StylizedRuins Midground");Undo.RegisterCreatedObjectUndo(root,"Add seam occluders");}
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(rootPath+"/Meshes/BackdropQuad.asset");
            var forward=Vector3.ProjectOnPlane(camera.transform.forward,Vector3.up).normalized;var right=Vector3.Cross(Vector3.up,forward);
            void Place(string name,float x,float depth,float height,Material mat,float baseMargin)
            {
                if(root.transform.Find(name))return;
                var go=new GameObject(name);Undo.RegisterCreatedObjectUndo(go,"Place midground rubble");go.transform.SetParent(root.transform,false);
                float widthAtDepth=2*depth*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f)*16/9;
                var position=camera.transform.position+forward*depth+right*((x-.5f)*widthAtDepth);
                position.y=height*(.5f-baseMargin);
                go.transform.SetPositionAndRotation(position,Quaternion.LookRotation(forward,Vector3.up));
                var tex=mat.mainTexture;go.transform.localScale=new Vector3(height*tex.width/tex.height,height,1);
                go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=mat;
                r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;
            }
            Place("Rubble Left",.27f,18,3.3f,material,.08f);
            Place("Rubble Right",.77f,21,2.9f,material,.08f);
            var low=AssetDatabase.LoadAssetAtPath<Material>(rootPath+"/Materials/PaintedScene/M_StylizedRuins_ForegroundRubble.mat");
            Place("Low Stones Center",.53f,24,2.7f,low,.04f);
        }
    }
}
