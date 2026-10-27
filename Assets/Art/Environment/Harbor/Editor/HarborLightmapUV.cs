using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Security.Cryptography;

namespace HarborEnvironment.Editor
{
    public static class HarborLightmapUV
    {
        const string Output = "output/harbor-lightmap-uv";
        const float Margin = 4f / 1024f;
        sealed class Layout { public string source; public float[] edges; public Vector2[] uv; }
        static readonly Dictionary<string,List<Layout>> Layouts = new Dictionary<string,List<Layout>>();
        static Queue<Mesh> pending;
        static FixReport fixing;
        [Serializable] public class FixedMesh
        {
            public string mesh, sharedLayout;
            public int triangles, overlaps, collapsedUVTriangles, zeroAreaGeometry;
            public bool uv0AndGeometryPreserved;
        }
        [Serializable] public class FixReport
        {
            public string status;
            public int total, uniqueLayouts;
            public List<FixedMesh> meshes = new List<FixedMesh>();
        }
        public static void ResetLayoutCache() => Layouts.Clear();

        // A signature of triangle-corner UV0 and submesh boundaries. Index splits do
        // not change the signature. Geometry edge ratios additionally reject models
        // that merely share an atlas, while allowing translated/rotated/scaled copies.
        static string UV0Signature(Mesh mesh)
        {
            using var bytes=new MemoryStream();using var writer=new BinaryWriter(bytes);
            var uv=mesh.uv;writer.Write(mesh.subMeshCount);
            for(int s=0;s<mesh.subMeshCount;s++)
            {
                var indices=mesh.GetTriangles(s);writer.Write(indices.Length);
                foreach(int i in indices){writer.Write(uv[i].x);writer.Write(uv[i].y);}
            }
            writer.Flush();using var hash=SHA256.Create();return Convert.ToBase64String(hash.ComputeHash(bytes.ToArray()));
        }
        static float[] EdgeRatios(Mesh mesh)
        {
            var p=mesh.vertices;var t=mesh.triangles;var edges=new float[t.Length];float max=0;
            for(int i=0;i<t.Length;i+=3)for(int j=0;j<3;j++)
            {float length=(p[t[i+j]]-p[t[i+(j+1)%3]]).magnitude;edges[i+j]=length;max=Mathf.Max(max,length);}
            if(max>0)for(int i=0;i<edges.Length;i++)edges[i]/=max;
            return edges;
        }
        static bool SameGeometry(float[] a,float[] b)
        {
            if(a.Length!=b.Length)return false;
            for(int i=0;i<a.Length;i++)if(Mathf.Abs(a[i]-b[i])>0.0001f)return false;
            return true;
        }
        static Vector2[] Unwrap(Mesh mesh, int quality = 0)
        {
            var p=mesh.vertices;var t=mesh.triangles;
            var actualBounds=new Bounds(p[0],Vector3.zero);foreach(var vertex in p)actualBounds.Encapsulate(vertex);
            var size=actualBounds.size;
            int thin=size.x<size.y?(size.x<size.z?0:2):(size.y<size.z?1:2);
            int x=(thin+1)%3,y=(thin+2)%3;float span=Mathf.Max(size[x],size[y]);
            // Exactly planar meshes (including the dense Gerstner rest grid) need
            // one chart. Uniform scaling preserves their actual aspect ratio.
            if(size[thin]<Mathf.Max(span,1)*0.000001f && span>0)
            {
                var result=new Vector2[t.Length];var min=actualBounds.min;
                for(int i=0;i<t.Length;i++)result[i]=new Vector2((p[t[i]][x]-min[x])/span,(p[t[i]][y]-min[y])/span)*(1-2*Margin)+Vector2.one*Margin;
                return result;
            }
            UnwrapParam.SetDefaults(out var param);param.hardAngle=quality==0?60:quality==1?30:quality==2?10:0;
            param.angleError=quality==0?.08f:.01f;param.areaError=quality==0?.15f:.01f;param.packMargin=Margin;
            return Unwrapping.GeneratePerTriangleUV(mesh,param);
        }
        public static FixedMesh Generate(Mesh mesh)
        {
            string signature=UV0Signature(mesh);var edges=EdgeRatios(mesh);
            if(!Layouts.TryGetValue(signature,out var candidates))Layouts[signature]=candidates=new List<Layout>();
            var layout=candidates.FirstOrDefault(c=>SameGeometry(edges,c.edges));
            if(layout==null)
            {
                var existing=mesh.uv2;
                bool reusable=existing.Length==mesh.vertexCount;
                if(reusable){var v=Validate(mesh);reusable=v.overlaps==0&&v.collapsedUVTriangles==0;}
                layout=new Layout {source=mesh.name,edges=edges,uv=reusable?mesh.triangles.Select(i=>existing[i]).ToArray():Unwrap(mesh)};
                RepairCollapsedCharts(mesh,layout.uv);candidates.Add(layout);
            }
            if(layout.uv==null || layout.uv.Length!=mesh.triangles.Length)throw new InvalidOperationException("Unwrap failed: "+mesh.name);
            var before=CornerData(mesh);var bounds=mesh.bounds;
            MeshUtility.SetPerTriangleUV2(mesh,layout.uv);mesh.bounds=bounds;
            var check=Validate(mesh);check.sharedLayout=layout.source;
            for(int quality=1;(check.overlaps>0||check.collapsedUVTriangles>0)&&quality<=3;quality++)
            {
                layout.uv=Unwrap(mesh,quality);RepairCollapsedCharts(mesh,layout.uv);MeshUtility.SetPerTriangleUV2(mesh,layout.uv);mesh.bounds=bounds;
                check=Validate(mesh);check.sharedLayout=layout.source;
            }
            check.uv0AndGeometryPreserved=before==CornerData(mesh);
            if(!check.uv0AndGeometryPreserved || check.overlaps!=0 || check.collapsedUVTriangles!=0)
                throw new InvalidOperationException("UV1 validation failed: "+JsonUtility.ToJson(check));
            return check;
        }
        static void RepairCollapsedCharts(Mesh mesh,Vector2[] uv)
        {
            // Scanned geometry can contain almost collinear faces that the native
            // packer collapses numerically. Isolate only those faces in a reserved
            // strip; do not change source geometry or fragment all the main charts.
            var p=mesh.vertices;var t=mesh.triangles;var bad=new List<int>();
            for(int i=0;i<t.Length;i+=3)
                if(Vector3.Cross(p[t[i+1]]-p[t[i]],p[t[i+2]]-p[t[i]]).sqrMagnitude>=1e-20f && Math.Abs(Cross(uv[i],uv[i+1],uv[i+2]))<1e-14)bad.Add(i);
            if(bad.Count==0)return;
            const float cell=.025f;const int columns=40;
            float strip=Mathf.Ceil(bad.Count/(float)columns)*cell+Margin;
            if(strip>.25f)throw new InvalidOperationException("Too many collapsed source triangles: "+mesh.name);
            for(int i=0;i<uv.Length;i++)uv[i]=new Vector2(uv[i].x,strip+uv[i].y*(1-strip));
            for(int n=0;n<bad.Count;n++)
            {
                int i=bad[n];var origin=new Vector2(n%columns*cell+Margin,n/columns*cell+Margin);
                float side=cell-2*Margin;
                uv[i]=origin;uv[i+1]=origin+Vector2.right*side;uv[i+2]=origin+Vector2.up*side;
            }
        }
        static string CornerData(Mesh mesh)
        {
            using var bytes=new MemoryStream();using var w=new BinaryWriter(bytes);
            var p=mesh.vertices;var n=mesh.normals;var uv=mesh.uv;var c=mesh.colors;var tangents=mesh.tangents;
            w.Write(mesh.subMeshCount);
            for(int s=0;s<mesh.subMeshCount;s++)
            {
                var indices=mesh.GetTriangles(s);w.Write(indices.Length);
                foreach(int i in indices)
                {
                    w.Write(p[i].x);w.Write(p[i].y);w.Write(p[i].z);w.Write(uv[i].x);w.Write(uv[i].y);
                    if(n.Length>0){w.Write(n[i].x);w.Write(n[i].y);w.Write(n[i].z);}
                    if(c.Length>0){w.Write(c[i].r);w.Write(c[i].g);w.Write(c[i].b);w.Write(c[i].a);}
                    if(tangents.Length>0){w.Write(tangents[i].x);w.Write(tangents[i].y);w.Write(tangents[i].z);w.Write(tangents[i].w);}
                }
            }
            w.Flush();using var hash=SHA256.Create();return Convert.ToBase64String(hash.ComputeHash(bytes.ToArray()));
        }
        static double Cross(Vector2 a,Vector2 b,Vector2 c) => (double)(b.x-a.x)*(c.y-a.y)-(double)(b.y-a.y)*(c.x-a.x);
        static bool Separated(Vector2[] uv,int[] t,int a,int b)
        {
            double sign=Cross(uv[t[a]],uv[t[a+1]],uv[t[a+2]])>=0?1:-1;
            for(int edge=0;edge<3;edge++)
            {
                var p=uv[t[a+edge]];var q=uv[t[a+(edge+1)%3]];
                if(Cross(p,q,uv[t[b]])*sign<=1e-10 && Cross(p,q,uv[t[b+1]])*sign<=1e-10 && Cross(p,q,uv[t[b+2]])*sign<=1e-10)return true;
            }
            return false;
        }
        static FixedMesh Validate(Mesh mesh)
        {
            var uv=mesh.uv2;var t=mesh.triangles;var p=mesh.vertices;
            if(uv.Length!=p.Length || uv.Any(v=>float.IsNaN(v.x)||float.IsNaN(v.y)||v.x<0||v.x>1||v.y<0||v.y>1))
                throw new InvalidOperationException("UV1 missing or out of bounds: "+mesh.name);
            var result=new FixedMesh {mesh=mesh.name,triangles=t.Length/3};
            var cells=new Dictionary<int,List<int>>();var seen=new HashSet<int>();
            for(int i=0;i<t.Length;i+=3)
            {
                if(Vector3.Cross(p[t[i+1]]-p[t[i]],p[t[i+2]]-p[t[i]]).sqrMagnitude<1e-20f){result.zeroAreaGeometry++;continue;}
                if(Math.Abs(Cross(uv[t[i]],uv[t[i+1]],uv[t[i+2]]))<1e-14){result.collapsedUVTriangles++;continue;}
                var min=Vector2.Min(uv[t[i]],Vector2.Min(uv[t[i+1]],uv[t[i+2]]));
                var max=Vector2.Max(uv[t[i]],Vector2.Max(uv[t[i+1]],uv[t[i+2]]));seen.Clear();
                for(int x=Mathf.FloorToInt(min.x*64);x<=Mathf.FloorToInt(max.x*64);x++)
                for(int y=Mathf.FloorToInt(min.y*64);y<=Mathf.FloorToInt(max.y*64);y++)
                {
                    int key=x+y*65;if(!cells.TryGetValue(key,out var list))cells[key]=list=new List<int>();
                    foreach(int other in list)if(seen.Add(other)&&!Separated(uv,t,i,other)&&!Separated(uv,t,other,i))result.overlaps++;
                    list.Add(i);
                }
            }
            return result;
        }
        [MenuItem("Tools/Harbor/Prepare Contribute GI UV1")]
        public static void Prepare()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before preparing saved mesh assets.");
            if(Lightmapping.isRunning)throw new InvalidOperationException("Wait for the current bake to finish.");
            if(pending!=null)throw new InvalidOperationException("UV1 generation already running.");
            Audit();Directory.CreateDirectory(Output);File.Copy(Output+"/audit.json",Output+"/before.json",true);
            var meshes=Contributors().Select(r=>r.GetComponent<MeshFilter>()?.sharedMesh).Where(m=>m).Distinct().OrderBy(m=>m.name).ToArray();
            if(meshes.Any(m=>!AssetDatabase.GetAssetPath(m).StartsWith("Assets/Art/Environment/Harbor/Meshes/") || !m.isReadable))
                throw new InvalidOperationException("Contributor mesh outside the supported generated Harbor assets; inspect audit first.");
            ResetLayoutCache();pending=new Queue<Mesh>(meshes);fixing=new FixReport {status="RUNNING",total=meshes.Length};
            WriteProgress();EditorApplication.update+=Step;
        }
        static void WriteProgress()
        {
            fixing.uniqueLayouts=Layouts.Values.Sum(x=>x.Count);
            File.WriteAllText(Output+"/result.json",JsonUtility.ToJson(fixing,true));
        }
        static void Step()
        {
            try
            {
                if(pending.Count==0)
                {
                    AssetDatabase.SaveAssets();Audit();EditorSceneManager.SaveOpenScenes();
                    fixing.status="SUCCESS";WriteProgress();EditorApplication.update-=Step;pending=null;
                    Debug.Log($"GI_UV_READY: {fixing.total} meshes / {fixing.uniqueLayouts} shared UV1 layouts");return;
                }
                var original=pending.Dequeue();var path=AssetDatabase.GetAssetPath(original);
                Directory.CreateDirectory(Output+"/backup");var backup=Output+"/backup/"+Path.GetFileName(path);
                if(!File.Exists(backup))File.Copy(path,backup);
                var copy=UnityEngine.Object.Instantiate(original);copy.name=original.name;
                try
                {
                    var result=Generate(copy);EditorUtility.CopySerialized(copy,original);EditorUtility.SetDirty(original);
                    fixing.meshes.Add(result);WriteProgress();
                }
                finally {UnityEngine.Object.DestroyImmediate(copy);}
            }
            catch(Exception e)
            {
                AssetDatabase.SaveAssets();fixing.status="FAILED: "+e.Message;WriteProgress();
                EditorApplication.update-=Step;pending=null;Debug.LogException(e);
            }
        }
        [Serializable] public class Entry
        {
            public string scene, hierarchy, mesh, asset, importer, shader;
            public int vertices, triangles, uv1Count, receiveGI;
            public bool active, readable, finiteInBounds, generateSecondaryUV;
        }
        [Serializable] public class Report { public bool baking; public List<Entry> objects = new List<Entry>(); }
        static string Hierarchy(Transform t) => t.parent ? Hierarchy(t.parent) + "/" + t.name : t.name;
        static IEnumerable<MeshRenderer> Contributors()
        {
            for (int i=0;i<SceneManager.sceneCount;i++)
            {
                var scene=SceneManager.GetSceneAt(i);
                if(!scene.isLoaded)continue;
                foreach(var root in scene.GetRootGameObjects())
                foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))
                    if((GameObjectUtility.GetStaticEditorFlags(r.gameObject)&StaticEditorFlags.ContributeGI)!=0)yield return r;
            }
        }
        [MenuItem("Tools/Harbor/Audit Contribute GI UV1")]
        public static void Audit()
        {
            var report=new Report { baking=Lightmapping.isRunning };
            foreach(var r in Contributors())
            {
                var mf=r.GetComponent<MeshFilter>();var m=mf?mf.sharedMesh:null;
                var path=m?AssetDatabase.GetAssetPath(m):"";
                var importer=AssetImporter.GetAtPath(path) as ModelImporter;
                var uv=new List<Vector2>();if(m)m.GetUVs(1,uv);
                report.objects.Add(new Entry {scene=r.gameObject.scene.path,hierarchy=Hierarchy(r.transform),mesh=m?m.name:"MISSING",asset=path,
                    importer=AssetImporter.GetAtPath(path)?.GetType().Name,shader=string.Join(";",r.sharedMaterials.Select(mat=>mat?mat.shader.name:"MISSING")),
                    vertices=m?m.vertexCount:0,triangles=m?m.triangles.Length/3:0,uv1Count=uv.Count,receiveGI=(int)r.receiveGI,
                    active=r.gameObject.activeInHierarchy&&r.enabled,readable=m&&m.isReadable,generateSecondaryUV=importer&&importer.generateSecondaryUV,
                    finiteInBounds=m&&uv.Count==m.vertexCount&&uv.All(v=>!float.IsNaN(v.x)&&!float.IsNaN(v.y)&&v.x>=0&&v.x<=1&&v.y>=0&&v.y<=1)});
            }
            Directory.CreateDirectory(Output);File.WriteAllText(Output+"/audit.json",JsonUtility.ToJson(report,true));
            Debug.Log($"GI_UV_AUDIT: {report.objects.Count} contributors, {report.objects.Count(e=>e.uv1Count==0)} without UV1. Baking: {report.baking}");
        }
    }
}
