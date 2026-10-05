using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CardArt.Editor
{
    public static class CardRedCrossSetup
    {
        public const string TexturePath = "Assets/CardArt/Textures/RedCross.png";
        const string SourcePath = "ArtSource/Cards/RedCross/RedCross_Portrait.png";

        [MenuItem("Tools/Card Art/Prepare Red Cross Texture And Materials")]
        public static void Prepare()
        {
            // Build the runtime texture once; preserve the full-resolution source outside Assets.
            if (!File.Exists(TexturePath))
            {
                var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                var output = new Texture2D(128, 192, TextureFormat.RGBA32, false);
                try
                {
                    if (!source.LoadImage(File.ReadAllBytes(SourcePath))) throw new IOException("Cannot read red cross source.");
                    if (source.width * 3 != source.height * 2) throw new InvalidOperationException("Red cross source must be 2:3.");
                    var original = source.GetPixels32();
                    var pixels = new Color32[128 * 192];
                    for (int y = 0; y < 192; y++)
                        for (int x = 0; x < 128; x++)
                        {
                            int sx = Mathf.Min((int)((x + 0.5f) * source.width / 128), source.width - 1);
                            int sy = Mathf.Min((int)((y + 0.5f) * source.height / 192), source.height - 1);
                            pixels[y * 128 + x] = original[sy * source.width + sx];
                        }
                    output.SetPixels32(pixels); output.Apply();
                    File.WriteAllBytes(TexturePath, output.EncodeToPNG());
                }
                finally { UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(output); }
            }
            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 256;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            var shader = Shader.Find(CardShaderProperties.ShaderName);
            CardRenderingValidation.SetTextureDefaults(shader, new[] { "_RedCrossMap" }, new Texture[] { texture });
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (material.shader != shader) continue;
                var current = material.GetTexture(CardShaderProperties.RedCrossMap);
                if (current != null && AssetDatabase.Contains(current)) continue;
                material.SetTexture(CardShaderProperties.RedCrossMap, texture);
                EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"Red cross prepared: {texture.width}×{texture.height}, Point/Clamp, no mipmaps. Per-card visibility remains unchanged.");
        }
    }
}
