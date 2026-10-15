using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RetroRendering;

public static class RigidVATAssetImport
{
    public const string ShaderName = "Rendering/Gargoyle Cracks Deferred";
    public static Vector3 Vector(JToken a) => new Vector3((float)a[0], (float)a[1], (float)a[2]);
    public static Vector3 ToUnity(Vector3 v) => new Vector3(v.x, v.z, v.y);
    public static void Configure(string manifestPath, string sourceMaterialPath = null, bool createPrefab = true)
    {
        if (!manifestPath.StartsWith("Assets/") || !manifestPath.EndsWith("_vat.json"))
            throw new ArgumentException("Select a VAT manifest inside Assets.");
        string stem = manifestPath.Substring(0,manifestPath.Length-"_vat.json".Length);
        string meshPath = stem+"_Unity.asset", surfacePath = stem+"_Surface.mat";
        var data = JObject.Parse(File.ReadAllText(manifestPath));
        ValidatePublishedFiles(stem,data);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        if ((string)data["schema"] != "statue-rigid-vat-v1" || (int)data["frame_count"] < 1 ||
            (int)data["piece_count"] > (int)data["texture"]["width"] ||
            (int)data["frame_count"] > (int)data["texture"]["height"])
            throw new InvalidOperationException("Unsupported or invalid rigid VAT package.");
        foreach (string suffix in new[] {"_position.exr", "_rotation.exr"}) ConfigureTexture(stem + suffix, data);
        var shader = Shader.Find(ShaderName);
        if (shader == null) throw new InvalidOperationException("Gargoyle shader is missing.");
        var original = ResolveSourceMaterial(manifestPath,data,sourceMaterialPath);
        if (original == null) throw new InvalidOperationException("Original surface material is missing.");
        var surface = AssetDatabase.LoadAssetAtPath<Material>(surfacePath);
        if (surface == null)
        {
            surface = new Material(original) {name = Path.GetFileName(stem)+"_Surface"};
            surface.SetColor("_InteriorColor", new Color(.09f,.105f,.12f,1).gamma);
            surface.SetFloat("_InteriorMetallic",0);surface.SetFloat("_InteriorSmoothness",.08f);
            AssetDatabase.CreateAsset(surface,surfacePath);
        }
        surface.name = Path.GetFileName(stem)+"_Surface";
        surface.SetFloat("_UseSurfaceMask",1);
        Bind(surface,data,stem);
        var importer = (ModelImporter)AssetImporter.GetAtPath(stem + ".fbx");
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
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(stem + ".fbx");
        var filters = model.GetComponentsInChildren<MeshFilter>();
        if (filters.Length != 1) throw new InvalidOperationException("Rigid VAT package must contain one bind mesh.");
        var mf = filters[0]; var source = mf.sharedMesh;
        var transform = mf.transform.localToWorldMatrix;
        // Unity keeps the Blender FBX centimeter/root-axis correction on the node.
        // Bake it into a standalone mesh so the existing statue transform is kept.
        var mesh = UnityEngine.Object.Instantiate(source); mesh.name = Path.GetFileName(stem)+"_Unity";
        mesh.vertices = source.vertices.Select(transform.MultiplyPoint3x4).ToArray();
        var normalTransform = transform.inverse.transpose;
        mesh.normals = source.normals.Select(n => normalTransform.MultiplyVector(n).normalized).ToArray();
        mesh.tangents = source.tangents.Select(t => {
            var v = transform.MultiplyVector(new Vector3(t.x,t.y,t.z)).normalized;
            return new Vector4(v.x,v.y,v.z,t.w * Mathf.Sign(transform.determinant));
        }).ToArray();
        PrepareUVChannels(mesh,data);
        if (transform.determinant < 0)
            for (int s = 0; s < mesh.subMeshCount; ++s)
            { var indices = mesh.GetTriangles(s); for (int i=0;i<indices.Length;i+=3) (indices[i+1],indices[i+2])=(indices[i+2],indices[i+1]); mesh.SetTriangles(indices,s,false); }
        var bounds = new Bounds();
        bounds.SetMinMax(ToUnity(Vector(data["animation_bounds"]["min"])), ToUnity(Vector(data["animation_bounds"]["max"])));
        // Export bounds include .003m padding; add a margin for half-float decode error.
        bounds.Expand(.004f); mesh.bounds = bounds;
        ValidateChannels(mesh, data);
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (existing == null) AssetDatabase.CreateAsset(mesh, meshPath);
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
        if (createPrefab) UpdatePrefab(stem,data);
        AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<Mesh>(meshPath));
        AssetDatabase.SaveAssetIfDirty(surface);
        Debug.Log(stem+" VAT configured: bind mesh node transform baked; UV0–3 preserved; RGBAHalf point/clamp textures; full animation bounds.");
    }

    static void ValidatePublishedFiles(string stem, JObject data)
    {
        // Blender publishes the manifest last. Hashes prevent importing mixed
        // files if Unity notices an export while it is being replaced.
        var hashes = data["unity_export"]?["runtime_files_sha256"] as JObject;
        if (hashes == null) return; // Earlier manually copied packages remain supported.
        foreach (string suffix in new[]{".fbx", "_position.exr", "_rotation.exr"})
        {
            string path = stem + suffix, name = Path.GetFileName(path);
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

    static void PrepareUVChannels(Mesh mesh,JObject data)
    {
        // The shader always uses Unity UV1/2/3. Blender may have several original
        // UV sets; keep UV0 and move extra originals to UV4+ without losing them.
        var names=data["uv_channels"].Select(c=>(string)c["name"]).ToArray();
        int lookup=Array.IndexOf(names,"VAT_UV"), xy=Array.IndexOf(names,"VAT_PIVOT_XY"), zid=Array.IndexOf(names,"VAT_PIVOT_Z_ID");
        if(lookup<1 || xy!=lookup+1 || zid!=lookup+2 || names.Length>8)
            throw new InvalidOperationException("Unsupported VAT UV layout.");
        var channels=new System.Collections.Generic.List<Vector2>[names.Length];
        for(int i=0;i<names.Length;++i)
        { channels[i]=new System.Collections.Generic.List<Vector2>();mesh.GetUVs(i,channels[i]); }
        mesh.SetUVs(1,channels[lookup]);mesh.SetUVs(2,channels[xy]);mesh.SetUVs(3,channels[zid]);
        for(int i=1;i<lookup;++i)mesh.SetUVs(i+3,channels[i]);
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
            // VAT channels are numeric transforms, never color textures. Keep explicit
            // full-precision, uncompressed settings on mobile as well as desktop.
            var p = t.GetPlatformTextureSettings(platform);
            p.overridden = true; p.format = settings.format; p.maxTextureSize = maxSize;
            p.textureCompression = TextureImporterCompression.Uncompressed;
            p.crunchedCompression = false; p.allowsAlphaSplitting = false;
            t.SetPlatformTextureSettings(p);
        }
        t.SaveAndReimport();
    }

    [MenuItem("Tools/Art Assets/Statue VAT/Verify Uncompressed Platform Textures")]
    public static void ConfigureAndValidateDataTextures()
    {
        int count = 0;
        foreach (var manifest in Directory.GetFiles("Assets", "*_vat.json", SearchOption.AllDirectories))
        {
            var data = JObject.Parse(File.ReadAllText(manifest));
            if ((string)data["schema"] != "statue-rigid-vat-v1") continue;
            var stem = manifest.Substring(0, manifest.Length - "_vat.json".Length);
            var expected = (int)data["texture"]["component_bits"] == 32
                ? TextureFormat.RGBAFloat : TextureFormat.RGBAHalf;
            foreach (var suffix in new[] { "_position.exr", "_rotation.exr" })
            {
                var path = stem + suffix;
                ConfigureTexture(path, data);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null || texture.format != expected ||
                    texture.width != (int)data["texture"]["width"] ||
                    texture.height != (int)data["texture"]["height"] || texture.mipmapCount != 1)
                    throw new InvalidOperationException("VAT data precision or dimensions changed: " + path);
                Debug.Log($"VAT_DATA_VERIFIED: {path}: {texture.width}x{texture.height} {texture.format}, uncompressed, linear, point, no mipmaps");
                count++;
            }
        }
        Debug.Log($"VAT_DATA_VERIFIED: {count} textures for {EditorUserBuildSettings.activeBuildTarget}");
    }

    public static void Bind(Material mat, JObject data, string stem)
    {
        mat.shader = Shader.Find(ShaderName); mat.shaderKeywords = Array.Empty<string>();
        mat.SetTexture("_VATPosition", AssetDatabase.LoadAssetAtPath<Texture2D>(stem + "_position.exr"));
        mat.SetTexture("_VATRotation", AssetDatabase.LoadAssetAtPath<Texture2D>(stem + "_rotation.exr"));
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

    static Material ResolveSourceMaterial(string manifestPath,JObject data,string explicitPath)
    {
        string path = explicitPath ?? (string)data["unity_export"]?["source_material"];
        if (!string.IsNullOrEmpty(path)) return AssetDatabase.LoadAssetAtPath<Material>(path);
        string root = Path.GetDirectoryName(Path.GetDirectoryName(manifestPath)).Replace('\\','/');
        var names = data["source_materials"].Values<string>().ToArray();
        var matches = AssetDatabase.FindAssets("t:Material",new[]{root}).Select(AssetDatabase.GUIDToAssetPath)
            .Where(p=>names.Contains(Path.GetFileNameWithoutExtension(p))).ToArray();
        if (matches.Length!=1) throw new InvalidOperationException("Set the Unity source material in Blender, or provide one uniquely named material beside the VAT folder.");
        return AssetDatabase.LoadAssetAtPath<Material>(matches[0]);
    }

    static void UpdatePrefab(string stem,JObject data)
    {
        string path = stem+"_VAT.prefab";
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path)!=null;
        GameObject root = null;
        var preview = default(UnityEngine.SceneManagement.Scene);
        try
        {
            if (exists) root = PrefabUtility.LoadPrefabContents(path);
            else
            {
                preview = EditorSceneManager.NewPreviewScene();
                root = EditorUtility.CreateGameObjectWithHideFlags(Path.GetFileName(stem),HideFlags.HideAndDontSave);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,preview);
                root.hideFlags = HideFlags.None;
            }
            var filter = root.GetComponent<MeshFilter>();
            if(filter==null)filter=root.AddComponent<MeshFilter>();
            var renderer = root.GetComponent<MeshRenderer>();
            if(renderer==null)renderer=root.AddComponent<MeshRenderer>();
            filter.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(stem+"_Unity.asset");
            renderer.sharedMaterials = new[]{AssetDatabase.LoadAssetAtPath<Material>(stem+"_Surface.mat")};
            var player = root.GetComponent<RigidVATPlayer>();
            if(player==null)player=root.AddComponent<RigidVATPlayer>();
            player.Configure(((int)data["frame_count"]-1)/(float)data["fps"]);
            PrefabUtility.SaveAsPrefabAsset(root,path,out bool success);
            if (!success) throw new IOException("Could not save VAT prefab: "+path);
        }
        finally
        {
            if (exists && root!=null) PrefabUtility.UnloadPrefabContents(root);
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    [MenuItem("Tools/Art Assets/Statue VAT/Configure Selected Manifest")]
    static void ConfigureSelected() => Configure(AssetDatabase.GetAssetPath(Selection.activeObject));

    [MenuItem("Tools/Art Assets/Statue VAT/Configure Selected Manifest",true)]
    static bool CanConfigureSelected() => AssetDatabase.GetAssetPath(Selection.activeObject).EndsWith("_vat.json");

}
