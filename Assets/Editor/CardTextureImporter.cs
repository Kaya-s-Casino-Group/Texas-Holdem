using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public sealed class CardTextureImporter : AssetPostprocessor
{
    private const string SkinFolder = "Assets/Art/Cards/FourBeasts/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(SkinFolder))
            return;

        var importer = (TextureImporter)assetImporter;
        // Generated atlases retain the settings chosen in the Atlas Builder.
        if (importer.userData == "CardAtlasGenerated") return;
        importer.textureType = TextureImporterType.Default;
        importer.filterMode = UnityEngine.FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 256;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaSource = TextureImporterAlphaSource.None;
        importer.sRGBTexture = true;
    }

    [MenuItem("Tools/Cards/Reimport Final Textures")]
    private static void ReimportFinalTextures()
    {
        var folders = new[] { SkinFolder.TrimEnd('/') };
        var imported = 0;
        var report = new StringBuilder();
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", folders))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (((TextureImporter)AssetImporter.GetAtPath(path)).userData == "CardAtlasGenerated") continue;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.filterMode != FilterMode.Point || importer.mipmapEnabled ||
                importer.maxTextureSize != 256 || importer.npotScale != TextureImporterNPOTScale.None ||
                importer.textureCompression != TextureImporterCompression.Uncompressed)
                throw new System.InvalidOperationException("Card import settings incorrect: " + path);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null || System.Math.Abs(texture.width * 3 - texture.height * 2) > 3)
                throw new System.InvalidOperationException("Card aspect ratio incorrect: " + path);
            report.AppendLine($"{path}: {texture.width}x{texture.height}");
            imported++;
        }

        var projectRoot = Directory.GetParent(Application.dataPath).FullName;
        File.WriteAllText(Path.Combine(projectRoot, "ArtSource/Cards/unity_import_report.txt"), report.ToString());
        Debug.Log($"Verified {imported} final card textures: 2:3 aspect, Point, no mipmaps, 256 max, uncompressed.");
    }
}
