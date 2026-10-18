using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace GothicChapel.Editor
{
    public static class GothicChapelImport
    {
        public const string Folder = "Assets/Art/Models/Environment/GothicChapel";
        public const string ReportFolder = "output/gothic-scene/unity-export";
        public const string ShaderName = "Gothic Chapel/Stylized Deferred";
        [Serializable] public class Submesh { public int[] indices; }
        [Serializable] public class MeshSpec
        {
            public string name, category;
            public Vector3 position;
            public string[] materials;
            public bool backdrop, foliage;
            public Vector3[] vertices, normals;
            public Vector2[] uv0;
            public Submesh[] submeshes;
        }
        [Serializable] public class ObjectSpec { public string name, path, category; public Vector3 position; public bool backdrop, foliage; }
        [Serializable] public class MaterialSpec
        {
            public string name, baseMap, normalMap, maskMap;
            public float[] color, atmosphere;
            public float normalStrength, metallic, smoothness, cutoff, unlit, bakeEmission, atmosphereBlend;
        }
        [Serializable] public class CameraSpec { public string name; public Vector3 position, forward, up; public float verticalFov, near, far, aspect; }
        [Serializable] public class Manifest { public string units; public MaterialSpec[] materials; public ObjectSpec[] objects; public CameraSpec camera; }
        [Serializable] public class MeshAudit
        {
            public string name;
            public int vertices, triangles, uv1Count, overlapSamples, coveredSamples, degenerateUVTriangles;
            public bool lightmapped, inBounds;
            public Vector3 bounds;
        }
        [Serializable] public class Audit { public string pipeline, scene; public List<MeshAudit> meshes = new List<MeshAudit>(); public List<string> errors = new List<string>(); }
        public static Manifest Read() => JsonUtility.FromJson<Manifest>(File.ReadAllText(Folder + "/Source/GothicChapel_Layout.json"));

        [MenuItem("Tools/Scene Art/Gothic Chapel/Build Assets and Lightmap UV1")]
        public static void Build()
        {
            Directory.CreateDirectory(ReportFolder);
            File.WriteAllText(ReportFolder + "/build-status.txt", "Building");
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var manifest = Read();
                var shader = Shader.Find(ShaderName);
                if (!shader || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Gothic Chapel shader missing or has compiler errors.");
                foreach (var spec in manifest.materials) BuildMaterial(spec, shader);
                var audit = new Audit { pipeline = AssetDatabase.GetAssetPath(GraphicsSettings.currentRenderPipeline), scene = SceneManager.GetActiveScene().path };
                foreach (var obj in manifest.objects)
                {
                    var spec = JsonUtility.FromJson<MeshSpec>(File.ReadAllText(obj.path));
                    var mesh = new Mesh { name = spec.name + "_Mesh", indexFormat = IndexFormat.UInt32 };
                    mesh.vertices = spec.vertices; mesh.normals = spec.normals; mesh.uv = spec.uv0;
                    mesh.subMeshCount = spec.submeshes.Length;
                    for (int i = 0; i < spec.submeshes.Length; i++)
                    {
                        var raw = spec.submeshes[i].indices;
                        var valid = new List<int>(raw.Length);
                        for (int j = 0; j < raw.Length; j += 3)
                        {
                            var a = spec.vertices[raw[j]]; var b = spec.vertices[raw[j+1]]; var c = spec.vertices[raw[j+2]];
                            if (Vector3.Cross(b-a,c-a).sqrMagnitude < 1e-18f) continue;
                            valid.Add(raw[j]); valid.Add(raw[j+1]); valid.Add(raw[j+2]);
                        }
                        mesh.SetTriangles(valid, i);
                    }
                    mesh.RecalculateBounds();
                    if (spec.backdrop) mesh.uv2 = spec.uv0;
                    else
                    {
                        UnwrapParam.SetDefaults(out var unwrap);
                        unwrap.hardAngle = 60; unwrap.angleError = 8; unwrap.areaError = 15;
                        // Eight texels at a 1024 reference atlas; UV0 remains untouched.
                        unwrap.packMargin = 8f / 1024f;
                        if (!Unwrapping.GenerateSecondaryUVSet(mesh, unwrap)) throw new InvalidOperationException("UV1 unwrap failed: " + spec.name);
                        RepairCollapsedUV(mesh);
                    }
                    mesh.RecalculateTangents();
                    var record = AuditMesh(mesh, !spec.backdrop);
                    record.name = spec.name; audit.meshes.Add(record);
                    if (!record.inBounds || record.uv1Count != record.vertices) audit.errors.Add(spec.name + ": invalid UV1");
                    if (record.overlapSamples > 0) audit.errors.Add(spec.name + ": UV1 sampled overlaps=" + record.overlapSamples);
                    if (record.degenerateUVTriangles > 0) audit.errors.Add(spec.name + ": collapsed UV1 triangles=" + record.degenerateUVTriangles);
                    var path = Folder + "/Meshes/" + spec.name + ".asset";
                    var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (existing) { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(existing); }
                    else AssetDatabase.CreateAsset(mesh, path);
                }
                File.WriteAllText(ReportFolder + "/uv1-audit.json", JsonUtility.ToJson(audit, true));
                if (audit.errors.Count > 0) throw new InvalidOperationException(string.Join("\n",audit.errors));
                BuildPrefab(manifest);
                AssetDatabase.SaveAssets();
                File.WriteAllText(ReportFolder + "/build-status.txt", "PASS: " + audit.meshes.Count + " meshes, " + manifest.materials.Length + " materials; UV1 audit passed.");
                Debug.Log(File.ReadAllText(ReportFolder + "/build-status.txt"));
            }
            catch (Exception e) { File.WriteAllText(ReportFolder + "/build-status.txt", "FAILED: " + e); throw; }
        }

        // Unity's unwrapper can collapse tiny slivers. Give those faces separate islands
        // in a reserved strip, preserving their geometry, normals and texture UVs.
        static void RepairCollapsedUV(Mesh mesh)
        {
            var uv = mesh.uv2.ToList();
            var indices = Enumerable.Range(0,mesh.subMeshCount).Select(mesh.GetTriangles).ToArray();
            int count = indices.Sum(ts => Enumerable.Range(0,ts.Length/3).Count(i =>
                Mathf.Abs(Cross(uv[ts[i*3+1]]-uv[ts[i*3]],uv[ts[i*3+2]]-uv[ts[i*3]])) < 1e-12f));
            if(count == 0) return;
            if(count > 24) throw new InvalidOperationException("Too many collapsed UV islands: " + mesh.name);
            var vertices=mesh.vertices.ToList();var normals=mesh.normals.ToList();var uv0=mesh.uv.ToList();
            for(int i=0;i<uv.Count;i++)uv[i]=new Vector2(uv[i].x,uv[i].y*.94f);
            int island=0;
            foreach(var ts in indices)for(int i=0;i<ts.Length;i+=3)
            {
                if(Mathf.Abs(Cross(uv[ts[i+1]]-uv[ts[i]],uv[ts[i+2]]-uv[ts[i]])) >= 1e-12f)continue;
                float x=.015f+island++*.04f;
                var chart=new[]{new Vector2(x,.965f),new Vector2(x+.018f,.965f),new Vector2(x,.983f)};
                for(int k=0;k<3;k++)
                {
                    int old=ts[i+k];ts[i+k]=vertices.Count;
                    vertices.Add(vertices[old]);normals.Add(normals[old]);uv0.Add(uv0[old]);uv.Add(chart[k]);
                }
            }
            mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv0);mesh.SetUVs(1,uv);
            for(int i=0;i<indices.Length;i++)mesh.SetTriangles(indices[i],i);
        }

        static void ConfigureTexture(string path, bool normal, bool mask, bool alpha)
        {
            if (string.IsNullOrEmpty(path)) return;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new IOException("Missing texture " + path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal && !mask; importer.convertToNormalmap = false;
            importer.alphaSource = (alpha || mask) ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = alpha && !mask;
            importer.mipmapEnabled = true; importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 4096; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 4;
            importer.SaveAndReimport();
        }
        static Color C(float[] v) => new Color(v[0],v[1],v[2],v.Length > 3 ? v[3] : 1);
        static void BuildMaterial(MaterialSpec s, Shader shader)
        {
            ConfigureTexture(s.baseMap,false,false,s.cutoff>0); ConfigureTexture(s.normalMap,true,false,false); ConfigureTexture(s.maskMap,false,true,false);
            string path = Folder + "/Materials/" + s.name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(shader) { name=s.name }; AssetDatabase.CreateAsset(m,path); }
            m.shader=shader;
            m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(s.baseMap));
            m.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(s.normalMap));
            m.SetTexture("_MaskMap",AssetDatabase.LoadAssetAtPath<Texture2D>(s.maskMap));
            m.SetColor("_BaseColor",C(s.color)); m.SetFloat("_BumpScale",s.normalStrength);
            m.SetFloat("_Metallic",s.metallic);m.SetFloat("_Smoothness",s.smoothness);
            m.SetFloat("_Cutoff",s.cutoff);m.SetFloat("_Unlit",s.unlit);m.SetFloat("_BakeEmission",s.bakeEmission);
            m.SetFloat("_ExcludeFromGI",s.unlit > .5f ? 1 : 0);
            m.SetFloat("_AtmosphereBlend",s.atmosphereBlend);
            if (s.atmosphere != null && s.atmosphere.Length >= 3) m.SetColor("_AtmosphereColor",C(s.atmosphere));
            m.SetFloat("_WindStrength",0);m.SetFloat("_VertexTone",0);m.SetFloat("_Cull",0);
            m.doubleSidedGI=true;m.enableInstancing=true;m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            m.renderQueue=s.cutoff>0 ? 2450 : 2000;
            m.SetOverrideTag("RenderType",s.cutoff>0 ? "TransparentCutout" : "Opaque");
            EditorUtility.SetDirty(m);
        }

        public static MeshAudit AuditMesh(Mesh mesh,bool lightmapped)
        {
            var uv=mesh.uv2;var tris=mesh.triangles;const int res=512;
            var owner=new int[res*res];for(int i=0;i<owner.Length;i++)owner[i]=-1;
            var audit=new MeshAudit { vertices=mesh.vertexCount,triangles=tris.Length/3,uv1Count=uv.Length,lightmapped=lightmapped,bounds=mesh.bounds.size,
                inBounds=uv.Length==mesh.vertexCount && uv.All(p=>!float.IsNaN(p.x)&&!float.IsNaN(p.y)&&p.x>=-.0001f&&p.y>=-.0001f&&p.x<=1.0001f&&p.y<=1.0001f) };
            if(!audit.inBounds)return audit;
            for(int t=0;t<tris.Length;t+=3)
            {
                var a=uv[tris[t]];var b=uv[tris[t+1]];var c=uv[tris[t+2]];
                float area=Cross(b-a,c-a);if(Mathf.Abs(area)<1e-12f){audit.degenerateUVTriangles++;continue;}
                int x0=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x,b.x,c.x)*res),0,res-1),x1=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x,b.x,c.x)*res),0,res-1);
                int y0=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y,b.y,c.y)*res),0,res-1),y1=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y,b.y,c.y)*res),0,res-1);
                for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                {
                    var p=new Vector2((x+.5f)/res,(y+.5f)/res);
                    float u=Cross(b-p,c-p)/area,v=Cross(c-p,a-p)/area,w=1-u-v;
                    if(u<1e-5f||v<1e-5f||w<1e-5f)continue;
                    int index=y*res+x;
                    if(owner[index]>=0 && owner[index]!=t) audit.overlapSamples++;
                    else if(owner[index]<0)audit.coveredSamples++;
                    owner[index]=t;
                }
            }
            return audit;
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;

        static void BuildPrefab(Manifest manifest)
        {
            var preview=EditorSceneManager.NewPreviewScene();
            try
            {
                var root=new GameObject("PF_GC_GothicChapel");SceneManager.MoveGameObjectToScene(root,preview);
                var groups=new Dictionary<string,Transform>();
                Transform Group(string key)
                {
                    if(groups.TryGetValue(key,out var value))return value;
                    var go=new GameObject(key.Replace("COL_", ""));go.transform.SetParent(root.transform,false);groups.Add(key,go.transform);return go.transform;
                }
                foreach(var obj in manifest.objects)
                {
                    var spec=JsonUtility.FromJson<MeshSpec>(File.ReadAllText(obj.path));
                    var go=new GameObject(spec.name);go.transform.SetParent(Group(spec.category),false);go.transform.localPosition=spec.position;
                    go.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Meshes/"+spec.name+".asset");
                    var r=go.AddComponent<MeshRenderer>();r.sharedMaterials=spec.materials.Select(n=>AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Materials/"+n+".mat")).ToArray();
                    r.shadowCastingMode=spec.backdrop?ShadowCastingMode.Off:ShadowCastingMode.TwoSided;
                    r.receiveShadows=!spec.backdrop;r.receiveGI=spec.backdrop?ReceiveGI.LightProbes:ReceiveGI.Lightmaps;
                    r.scaleInLightmap=spec.backdrop?0:spec.foliage?.25f:spec.name.StartsWith("SM_Architecture")?1.25f:1;
                    r.lightProbeUsage=spec.backdrop?LightProbeUsage.Off:LightProbeUsage.BlendProbes;
                    r.reflectionProbeUsage=spec.backdrop?ReflectionProbeUsage.Off:ReflectionProbeUsage.BlendProbes;
                    if(!spec.backdrop)GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.ContributeGI|StaticEditorFlags.ReflectionProbeStatic);
                }
                var camera=new GameObject("CAM_GC_Main");camera.transform.SetParent(Group("09_Camera"),false);
                camera.transform.SetPositionAndRotation(manifest.camera.position,Quaternion.LookRotation(manifest.camera.forward,manifest.camera.up));
                var c=camera.AddComponent<Camera>();c.fieldOfView=manifest.camera.verticalFov;c.aspect=manifest.camera.aspect;c.nearClipPlane=manifest.camera.near;c.farClipPlane=manifest.camera.far;
                c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.025f,.035f,.05f);c.allowHDR=true;
                c.GetUniversalAdditionalCameraData().SetRenderer(0);
                // Retain the candle-holder relationship while preserving both world transforms.
                var transforms=root.GetComponentsInChildren<Transform>();
                var candle=transforms.First(t=>t.name=="SM_Candle_HolderMounted");var holder=transforms.First(t=>t.name=="SM_Holder_Brass");candle.SetParent(holder,true);
                PrefabUtility.SaveAsPrefabAsset(root,Folder+"/Prefabs/PF_GC_GothicChapel.prefab");
            }
            finally {EditorSceneManager.ClosePreviewScene(preview);}
        }

        [MenuItem("Tools/Scene Art/Gothic Chapel/Place in Current Scene")]
        public static void Place()
        {
            var scene=SceneManager.GetActiveScene();
            if(scene.GetRootGameObjects().Any(g=>g.name=="PF_GC_GothicChapel"))throw new InvalidOperationException("Gothic Chapel already placed; update its prefab instead of duplicating.");
            GothicChapelInspect.Inspect();
            var tag=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);var layers=tag.FindProperty("layers");
            int layer=LayerMask.NameToLayer("GothicChapel");
            if(layer<0)
            {
                for(int i=8;i<32;i++)if(string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)){layer=i;layers.GetArrayElementAtIndex(i).stringValue="GothicChapel";break;}
                if(layer<0)throw new InvalidOperationException("No free layer for isolated chapel preview.");tag.ApplyModifiedProperties();
            }
            int bit=1<<layer;var rootsBefore=scene.GetRootGameObjects();
            var existingCameras=rootsBefore.SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).ToArray();
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Prefabs/PF_GC_GothicChapel.prefab");
            if(!prefab)throw new InvalidOperationException("Build assets first.");
            var root=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);Undo.RegisterCreatedObjectUndo(root,"Place Gothic Chapel");
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer=layer;
                PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
            }
            var cam=root.GetComponentInChildren<Camera>();cam.cullingMask=bit;cam.depth=existingCameras.Length>0?existingCameras.Max(c=>c.depth)+1:0;
            foreach(var component in root.GetComponentsInChildren<Component>(true))if(component)PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject=root;
            File.WriteAllText(ReportFolder+"/placement.txt",scene.path+"\nRoot="+root.name+"\nLayer="+layer+"\nExisting roots preserved="+rootsBefore.Length);
            Debug.Log("Gothic Chapel placed in "+scene.path+"; original objects preserved; imported camera renders its isolated layer.");
        }
    }
}
