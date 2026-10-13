using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RetroRendering;

// Import is deliberately separate from applying the prepared mesh to the scene.
public static class GargoyleVATImport
{
    public const string Folder = "Assets/Art/Models/Creatures/DragonGargoyle/VAT";
    public const string Stem = Folder + "/DragonGargoyle_Collapse";
    public const string MeshPath = Stem + "_Unity.asset";
    public const string SurfacePath = Stem + "_Surface.mat";
    const string LegacyExteriorPath = Stem + "_Exterior.mat";
    const string LegacyInteriorPath = Stem + "_Interior.mat";
    public const string OriginalMaterial = "Assets/Art/Models/Creatures/DragonGargoyle/DragonGargoyle_Material.mat";
    public const string ShaderName = "Rendering/Gargoyle Cracks Deferred";
    public static JObject Metadata => JObject.Parse(File.ReadAllText(Stem + "_vat.json"));
    public static Vector3 Vector(JToken a) => new Vector3((float)a[0], (float)a[1], (float)a[2]);
    public static Vector3 ToUnity(Vector3 v) => new Vector3(v.x, v.z, v.y);

    [MenuItem("Tools/Art Assets/Dragon Gargoyle VAT/Configure Assets")]
    public static void Configure()
    {
        var data = Metadata;
        ValidatePublishedFiles(data);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        if ((string)data["schema"] != "statue-rigid-vat-v1" || (int)data["frame_count"] < 1 ||
            (int)data["piece_count"] > (int)data["texture"]["width"] ||
            (int)data["frame_count"] > (int)data["texture"]["height"])
            throw new InvalidOperationException("Unsupported or invalid rigid VAT package.");
        foreach (string suffix in new[] {"_position.exr", "_rotation.exr"}) ConfigureTexture(Stem + suffix, data);
        var shader = Shader.Find(ShaderName);
        if (shader == null) throw new InvalidOperationException("Gargoyle shader is missing.");
        var original = AssetDatabase.LoadAssetAtPath<Material>(OriginalMaterial);
        if (original == null) throw new InvalidOperationException("Original surface material is missing.");
        // Move preserves the material GUID and existing surface tuning.
        var surface = AssetDatabase.LoadAssetAtPath<Material>(SurfacePath);
        if (surface == null && AssetDatabase.LoadAssetAtPath<Material>(LegacyExteriorPath) != null)
        {
            string error = AssetDatabase.MoveAsset(LegacyExteriorPath,SurfacePath);
            if (!string.IsNullOrEmpty(error)) throw new IOException(error);
            surface = AssetDatabase.LoadAssetAtPath<Material>(SurfacePath);
        }
        if (surface == null)
        {
            surface = new Material(original) {name = "DragonGargoyle_Collapse_Surface"};
            surface.SetColor("_InteriorColor", new Color(.09f,.105f,.12f,1).gamma);
            surface.SetFloat("_InteriorMetallic",0);surface.SetFloat("_InteriorSmoothness",.08f);
            AssetDatabase.CreateAsset(surface,SurfacePath);
        }
        var legacyInterior = AssetDatabase.LoadAssetAtPath<Material>(LegacyInteriorPath);
        if (legacyInterior != null)
        {
            surface.SetColor("_InteriorColor",legacyInterior.GetColor("_BaseColor"));
            surface.SetFloat("_InteriorMetallic",legacyInterior.GetFloat("_Metallic"));
            surface.SetFloat("_InteriorSmoothness",legacyInterior.GetFloat("_Smoothness"));
        }
        surface.name = "DragonGargoyle_Collapse_Surface";
        surface.SetFloat("_UseSurfaceMask",1);
        Bind(surface,data);
        var importer = (ModelImporter)AssetImporter.GetAtPath(Stem + ".fbx");
        importer.globalScale = 1; importer.useFileScale = true; importer.bakeAxisConversion = true;
        importer.importAnimation = false; importer.animationType = ModelImporterAnimationType.None;
        importer.importCameras = false; importer.importLights = false;
        importer.meshCompression = ModelImporterMeshCompression.Off;
        importer.importNormals = ModelImporterNormals.Import; importer.importTangents = ModelImporterTangents.Import;
        importer.generateSecondaryUV = false; importer.swapUVChannels = false;
        importer.weldVertices = false; importer.optimizeMeshPolygons = false; importer.optimizeMeshVertices = false;
        importer.isReadable = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        // Raw FBX still carries the node transform; keep its preview undeformed.
        // The VAT materials belong to the standalone prepared mesh below.
        foreach (var mapping in importer.GetExternalObjectMap())
            if (mapping.Key.type==typeof(Material) && !data["materials"].Values<string>().Contains(mapping.Key.name))
                importer.RemoveRemap(mapping.Key);
        foreach (string name in data["materials"].Values<string>())
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), original);
        importer.SaveAndReimport();
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Stem + ".fbx");
        var filters = model.GetComponentsInChildren<MeshFilter>();
        if (filters.Length != 1) throw new InvalidOperationException("Rigid VAT package must contain one bind mesh.");
        var mf = filters[0]; var source = mf.sharedMesh;
        var transform = mf.transform.localToWorldMatrix;
        // Unity keeps the Blender FBX centimeter/root-axis correction on the node.
        // Bake it into a standalone mesh so the existing statue transform is kept.
        var mesh = UnityEngine.Object.Instantiate(source); mesh.name = "DragonGargoyle_Collapse_Unity";
        mesh.vertices = source.vertices.Select(transform.MultiplyPoint3x4).ToArray();
        var normalTransform = transform.inverse.transpose;
        mesh.normals = source.normals.Select(n => normalTransform.MultiplyVector(n).normalized).ToArray();
        mesh.tangents = source.tangents.Select(t => {
            var v = transform.MultiplyVector(new Vector3(t.x,t.y,t.z)).normalized;
            return new Vector4(v.x,v.y,v.z,t.w * Mathf.Sign(transform.determinant));
        }).ToArray();
        if (transform.determinant < 0)
            for (int s = 0; s < mesh.subMeshCount; ++s)
            { var indices = mesh.GetTriangles(s); for (int i=0;i<indices.Length;i+=3) (indices[i+1],indices[i+2])=(indices[i+2],indices[i+1]); mesh.SetTriangles(indices,s,false); }
        var bounds = new Bounds();
        bounds.SetMinMax(ToUnity(Vector(data["animation_bounds"]["min"])), ToUnity(Vector(data["animation_bounds"]["max"])));
        // Export bounds include .003m padding; add a margin for half-float decode error.
        bounds.Expand(.004f); mesh.bounds = bounds;
        ValidateChannels(mesh, data);
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (existing == null) AssetDatabase.CreateAsset(mesh, MeshPath);
        else
        {
            // CopySerialized changes CPU mesh data without reliably invalidating
            // resident GPU vertex buffers when the vertex layout gains COLOR.
            // Native Mesh setters rebuild the live asset while retaining its GUID.
            existing.Clear(false);existing.name=mesh.name;existing.indexFormat=mesh.indexFormat;
            existing.vertices=mesh.vertices;existing.normals=mesh.normals;existing.tangents=mesh.tangents;
            existing.colors32=mesh.colors32;
            var uvs=new System.Collections.Generic.List<Vector2>();
            for(int channel=0;channel<8;++channel)
            {
                mesh.GetUVs(channel,uvs);
                if(uvs.Count>0)existing.SetUVs(channel,uvs);
            }
            existing.subMeshCount=mesh.subMeshCount;
            for(int submesh=0;submesh<mesh.subMeshCount;++submesh)
                existing.SetIndices(mesh.GetIndices(submesh),mesh.GetTopology(submesh),submesh,false);
            existing.bounds=mesh.bounds;existing.UploadMeshData(false);
            UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existing);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Gargoyle VAT configured: bind mesh node transform baked; UV0–3 preserved; RGBAHalf point/clamp textures; full animation bounds.");
    }

    static void ValidatePublishedFiles(JObject data)
    {
        // Blender publishes the manifest last. Hashes prevent importing mixed
        // files if Unity notices an export while it is being replaced.
        var hashes = data["unity_export"]?["runtime_files_sha256"] as JObject;
        if (hashes == null) return; // Earlier manually copied packages remain supported.
        foreach (string suffix in new[]{".fbx", "_position.exr", "_rotation.exr"})
        {
            string path = Stem + suffix, name = Path.GetFileName(path);
            if (!File.Exists(path) || hashes[name] == null)
                throw new IOException("Incomplete VAT export: " + path);
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
            {
                string hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                if (hash != (string)hashes[name])
                    throw new IOException("VAT export is incomplete or modified: " + path + ". Export again from Blender.");
            }
        }
    }

    static void ConfigureTexture(string path, JObject data)
    {
        var t = (TextureImporter)AssetImporter.GetAtPath(path);
        if (t == null) throw new InvalidOperationException("Missing VAT texture: " + path);
        t.textureType = TextureImporterType.Default; t.sRGBTexture = false;
        t.alphaSource = TextureImporterAlphaSource.FromInput; t.alphaIsTransparency = false;
        t.mipmapEnabled = false; t.npotScale = TextureImporterNPOTScale.None;
        t.filterMode = FilterMode.Point; t.wrapMode = TextureWrapMode.Clamp; t.anisoLevel = 0;
        t.textureCompression = TextureImporterCompression.Uncompressed; t.crunchedCompression = false;
        t.isReadable = true; // Enables the editor's CPU/GPU contract validation.
        int maxSize = Mathf.NextPowerOfTwo(Mathf.Max((int)data["texture"]["width"],(int)data["texture"]["height"]));
        t.maxTextureSize = maxSize;
        var settings = t.GetDefaultPlatformTextureSettings();
        settings.format = (int)data["texture"]["component_bits"] == 32 ? TextureImporterFormat.RGBAFloat : TextureImporterFormat.RGBAHalf;
        settings.maxTextureSize = maxSize; settings.textureCompression = TextureImporterCompression.Uncompressed;
        t.SetPlatformTextureSettings(settings);
        foreach (string platform in new[] {"Standalone", "Android", "iPhone"})
        {
            var p = t.GetPlatformTextureSettings(platform); p.overridden = false; t.SetPlatformTextureSettings(p);
        }
        t.SaveAndReimport();
    }

    public static void Bind(Material mat, JObject data)
    {
        mat.shader = Shader.Find(ShaderName); mat.shaderKeywords = Array.Empty<string>();
        mat.SetTexture("_VATPosition", AssetDatabase.LoadAssetAtPath<Texture2D>(Stem + "_position.exr"));
        mat.SetTexture("_VATRotation", AssetDatabase.LoadAssetAtPath<Texture2D>(Stem + "_rotation.exr"));
        mat.SetFloat("_VATEnabled", 1); mat.SetFloat("_VATFrameCount", (int)data["frame_count"]);
        int w = (int)data["texture"]["width"], h = (int)data["texture"]["height"];
        mat.SetVector("_VATTextureSize", new Vector4(w,h,1f/w,1f/h));
        mat.SetVector("_VATPositionMin", Vector(data["position_min"]));
        mat.SetVector("_VATPositionRange", Vector(data["position_max_minus_min"]));
        EditorUtility.SetDirty(mat);
    }

    public static void ValidateChannels(Mesh mesh, JObject data)
    {
        int n = mesh.vertexCount, pieces = (int)data["piece_count"], w = (int)data["texture"]["width"], h = (int)data["texture"]["height"];
        if (mesh.subMeshCount != 1 || mesh.triangles.Length/3 != (int)data["triangle_count"] ||
            mesh.uv.Length != n || mesh.uv2.Length != n || mesh.uv3.Length != n || mesh.uv4.Length != n ||
            mesh.normals.Length != n || mesh.tangents.Length != n)
            throw new InvalidOperationException("VAT bind mesh must retain triangles, one material slot and vertex channels.");
        if ((string)data["surface_mask"]?["semantic"] != "COLOR.r" || (int?)data["surface_mask"]?["material_slots"] != 1 || mesh.colors32.Length != n)
            throw new InvalidOperationException("Re-export from Blender with the single-material COLOR.r surface marker.");
        var colors = mesh.colors32; var triangles = mesh.triangles;
        int exteriorTriangles=0,interiorTriangles=0;
        for (int i=0;i<triangles.Length;i+=3)
        {
            byte marker=colors[triangles[i]].r;
            if ((marker!=0 && marker!=255) || colors[triangles[i+1]].r!=marker || colors[triangles[i+2]].r!=marker)
                throw new InvalidOperationException("Surface markers must be binary and constant on each triangle.");
            if (marker==0) ++exteriorTriangles; else ++interiorTriangles;
        }
        if (exteriorTriangles==0 || interiorTriangles==0) throw new InvalidOperationException("Exterior/interior marker was lost during FBX import.");
        var lookup = mesh.uv2; var xy = mesh.uv3; var zid = mesh.uv4;
        var seen = new bool[pieces];
        for (int i = 0; i < n; ++i)
        {
            int id = Mathf.RoundToInt(zid[i].y);
            if (id < 0 || id >= pieces) throw new InvalidOperationException("Invalid piece ID.");
            seen[id] = true;
            var expected = Vector(data["pieces"][id]["pivot_rest"]);
            if (Vector2.Distance(lookup[i],new Vector2((id+.5f)/w,1-.5f/h)) > .000001f ||
                Vector3.Distance(new Vector3(xy[i].x,xy[i].y,zid[i].x),expected) > .000001f)
                throw new InvalidOperationException("FBX importer changed the VAT lookup or pivot UV channels.");
        }
        if (seen.Any(x => !x)) throw new InvalidOperationException("A rigid piece is missing from the mesh.");
    }

    [MenuItem("Tools/Art Assets/Dragon Gargoyle VAT/Apply To Current Scene")]
    public static void ApplyToScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Apply in edit mode.");
        var targets = UnityEngine.Object.FindObjectsOfType<MeshFilter>(true).Where(x => x.name == "DragonGargoyle" && x.gameObject.scene.IsValid()).ToArray();
        if (targets.Length != 1) throw new InvalidOperationException("Expected exactly one DragonGargoyle in the loaded scenes.");
        Configure(); GargoyleVATValidation.Validate();
        var mf = targets[0]; var renderer = mf.GetComponent<MeshRenderer>();
        if (renderer == null) throw new InvalidOperationException("Statue MeshRenderer is missing.");
        Undo.RecordObjects(new UnityEngine.Object[] {mf,renderer}, "Replace Gargoyle with VAT bind mesh");
        mf.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        renderer.sharedMaterials = new[] {AssetDatabase.LoadAssetAtPath<Material>(SurfacePath)};
        renderer.ResetLocalBounds();
        var player = mf.GetComponent<RigidVATPlayer>();
        if (player == null) player = Undo.AddComponent<RigidVATPlayer>(mf.gameObject);
        Undo.RecordObject(player, "Configure Gargoyle Collapse");
        player.Configure(((int)Metadata["frame_count"] - 1) / (float)Metadata["fps"]);
        player.ResetPose();
        PrefabUtility.RecordPrefabInstancePropertyModifications(mf);
        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        PrefabUtility.RecordPrefabInstancePropertyModifications(player);
        var prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(mf.gameObject);
        if (prefabRoot != null)
        {
            var sourceRenderer = PrefabUtility.GetCorrespondingObjectFromSource(renderer);
            var mods = PrefabUtility.GetPropertyModifications(prefabRoot);
            if (mods != null)
                PrefabUtility.SetPropertyModifications(prefabRoot,mods.Where(m => !(m.target==sourceRenderer &&
                    m.propertyPath.StartsWith("m_Materials.Array.data[") && m.propertyPath!="m_Materials.Array.data[0]")).ToArray());
        }
        EditorSceneManager.MarkSceneDirty(mf.gameObject.scene);
        if (!EditorSceneManager.SaveScene(mf.gameObject.scene)) throw new IOException("Could not save the updated scene.");
        // Remove the old generated cap material only when no saved asset uses it.
        if (AssetDatabase.LoadAssetAtPath<Material>(LegacyInteriorPath) != null &&
            !AssetDatabase.GetAllAssetPaths().Where(p => p != LegacyInteriorPath &&
                new[]{".unity",".prefab",".asset",".fbx",".mat"}.Contains(Path.GetExtension(p)))
                .Any(p=>AssetDatabase.GetDependencies(p,false).Contains(LegacyInteriorPath)))
            AssetDatabase.DeleteAsset(LegacyInteriorPath);
        Selection.activeGameObject = mf.gameObject; SceneView.RepaintAll();
        Debug.Log("DragonGargoyle scene mesh replaced and saved; existing transform/components retained.");
    }

    [MenuItem("Tools/Art Assets/Dragon Gargoyle VAT/Preview/Rest")]
    static void Rest() => Preview(0);
    [MenuItem("Tools/Art Assets/Dragon Gargoyle VAT/Preview/Mid Collapse")]
    static void Middle() => Preview(50.25f/109);
    [MenuItem("Tools/Art Assets/Dragon Gargoyle VAT/Preview/Collapsed")]
    static void End() => Preview(1);
    public static void Preview(float progress)
    {
        foreach (string path in new[] {SurfacePath})
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) throw new InvalidOperationException("Configure the VAT assets first.");
            Undo.RecordObject(mat,"Preview Gargoyle Collapse"); mat.SetFloat("_VATProgress",Mathf.Clamp01(progress));
            EditorUtility.SetDirty(mat); AssetDatabase.SaveAssetIfDirty(mat);
        }
        foreach (var player in UnityEngine.Object.FindObjectsOfType<RigidVATPlayer>(true))
            if (player.name == "DragonGargoyle")
            {
                Undo.RecordObject(player, "Preview Gargoyle Collapse"); player.SetProgress(progress);
                EditorUtility.SetDirty(player);
                EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            }
        SceneView.RepaintAll();
    }
}
