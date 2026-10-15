using UnityEditor;
using UnityEngine;

public static class PokerAppIcon
{
    public const string AssetPath = "Assets/Art/AppIcon/PokerPixelIcon.png";

    [MenuItem("Tools/Poker/Apply Pixel App Icon")]
    public static void Apply()
    {
        AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(AssetPath);
        importer.textureType = TextureImporterType.Default;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 2048;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaSource = TextureImporterAlphaSource.None;
        importer.SaveAndReimport();

        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetPath);
        if (texture == null) throw new System.InvalidOperationException("Poker icon could not be imported.");
        PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { texture });
        var kinds = PlayerSettings.GetSupportedIconKindsForPlatform(BuildTargetGroup.iOS);
        foreach (var kind in kinds)
        {
            var icons = PlayerSettings.GetPlatformIcons(BuildTargetGroup.iOS, kind);
            foreach (var icon in icons) icon.SetTexture(texture);
            PlayerSettings.SetPlatformIcons(BuildTargetGroup.iOS, kind, icons);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("POKER_PIXEL_ICON_APPLIED: " + AssetPath);
    }
}
