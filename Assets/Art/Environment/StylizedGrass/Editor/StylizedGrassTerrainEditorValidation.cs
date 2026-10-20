using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace StylizedGrass.Editor
{
    public static class StylizedGrassTerrainEditorValidation
    {
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        [MenuItem("Tools/Scene Art/StylizedGrass/Validate Terrain Editor")]
        public static void Validate()
        {
            var manager=UnityEngine.Object.FindObjectOfType<StylizedGrassManager>();
            Require(manager && manager.terrainData,"Install terrain editor first.");
            var originalData=manager.terrainData;var filter=manager.groundRenderer.GetComponent<MeshFilter>();
            var collider=manager.groundRenderer.GetComponent<MeshCollider>();var originalMesh=filter.sharedMesh;
            var probe=UnityEngine.Object.Instantiate(originalData);var mesh=UnityEngine.Object.Instantiate(originalMesh);
            string temp=AssetDatabase.GenerateUniqueAssetPath("Assets/Art/Environment/StylizedGrass/TerrainData/EditorValidation.asset");
            string originalSettings=EditorJsonUtility.ToJson(manager),originalGround=EditorJsonUtility.ToJson(manager.groundMaterial);
            int undoGroup=-1;
            try
            {
                AssetDatabase.CreateAsset(probe,temp);
                var fresh=ScriptableObject.CreateInstance<StylizedGrassTerrainData>();fresh.Initialize(originalData.Size);
                try
                {
                    for(int z=0;z<10;z++)for(int x=0;x<10;x++)
                    {
                        float px=x-5,pz=z-5;
                        Require(fresh.Height(px,pz)==StylizedGrassTerrainShape.Height(px,pz),"Zero-offset terrain changed base height.");
                        Require(fresh.Coverage(px,pz)==StylizedGrassTerrainShape.Coverage(px,pz),"Zero-offset terrain changed base coverage.");
                    }
                    float baseHeight=fresh.Height(0,0),outside=fresh.Height(5,5);
                    fresh.Paint(Vector2.zero,1,.5f,.25f,GrassTerrainBrush.Raise);
                    Require(Mathf.Abs(fresh.Height(0,0)-baseHeight-.5f)<.0001f && fresh.Height(5,5)==outside,"Raise or brush boundary failed.");
                    fresh.Paint(Vector2.zero,.25f,.7f,0,GrassTerrainBrush.Raise);
                    float peak=fresh.Height(0,0);fresh.Paint(Vector2.zero,1,1,.25f,GrassTerrainBrush.Smooth);
                    Require(fresh.Height(0,0)<peak,"Smooth did not lower a local peak.");
                    fresh.Paint(Vector2.zero,1,1,.25f,GrassTerrainBrush.Flatten,4);
                    Require(Mathf.Abs(fresh.Height(0,0)-4)<.0001f,"Flatten failed.");
                    fresh.Paint(Vector2.zero,1,.3f,.25f,GrassTerrainBrush.Lower);
                    Require(Mathf.Abs(fresh.Height(0,0)-3.7f)<.0001f,"Lower failed.");
                    fresh.Paint(Vector2.zero,1,2,.25f,GrassTerrainBrush.EraseGrass);
                    Require(fresh.Coverage(0,0)==0,"Erase failed.");
                    fresh.Paint(Vector2.zero,1,2,.25f,GrassTerrainBrush.AddGrass);
                    Require(fresh.Coverage(0,0)==1,"Add grass failed.");
                    fresh.Paint(Vector2.zero,1,10,.25f,GrassTerrainBrush.Restore);
                    Require(fresh.Height(0,0)==baseHeight && fresh.Coverage(0,0)==StylizedGrassTerrainShape.Coverage(0,0),"Restore failed.");
                }
                finally{UnityEngine.Object.DestroyImmediate(fresh);}
                manager.terrainData=probe;filter.sharedMesh=mesh;collider.sharedMesh=mesh;manager.Apply();
                float initial=probe.Height(0,0);
                Undo.IncrementCurrentGroup();undoGroup=Undo.GetCurrentGroup();
                Undo.RegisterCompleteObjectUndo(probe,"Validate terrain brush undo");
                probe.Paint(Vector2.zero,1,.4f,.25f,GrassTerrainBrush.Raise);EditorUtility.SetDirty(probe);
                Undo.FlushUndoRecordObjects();StylizedGrassTerrainEditing.Sync(manager);
                Require(Mathf.Abs(probe.Height(0,0)-initial-.4f)<.0001f,"Paint failed.");
                Undo.PerformUndo();Require(Mathf.Abs(probe.Height(0,0)-initial)<.0001f,"Undo failed.");
                Undo.PerformRedo();Require(Mathf.Abs(probe.Height(0,0)-initial-.4f)<.0001f,"Redo failed.");
                foreach(var v in mesh.vertices)Require(Mathf.Abs(v.y-manager.field.SurfaceHeight(v.x,v.z))<.0001f,"Grass / mesh height mismatch.");
                Require(collider.sharedMesh==mesh,"Collider not updated.");
                Vector3 surface=manager.groundRenderer.transform.TransformPoint(new Vector3(0,probe.Height(0,0),0));
                Vector3 up=manager.groundRenderer.transform.up;
                Require(collider.Raycast(new Ray(surface+up*5,-up),out var hit,10) && Vector3.Distance(hit.point,surface)<.03f,"Collision surface mismatch.");
                float saved=probe.Height(0,0);StylizedGrassTerrainEditing.Save(manager);
                AssetDatabase.ImportAsset(temp,ImportAssetOptions.ForceUpdate);
                Require(Mathf.Abs(AssetDatabase.LoadAssetAtPath<StylizedGrassTerrainData>(temp).Height(0,0)-saved)<.0001f,"Saved data did not survive reimport.");
                probe.Paint(Vector2.zero,100,3,.95f,GrassTerrainBrush.EraseGrass);StylizedGrassTerrainEditing.Sync(manager);
                StylizedGrassSetup.Capture(manager.LodCamera,Path.Combine(Path.GetTempPath(),"StylizedGrass-ErasedValidation.png"));
                Require(manager.field.TotalInstances==0,"Erased coverage still produced grass instances.");
                foreach(var color in mesh.colors)Require(color.r<.001f,"Erased coverage did not update the grass/soil blend.");
                Directory.CreateDirectory("ArtSource/StylizedGrass");
                File.WriteAllText("ArtSource/StylizedGrass/terrain-editor-validation.json","{\n  \"brushes\": true,\n  \"undoRedo\": true,\n  \"savedReimport\": true,\n  \"meshGrassColliderMatch\": true,\n  \"erasedGrassInstances\": 0,\n  \"soilBlendMatches\": true\n}\n");
                Debug.Log("Terrain editor validation passed: all brushes, undo/redo, save/reimport, mesh/grass/collider alignment, complete erase and soil blend.");
            }
            finally
            {
                manager.terrainData=originalData;filter.sharedMesh=originalMesh;collider.sharedMesh=null;collider.sharedMesh=originalMesh;
                manager.Apply();manager.field.Rebuild();
                Undo.ClearUndo(probe);AssetDatabase.DeleteAsset(temp);UnityEngine.Object.DestroyImmediate(mesh);
                Require(EditorJsonUtility.ToJson(manager)==originalSettings,"Validation changed manager settings.");
                Require(EditorJsonUtility.ToJson(manager.groundMaterial)==originalGround,"Validation changed ground material.");
                SceneView.RepaintAll();
            }
        }
    }
}
