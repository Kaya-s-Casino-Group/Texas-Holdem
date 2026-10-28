using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace HarborEnvironment.Editor
{
    // Two-phase workflow: export immutable input, then apply only validated UV caches.
    // Original mesh assets, colliders and behaviours are never deleted or modified.
    public static class HarborMaterialGI
    {
        const string Work="output/harbor-material-gi";
        const string AssetsRoot="Assets/Art/Environment/Harbor/Meshes/MaterialGI";
        [Serializable] public class Item
        {
            public string name,material,kind,hash;
            public string[] sources;
            public float[] positions,uv0;
            public int[] triangles;
            public float surfaceArea,scaleInLightmap;
        }
        [Serializable] public class Input { public string scene; public int atlasSize; public float lightmapResolution; public List<Item> groups=new List<Item>(); }
        [Serializable] public class UVItem { public string name,hash; public float[] uv1; public bool validated; public int overlaps,collapsed,charts; public float coverage; }
        [Serializable] public class Cache { public List<UVItem> groups=new List<UVItem>(); }
        [Serializable] public class Applied { public string backup; public int sourceRenderers,activeRenderers; public bool attributesPreserved; public List<string> assets=new List<string>(); }
        sealed class Group { public string name,kind; public Material mat; public MeshRenderer[] renderers; public Mesh mesh; public Item data; }
        static string Hierarchy(Transform t)=>t.parent?Hierarchy(t.parent)+"/"+t.name:t.name;
        static string Kind(MeshRenderer r)
        {
            var m=r.sharedMaterial;
            if(m.HasProperty("_DeformMode")&&m.GetFloat("_DeformMode")>.5f)return "flag";
            if(m.shader.name.Contains("Water")||r.GetComponents<Component>().Any(c=>c&&c.GetType().Name.Contains("PlanarReflection")))return "water";
            return "static";
        }
        static string RenderSettings(MeshRenderer r)
        {
            var so=new SerializedObject(r);
            return $"{r.gameObject.layer}|{r.shadowCastingMode}|{r.receiveShadows}|{r.receiveGI}|{r.lightProbeUsage}|{r.reflectionProbeUsage}|{r.renderingLayerMask}|{so.FindProperty("m_ScaleInLightmap").floatValue}";
        }
        static void Guard()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||Lightmapping.isRunning)throw new InvalidOperationException("Exit play mode and wait for light baking.");
            if(SceneManager.GetActiveScene().path!="Assets/Scene/InTest/SingleBattle.unity")throw new InvalidOperationException("Open SingleBattle first.");
            if(GameObject.Find("Harbor_Environment/GI_Merged"))throw new InvalidOperationException("Already applied. Restore the saved backup before rebuilding.");
        }
        static List<Group> Build()
        {
            Guard();var root=GameObject.Find("Harbor_Environment");if(!root)throw new InvalidOperationException("Missing harbor root.");
            var rs=root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.enabled&&r.gameObject.activeInHierarchy&&(GameObjectUtility.GetStaticEditorFlags(r.gameObject)&StaticEditorFlags.ContributeGI)!=0).OrderBy(r=>Hierarchy(r.transform)).ToArray();
            if(rs.Length==0)throw new InvalidOperationException("No GI contributors.");
            foreach(var r in rs)
            {
                var mf=r.GetComponent<MeshFilter>();
                if(!mf||!mf.sharedMesh||!mf.sharedMesh.isReadable||r.sharedMaterials.Length!=1||!r.sharedMaterial||mf.sharedMesh.subMeshCount!=1||r.HasPropertyBlock())throw new InvalidOperationException("Unsupported mesh/material/property block: "+Hierarchy(r.transform));
                if(Kind(r)=="static"&&r.GetComponents<Component>().Any(c=>c&&!(c is Transform)&&!(c is MeshFilter)&&!(c is MeshRenderer)&&!(c is Collider)))throw new InvalidOperationException("Behaviour needs separate handling: "+Hierarchy(r.transform));
                if(r.transform.localToWorldMatrix.determinant<=0)throw new InvalidOperationException("Mirrored transform needs separate handling: "+r.name);
            }
            var result=new List<Group>();
            try
            {
                foreach(var set in rs.GroupBy(r=>AssetDatabase.GetAssetPath(r.sharedMaterial)+(Kind(r)=="static"?"":"|"+Hierarchy(r.transform))).OrderBy(g=>g.Key))
                {
                    var list=set.ToArray();var first=list[0];var kind=Kind(first);
                    if(list.Select(RenderSettings).Distinct().Count()!=1)throw new InvalidOperationException("Renderer settings differ for "+first.sharedMaterial.name);
                    var mesh=new Mesh {name="GI_"+first.sharedMaterial.name,indexFormat=IndexFormat.UInt32};
                    // Single objects keep their local coordinates (including deformation space).
                    if(list.Length==1) { UnityEngine.Object.DestroyImmediate(mesh);mesh=UnityEngine.Object.Instantiate(first.GetComponent<MeshFilter>().sharedMesh);mesh.name="GI_"+first.sharedMaterial.name; }
                    else mesh.CombineMeshes(list.Select(r=>new CombineInstance {mesh=r.GetComponent<MeshFilter>().sharedMesh,subMeshIndex=0,transform=root.transform.worldToLocalMatrix*r.transform.localToWorldMatrix}).ToArray(),true,true,false);
                    var g=new Group{name=mesh.name,kind=kind,mat=first.sharedMaterial,renderers=list,mesh=mesh};result.Add(g);
                    VerifyCombined(g,root.transform);
                    float area=0;var p=mesh.vertices;var t=mesh.triangles;for(int i=0;i<t.Length;i+=3)area+=Vector3.Cross(p[t[i+1]]-p[t[i]],p[t[i+2]]-p[t[i]]).magnitude*.5f;
                    var so=new SerializedObject(first);
                    g.data=new Item {name=g.name,material=AssetDatabase.GetAssetPath(g.mat),kind=kind,sources=list.Select(r=>Hierarchy(r.transform)).ToArray(),positions=p.SelectMany(v=>new[]{v.x,v.y,v.z}).ToArray(),uv0=mesh.uv.SelectMany(v=>new[]{v.x,v.y}).ToArray(),triangles=t,surfaceArea=area,scaleInLightmap=so.FindProperty("m_ScaleInLightmap").floatValue};
                    // Hash all render attributes as well as transform/material/source identity.
                    g.data.hash=Digest(g.data)+":"+Attributes(mesh);
                }
                return result;
            }
            catch {foreach(var g in result)UnityEngine.Object.DestroyImmediate(g.mesh);throw;}
        }
        static string Digest(object o) {using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(o))));}
        static void VerifyCombined(Group g,Transform root)
        {
            var positions=g.mesh.vertices;var uv=g.mesh.uv;var indices=g.mesh.triangles;int corner=0;
            foreach(var r in g.renderers)
            {
                var src=r.GetComponent<MeshFilter>().sharedMesh;var p=src.vertices;var u=src.uv;
                var matrix=g.renderers.Length==1?Matrix4x4.identity:root.worldToLocalMatrix*r.transform.localToWorldMatrix;
                foreach(int index in src.triangles)
                {
                    int actual=indices[corner++];
                    if((positions[actual]-matrix.MultiplyPoint3x4(p[index])).sqrMagnitude>1e-10f||uv[actual]!=u[index])throw new InvalidOperationException("Combined geometry or UV0 mismatch: "+r.name);
                }
            }
            if(corner!=indices.Length)throw new InvalidOperationException("Combined triangle count mismatch.");
        }
        static string Attributes(Mesh m)
        {
            using var stream=new MemoryStream();using var w=new BinaryWriter(stream);
            var p=m.vertices;var n=m.normals;var uv=m.uv;var c=m.colors;var tang=m.tangents;
            foreach(int i in m.triangles)
            {
                w.Write(p[i].x);w.Write(p[i].y);w.Write(p[i].z);w.Write(uv[i].x);w.Write(uv[i].y);
                if(n.Length>0){w.Write(n[i].x);w.Write(n[i].y);w.Write(n[i].z);}
                if(c.Length>0){w.Write(c[i].r);w.Write(c[i].g);w.Write(c[i].b);w.Write(c[i].a);}
                if(tang.Length>0){w.Write(tang[i].x);w.Write(tang[i].y);w.Write(tang[i].z);w.Write(tang[i].w);}
            }
            w.Flush();using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(stream.ToArray()));
        }
        [MenuItem("Tools/Harbor/Material GI/1 Export UV0 Groups")]
        public static void Export()
        {
            var groups=Build();try
            {
                var settings=Lightmapping.lightingSettings;
                var input=new Input {scene=SceneManager.GetActiveScene().path,atlasSize=settings?settings.lightmapMaxSize:1024,lightmapResolution=settings?settings.lightmapResolution:20};
                input.groups=groups.Select(g=>g.data).ToList();Directory.CreateDirectory(Work);File.WriteAllText(Work+"/input.json",JsonUtility.ToJson(input));
                Debug.Log("MATERIAL_GI_EXPORT: "+groups.Count+" groups, "+groups.Sum(g=>g.renderers.Length)+" renderers");
            }finally {foreach(var g in groups)UnityEngine.Object.DestroyImmediate(g.mesh);}
        }
        [MenuItem("Tools/Harbor/Material GI/2 Apply Validated UV1")]
        public static void Apply()
        {
            var cache=JsonUtility.FromJson<Cache>(File.ReadAllText(Work+"/uv1.json"));var groups=Build();var assets=new List<string>();int undo=-1;
            try
            {
                if(cache.groups.Count!=groups.Count)throw new InvalidDataException("Cache group count mismatch.");
                foreach(var g in groups)
                {
                    var entry=cache.groups.Single(x=>x.name==g.name);
                    if(entry.hash!=g.data.hash||!entry.validated||entry.overlaps!=0||entry.collapsed!=0||entry.uv1.Length!=g.mesh.triangles.Length*2)throw new InvalidDataException("Invalid/stale UV cache: "+g.name);
                    var uv=Enumerable.Range(0,entry.uv1.Length/2).Select(i=>new Vector2(entry.uv1[i*2],entry.uv1[i*2+1])).ToArray();
                    if(uv.Any(v=>float.IsNaN(v.x)||float.IsNaN(v.y)||v.x<0||v.x>1||v.y<0||v.y>1))throw new InvalidDataException("Invalid UV coordinates: "+g.name);
                    string before=Attributes(g.mesh);var bounds=g.mesh.bounds;MeshUtility.SetPerTriangleUV2(g.mesh,uv);g.mesh.bounds=bounds;
                    if(before!=Attributes(g.mesh))throw new InvalidOperationException("Vertex attributes changed: "+g.name);
                }
                Directory.CreateDirectory(Work);var scene=SceneManager.GetActiveScene();string backup=Work+"/SingleBattle_before_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity";
                if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Cannot back up current unsaved scene.");
                Directory.CreateDirectory(AssetsRoot);AssetDatabase.Refresh();
                Undo.IncrementCurrentGroup();undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Merge Harbor GI by Material and rebuild UV1");
                var parent=new GameObject("GI_Merged");Undo.RegisterCreatedObjectUndo(parent,"GI merged root");parent.transform.SetParent(GameObject.Find("Harbor_Environment").transform,false);
                foreach(var g in groups)
                {
                    var path=AssetsRoot+"/"+g.name+".asset";if(File.Exists(path))throw new IOException("Refusing to overwrite existing mesh: "+path);
                    AssetDatabase.CreateAsset(g.mesh,path);assets.Add(path);
                    if(g.renderers.Length==1)
                    {
                        var r=g.renderers[0];var mf=r.GetComponent<MeshFilter>();Undo.RecordObject(mf,"Assign rebuilt GI mesh");mf.sharedMesh=g.mesh;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(mf);
                        Undo.RecordObject(r,"Invalidate stale lightmap binding");r.lightmapIndex=-1;r.lightmapScaleOffset=new Vector4(1,1,0,0);PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                    }
                    else
                    {
                        var source=g.renderers[0];var go=new GameObject(g.name);Undo.RegisterCreatedObjectUndo(go,"Create merged GI renderer");go.transform.SetParent(parent.transform,false);go.layer=source.gameObject.layer;
                        go.AddComponent<MeshFilter>().sharedMesh=g.mesh;var renderer=go.AddComponent<MeshRenderer>();EditorUtility.CopySerialized(source,renderer);renderer.lightmapIndex=-1;renderer.lightmapScaleOffset=new Vector4(1,1,0,0);
                        GameObjectUtility.SetStaticEditorFlags(go,GameObjectUtility.GetStaticEditorFlags(source.gameObject));
                        foreach(var r in g.renderers)
                        {
                            Undo.RecordObject(r,"Disable merged source rendering");r.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                            Undo.RecordObject(r.gameObject,"Remove merged source GI participation");GameObjectUtility.SetStaticEditorFlags(r.gameObject,GameObjectUtility.GetStaticEditorFlags(r.gameObject)&~StaticEditorFlags.ContributeGI);PrefabUtility.RecordPrefabInstancePropertyModifications(r.gameObject);
                        }
                    }
                }
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save merged scene.");
                Undo.CollapseUndoOperations(undo);
                File.WriteAllText(Work+"/applied.json",JsonUtility.ToJson(new Applied {backup=backup,sourceRenderers=groups.Sum(g=>g.renderers.Length),activeRenderers=groups.Count,attributesPreserved=true,assets=assets},true));
                Debug.Log("MATERIAL_GI_APPLIED: "+groups.Sum(g=>g.renderers.Length)+" -> "+groups.Count+" renderers. Rebake required.");
            }
            catch {if(undo>=0)Undo.RevertAllDownToGroup(undo);foreach(var p in assets)AssetDatabase.DeleteAsset(p);throw;}
            finally {foreach(var g in groups)if(g.mesh&&!AssetDatabase.Contains(g.mesh))UnityEngine.Object.DestroyImmediate(g.mesh);}
        }
        [Serializable] public class Verification { public string name,material; public string attributes; public float[] positions,uv0,uv1; public int[] triangles; public int lightmapIndex; }
        [Serializable] public class VerificationReport { public List<Verification> groups=new List<Verification>(); public int activeContributors,disabledMergedSources; }
        [MenuItem("Tools/Harbor/Material GI/3 Verify Applied Meshes")]
        public static void Verify()
        {
            var root=GameObject.Find("Harbor_Environment");var report=new VerificationReport();
            foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(!r.enabled){report.disabledMergedSources++;continue;}
                if((GameObjectUtility.GetStaticEditorFlags(r.gameObject)&StaticEditorFlags.ContributeGI)==0)continue;
                report.activeContributors++;var m=r.GetComponent<MeshFilter>().sharedMesh;
                if(!AssetDatabase.GetAssetPath(m).StartsWith(AssetsRoot+"/"))throw new InvalidOperationException("Contributor still uses old mesh: "+r.name);
                report.groups.Add(new Verification {name=m.name,material=AssetDatabase.GetAssetPath(r.sharedMaterial),attributes=Attributes(m),positions=m.vertices.SelectMany(v=>new[]{v.x,v.y,v.z}).ToArray(),uv0=m.uv.SelectMany(v=>new[]{v.x,v.y}).ToArray(),uv1=m.uv2.SelectMany(v=>new[]{v.x,v.y}).ToArray(),triangles=m.triangles,lightmapIndex=r.lightmapIndex});
            }
            File.WriteAllText(Work+"/verification.json",JsonUtility.ToJson(report));Debug.Log("MATERIAL_GI_VERIFIED: "+report.activeContributors+" contributors");
        }
        static string PrefabStructure(GameObject root)
        {
            var lines=new List<string>();
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
            {
                string path=AnimationUtility.CalculateTransformPath(t,root.transform);
                lines.Add(path+"|"+t.gameObject.activeSelf+"|"+t.gameObject.layer+"|"+(int)GameObjectUtility.GetStaticEditorFlags(t.gameObject)+"|"+string.Join(",",t.GetComponents<Component>().Select(c=>c?c.GetType().FullName:"MISSING")));
                if(t!=root.transform)lines.Add(path+"|"+JsonUtility.ToJson(t.localPosition)+"|"+JsonUtility.ToJson(t.localRotation)+"|"+JsonUtility.ToJson(t.localScale));
                var mf=t.GetComponent<MeshFilter>();if(mf)lines.Add(path+"|mesh="+AssetDatabase.GetAssetPath(mf.sharedMesh));
                var r=t.GetComponent<MeshRenderer>();if(r)lines.Add(path+"|renderer="+r.enabled+"|"+RenderSettings(r)+"|"+string.Join(",",r.sharedMaterials.Select(m=>AssetDatabase.GetAssetPath(m))));
            }
            return string.Join("\n",lines.OrderBy(x=>x));
        }
        [MenuItem("Tools/Harbor/Material GI/4 Update Harbor Prefab")]
        public static void UpdatePrefab()
        {
            const string path="Assets/Art/Environment/Harbor/Prefabs/Harbor_Environment.prefab";
            if(EditorApplication.isPlayingOrWillChangePlaymode||Lightmapping.isRunning)throw new InvalidOperationException("Wait for play mode/baking to finish.");
            var root=GameObject.Find("Harbor_Environment");
            if(!root||!root.transform.Find("GI_Merged"))throw new InvalidOperationException("Merged Harbor scene instance not found.");
            if(!PrefabUtility.IsAnyPrefabInstanceRoot(root)||PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root)!=path)throw new InvalidOperationException("Scene root is not an instance of the expected Harbor prefab.");
            int contributors=root.GetComponentsInChildren<MeshRenderer>(true).Count(r=>r.enabled&&r.gameObject.activeInHierarchy&&(GameObjectUtility.GetStaticEditorFlags(r.gameObject)&StaticEditorFlags.ContributeGI)!=0);
            if(contributors!=18)throw new InvalidOperationException("Unexpected GI contributor count: "+contributors);
            string expected=PrefabStructure(root),guid=AssetDatabase.AssetPathToGUID(path);
            Directory.CreateDirectory(Work);string backup=Work+"/Harbor_Environment_before_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".prefab";File.Copy(path,backup);
            PrefabUtility.ApplyPrefabInstance(root,InteractionMode.UserAction);
            AssetDatabase.SaveAssets();var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(guid!=AssetDatabase.AssetPathToGUID(path)||expected!=PrefabStructure(asset)||expected!=PrefabStructure(root))throw new InvalidOperationException("Prefab structure verification failed; original backup: "+backup);
            if(!EditorSceneManager.SaveScene(root.scene))throw new IOException("Prefab updated but scene save failed.");
            File.WriteAllText(Work+"/prefab-update.txt","PASS\nPrefab: "+path+"\nGUID preserved: "+guid+"\nActive GI renderers: "+contributors+"\nHierarchy, transforms, mesh/material references, components and renderer settings match.\nBackup: "+backup);
            Debug.Log("MATERIAL_GI_PREFAB_UPDATED: "+path+"; verified "+contributors+" contributors");
        }
        [MenuItem("Tools/Harbor/Material GI/5 Inspect Current Bake")]
        public static void InspectBake()
        {
            var lines=new List<string>();lines.Add("Baking="+Lightmapping.isRunning);
            var r=GameObject.Find("SM_Harbor_Tavern_001").GetComponent<MeshRenderer>();var m=r.GetComponent<MeshFilter>().sharedMesh;
            lines.Add("Mesh="+AssetDatabase.GetAssetPath(m));lines.Add("Attributes="+Attributes(m));
            lines.Add("ScaleInLightmap="+r.scaleInLightmap+" Index="+r.lightmapIndex+" ST="+r.lightmapScaleOffset);
            foreach(var lm in LightmapSettings.lightmaps)lines.Add("Lightmap="+(lm.lightmapColor?AssetDatabase.GetAssetPath(lm.lightmapColor)+" "+lm.lightmapColor.width+"x"+lm.lightmapColor.height:"NONE"));
            var methods=typeof(Lightmapping).GetMethods(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Where(x=>x.Name.ToLower().Contains("overlap"));
            foreach(var method in methods)lines.Add("API="+method);
            File.WriteAllLines(Work+"/current-bake.txt",lines);
        }
        [MenuItem("Tools/Harbor/Material GI/6 Remove Replaced Wreck Nodes")]
        public static void RemoveReplacedWreckNodes()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||Lightmapping.isRunning)throw new InvalidOperationException("Wait for play mode/baking to finish.");
            var root=GameObject.Find("Harbor_Environment");if(!root)throw new InvalidOperationException("Harbor root missing.");
            var merged=root.transform.Find("GI_Merged/GI_M_Harbor_Wooden_Dock_Wreck_Material_001");
            if(!merged||!merged.GetComponent<MeshRenderer>().enabled)throw new InvalidOperationException("Active merged wreck missing.");
            var sources=root.GetComponentsInChildren<Transform>(true).Where(t=>System.Text.RegularExpressions.Regex.IsMatch(t.name,@"^SM_Harbor_Wooden_Dock_Wreck_\d+$")).ToArray();
            if(sources.Length!=119)throw new InvalidOperationException("Expected 119 source nodes, got "+sources.Length);
            foreach(var t in sources)
            {
                var r=t.GetComponent<MeshRenderer>();
                if(t.childCount!=0||!r||r.enabled||(GameObjectUtility.GetStaticEditorFlags(t.gameObject)&StaticEditorFlags.ContributeGI)!=0||t.GetComponents<Component>().Any(c=>!c||(!(c is Transform)&&!(c is MeshFilter)&&!(c is MeshRenderer))))throw new InvalidOperationException("Unsafe source node: "+t.name);
            }
            Directory.CreateDirectory(Work);var backup=Work+"/SingleBattle_before_wreck_cleanup_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity";
            if(!EditorSceneManager.SaveScene(root.scene,backup,true))throw new IOException("Scene backup failed.");
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Remove replaced wreck source nodes");
            foreach(var t in sources)Undo.DestroyObjectImmediate(t.gameObject);
            Undo.CollapseUndoOperations(group);
            UpdatePrefab();
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Environment/Harbor/Prefabs/Harbor_Environment.prefab");
            if(root.GetComponentsInChildren<Transform>(true).Any(t=>t.name.StartsWith("SM_Harbor_Wooden_Dock_Wreck_"))||asset.GetComponentsInChildren<Transform>(true).Any(t=>t.name.StartsWith("SM_Harbor_Wooden_Dock_Wreck_")))throw new InvalidOperationException("Source nodes still exist.");
            File.WriteAllText(Work+"/wreck-cleanup.txt","PASS\nRemoved: "+sources.Length+"\nMerged renderer, source mesh assets and 18 active GI renderers preserved.\nPrefab synchronized.\nBackup: "+backup);
        }

        [MenuItem("Tools/Harbor/Material GI/Stop Current Bake")]
        public static void StopCurrentBake() { if(Lightmapping.isRunning)Lightmapping.Cancel(); }

    }
}
