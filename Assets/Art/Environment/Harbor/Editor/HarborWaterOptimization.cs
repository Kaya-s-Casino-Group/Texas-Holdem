using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HarborEnvironment
{
    public static class HarborWaterOptimization
    {
        const string MeshPath = "Assets/Art/Environment/Harbor/Meshes/HarborWater_Graded.asset";

        [MenuItem("Tools/Harbor/Optimize Current Water")]
        public static void Optimize()
        {
            var water = GameObject.Find("Harbor_Environment/Mesh/Water/SM_Harbor_Water_Surface_001");
            if (!water) throw new InvalidOperationException("Harbor water missing.");
            var filter = water.GetComponent<MeshFilter>();
            var original = filter.sharedMesh;
            // Use actual vertices rather than the bounds expanded for shader displacement.
            var bounds = new Bounds(original.vertices[0], Vector3.zero);
            foreach (var p in original.vertices) bounds.Encapsulate(p);
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            int previousStart = 0, previousSegments = 0;
            float worldDepth = bounds.size.z * Mathf.Abs(water.transform.lossyScale.z);
            float depth = 0;
            while (true)
            {
                int segments = depth < 12 ? 128 : depth < 32 ? 64 : 32;
                int start = vertices.Count;
                float z = Mathf.Lerp(bounds.min.z, bounds.max.z, depth / worldDepth);
                for (int x = 0; x <= segments; x++)
                    vertices.Add(new Vector3(Mathf.Lerp(bounds.min.x, bounds.max.x, (float)x / segments), bounds.center.y, z));
                if (start > 0)
                {
                    // Stitch different row densities without T-junctions or overlapping faces.
                    int a = 0, b = 0;
                    while (a < previousSegments || b < segments)
                    {
                        if (a < previousSegments && (b == segments || (float)(a + 1) / previousSegments <= (float)(b + 1) / segments))
                        { indices.Add(previousStart + a); indices.Add(start + b); indices.Add(previousStart + ++a); }
                        else
                        { indices.Add(previousStart + a); indices.Add(start + b); indices.Add(start + ++b); }
                    }
                }
                previousStart = start; previousSegments = segments;
                if (depth >= worldDepth) break;
                depth = Mathf.Min(worldDepth, depth + (depth < 12 ? .5f : depth < 32 ? 1f : 3f));
            }
            var mesh = new Mesh { name = "HarborWater_Graded" };
            mesh.SetVertices(vertices); mesh.SetTriangles(indices, 0); mesh.RecalculateNormals();
            mesh.RecalculateBounds(); var padded = mesh.bounds; padded.Expand(.5f); mesh.bounds = padded;
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (existing) { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; EditorUtility.SetDirty(mesh); }
            else AssetDatabase.CreateAsset(mesh, MeshPath);
            Undo.RecordObject(filter, "Optimize water mesh"); filter.sharedMesh = mesh;
            var reflection = water.GetComponent<HarborPlanarReflection>();
            if (reflection) Undo.DestroyObjectImmediate(reflection);
            PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(water.scene); EditorSceneManager.SaveScene(water.scene);
            Directory.CreateDirectory("output/harbor-water");
            File.WriteAllText("output/harbor-water/optimization.txt", $"Water triangles: {original.triangles.Length / 3} -> {indices.Count / 3}\nVertices: {original.vertexCount} -> {vertices.Count}\nPlanar reflection: removed; animated normals and environment sheen only.\n");
            Debug.Log("HARBOR_WATER_OPTIMIZED " + File.ReadAllText("output/harbor-water/optimization.txt"));
        }
        [MenuItem("Tools/Harbor/Use Simple Shimmer Water")]
        public static void UseSimpleShimmerWater()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
            var scene=EditorSceneManager.GetActiveScene();
            foreach(var root in scene.GetRootGameObjects())
                foreach(var reflection in root.GetComponentsInChildren<HarborPlanarReflection>(true))
                    Undo.DestroyObjectImmediate(reflection);
            // Remove the component from authored harbor prefabs too, so a fresh instance stays simple.
            foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Art/Environment/Harbor"}))
            {
                var path=AssetDatabase.GUIDToAssetPath(guid);
                var prefab=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var components=prefab.GetComponentsInChildren<HarborPlanarReflection>(true);
                    if(components.Length==0)continue;
                    foreach(var reflection in components)UnityEngine.Object.DestroyImmediate(reflection);
                    PrefabUtility.SaveAsPrefabAsset(prefab,path);
                }
                finally{PrefabUtility.UnloadPrefabContents(prefab);}
            }
            var shader=Shader.Find("Harbor/Gerstner Reflective Step Water");
            if(shader==null || ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Water shader compilation failed.");
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log("Simple shimmer water installed: no planar reflection components; existing wave and lighting settings preserved.");
        }
    }
}
