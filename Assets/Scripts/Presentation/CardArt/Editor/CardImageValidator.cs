using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CardArt.Editor
{
    [Serializable]
    public sealed class CardImageValidationSettings
    {
        public bool checkSize = true;
        public int sourceWidth = 1024;
        public int sourceHeight = 1536;
        public int ratioWidth = 2;
        public int ratioHeight = 3;
        public float ratioTolerancePercent = .5f;
    }

    public sealed class CardImageCheck
    {
        public string path;
        public Vector2Int size;
        public string error;
        public bool sizeMismatch;
        public bool ratioMismatch;
        public bool needsUpscale;
        public bool HasIssue => error != null || sizeMismatch || ratioMismatch || needsUpscale;
        public string Dimensions => error == null ? $"{size.x}×{size.y}" : "无法读取";
        public string Status => error != null ? error : string.Join(" / ", new[]
        {
            sizeMismatch ? "尺寸不符" : null,
            ratioMismatch ? "比例不符" : null,
            needsUpscale ? "需要放大" : null
        }.Where(message => message != null).DefaultIfEmpty("正常"));
    }

    public sealed class CardImageEntryCheck
    {
        public CardAtlasEntry entry;
        public CardImageCheck original;
        public CardImageCheck effective;
        public bool HasIssue => original.HasIssue || effective.HasIssue;
        public string Detail => "原图：" + original.Dimensions + " · " + original.Status
            + (ReferenceEquals(original, effective) ? "" : "\n替换：" + effective.Dimensions + " · " + effective.Status);
    }

    /// <summary>Caches decoded source dimensions, never imported/thumbnail dimensions or pixel buffers.</summary>
    public sealed class CardImageValidator
    {
        private sealed class CachedSize
        {
            public long ticks;
            public long length;
            public Vector2Int size;
            public string error;
        }
        private readonly Dictionary<string, CachedSize> sizes = new Dictionary<string, CachedSize>(StringComparer.Ordinal);
        public void Clear() => sizes.Clear();

        public static void ValidateSettings(CardImageValidationSettings settings)
        {
            if (settings == null || settings.ratioWidth <= 0 || settings.ratioHeight <= 0
                || (settings.checkSize && (settings.sourceWidth <= 0 || settings.sourceHeight <= 0))
                || float.IsNaN(settings.ratioTolerancePercent) || float.IsInfinity(settings.ratioTolerancePercent)
                || settings.ratioTolerancePercent < 0 || settings.ratioTolerancePercent > 5)
                throw new InvalidOperationException("尺寸 / 比例校验标准必须为正数，比例容差须在 0–5% 之间。");
        }

        public static bool MatchesRatio(int width, int height, CardImageValidationSettings settings)
        {
            ValidateSettings(settings);
            if (width <= 0 || height <= 0) return false;
            var relativeError = Math.Abs((double)width * settings.ratioHeight / ((double)height * settings.ratioWidth) - 1);
            return relativeError <= settings.ratioTolerancePercent / 100d + 1e-12;
        }

        public List<CardImageEntryCheck> Analyze(CardAtlasRecipe recipe)
        {
            ValidateSettings(recipe.imageValidation);
            var groupScale = Vector2.one;
            if (recipe.cards.Any(entry => entry.included))
            {
                // Invalid layout settings are reported separately by the layout preview.
                try { groupScale = CardAtlasBuilder.CreateLayout(recipe).scale; }
                catch (InvalidOperationException) { }
            }
            return recipe.cards.Select(entry =>
            {
                var original = Check(entry.sourcePath, recipe.imageValidation, recipe.settings, groupScale);
                var effective = string.Equals(entry.sourcePath, entry.EffectivePath, StringComparison.Ordinal)
                    ? original : Check(entry.EffectivePath, recipe.imageValidation, recipe.settings, groupScale);
                if (!ReferenceEquals(original, effective)) original.needsUpscale = false;
                return new CardImageEntryCheck { entry = entry, original = original, effective = effective };
            }).ToList();
        }

        public CardImageCheck Check(string path, CardImageValidationSettings settings, CardAtlasSettings atlas, Vector2? groupScale = null)
        {
            ValidateSettings(settings);
            var result = new CardImageCheck { path = path };
            try
            {
                if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("未指定图片路径");
                var full = CardAtlasBuilder.Absolute(path);
                var file = new FileInfo(full);
                if (!file.Exists) throw new FileNotFoundException("图片文件不存在");
                if (!sizes.TryGetValue(full, out var cached) || cached.ticks != file.LastWriteTimeUtc.Ticks || cached.length != file.Length)
                {
                    cached = new CachedSize { ticks = file.LastWriteTimeUtc.Ticks, length = file.Length };
                    Texture2D source = null;
                    try
                    {
                        source = CardAtlasBuilder.ReadSource(full);
                        cached.size = new Vector2Int(source.width, source.height);
                    }
                    catch (Exception e) { cached.error = e.Message; }
                    finally { if (source != null) UnityEngine.Object.DestroyImmediate(source); }
                    sizes[full] = cached;
                }
                result.size = cached.size;
                result.error = cached.error;
                if (result.error == null)
                {
                    result.sizeMismatch = settings.checkSize && (result.size.x != settings.sourceWidth || result.size.y != settings.sourceHeight);
                    result.ratioMismatch = !MatchesRatio(result.size.x, result.size.y, settings);
                    var scale = groupScale ?? Vector2.one;
                    var x = (double)atlas.cardWidth * scale.x / result.size.x;
                    var y = (double)atlas.cardHeight * scale.y / result.size.y;
                    result.needsUpscale = Math.Max(x, y) > 1 + 1e-7;
                }
            }
            catch (Exception e) { result.error = e.Message; }
            return result;
        }
    }
}
