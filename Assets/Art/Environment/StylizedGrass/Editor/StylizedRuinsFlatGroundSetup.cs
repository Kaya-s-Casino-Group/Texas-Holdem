using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace StylizedGrass.Editor
{
    public static class StylizedRuinsFlatGroundSetup
    {
        const string Root="Assets/Art/Environment/StylizedRuins";
        const string Output="ArtSource/StylizedGrass";
        [MenuItem("Tools/Scene Art/StylizedRuins/Lay Ground Flat Preserving Picture")]
        public static void Apply()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
            var ground=GameObject.Find("StylizedRuins Ground Card");
            if(!ground)throw new InvalidOperationException("Ground card required.");
            var renderer=ground.GetComponent<MeshRenderer>();var material=renderer.sharedMaterial;
            if(material.GetFloat("_ProjectiveUV")>.5f){Validate();return;}
            if(ShaderUtil.ShaderHasError(material.shader))throw new InvalidOperationException("Compile the backdrop shader first.");
            var camera=Camera.main;var t=ground.transform;
            Vector3 origin=camera.transform.position,center=t.position,right=t.right,up=t.up,normal=t.forward;
            float width=t.lossyScale.x,height=t.lossyScale.y,planeHeight=0;
            float d=Vector3.Dot(center-origin,normal);
            var uvCorners=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,.54f),new Vector2(0,.54f)};
            var vertices=new Vector3[4];var uvq=new List<Vector3>();
            var plane=new Plane(Vector3.up,new Vector3(0,planeHeight,0));
            for(int i=0;i<4;i++)
            {
                var uv=uvCorners[i];var target=center+right*((uv.x-.5f)*width)+up*((uv.y-.5f)*height);
                var ray=new Ray(origin,(target-origin).normalized);
                if(!plane.Raycast(ray,out float distance))throw new InvalidOperationException("Ground horizon does not intersect the horizontal floor.");
                var world=ray.GetPoint(distance);vertices[i]=world;
                var delta=world-origin;float q=Vector3.Dot(delta,normal);
                float u=(Vector3.Dot(origin-center,right)/width+.5f)*q+Vector3.Dot(delta,right)*d/width;
                float v=(Vector3.Dot(origin-center,up)/height+.5f)*q+Vector3.Dot(delta,up)*d/height;
                uvq.Add(new Vector3(u,v,q));
            }
            // Isolate the floor to verify reprojection independently of changed physical depth occlusion.
            var renderers=UnityEngine.Object.FindObjectsOfType<MeshRenderer>();var enabled=new bool[renderers.Length];
            for(int i=0;i<renderers.Length;i++){enabled[i]=renderers[i].enabled;if(renderers[i]!=renderer)renderers[i].enabled=false;}
            try
            {
                var before=StylizedGrassSetup.Capture(camera,Output+"/GroundPerspective_Before.png");
                var mesh=new Mesh{name="StylizedRuinsFlatGround"};mesh.vertices=vertices;mesh.SetUVs(0,uvq);mesh.triangles=new[]{0,2,1,0,3,2};mesh.RecalculateNormals();mesh.RecalculateBounds();
                string path=Root+"/Meshes/StylizedRuinsFlatGround.asset";
                var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(saved){EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);mesh=saved;}
                else AssetDatabase.CreateAsset(mesh,path);
                Undo.RecordObject(t,"Lay ground flat");t.SetPositionAndRotation(Vector3.zero,Quaternion.identity);t.localScale=Vector3.one;
                var filter=ground.GetComponent<MeshFilter>();Undo.RecordObject(filter,"Use perspective-correct ground mesh");filter.sharedMesh=mesh;
                Undo.RecordObject(material,"Use baked projective UV");material.SetFloat("_ProjectiveUV",1);EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);AssetDatabase.SaveAssetIfDirty(mesh);
                var after=StylizedGrassSetup.Capture(camera,Output+"/GroundPerspective_After.png");
                double error=0;int maxError=0;
                for(int i=0;i<before.Length;i++)
                {
                    int delta=Math.Abs(before[i].r-after[i].r)+Math.Abs(before[i].g-after[i].g)+Math.Abs(before[i].b-after[i].b);
                    error+=delta;maxError=Math.Max(maxError,delta);
                }
                error/=before.Length*3;
                File.WriteAllText(Output+"/flat-ground-reprojection.json",$"{{\"averageChannelError255\":{error.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"maxRGBSumError\":{maxError},\"horizontalPlaneY\":0,\"runtimeProjectionComponents\":0}}");
                if(error>1)throw new InvalidOperationException($"Ground reprojection changed the picture: average channel error {error:F3}.");
            }
            finally{for(int i=0;i<renderers.Length;i++)if(renderers[i])renderers[i].enabled=enabled[i];}
            Validate();EditorSceneManager.MarkSceneDirty(ground.scene);EditorSceneManager.SaveScene(ground.scene);Selection.activeGameObject=ground;
            StylizedRuinsCardsOnlySetup.Capture();SceneView.RepaintAll();
        }
        [MenuItem("Tools/Scene Art/StylizedRuins/Validate Flat Ground")]
        public static void Validate()
        {
            var ground=GameObject.Find("StylizedRuins Ground Card");var mesh=ground.GetComponent<MeshFilter>().sharedMesh;
            var uvq=new List<Vector3>();mesh.GetUVs(0,uvq);
            if(uvq.Count!=4 || mesh.vertexCount!=4)throw new InvalidOperationException("Expected a single ground quad with baked projective UV.");
            foreach(var p in mesh.vertices)if(Mathf.Abs(ground.transform.TransformPoint(p).y)>.0001f)throw new InvalidOperationException("Ground is not horizontal at Y=0.");
            if(ground.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_ProjectiveUV")<.5f)throw new InvalidOperationException("Perspective UV disabled.");
            Debug.Log("Ground is a horizontal two-triangle plane. Perspective compensation is baked into mesh UVQ; no live terrain/projection controller.");
        }
    }
}
