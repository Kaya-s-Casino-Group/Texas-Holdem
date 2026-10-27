using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HarborEnvironment.Editor
{
    public static class HarborEnvironmentImporter
    {
        const string Root = "Assets/Art/Environment/Harbor";
        const string ScenePath = "Assets/Scene/InTest/SingleBattle.unity";
        const string PrefabPath = Root + "/Prefabs/Harbor_Environment.prefab";
        [Serializable] public class Layout
        {
            public ObjectInfo[] objects; public MaterialInfo[] materials; public LightInfo[] lights;
            public string[] excludedCharacters; public CameraInfo camera; public float waveStrength = 1, waveSpeed = .8f;
        }
        [Serializable] public class ObjectInfo { public string name, sourceName, path, kind; public int[] materials; }
        [Serializable] public class MaterialInfo
        {
            public string name, kind, baseMap, normalMap;
            public float[] color, flagDirection, flagSide;
            public float roughness, metallic, emission, cutoff, normalStrength, flagAmplitude, flagSpeed;
        }
        [Serializable] public class LightInfo
        {
            public string name, type; public float[] position, forward, up, color;
            public float intensity, range, radius;
        }
        [Serializable] public class CameraInfo { public float[] position, forward, up; public float fov, near, far; }
        static Vector3 V(float[] f) => new Vector3(f[0], f[1], f[2]);
        static Color C(float[] f) => new Color(f[0], f[1], f[2], f.Length > 3 ? f[3] : 1);

        [MenuItem("Tools/Harbor/Import Environment and Place in SingleBattle")]
        public static void BuildAndPlace()
        {
            try { Build(); }
            catch (Exception e)
            {
                Debug.LogException(e); Directory.CreateDirectory("output/harbor-import");
                File.WriteAllText("output/harbor-import/result.txt", "FAILED\n" + e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }
        static void Build()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(Root + "/Source/Harbor_Layout.json"));
            if (layout.objects.Any(o => o.name.Contains("Palamedes") || o.name.Contains("Wraith")))
                throw new InvalidDataException("Character geometry in environment export.");
            Directory.CreateDirectory("output/harbor-import");
            var materials = layout.materials.Select(m => BuildMaterial(m, layout)).ToArray();
            var root = new GameObject("Harbor_Environment");
            var meshRoot = Child(root.transform, "Mesh"); var lightRoot = Child(root.transform, "Light");
            var groups = new Dictionary<string, Transform>();
            HarborLightmapUV.ResetLayoutCache();
            foreach (var item in layout.objects)
            {
                if (!groups.TryGetValue(item.kind, out var group)) groups[item.kind] = group = Child(meshRoot, item.kind);
                var go = new GameObject(item.name); go.transform.SetParent(group, false);
                var mesh = ReadMesh(item.path, item.name, item.kind != "Backdrop");
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = item.materials.Select(i => materials[i]).ToArray();
                if (item.kind == "Backdrop" || item.kind == "Emission" || item.kind == "Water")
                { renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = item.kind == "Water"; }
                if (item.kind == "Water")
                {
                    go.layer = 4;
                    var reflection = go.AddComponent<HarborPlanarReflection>();
                    reflection.waterHeight = mesh.bounds.center.y;
                    reflection.resolution = 512;
                }
            }
            foreach (var item in layout.lights)
            {
                var go = new GameObject(item.name); go.transform.SetParent(lightRoot, false);
                go.transform.localPosition = V(item.position);
                go.transform.localRotation = Quaternion.LookRotation(V(item.forward), V(item.up));
                var light = go.AddComponent<Light>();
                light.type = item.type == "SUN" ? LightType.Directional : LightType.Point;
                light.color = C(item.color); light.intensity = item.intensity; light.range = item.range;
                light.shadows = LightShadows.Soft; light.shadowBias = .025f; light.shadowNormalBias = .15f;
                light.renderMode = LightRenderMode.ForcePixel;
                go.AddComponent<UniversalAdditionalLightData>();
            }
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            var backup = "output/harbor-import/SingleBattle_before_Harbor.unity";
            if (!File.Exists(backup)) File.Copy(ScenePath, backup);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var existing = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Harbor_Environment");
            if (existing) UnityEngine.Object.DestroyImmediate(existing);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            // Replace only the obsolete whitebox floor/lights; existing actors and gameplay are kept.
            foreach (var go in scene.GetRootGameObjects())
                if (new[] { "Plane", "Directional Light", "Point Light", "Point Light (1)" }.Contains(go.name)) go.SetActive(false);
            var camera = Camera.main;
            if (camera && layout.camera != null)
            {
                camera.transform.SetPositionAndRotation(V(layout.camera.position), Quaternion.LookRotation(V(layout.camera.forward), V(layout.camera.up)));
                camera.fieldOfView = layout.camera.fov; camera.nearClipPlane = layout.camera.near;
                camera.farClipPlane = layout.camera.far; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.1f, .14f, .12f); camera.allowHDR = true;
                var data = camera.GetUniversalAdditionalCameraData(); data.requiresColorTexture = true; data.requiresDepthTexture = true;
                var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if (pipeline)
                {
                    var serialized = new SerializedObject(pipeline); var renderers = serialized.FindProperty("m_RendererDataList");
                    for (int i = 0; renderers != null && i < renderers.arraySize; ++i)
                    {
                        var renderer = renderers.GetArrayElementAtIndex(i).objectReferenceValue;
                        if (renderer && renderer.name == "MainDeferredRenderer") { data.SetRenderer(i); break; }
                    }
                }
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.23f,.29f,.24f);
            RenderSettings.ambientEquatorColor = new Color(.13f,.19f,.16f);
            RenderSettings.ambientGroundColor = new Color(.06f,.075f,.06f);
            var errors = new List<string>();
            foreach (var shader in materials.Select(m => m.shader).Distinct())
            {
                var checkMaterial = new Material(shader);
                for (int pass=0; pass<checkMaterial.passCount; ++pass) ShaderUtil.CompilePass(checkMaterial, pass, true);
                UnityEngine.Object.DestroyImmediate(checkMaterial);
                foreach (var message in ShaderUtil.GetShaderMessages(shader))
                    if (message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error) errors.Add(shader.name + ": " + message.message);
            }
            foreach (var t in instance.GetComponentsInChildren<Transform>(true))
                if (t.GetComponent<MeshFilter>() && (t.localPosition != Vector3.zero || t.localRotation != Quaternion.identity || t.localScale != Vector3.one))
                    errors.Add("Non-identity mesh transform: " + t.name);
            if (instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length != 0) errors.Add("Unexpected skinned mesh.");
            if (errors.Count != 0) throw new InvalidOperationException(string.Join("\n", errors));
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            if(camera)
            {
                var target=new RenderTexture(1280,592,24,RenderTextureFormat.ARGBHalf);
                var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
                try
                {
                    camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                    var image=new Texture2D(1280,592,TextureFormat.RGB24,false);
                    image.ReadPixels(new Rect(0,0,1280,592),0,0);image.Apply();
                    File.WriteAllBytes("output/harbor-import/Unity_Harbor_Preview.png",image.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(image);
                }
                finally {camera.targetTexture=oldTarget;RenderTexture.active=oldActive;target.Release();UnityEngine.Object.DestroyImmediate(target);}
            }
            File.WriteAllText("output/harbor-import/result.txt", $"SUCCESS\n{layout.objects.Length} meshes, {layout.lights.Length} lights, {materials.Length} materials\nCharacters excluded: {layout.excludedCharacters.Length}\nAll mesh transforms identity\n{PrefabPath}\n{ScenePath}\n");
            Debug.Log("HARBOR_IMPORT_SUCCESS " + layout.objects.Length + " meshes");
        }
        static Transform Child(Transform parent, string name)
        { var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform; }
        static Mesh ReadMesh(string path, string name, bool lightmapUV)
        {
            using var reader = new BinaryReader(File.OpenRead(path));
            if (reader.ReadInt32() != 0x48415242) throw new InvalidDataException(path);
            int count = reader.ReadInt32(); var vertices = new Vector3[count]; var normals = new Vector3[count];
            var uv = new Vector2[count]; var colors = new Color[count];
            for (int i=0;i<count;++i)
            {
                vertices[i] = new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
                normals[i] = new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
                uv[i] = new Vector2(reader.ReadSingle(),reader.ReadSingle());
                colors[i] = new Color(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
            }
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.colors=colors;
            mesh.subMeshCount=reader.ReadInt32();
            for(int i=0;i<mesh.subMeshCount;++i)
            { int n=reader.ReadInt32();var indices=new int[n];for(int j=0;j<n;++j)indices[j]=reader.ReadInt32();mesh.SetTriangles(indices,i); }
            mesh.RecalculateTangents();mesh.RecalculateBounds();var bounds=mesh.bounds;bounds.Expand(.5f);mesh.bounds=bounds;
            if(lightmapUV)HarborLightmapUV.Generate(mesh);
            string asset=Root+"/Meshes/"+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(asset);
            if(existing){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existing);return existing;}
            AssetDatabase.CreateAsset(mesh,asset);return mesh;
        }
        static Material BuildMaterial(MaterialInfo info, Layout layout)
        {
            string shaderName=info.kind=="Water"?"Harbor/Gerstner Reflective Step Water":
                info.kind=="Backdrop"||info.kind=="Emission"?"Harbor/Painted Unlit":"Harbor/Environment Deferred";
            var shader=Shader.Find(shaderName);if(!shader)throw new InvalidOperationException("Missing shader "+shaderName);
            string path=Root+"/Materials/"+info.name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(shader);AssetDatabase.CreateAsset(m,path);}else m.shader=shader;
            m.SetColor("_BaseColor",info.kind=="Water"?new Color(.025f,.08f,.065f):C(info.color));
            if(info.kind=="Water")
            {m.SetFloat("_WaveStrength",layout.waveStrength);m.SetFloat("_WaveSpeed",layout.waveSpeed);}
            else
            {
                if(!string.IsNullOrEmpty(info.baseMap)) m.SetTexture("_BaseMap",ImportTexture(info.baseMap,false));
                if(!string.IsNullOrEmpty(info.normalMap)&&m.HasProperty("_BumpMap"))m.SetTexture("_BumpMap",ImportTexture(info.normalMap,true));
                if(m.HasProperty("_Cutoff"))m.SetFloat("_Cutoff",info.cutoff);
                if(m.HasProperty("_Saturation"))m.SetFloat("_Saturation",info.name.Contains("Distant")?.65f:.85f);
                if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",Mathf.Clamp01(1-info.roughness));
                if(m.HasProperty("_PaintFill"))m.SetFloat("_PaintFill",.08f);
                if(m.HasProperty("_BumpScale"))m.SetFloat("_BumpScale",.35f);
                if(info.kind=="Flag")
                {
                    m.SetFloat("_DeformMode",1);m.SetFloat("_WindStrength",info.flagAmplitude);m.SetFloat("_WindSpeed",info.flagSpeed);
                    m.SetVector("_FlagDirection",V(info.flagDirection));m.SetVector("_FlagSide",V(info.flagSide));m.SetFloat("_Cull",0);
                }
            }
            m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
        }
        static Texture2D ImportTexture(string path,bool normal)
        {
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer && (importer.textureType!=(normal?TextureImporterType.NormalMap:TextureImporterType.Default) || importer.maxTextureSize!=2048))
            {
                importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
                importer.sRGBTexture=!normal;importer.maxTextureSize=2048;importer.alphaSource=TextureImporterAlphaSource.FromInput;
                importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
