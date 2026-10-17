using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RetroRendering.Editor
{
    /// <summary>Editor-only, deterministic periodic value-noise baker. No runtime generation.</summary>
    public static class VolumeNoiseGenerator
    {
        public const int Size = 32;
        public const string DefaultPath = "Assets/Rendering/Textures/FogNoise32.asset";

        [Serializable]
        public sealed class Settings
        {
            public int seed = 1337;
            public int baseCells = 2;
            public int octaves = 3;
            public float persistence = 0.5f;
        }

        public static void Validate(Settings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (settings.baseCells < 2 || settings.baseCells > 8 || settings.octaves < 1 || settings.octaves > 4 ||
                (settings.baseCells << (settings.octaves - 1)) > Size / 2)
                throw new ArgumentException("基础格子数须为 2–8，层数为 1–4，最高频率不能超过 16 个格子。");
            if (float.IsNaN(settings.persistence) || settings.persistence < 0 || settings.persistence > 1)
                throw new ArgumentException("Persistence 必须在 0–1 之间。");
        }

        static int Wrap(int value, int period) => (value % period + period) % period;
        static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);

        static float Corner(int x, int y, int z, int period, int seed)
        {
            unchecked
            {
                uint h = (uint)seed ^ (uint)Wrap(x, period) * 0x8da6b343u ^
                    (uint)Wrap(y, period) * 0xd8163841u ^ (uint)Wrap(z, period) * 0xcb1ab31fu;
                h ^= h >> 16; h *= 0x7feb352du; h ^= h >> 15; h *= 0x846ca68bu; h ^= h >> 16;
                return (h & 0xffffffu) / 16777215f;
            }
        }

        static float Octave(Vector3 uvw, int period, int seed)
        {
            Vector3 p = uvw * period;
            int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y), z = Mathf.FloorToInt(p.z);
            float tx = Fade(p.x - x), ty = Fade(p.y - y), tz = Fade(p.z - z);
            float a = Mathf.Lerp(Corner(x, y, z, period, seed), Corner(x + 1, y, z, period, seed), tx);
            float b = Mathf.Lerp(Corner(x, y + 1, z, period, seed), Corner(x + 1, y + 1, z, period, seed), tx);
            float c = Mathf.Lerp(Corner(x, y, z + 1, period, seed), Corner(x + 1, y, z + 1, period, seed), tx);
            float d = Mathf.Lerp(Corner(x, y + 1, z + 1, period, seed), Corner(x + 1, y + 1, z + 1, period, seed), tx);
            return Mathf.Lerp(Mathf.Lerp(a, b, ty), Mathf.Lerp(c, d, ty), tz);
        }

        // UVW spans one tile. Integer-period lattices make all three axes seamless.
        public static float Sample(Vector3 uvw, Settings settings)
        {
            float value = 0, totalWeight = 0, weight = 1;
            for (int i = 0; i < settings.octaves; ++i)
            {
                value += Octave(uvw, settings.baseCells << i, unchecked(settings.seed + i * 1013)) * weight;
                totalWeight += weight; weight *= settings.persistence;
            }
            return value / totalWeight;
        }

        public static byte[] Bake(Settings settings)
        {
            Validate(settings);
            var result = new byte[Size * Size * Size];
            for (int z = 0; z < Size; ++z)
            for (int y = 0; y < Size; ++y)
            for (int x = 0; x < Size; ++x)
            {
                // Sample texel centers; repeat filtering interpolates across the tile boundary.
                float n = Sample(new Vector3(x + .5f, y + .5f, z + .5f) / Size, settings);
                result[x + Size * (y + Size * z)] = (byte)Mathf.Clamp(Mathf.RoundToInt(n * 255), 0, 255);
            }
            return result;
        }

        public static Texture3D Save(string path, Settings settings)
        {
            if (Application.isPlaying) throw new InvalidOperationException("请退出 Play 后生成噪声。");
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) || path.Contains("..") || path.Contains("\\"))
                throw new ArgumentException("请选择 Assets 下的 .asset 路径。");
            var bytes = Bake(settings);
            var existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing == null && File.Exists(path))
                throw new InvalidOperationException("目标文件尚未正确导入，请先刷新资源或另选路径。");
            var texture = existing as Texture3D;
            if (existing != null && (texture == null || texture.width != Size || texture.height != Size ||
                texture.depth != Size || texture.format != TextureFormat.R8 || texture.mipmapCount != 1))
                throw new InvalidOperationException("目标不是 32³、R8、无 mip 的 Texture3D；请另选路径。");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.Refresh();
            bool created = texture == null;
            if (created) texture = new Texture3D(Size, Size, Size, TextureFormat.R8, false);
            try
            {
                texture.name = Path.GetFileNameWithoutExtension(path);
                texture.wrapMode = TextureWrapMode.Repeat;
                texture.filterMode = FilterMode.Bilinear; texture.anisoLevel = 0;
                texture.SetPixelData(bytes, 0);
                // R8 is linear data. Keep the tiny CPU copy for editor slice previews and rebaking.
                texture.Apply(false, false);
                if (created) AssetDatabase.CreateAsset(texture, path);
                EditorUtility.SetDirty(texture); AssetDatabase.SaveAssetIfDirty(texture);
                var importer = AssetImporter.GetAtPath(path);
                importer.userData = JsonUtility.ToJson(settings);
                importer.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Texture3D>(path);
            }
            catch { if (created && !AssetDatabase.Contains(texture)) UnityEngine.Object.DestroyImmediate(texture); throw; }
        }

        public static Texture2D Slice(Texture3D texture, int z)
        {
            var values = texture.GetPixelData<byte>(0);
            var colors = new Color32[Size * Size]; z = Mathf.Clamp(z, 0, Size - 1);
            for (int i = 0; i < colors.Length; ++i)
            {
                byte v = values[z * Size * Size + i]; colors[i] = new Color32(v, v, v, 255);
            }
            var preview = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true)
                { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
            preview.SetPixels32(colors); preview.Apply(false, false); return preview;
        }

        [MenuItem("工具/渲染/三维噪声/生成默认雾噪声")]
        public static void GenerateDefault()
        {
            var texture = Save(DefaultPath, new Settings());
            EditorGUIUtility.PingObject(texture);
            Debug.Log("32³ R8 可平铺噪声已生成（32 KiB，无 mip）：" + DefaultPath);
        }
    }
}
