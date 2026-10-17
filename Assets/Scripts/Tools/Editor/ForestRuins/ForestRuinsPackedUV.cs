using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace ForestRuins.Editor
{
    // Editor-only UV1 cache. Imported geometry, UV0 and shading attributes are retained.
    public static class ForestRuinsPackedUV
    {
        [Serializable] class Cache { public string sourceHash; public MeshUV[] meshes; }
        [Serializable] class MeshUV { public string name; public Vector3[] positions; public Vector2[] uv; }
        public static string CachePath(string assetPath) => ForestRuinsAssetImport.Folder + "/Editor/LightmapUV/" + Path.GetFileNameWithoutExtension(assetPath) + ".json";
        public static bool HasCache(string assetPath) => File.Exists(CachePath(assetPath));
        public static void Apply(string assetPath, GameObject root)
        {
            string path=CachePath(assetPath);
            if(!File.Exists(path)) return;
            var cache=JsonUtility.FromJson<Cache>(File.ReadAllText(path));
            using(var sha=SHA256.Create())
                if(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(assetPath))).Replace("-","").ToLowerInvariant()!=cache.sourceHash)
                    throw new InvalidOperationException("Forest UV1 cache is stale; regenerate after changing geometry: "+assetPath);
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var data=cache.meshes.FirstOrDefault(m=>m.name==filter.name);
                if(data==null) throw new InvalidOperationException("Missing lightmap UV1 cache: "+filter.name);
                var mesh=filter.sharedMesh;var positions=mesh.vertices;var triangles=mesh.triangles;
                if(data.positions.Length!=triangles.Length || data.uv.Length!=triangles.Length)
                    throw new InvalidOperationException("Lightmap UV1 triangle count changed: "+filter.name);
                var byTriangle=new Dictionary<string,Queue<int>>();
                for(int i=0;i<data.positions.Length;i+=3)
                {
                    string key=Key(data.positions[i],data.positions[i+1],data.positions[i+2]);
                    if(!byTriangle.TryGetValue(key,out var q))byTriangle[key]=q=new Queue<int>();
                    q.Enqueue(i);
                }
                var uv=new Vector2[triangles.Length];
                for(int i=0;i<triangles.Length;i+=3)
                {
                    bool found=false;
                    for(int shift=0;shift<3;shift++)
                    {
                        string key=Key(positions[triangles[i+shift]],positions[triangles[i+(shift+1)%3]],positions[triangles[i+(shift+2)%3]]);
                        if(!byTriangle.TryGetValue(key,out var q)||q.Count==0)continue;
                        int source=q.Dequeue();for(int j=0;j<3;j++)uv[i+(j+shift)%3]=data.uv[source+j];
                        found=true;break;
                    }
                    if(!found)throw new InvalidOperationException("Lightmap UV1 correspondence changed: "+filter.name+" triangle "+i/3);
                }
                if(!MeshUtility.SetPerTriangleUV2(mesh,uv))throw new InvalidOperationException("Could not apply UV1: "+filter.name);
            }
        }
        static string Point(Vector3 v) => Mathf.RoundToInt(v.x*100000)+","+Mathf.RoundToInt(v.y*100000)+","+Mathf.RoundToInt(v.z*100000);
        static string Key(Vector3 a,Vector3 b,Vector3 c) => Point(a)+";"+Point(b)+";"+Point(c);
    }
}
