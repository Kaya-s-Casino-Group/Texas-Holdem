using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CardArt.Editor
{
    [Serializable]
    public sealed class CardPrefabExportSettings
    {
        public CardAtlasData atlas;
        public CardArtProfile artProfile;
        public Shader shader;
        public Mesh mesh;
        public string cardId = "spades_a";
        public string outputFolder = "Assets/CardArt/Prefabs";
        public string outputName = "FourBeastsCard";
        public bool addCollider = true;
        public Texture2D redCrossMap;
        public bool redCrossEnabled;
        public Texture2D sealMap;
        public Texture2D sealNormalMap;
        public float sealNormalScale = 1;
        public float sealEmissionStrength;
        public bool sealEnabled;
        public Color sealColor = new Color(0.65f, 0.08f, 0.04f, 1);
        public Vector4 sealRect = new Vector4(0.30f, 0.62f, 0.45254834f, 0.3016989f);
        public float sealRotation = -12;
    }

    public sealed class CardPrefabCheck
    {
        public readonly List<string> errors = new List<string>();
        public readonly List<string> warnings = new List<string>();
        public Quaternion surfaceRotation;
        public Bounds surfaceBounds;
        public int faceCount;
        public string prefabPath, materialPath;
        public bool CanExport => errors.Count == 0;
    }

    public static class CardPrefabExporter
    {
        static readonly string[] RequiredPasses = { "UniversalGBuffer", "UniversalForward", "ShadowCaster", "DepthOnly", "DepthNormals" };
        static readonly string[] RequiredUnlitPasses = { "UniversalForwardOnly", "DepthOnly" };

        public static CardPrefabCheck Validate(CardPrefabExportSettings settings)
        {
            var check = new CardPrefabCheck();
            if (settings == null) { check.errors.Add("缺少导出配置。"); return check; }
            if (settings.artProfile != null) { try { settings.artProfile.ValidateAtlas(settings.atlas); } catch (Exception exception) { check.errors.Add(exception.Message); } }
            ValidateShader(settings.shader, check.errors);
            if(settings.artProfile!=null && settings.shader!=null)
            {
                var probe=new Material(settings.shader);
                try { ValidateProfileParameters(settings.artProfile,probe); }
                catch(Exception exception) { check.errors.Add(exception.Message); }
                finally { UnityEngine.Object.DestroyImmediate(probe); }
            }
            ValidateAtlas(settings, check);
            ValidateSeal(settings, check);
            ValidateRedCross(settings, check);
            try { ValidateMesh(settings.mesh, check); }
            catch (Exception e) { check.errors.Add("读取 Mesh 失败：" + e.Message); }
            if (settings.atlas != null && settings.atlas.cardSize.x > 0 && settings.atlas.cardSize.y > 0 && check.surfaceBounds.size.z > 0)
            {
                var meshRatio = check.surfaceBounds.size.x / check.surfaceBounds.size.z;
                var cardRatio = (float)settings.atlas.cardSize.x / settings.atlas.cardSize.y;
                if (Mathf.Abs(meshRatio / cardRatio - 1) > 0.01f)
                    check.errors.Add($"Mesh 比例 {meshRatio:0.####} 与图集逻辑单牌比例 {cardRatio:0.####} 不符（容差 1%）。");
            }
            ValidateOutput(settings, check);
            return check;
        }

        static void ValidateRedCross(CardPrefabExportSettings settings, CardPrefabCheck check)
        {
            if (settings.redCrossMap == null && !settings.redCrossEnabled) return;
            if (settings.redCrossMap == null) check.errors.Add("开启红叉时请选择红叉贴图。");
            if (settings.shader != null)
            {
                var probe = new Material(settings.shader);
                try
                {
                    if (!CardShaderProperties.SupportsRedCross(probe))
                        check.errors.Add("Shader 不支持红叉扩展 CardRedCrossContract=OverlayV1。");
                    int mapIndex = settings.shader.FindPropertyIndex("_RedCrossMap");
                    if (mapIndex < 0 || settings.shader.GetPropertyType(mapIndex) != ShaderPropertyType.Texture ||
                        settings.shader.GetPropertyTextureDimension(mapIndex) != TextureDimension.Tex2D)
                        check.errors.Add("_RedCrossMap 必须为 Texture2D。");
                    int enabledIndex = settings.shader.FindPropertyIndex("_RedCrossEnabled");
                    if (enabledIndex < 0 || settings.shader.GetPropertyType(enabledIndex) != ShaderPropertyType.Float)
                        check.errors.Add("_RedCrossEnabled 必须为 Float。");
                    string path = AssetDatabase.GetAssetPath(settings.shader);
                    if (File.Exists(path))
                    {
                        string source = string.Join("\n", ReadShaderSources(path, new HashSet<string>()));
                        if (!Regex.IsMatch(source, @"UNITY_DEFINE_INSTANCED_PROP\s*\(\s*(?:float|half|real)\s*,\s*_RedCrossEnabled\s*\)") ||
                            !Regex.IsMatch(source, @"UNITY_ACCESS_INSTANCED_PROP\s*\([^,]+,\s*_RedCrossEnabled\s*\)"))
                            check.errors.Add("_RedCrossEnabled 必须声明并读取 GPU 实例属性。");
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(probe); }
            }
            var texture = settings.redCrossMap;
            if (texture == null) return;
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
            if (importer == null || importer.textureType == TextureImporterType.NormalMap ||
                importer.alphaSource == TextureImporterAlphaSource.None || !importer.DoesSourceTextureHaveAlpha())
                check.errors.Add("红叉需要已保存的颜色贴图及有效 Alpha 轮廓。");
            if (texture.filterMode != FilterMode.Point || texture.mipmapCount > 1 || texture.wrapMode != TextureWrapMode.Clamp)
                check.errors.Add("红叉贴图必须使用 Point、Clamp 并关闭 mipmap。");
            if (settings.atlas != null && settings.atlas.cardSize.x > 0 && settings.atlas.cardSize.y > 0 &&
                Mathf.Abs(((float)texture.width / texture.height) / ((float)settings.atlas.cardSize.x / settings.atlas.cardSize.y) - 1) > 0.01f)
                check.errors.Add("红叉贴图宽高比例必须与图集单牌比例一致（容差 1%）。");
            if (texture.width > 256 || texture.height > 256)
                check.warnings.Add("红叉贴图建议不超过 256 像素，内置版本为 128×192。");
        }

        static void ValidateSeal(CardPrefabExportSettings settings, CardPrefabCheck check)
        {
            if (float.IsNaN(settings.sealEmissionStrength) || settings.sealEmissionStrength < 0 || settings.sealEmissionStrength > 1)
                check.errors.Add("火漆自发光强度必须在 0–1 范围内。");
            if (settings.sealMap == null && settings.sealNormalMap == null && !settings.sealEnabled && settings.sealEmissionStrength == 0) return;
            if (settings.sealMap == null) check.errors.Add("使用火漆或法线时，请同时选择火漆颜色贴图（Alpha 提供轮廓）。");
            if (settings.shader != null)
            {
                var material = new Material(settings.shader);
                try
                {
                    if (!CardShaderProperties.SupportsSeal(material)) check.errors.Add("Shader 不支持火漆扩展 CardSealContract=ColorNormalV1。");
                    var types = new[] { ("_SealMap", ShaderPropertyType.Texture), ("_SealNormalMap", ShaderPropertyType.Texture),
                        ("_SealColor", ShaderPropertyType.Color), ("_SealEnabled", ShaderPropertyType.Float), ("_SealRect", ShaderPropertyType.Vector) };
                    foreach (var item in types)
                    {
                        int index = settings.shader.FindPropertyIndex(item.Item1);
                        if (index < 0 || settings.shader.GetPropertyType(index) != item.Item2)
                            check.errors.Add("火漆 Shader 属性类型不符：" + item.Item1);
                        else if (item.Item2 == ShaderPropertyType.Texture && settings.shader.GetPropertyTextureDimension(index) != TextureDimension.Tex2D)
                            check.errors.Add(item.Item1 + " 必须是 Texture2D。");
                    }
                    int scaleIndex = settings.shader.FindPropertyIndex("_SealNormalScale");
                    if (scaleIndex < 0 || (settings.shader.GetPropertyType(scaleIndex) != ShaderPropertyType.Float &&
                        settings.shader.GetPropertyType(scaleIndex) != ShaderPropertyType.Range)) check.errors.Add("缺少浮点属性 _SealNormalScale。");
                    int rotationIndex = settings.shader.FindPropertyIndex("_SealRotation");
                    if (rotationIndex >= 0)
                    {
                        var type = settings.shader.GetPropertyType(rotationIndex);
                        if (type != ShaderPropertyType.Float && type != ShaderPropertyType.Range)
                            check.errors.Add("火漆旋转属性 _SealRotation 必须为 Float 或 Range。");
                    }
                    else if (settings.sealRotation != 0)
                        check.errors.Add("Shader 不支持火漆旋转：缺少 _SealRotation（可将旋转设为 0）。");
                    int emissionIndex = settings.shader.FindPropertyIndex("_SealEmissionStrength");
                    if (emissionIndex >= 0)
                    {
                        var type = settings.shader.GetPropertyType(emissionIndex);
                        if (type != ShaderPropertyType.Float && type != ShaderPropertyType.Range)
                            check.errors.Add("火漆自发光属性 _SealEmissionStrength 必须为 Float 或 Range。");
                    }
                    else if (settings.sealEmissionStrength != 0)
                        check.errors.Add("Shader 不支持火漆自发光：缺少 _SealEmissionStrength。");
                    string path = AssetDatabase.GetAssetPath(settings.shader);
                    if (File.Exists(path))
                    {
                        string source = string.Join("\n", ReadShaderSources(path, new HashSet<string>()));
                        foreach (var item in new[] { ("_SealEnabled", "(?:float|half|real)"), ("_SealColor", "(?:float4|half4|real4)"), ("_SealEmissionStrength", "(?:float|half|real)") })
                        {
                            if (item.Item1 == "_SealEmissionStrength" && emissionIndex < 0) continue;
                            if (!Regex.IsMatch(source, @"UNITY_DEFINE_INSTANCED_PROP\s*\(\s*" + item.Item2 + @"\s*,\s*" + item.Item1 + @"\s*\)") ||
                                !Regex.IsMatch(source, @"UNITY_ACCESS_INSTANCED_PROP\s*\([^,]+,\s*" + item.Item1 + @"\s*\)"))
                                check.errors.Add(item.Item1 + " 必须声明并读取 GPU 实例属性。");
                        }
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(material); }
            }
            foreach (var texture in new[] { settings.sealMap, settings.sealNormalMap })
            {
                if (texture == null) continue;
                if (!AssetDatabase.Contains(texture)) check.errors.Add("火漆贴图必须是已保存的项目资产。");
                if (texture.filterMode != FilterMode.Point || texture.mipmapCount > 1)
                    check.warnings.Add(texture.name + " 建议使用 Point 并关闭 mipmap，以保留像素质感。");
            }
            if (settings.sealMap != null)
            {
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(settings.sealMap)) as TextureImporter;
                if (importer == null || importer.textureType == TextureImporterType.NormalMap ||
                    importer.alphaSource == TextureImporterAlphaSource.None || !importer.DoesSourceTextureHaveAlpha())
                    check.errors.Add("火漆颜色贴图需要有效的 Alpha 轮廓，不能作为 Normal Map 导入。");
            }
            if (settings.sealNormalMap != null)
            {
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(settings.sealNormalMap)) as TextureImporter;
                if (importer == null || importer.textureType != TextureImporterType.NormalMap)
                    check.errors.Add("火漆法线贴图必须以 Normal Map 类型导入。");
                if (settings.sealMap != null && settings.sealMap.width * settings.sealNormalMap.height != settings.sealMap.height * settings.sealNormalMap.width)
                    check.errors.Add("火漆颜色与法线贴图的宽高比例必须一致。");
            }
            if (float.IsNaN(settings.sealNormalScale) || settings.sealNormalScale < 0 || settings.sealNormalScale > 2)
                check.errors.Add("火漆法线强度必须在 0–2 范围内。");
            if (float.IsNaN(settings.sealRotation) || settings.sealRotation < -180 || settings.sealRotation > 180)
                check.errors.Add("火漆旋转角度必须在 -180–180 范围内。");
            var r = settings.sealRect;
            if (!(r.z > 0 && r.z <= 1 && r.w > 0 && r.w <= 1 && r.x >= 0 && r.x <= 1 && r.y >= 0 && r.y <= 1))
                check.errors.Add("火漆中心必须在 0–1，宽高必须在 (0,1] 范围内。");
        }

        public static void ValidateShader(Shader shader, List<string> errors)
        {
            if (shader == null) { errors.Add("请选择 Shader。"); return; }
            var probe = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                errors.AddRange(CardShaderProperties.GetContractErrors(probe));
                if (!shader.isSupported) errors.Add("此 Shader 不支持当前图形设备。");
                foreach (var message in ShaderUtil.GetShaderMessages(shader).Where(m => m.severity.ToString() == "Error"))
                    errors.Add("Shader 编译错误：" + message.message);
                var passes = new HashSet<string>();
                for (int i = 0; i < probe.passCount; i++)
                    passes.Add(shader.FindPassTagValue(i, new ShaderTagId("LightMode")).name);

                var materialType = probe.GetTag("UniversalMaterialType", false);
                bool unlit = materialType == "Unlit";
                var requiredPasses = unlit ? RequiredUnlitPasses : RequiredPasses;
                if (!unlit && materialType != "Lit" && materialType != "SimpleLit")
                    errors.Add("UniversalMaterialType 必须为 Lit、SimpleLit 或 Unlit；受光卡牌使用 GBuffer，无光照卡牌使用 ForwardOnly。");
                if (probe.renderQueue > (int)RenderQueue.AlphaTest)
                    errors.Add("卡牌必须使用不透明 / AlphaTest 队列，不能使用透明队列。");

                var path = AssetDatabase.GetAssetPath(shader);
                if (!path.EndsWith(".shader", StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
                {
                    errors.Add("需要可读取的 ShaderLab .shader 资产，以校验实例化声明。");
                    return;
                }
                var source = StripComments(File.ReadAllText(path));
                var sources = ReadShaderSources(path, new HashSet<string>());
                var includes = string.Join("\n", sources);
                var bodies = PassBodies(source).ToArray();
                // Unity treats ShadowCaster as a special pass; its LightMode can be absent
                // from FindPassTagValue. Match its declared tag to an imported named pass.
                foreach (var body in bodies)
                {
                    var tag = Regex.Match(body, "\"LightMode\"\\s*=\\s*\"([^\"]+)\"");
                    var name = Regex.Match(body, "\\bName\\s*\"([^\"]+)\"");
                    if (tag.Success && tag.Groups[1].Value == "ShadowCaster" && name.Success && probe.FindPass(name.Groups[1].Value) >= 0)
                        passes.Add("ShadowCaster");
                }
                foreach (var pass in requiredPasses)
                    if (!passes.Contains(pass)) errors.Add("缺少 LightMode = " + pass + " 的 Pass。");
                foreach (var property in new[] { "_FrontUVRect", "_ColorTint" })
                {
                    if (!Regex.IsMatch(source + "\n" + includes, @"UNITY_DEFINE_INSTANCED_PROP\s*\(\s*(?:float4|half4|real4)\s*,\s*" + property + @"\s*\)"))
                        errors.Add(property + " 必须声明为 UNITY_DEFINE_INSTANCED_PROP(float4, " + property + ")。");
                    if (!Regex.IsMatch(source + "\n" + includes, @"UNITY_ACCESS_INSTANCED_PROP\s*\([^,]+,\s*" + property + @"\s*\)"))
                        errors.Add("Shader 必须通过 UNITY_ACCESS_INSTANCED_PROP 读取实例 " + property + "。");
                }
                foreach (var body in bodies)
                {
                    var tag = Regex.Match(body, "\"LightMode\"\\s*=\\s*\"([^\"]+)\"");
                    if (!tag.Success || !(requiredPasses.Contains(tag.Groups[1].Value) || unlit && tag.Groups[1].Value == "Universal2D")) continue;
                    var hasInstancing = Regex.IsMatch(body, @"#\s*pragma\s+multi_compile_instancing\b");
                    if (!hasInstancing)
                    {
                        foreach (Match include in Regex.Matches(body, "#\\s*include_with_pragmas\\s+\"([^\"]+)\""))
                        {
                            var includePath = ResolveInclude(path, include.Groups[1].Value);
                            if (includePath != null && Regex.IsMatch(StripComments(File.ReadAllText(includePath)), @"#\s*pragma\s+multi_compile_instancing\b")) hasInstancing = true;
                        }
                    }
                    if (!hasInstancing) errors.Add(tag.Groups[1].Value + " Pass 缺少 multi_compile_instancing。");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(probe); }
        }

        public static void ValidateProfileParameters(CardArtProfile profile, Material material)
        {
            if(profile==null || material==null) throw new ArgumentException("缺少美术配置或材质。");
            var sources=string.Join("\n",ReadShaderSources(AssetDatabase.GetAssetPath(material.shader),new HashSet<string>()));
            foreach(var combination in profile.Combinations)
            {
                profile.Resolve(new[]{combination.Id},Color.white).ValidateFor(material);
                foreach(var parameter in combination.Parameters)
                {
                    string name=Regex.Escape(parameter.Name);
                    string type=parameter.Kind==CardShaderValueKind.Float ? "(?:float|half|real)" : "(?:float4|half4|real4)";
                    if(!Regex.IsMatch(sources,@"UNITY_DEFINE_INSTANCED_PROP\s*\(\s*"+type+@"\s*,\s*"+name+@"\s*\)") ||
                        !Regex.IsMatch(sources,@"UNITY_ACCESS_INSTANCED_PROP\s*\([^,]+,\s*"+name+@"\s*\)"))
                        throw new ArgumentException("组合参数必须在 Shader 中声明并读取实例值："+parameter.Name);
                }
            }
        }

        static List<string> ReadShaderSources(string path, HashSet<string> visited)
        {
            var sources = new List<string>();
            var fullPath = Path.GetFullPath(path);
            if (!visited.Add(fullPath) || !File.Exists(fullPath)) return sources;
            var source = StripComments(File.ReadAllText(fullPath));
            sources.Add(source);
            foreach (Match include in Regex.Matches(source, "#\\s*include(?:_with_pragmas)?\\s*[\"<]([^\">]+)[\">]"))
            {
                var resolved = ResolveInclude(fullPath, include.Groups[1].Value);
                if (resolved != null) sources.AddRange(ReadShaderSources(resolved, visited));
            }
            return sources;
        }

        static string ResolveInclude(string sourcePath, string include)
        {
            var relative = Path.Combine(Path.GetDirectoryName(sourcePath), include);
            if (File.Exists(relative)) return relative;
            if (File.Exists(include)) return include;
            if (include.StartsWith("Packages/", StringComparison.Ordinal))
            {
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(include);
                if (package != null)
                {
                    var resolved = Path.Combine(package.resolvedPath, include.Substring(("Packages/" + package.name + "/").Length));
                    if (File.Exists(resolved)) return resolved;
                }
            }
            var builtin = Path.Combine(EditorApplication.applicationContentsPath, "CGIncludes", include);
            return File.Exists(builtin) ? builtin : null;
        }

        static string StripComments(string source) => Regex.Replace(source, @"/\*[\s\S]*?\*/|//[^\r\n]*", "");

        static IEnumerable<string> PassBodies(string source)
        {
            foreach (Match match in Regex.Matches(source, @"\bPass\s*\{"))
            {
                var start = match.Index + match.Length;
                var depth = 1;
                var quoted = false;
                for (int i = start; i < source.Length; i++)
                {
                    if (source[i] == '"' && source[i - 1] != '\\') quoted = !quoted;
                    if (quoted) continue;
                    if (source[i] == '{') depth++;
                    if (source[i] == '}' && --depth == 0) { yield return source.Substring(start, i - start); break; }
                }
            }
        }

        static void ValidateAtlas(CardPrefabExportSettings settings, CardPrefabCheck check)
        {
            var atlas = settings.atlas;
            if (atlas == null) { check.errors.Add("请选择图集 Data。"); return; }
            if (!AssetDatabase.Contains(atlas)) check.errors.Add("图集 Data 必须保存为项目资产。");
            if (atlas.texture == null || !AssetDatabase.Contains(atlas.texture)) check.errors.Add("图集必须引用项目中的 Texture2D 资产。");
            if (atlas.cardSize.x <= 0 || atlas.cardSize.y <= 0) check.errors.Add("图集逻辑单牌尺寸必须大于零。");
            if (atlas.cards == null || atlas.cards.Count == 0) { check.errors.Add("图集没有卡牌区域。"); return; }
            if (atlas.cards.Any(c => c == null || string.IsNullOrWhiteSpace(c.id))) check.errors.Add("图集含空区域或空卡牌 ID。");
            var cards = atlas.cards.Where(c => c != null).ToList();
            foreach (var duplicate in cards.GroupBy(c => c.id).Where(g => g.Count() > 1)) check.errors.Add("图集重复 ID：" + duplicate.Key);
            var back = cards.FirstOrDefault(c => c.id == "card_back");
            if (back == null || back.clipped) check.errors.Add("图集必须包含完整的 card_back。");
            var face = cards.FirstOrDefault(c => c.id == settings.cardId && c.id != "card_back");
            if (face == null || face.clipped) check.errors.Add("默认牌面不存在或被裁切，请选择完整的正面区域。");
            foreach (var card in cards.Where(c => !c.clipped))
            {
                var uv = card.uv;
                if (!Finite(uv.x) || !Finite(uv.y) || !Finite(uv.z) || !Finite(uv.w) ||
                    uv.z <= 0 || uv.w <= 0 || uv.x < -0.00001f || uv.y < -0.00001f || uv.x + uv.z > 1.00001f || uv.y + uv.w > 1.00001f)
                    check.errors.Add("无效 UV Rect：" + card.id + "（xy 偏移 / zw 缩放须在图集内）。");
            }
            check.faceCount = cards.Count(c => c.id != "card_back" && !c.clipped);
            if (cards.Any(c => c.clipped)) check.warnings.Add("被裁切的区域不能作为完整卡牌，默认牌面列表已排除这些区域。");
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);

        static void ValidateMesh(Mesh mesh, CardPrefabCheck check)
        {
            if (mesh == null) { check.errors.Add("请选择 Mesh。"); return; }
            if (!AssetDatabase.Contains(mesh)) check.errors.Add("Mesh 必须是项目中的持久化资产。");
            if (mesh.subMeshCount != 1 || mesh.GetTopology(0) != MeshTopology.Triangles)
            { check.errors.Add("Mesh 必须只有一个三角形子网格 / 材质槽。"); return; }
            foreach (var attribute in new[] { VertexAttribute.Normal, VertexAttribute.TexCoord0, VertexAttribute.TexCoord1 })
                if (!mesh.HasVertexAttribute(attribute)) check.errors.Add("Mesh 缺少 " + attribute + "。");
            if (!mesh.HasVertexAttribute(VertexAttribute.Normal) || !mesh.HasVertexAttribute(VertexAttribute.TexCoord0) || !mesh.HasVertexAttribute(VertexAttribute.TexCoord1)) return;
            if (mesh.GetVertexAttributeDimension(VertexAttribute.TexCoord0) != 2 || mesh.GetVertexAttributeDimension(VertexAttribute.TexCoord1) != 2)
            { check.errors.Add("UV0 和 UV1 必须是二维数据。"); return; }
            using (var data = MeshUtility.AcquireReadOnlyMeshData(mesh))
            using (var vertices = new NativeArray<Vector3>(mesh.vertexCount, Allocator.Temp))
            using (var normals = new NativeArray<Vector3>(mesh.vertexCount, Allocator.Temp))
            using (var uv = new NativeArray<Vector2>(mesh.vertexCount, Allocator.Temp))
            using (var flags = new NativeArray<Vector2>(mesh.vertexCount, Allocator.Temp))
            using (var indices = new NativeArray<int>((int)mesh.GetIndexCount(0), Allocator.Temp))
            {
                data[0].GetVertices(vertices); data[0].GetNormals(normals);
                data[0].GetUVs(0, uv); data[0].GetUVs(1, flags); data[0].GetIndices(indices, 0);
                if (mesh.vertexCount == 0 || indices.Length == 0) { check.errors.Add("Mesh 没有几何数据。"); return; }
                var kinds = new int[mesh.vertexCount];
                for (int i = 0; i < mesh.vertexCount; i++)
                {
                    kinds[i] = (flags[i] - Vector2.zero).sqrMagnitude < 0.00000001f ? 0 :
                        (flags[i] - Vector2.right).sqrMagnitude < 0.00000001f ? 1 :
                        (flags[i] - Vector2.up).sqrMagnitude < 0.00000001f ? 2 : -1;
                    if (kinds[i] < 0 || !Finite(vertices[i]) || !Finite(normals[i]) || normals[i].sqrMagnitude < 0.1f ||
                        !Finite(uv[i].x) || !Finite(uv[i].y) || uv[i].x < -0.00001f || uv[i].y < -0.00001f || uv[i].x > 1.00001f || uv[i].y > 1.00001f)
                    { check.errors.Add("Mesh 含无效坐标 / 法线 / UV，或 UV1 不符合 (0,0) 正面、(1,0) 背面、(0,1) 侧边的约定。"); return; }
                }
                if (!kinds.Contains(0) || !kinds.Contains(1) || !kinds.Contains(2))
                { check.errors.Add("Mesh 必须包含正面、背面和实体侧边。"); return; }
                for (int i = 0; i < indices.Length; i += 3)
                    if (kinds[indices[i]] != kinds[indices[i + 1]] || kinds[indices[i]] != kinds[indices[i + 2]])
                    { check.errors.Add("同一三角形的 UV1 表面标识必须一致，避免在片元中插值错判表面。"); return; }
                var front = Enumerable.Range(0, mesh.vertexCount).Where(i => kinds[i] == 0).ToArray();
                var top = front.Where(i => uv[i].y > 0.9f).ToArray();
                var bottom = front.Where(i => uv[i].y < 0.1f).ToArray();
                var left = front.Where(i => uv[i].x < 0.1f).ToArray();
                var right = front.Where(i => uv[i].x > 0.9f).ToArray();
                if (top.Length == 0 || bottom.Length == 0 || left.Length == 0 || right.Length == 0)
                { check.errors.Add("正面 UV0 必须覆盖 0–1 范围。"); return; }
                Vector3 Average(int[] ids) => ids.Aggregate(Vector3.zero, (sum, i) => sum + vertices[i]) / ids.Length;
                var normal = front.Aggregate(Vector3.zero, (sum, i) => sum + normals[i]).normalized;
                var up = Average(top) - Average(bottom);
                if (normal.sqrMagnitude < 0.9f || up.sqrMagnitude < 0.000000000001f || Mathf.Abs(Vector3.Dot(up.normalized, normal)) > 0.01f)
                { check.errors.Add("Mesh 正面法线或 UV 上方向退化，无法统一到 XZ 平面。"); return; }
                check.surfaceRotation = Quaternion.Inverse(Quaternion.LookRotation(up, normal));
                if ((check.surfaceRotation * (Average(right) - Average(left))).x <= 0)
                { check.errors.Add("正面 UV0 被镜像，请修正模型 UV。"); return; }
                var bounds = new Bounds(check.surfaceRotation * vertices[0], Vector3.zero);
                for (int i = 1; i < mesh.vertexCount; i++) bounds.Encapsulate(check.surfaceRotation * vertices[i]);
                check.surfaceBounds = bounds;
                if (bounds.size.x <= 0 || bounds.size.y <= 0 || bounds.size.z <= 0) check.errors.Add("Mesh 必须有宽、高和实际厚度。");
            }
        }

        static void ValidateOutput(CardPrefabExportSettings settings, CardPrefabCheck check)
        {
            try
            {
                var name = settings.outputName;
                if (string.IsNullOrWhiteSpace(name) || name != name.Trim() || name == "." || name == ".." ||
                    name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.IndexOfAny("<>:\"|?*/\\".ToCharArray()) >= 0)
                    throw new ArgumentException("Prefab 名称必须是有效的单个文件名，不要包含扩展名或路径。");
                if (name.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("名称不需要填写 .prefab / .mat 扩展名。");
                if (string.IsNullOrWhiteSpace(settings.outputFolder)) throw new ArgumentException("请选择导出目录。");
                var assets = Path.GetFullPath(Path.Combine(CardAtlasBuilder.ProjectRoot, "Assets"));
                var folder = Path.GetFullPath(CardAtlasBuilder.Absolute(settings.outputFolder));
                if (folder != assets && !folder.StartsWith(assets + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    throw new ArgumentException("Prefab 和材质必须导出到项目 Assets 内。");
                for (var parent = folder; parent != null; parent = Path.GetDirectoryName(parent))
                {
                    if (File.Exists(parent)) throw new ArgumentException("导出目录或其父级指向一个文件：" + parent);
                    if (parent == assets) break;
                }
                var relative = "Assets" + folder.Substring(assets.Length).Replace('\\', '/');
                check.prefabPath = relative + "/" + name + ".prefab";
                check.materialPath = relative + "/" + name + ".mat";
                CheckExisting(check.prefabPath, typeof(GameObject), check);
                CheckExisting(check.materialPath, typeof(Material), check);
            }
            catch (Exception e) { check.errors.Add(e.Message); }
        }

        static void CheckExisting(string path, Type type, CardPrefabCheck check)
        {
            if (!File.Exists(path) && !Directory.Exists(path)) return;
            var existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing == null || !type.IsInstanceOfType(existing)) check.errors.Add("导出路径已有其他类型 / 无法读取的资产：" + path);
        }

        public static GameObject Export(CardPrefabExportSettings settings, bool overwrite = false)
        {
            var check = Validate(settings);
            if (!check.CanExport) throw new ArgumentException(string.Join("\n", check.errors));
            var exists = File.Exists(check.prefabPath) || File.Exists(check.materialPath);
            if (exists && !overwrite) throw new InvalidOperationException("已有同名 Prefab / 材质，需明确选择覆盖。");
            var generated = new Material(settings.shader) { name = settings.outputName };
            var previewScene = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            Material material = null, backup = null;
            var newMaterial = false;
            try
            {
                CardShaderProperties.ConfigureSkin(generated, settings.atlas);
                if (CardShaderProperties.SupportsSeal(generated))
                {
                    generated.SetTexture(CardShaderProperties.SealMap, settings.sealMap);
                    generated.SetTexture(CardShaderProperties.SealNormalMap, settings.sealNormalMap);
                    generated.SetFloat(CardShaderProperties.SealNormalScale, settings.sealNormalMap != null ? settings.sealNormalScale : 0);
                    generated.SetVector(CardShaderProperties.SealRect, settings.sealRect);
                    if (generated.HasProperty(CardShaderProperties.SealRotation))
                        generated.SetFloat(CardShaderProperties.SealRotation, settings.sealRotation);
                }
                if (CardShaderProperties.SupportsRedCross(generated))
                    generated.SetTexture(CardShaderProperties.RedCrossMap, settings.redCrossMap);
                Directory.CreateDirectory(Path.GetDirectoryName(check.prefabPath));
                AssetDatabase.Refresh();
                material = AssetDatabase.LoadAssetAtPath<Material>(check.materialPath);
                if (material == null)
                {
                    material = generated;
                    AssetDatabase.CreateAsset(material, check.materialPath);
                    newMaterial = true;
                }
                else
                {
                    backup = new Material(material);
                    EditorUtility.CopySerialized(generated, material);
                    EditorUtility.SetDirty(material);
                }
                root = new GameObject(settings.outputName);
                SceneManager.MoveGameObjectToScene(root, previewScene);
                var surface = new GameObject("Surface");
                surface.transform.SetParent(root.transform, false);
                surface.transform.localRotation = check.surfaceRotation;
                surface.transform.localPosition = -check.surfaceBounds.center;
                var view = surface.AddComponent<CardView>();
                view.Bind(settings.mesh, material, settings.atlas, settings.cardId);
                view.SetArtProfile(settings.artProfile);
                view.SetSealColor(settings.sealColor);
                view.SetSealEmissionStrength(settings.sealEmissionStrength);
                view.SetSealEnabled(settings.sealEnabled);
                view.SetRedCrossEnabled(settings.redCrossEnabled);
                if (settings.addCollider)
                {
                    var collider = root.AddComponent<BoxCollider>();
                    var size = check.surfaceBounds.size;
                    size.y = Mathf.Max(size.y, 0.001f);
                    collider.size = size;
                }
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, check.prefabPath, out var success);
                if (!success || prefab == null) throw new IOException("Prefab 保存失败：" + check.prefabPath);
                AssetDatabase.SaveAssetIfDirty(material);
                return prefab;
            }
            catch
            {
                if (newMaterial) AssetDatabase.DeleteAsset(check.materialPath);
                else if (backup != null) { EditorUtility.CopySerialized(backup, material); EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material); }
                throw;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
                if (generated != material) UnityEngine.Object.DestroyImmediate(generated);
                if (backup != null) UnityEngine.Object.DestroyImmediate(backup);
            }
        }
    }
}
