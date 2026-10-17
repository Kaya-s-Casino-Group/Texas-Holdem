using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace CardArt.Editor
{
    public sealed class CardAtlasLayout
    {
        public Vector2Int size;
        public int columns;
        public int rows;
        public Vector2 gridSize;
        public Vector2 scale;
        public List<Rect> cardRects = new List<Rect>();
        public List<Rect> cellRects = new List<Rect>();
        public List<CardAtlasEntry> entries;
        public List<CardAtlasRegion> cards = new List<CardAtlasRegion>();
    }

    public sealed class CardAtlasBuild : IDisposable
    {
        public Texture2D texture;
        public int columns;
        public int rows;
        public List<CardAtlasRegion> cards = new List<CardAtlasRegion>();
        public void Dispose() { if (texture != null) UnityEngine.Object.DestroyImmediate(texture); }
    }

    [Serializable]
    public sealed class CardAtlasManifest
    {
        public int version = 2;
        public string texture;
        public string coordinates = "bottom-left; uv.xy=offset, uv.zw=scale; indices run left-to-right, top-to-bottom";
        public Vector2Int atlasSize;
        public Vector2Int cardSize;
        public int columns;
        public int rows;
        public int padding;
        public string filterMode;
        public string wrapMode;
        public CardAtlasSettings settings;
        public List<CardAtlasRegion> cards;
    }

    public static class CardAtlasBuilder
    {
        public const string ImporterTag = "CardAtlasGenerated";
        public static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
        public static string Absolute(string path) => Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(ProjectRoot, path));

        public static string ProjectPath(string path)
        {
            var full = Absolute(path).Replace('\\', '/');
            var root = ProjectRoot.Replace('\\', '/') + "/";
            return full.StartsWith(root, StringComparison.Ordinal) ? full.Substring(root.Length) : full;
        }

        public static bool IsImage(string path)
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();
            return extension == ".png" || extension == ".jpg" || extension == ".jpeg";
        }

        public static void Scan(CardAtlasRecipe recipe)
        {
            var folder = Absolute(recipe.sourceFolder);
            if (!Directory.Exists(folder)) throw new InvalidOperationException("卡牌目录不存在。");
            var paths = Directory.GetFiles(folder, "*", recipe.recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                .Where(IsImage).OrderBy(path => path, StringComparer.Ordinal).Select(ProjectPath).ToList();
            var available = new HashSet<string>(paths, StringComparer.Ordinal);
            // Retain the existing order and replacements; append new cards deterministically.
            var cards = recipe.cards.Where(card => available.Contains(card.sourcePath)).ToList();
            var retained = new HashSet<string>(cards.Select(card => card.sourcePath), StringComparer.Ordinal);
            foreach (var path in paths.Where(path => !retained.Contains(path)))
            {
                var relative = Absolute(path).Substring(folder.TrimEnd(Path.DirectorySeparatorChar).Length + 1);
                cards.Add(new CardAtlasEntry { id = Path.ChangeExtension(relative, null).Replace('\\', '/'), sourcePath = path });
            }
            recipe.cards = cards;
            EditorUtility.SetDirty(recipe);
        }

        public static Vector2Int GetSize(CardAtlasSettings settings, int count)
        {
            if (count <= 0) throw new InvalidOperationException("请先扫描目录并启用至少一张卡牌。");
            if (settings.cardWidth < 1 || settings.cardHeight < 1 || settings.columns < 1 || settings.padding < 0)
                throw new InvalidOperationException("卡牌尺寸、列数必须大于零，留边不能小于零。");
            if (settings.cardWidth > 4096 || settings.cardHeight > 4096 || settings.columns > 256 || settings.padding > 128)
                throw new InvalidOperationException("卡牌尺寸上限 4096、列数上限 256、留边上限 128。");
            var limit = Math.Min(8192, SystemInfo.maxTextureSize);
            if (settings.maxSize < 1 || settings.maxSize > limit)
                throw new InvalidOperationException($"图集边长必须在 1–{limit} 之间。");
            var side = settings.powerOfTwo ? Mathf.NextPowerOfTwo(settings.maxSize) : settings.maxSize;
            if (side > limit) throw new InvalidOperationException($"取二次幂后的边长 {side} 超过 {limit} 上限。");
            return new Vector2Int(side, side);
        }

        public static Texture2D ReadSource(string path)
        {
            if (!IsImage(path)) throw new InvalidOperationException("当前支持 PNG、JPG、JPEG：" + path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(Absolute(path)), false))
                    throw new InvalidOperationException("图片无法解码：" + path);
                texture.hideFlags = HideFlags.HideAndDontSave;
                return texture;
            }
            catch { UnityEngine.Object.DestroyImmediate(texture); throw; }
        }

        public static CardAtlasBuild Build(CardAtlasRecipe recipe, bool showProgress = false)
        {
            var layout = CreateLayout(recipe);
            var entries = layout.entries;
            if (entries.Any(card => string.IsNullOrWhiteSpace(card.id))) throw new InvalidOperationException("卡牌 ID 不能为空。");
            var duplicate = entries.GroupBy(card => card.id, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
            if (duplicate != null) throw new InvalidOperationException("卡牌 ID 重复：" + duplicate.Key);
            var settings = recipe.settings;
            var size = layout.size;
            var result = new CardAtlasBuild { columns = layout.columns, rows = layout.rows, cards = layout.cards };
            var pixels = new Color32[size.x * size.y];
            try
            {
                for (var i = 0; i < entries.Count; i++)
                {
                    if (showProgress && EditorUtility.DisplayCancelableProgressBar("构建卡牌图集", entries[i].id, (float)i / entries.Count))
                        throw new OperationCanceledException("已取消构建。");
                    Texture2D source = null;
                    try
                    {
                        source = ReadSource(entries[i].EffectivePath);
                        var input = source.GetPixels32();
                        var card = layout.cardRects[i];
                        var bounds = Intersect(PixelBounds(layout.cellRects[i]), size);
                        for (var y = bounds.yMin; y < bounds.yMax; y++)
                        for (var x = bounds.xMin; x < bounds.xMax; x++)
                        {
                            // Resample once from the original image. Padding follows the grid transform
                            // and repeats the nearest card edge, including under nonuniform Stretch.
                            var u = Mathf.Clamp01((x + .5f - card.x) / card.width) * source.width - .5f;
                            var v = Mathf.Clamp01((y + .5f - card.y) / card.height) * source.height - .5f;
                            pixels[y * size.x + x] = Sample(input, source.width, source.height, u, v, settings.resizeFilter);
                        }
                    }
                    finally { if (source != null) UnityEngine.Object.DestroyImmediate(source); }
                }
                result.texture = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false, !settings.sRGB)
                { name = recipe.outputName, hideFlags = HideFlags.HideAndDontSave, filterMode = settings.filter, wrapMode = settings.wrap };
                result.texture.SetPixels32(pixels);
                result.texture.Apply(false, false);
                return result;
            }
            catch { result.Dispose(); throw; }
            finally { if (showProgress) EditorUtility.ClearProgressBar(); }
        }

        public static List<CardAtlasEntry> OrderedEntries(CardAtlasRecipe recipe)
        {
            var entries = recipe.cards.Where(card => card.included).ToList();
            return recipe.order == CardAtlasOrder.BySuit ? CardDeckValidator.OrderForAtlas(entries) : entries;
        }

        public static CardAtlasLayout CreateLayout(CardAtlasRecipe recipe)
        {
            var entries = OrderedEntries(recipe);
            var settings = recipe.settings;
            var size = GetSize(settings, entries.Count);
            var layout = new CardAtlasLayout
            {
                entries = entries, size = size, columns = Math.Min(settings.columns, entries.Count),
                rows = (entries.Count + settings.columns - 1) / settings.columns
            };
            var cellWidth = settings.cardWidth + settings.padding * 2;
            var cellHeight = settings.cardHeight + settings.padding * 2;
            layout.gridSize = new Vector2(cellWidth * layout.columns, cellHeight * layout.rows);
            layout.scale = new Vector2(size.x / layout.gridSize.x, size.y / layout.gridSize.y);
            if (settings.fit != CardFitMode.Stretch)
            {
                var scale = settings.fit == CardFitMode.Fit ? Mathf.Min(layout.scale.x, layout.scale.y) : Mathf.Max(layout.scale.x, layout.scale.y);
                layout.scale = new Vector2(scale, scale);
            }
            for (var i = 0; i < entries.Count; i++)
            {
                // Anchor the entire arranged grid at the top left, keeping the back first.
                var cell = new Rect((i % layout.columns) * cellWidth * layout.scale.x,
                    size.y - (i / layout.columns + 1) * cellHeight * layout.scale.y,
                    cellWidth * layout.scale.x, cellHeight * layout.scale.y);
                var card = new Rect(cell.x + settings.padding * layout.scale.x, cell.y + settings.padding * layout.scale.y,
                    settings.cardWidth * layout.scale.x, settings.cardHeight * layout.scale.y);
                var pixels = PixelBounds(card);
                layout.cellRects.Add(cell);
                layout.cardRects.Add(card);
                layout.cards.Add(new CardAtlasRegion
                {
                    id = entries[i].id, index = i, pixels = pixels, visiblePixels = Intersect(pixels, size),
                    clipped = card.xMin < -.001f || card.yMin < -.001f || card.xMax > size.x + .001f || card.yMax > size.y + .001f,
                    uv = new Vector4(card.x / size.x, card.y / size.y, card.width / size.x, card.height / size.y)
                });
            }
            return layout;
        }

        private static RectInt PixelBounds(Rect rect)
        {
            var x = Mathf.CeilToInt(rect.xMin - .5f); var y = Mathf.CeilToInt(rect.yMin - .5f);
            return new RectInt(x, y, Mathf.CeilToInt(rect.xMax - .5f) - x, Mathf.CeilToInt(rect.yMax - .5f) - y);
        }

        private static RectInt Intersect(RectInt rect, Vector2Int size)
        {
            var x = Mathf.Clamp(rect.xMin, 0, size.x); var y = Mathf.Clamp(rect.yMin, 0, size.y);
            return new RectInt(x, y, Mathf.Max(0, Mathf.Min(rect.xMax, size.x) - x), Mathf.Max(0, Mathf.Min(rect.yMax, size.y) - y));
        }

        private static Color32 Sample(Color32[] input, int width, int height, float u, float v, CardResizeFilter filter)
        {
            if (filter == CardResizeFilter.Nearest)
                return input[Mathf.Clamp(Mathf.FloorToInt(v + .5f), 0, height - 1) * width + Mathf.Clamp(Mathf.FloorToInt(u + .5f), 0, width - 1)];
            var left = Mathf.FloorToInt(u); var bottom = Mathf.FloorToInt(v);
            Color a = input[Mathf.Clamp(bottom, 0, height - 1) * width + Mathf.Clamp(left, 0, width - 1)];
            Color b = input[Mathf.Clamp(bottom, 0, height - 1) * width + Mathf.Clamp(left + 1, 0, width - 1)];
            Color c = input[Mathf.Clamp(bottom + 1, 0, height - 1) * width + Mathf.Clamp(left, 0, width - 1)];
            Color d = input[Mathf.Clamp(bottom + 1, 0, height - 1) * width + Mathf.Clamp(left + 1, 0, width - 1)];
            return Color.Lerp(Color.Lerp(a, b, u - left), Color.Lerp(c, d, u - left), v - bottom);
        }

        public static string Export(CardAtlasRecipe recipe, CardAtlasBuild build)
        {
            if (string.IsNullOrWhiteSpace(recipe.outputFolder)) throw new InvalidOperationException("请指定导出目录。");
            if (!Regex.IsMatch(recipe.outputName ?? "", @"^[a-zA-Z0-9_-]+$"))
                throw new InvalidOperationException("导出名称请使用英文、数字、下划线或连字符。");
            var folder = Absolute(recipe.outputFolder);
            var source = Absolute(recipe.sourceFolder).TrimEnd(Path.DirectorySeparatorChar);
            if (folder == source || folder.StartsWith(source + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new InvalidOperationException("请将图集导出到卡牌源目录以外，避免下次扫描将图集当作单张卡牌。");
            var png = Path.Combine(folder, recipe.outputName + ".png");
            var path = ProjectPath(png);
            if (path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                var existingData = AssetDatabase.LoadMainAssetAtPath(Path.ChangeExtension(path, ".asset"));
                if (existingData != null && !(existingData is CardAtlasData))
                    throw new InvalidOperationException("同名 .asset 不是卡牌图集数据，请换一个导出名称。");
            }
            Directory.CreateDirectory(folder);
            var manifest = new CardAtlasManifest
            {
                texture = Path.GetFileName(png), atlasSize = new Vector2Int(build.texture.width, build.texture.height),
                cardSize = new Vector2Int(recipe.settings.cardWidth, recipe.settings.cardHeight),
                columns = build.columns, rows = build.rows, padding = recipe.settings.padding,
                filterMode = recipe.settings.filter.ToString(), wrapMode = recipe.settings.wrap.ToString(),
                settings = recipe.settings, cards = build.cards
            };
            File.WriteAllBytes(png, build.texture.EncodeToPNG());
            File.WriteAllText(Path.ChangeExtension(png, ".json"), JsonUtility.ToJson(manifest, true));
            if (!path.StartsWith("Assets/", StringComparison.Ordinal)) return png;

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.userData = ImporterTag;
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = recipe.settings.filter;
            importer.wrapMode = recipe.settings.wrap;
            importer.mipmapEnabled = recipe.settings.mipmaps;
            importer.sRGBTexture = recipe.settings.sRGB;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.anisoLevel = recipe.settings.anisoLevel;
            importer.maxTextureSize = Mathf.Max(32, Mathf.NextPowerOfTwo(Mathf.Max(build.texture.width, build.texture.height)));
            importer.textureCompression = recipe.settings.compression;
            foreach (var platform in new[] { "Standalone", "Android", "iPhone", "WebGL" })
                importer.ClearPlatformTextureSettings(platform);
            importer.SaveAndReimport();
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var dataPath = Path.ChangeExtension(path, ".asset");
            var data = AssetDatabase.LoadAssetAtPath<CardAtlasData>(dataPath);
            if (data == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(dataPath) != null) throw new InvalidOperationException("同名 .asset 不是卡牌图集数据：" + dataPath);
                data = ScriptableObject.CreateInstance<CardAtlasData>();
                AssetDatabase.CreateAsset(data, dataPath);
            }
            data.texture = atlas; data.atlasSize = manifest.atlasSize; data.cardSize = manifest.cardSize;
            data.columns = build.columns; data.rows = build.rows; data.padding = manifest.padding; data.cards = build.cards;
            EditorUtility.SetDirty(data);
            AssetDatabase.ImportAsset(Path.ChangeExtension(path, ".json"));
            AssetDatabase.SaveAssets();
            return path;
        }
    }
}
