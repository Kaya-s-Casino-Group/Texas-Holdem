using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ForestRuins.Editor
{
    public static class ForestRuinsLightmapUV
    {
        [Serializable] public class Entry
        {
            public string path, name, kind;
            public int vertices;
            public bool autoUV;
            public Vector2[] uv0, uv1;
            public Vector3[] positions;
            public int[] triangles;
        }
        [Serializable] public class Report { public List<Entry> meshes = new List<Entry>(); public List<string> sceneMeshes = new List<string>(); }
        [Serializable] public class Allocation
        {
            public string name, path, mesh, kind;
            public bool active, contribute;
            public int index;
            public float scale, area, uvArea;
            public string receiveGI;
            public Vector4 atlas;
            public Vector3 worldScale;
        }
        [Serializable] public class PackingReport
        {
            public string scene, lightingSettings;
            public bool baking;
            public List<string> maps = new List<string>();
            public List<Allocation> renderers = new List<Allocation>();
        }
        [MenuItem("工具/场景美术/森林遗迹/优化光照贴图分配并烘焙")]
        public static void OptimizeAllocation()
        {
            if (Lightmapping.isRunning) throw new InvalidOperationException("A lighting bake is already running.");
            var scene=SceneManager.GetActiveScene();
            if(string.IsNullOrEmpty(scene.path)) throw new InvalidOperationException("Save the forest scene before configuring its lighting.");
            var root=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Transform>(true))
                .Select(t=>t.gameObject).FirstOrDefault(o=>o.name=="PF_FR_EnvironmentLayout");
            if(!root) throw new InvalidOperationException("Forest environment not found.");
            var manifest=ForestRuinsAssetImport.ReadManifest();
            Undo.IncrementCurrentGroup();Undo.SetCurrentGroupName("Optimize forest lightmap allocation");
            foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var spec=manifest.instances.FirstOrDefault(i=>i.name==r.name);
                if(spec==null) continue;
                if(spec.kind=="soil") {Undo.RecordObject(r,"Reduce hidden soil texels");r.scaleInLightmap=.15f;}
                else if(r.name=="SM_FR_Courtyard_MainCourtyard") {Undo.RecordObject(r,"Prioritize courtyard texels");r.scaleInLightmap=1.25f;}
                else if(r.name=="SM_FR_Arch_MainArch") {Undo.RecordObject(r,"Prioritize arch texels");r.scaleInLightmap=1.5f;}
                else if(r.name.StartsWith("SM_FR_RockFlat_Gap") || r.name.StartsWith("SM_FR_RockFlat_Small") || r.name.StartsWith("SM_FR_RockSplit_Small"))
                { Undo.RecordObject(r,"Lightmap detail for small rocks");r.scaleInLightmap=2; }
                else if(r.name.StartsWith("SM_FR_RootCurved"))
                { Undo.RecordObject(r,"Lightmap detail for roots");r.scaleInLightmap=1.5f; }
                else if(spec.kind=="backdrop" || spec.kind=="cutout")
                {
                    Undo.RecordObject(r.gameObject,"Exclude background from GI");
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject,GameObjectUtility.GetStaticEditorFlags(r.gameObject)&~StaticEditorFlags.ContributeGI);
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                PrefabUtility.RecordPrefabInstancePropertyModifications(r.gameObject);
            }
            const string folder="Assets/Settings/Lighting";
            if(!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Settings","Lighting");
            string path=folder+"/LS_"+scene.name+".lighting";
            var settings=AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
            if(!settings)
            {
                settings=Lightmapping.lightingSettings ? UnityEngine.Object.Instantiate(Lightmapping.lightingSettings) : new LightingSettings();
                settings.name="LS_"+scene.name;AssetDatabase.CreateAsset(settings,path);
            }
            Undo.RecordObject(settings,"Set compact forest atlas");settings.lightmapMaxSize=512;settings.lightmapResolution=44;
            EditorUtility.SetDirty(settings);Lightmapping.lightingSettings=settings;
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            AssetDatabase.ImportAsset(manifest.assemblyPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            Audit();
            if(!Lightmapping.BakeAsync()) throw new InvalidOperationException("Unity could not start the lighting bake.");
            Debug.Log("Forest lightmap bake started: 512 atlas, resolution 44, hidden soil .15, courtyard 1.25, arch and roots 1.5, small rocks 2. Lighting samples unchanged.");
        }
        [MenuItem("工具/场景美术/森林遗迹/检查光照贴图分配")]
        public static void AuditAllocation()
        {
            var scene = SceneManager.GetActiveScene();
            var manifest = ForestRuinsAssetImport.ReadManifest();
            var report = new PackingReport { scene=scene.path, baking=Lightmapping.isRunning,
                lightingSettings=Lightmapping.lightingSettings ? EditorJsonUtility.ToJson(Lightmapping.lightingSettings) : "none" };
            foreach (var map in LightmapSettings.lightmaps)
                report.maps.Add(map.lightmapColor ? AssetDatabase.GetAssetPath(map.lightmapColor) + " | " + map.lightmapColor.width + "x" + map.lightmapColor.height : "null");
            foreach (var root in scene.GetRootGameObjects())
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter = r.GetComponent<MeshFilter>();
                if (!filter || !filter.sharedMesh) continue;
                var mesh=filter.sharedMesh;
                var record=manifest.instances.FirstOrDefault(i=>i.name==r.name);
                var v=mesh.vertices;var t=mesh.triangles;var uv=mesh.uv2;float area=0,ua=0;
                for (int i=0;i<t.Length;i+=3)
                {
                    Vector3 a=r.localToWorldMatrix.MultiplyPoint3x4(v[t[i]]), b=r.localToWorldMatrix.MultiplyPoint3x4(v[t[i+1]]), c=r.localToWorldMatrix.MultiplyPoint3x4(v[t[i+2]]);
                    area+=Vector3.Cross(b-a,c-a).magnitude*.5f;
                    if(uv.Length==v.Length) {var u=uv[t[i+1]]-uv[t[i]];var w=uv[t[i+2]]-uv[t[i]];ua+=Mathf.Abs(u.x*w.y-u.y*w.x)*.5f;}
                }
                report.renderers.Add(new Allocation { name=r.name, path=AssetDatabase.GetAssetPath(mesh), mesh=mesh.name,
                    kind=record?.kind, active=r.gameObject.activeInHierarchy && r.enabled,
                    contribute=(GameObjectUtility.GetStaticEditorFlags(r.gameObject)&StaticEditorFlags.ContributeGI)!=0,
                    index=r.lightmapIndex, scale=r.scaleInLightmap, area=area, uvArea=ua,
                    receiveGI=r.receiveGI.ToString(),atlas=r.lightmapScaleOffset, worldScale=r.transform.lossyScale });
            }
            Directory.CreateDirectory("Library/ForestRuinsLightmapUV");
            File.WriteAllText("Library/ForestRuinsLightmapUV/allocation.json",JsonUtility.ToJson(report,true));
            Debug.Log("Forest Ruins lightmap allocation audit saved.");
        }
        [MenuItem("工具/场景美术/森林遗迹/生成光照贴图 UV1")]
        public static void Generate()
        {
            var scene = SceneManager.GetActiveScene();
            bool wasDirty = scene.isDirty;
            var manifest = ForestRuinsAssetImport.ReadManifest();
            var paths = manifest.models.Where(m => m.kind != "backdrop" && m.kind != "cutout")
                .Select(m => m.path).Concat(new[] { manifest.assemblyPath }).ToArray();
            foreach (string path in paths)
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) throw new InvalidOperationException("Missing importer: " + path);
                importer.generateSecondaryUV = !ForestRuinsPackedUV.HasCache(path);
                // Tighter chart seams remove the small fold in the standalone stone tile.
                if (path.EndsWith("/SM_FR_StoneTile.fbx", StringComparison.Ordinal))
                {
                    importer.secondaryUVHardAngle = 60;
                    importer.secondaryUVPackMargin = 8;
                }
                AssetDatabase.WriteImportSettingsIfDirty(path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            }
            Audit();
            if (SceneManager.GetActiveScene().handle != scene.handle || scene.isDirty != wasDirty)
                throw new InvalidOperationException("Unexpected scene change during UV import.");
            Debug.Log("Forest Ruins: refreshed lightmap UV1 for 27 environment models and the layout; packed UV caches are retained when present. Standalone background imports and VAT assets were left untouched. No lighting bake was started.");
        }
        [MenuItem("工具/场景美术/森林遗迹/检查光照贴图 UV1")]
        public static void Audit()
        {
            var manifest = ForestRuinsAssetImport.ReadManifest();
            var report = new Report();
            foreach (var path in manifest.models.Select(m => m.path).Concat(new[] { manifest.assemblyPath }))
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                foreach (var f in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    var m = f.sharedMesh;
                    var kind = path == manifest.assemblyPath ? manifest.instances.FirstOrDefault(i => i.name == f.name)?.kind : manifest.models.First(i => i.path == path).kind;
                    report.meshes.Add(new Entry {path=path, name=f.name, kind=kind, vertices=m.vertexCount, autoUV=importer.generateSecondaryUV, uv0=m.uv, uv1=m.uv2, positions=m.vertices, triangles=m.triangles});
                }
            }
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (var f in root.GetComponentsInChildren<MeshFilter>(true))
                    if (f.sharedMesh) report.sceneMeshes.Add(f.name + " | " + AssetDatabase.GetAssetPath(f.sharedMesh) + " | UV1=" + f.sharedMesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord1) + " | active=" + f.gameObject.activeInHierarchy);
            Directory.CreateDirectory("Library/ForestRuinsLightmapUV");
            File.WriteAllText("Library/ForestRuinsLightmapUV/lightmap_uv_audit.json", JsonUtility.ToJson(report));
            Debug.Log("Forest Ruins UV1 audit: " + report.meshes.Count + " meshes inspected; scene unchanged.");
        }
    }
}
