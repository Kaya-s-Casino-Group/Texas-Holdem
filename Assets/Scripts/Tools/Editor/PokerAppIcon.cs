using UnityEditor;
using UnityEngine;

public static class PokerAppIcon
{
    public const string AssetPath = "Assets/Art/AppIcon/PokerPixelIcon.png";

    [MenuItem("工具/构建发布/应用游戏图标")]
    public static void Apply()
    {
        AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(AssetPath);
        importer.textureType = TextureImporterType.Default;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 2048;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaSource = TextureImporterAlphaSource.None;
        importer.SaveAndReimport();

        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetPath);
        if (texture == null) throw new System.InvalidOperationException("Poker icon could not be imported.");
        PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { texture });
        var standaloneSizes = PlayerSettings.GetIconSizesForTargetGroup(BuildTargetGroup.Standalone);
        if (standaloneSizes.Length > 0)
        {
            var standaloneIcons = new Texture2D[standaloneSizes.Length];
            for (var i = 0; i < standaloneIcons.Length; i++) standaloneIcons[i] = texture;
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Standalone, standaloneIcons);
        }
        var kinds = PlayerSettings.GetSupportedIconKindsForPlatform(BuildTargetGroup.iOS);
        foreach (var kind in kinds)
        {
            var icons = PlayerSettings.GetPlatformIcons(BuildTargetGroup.iOS, kind);
            foreach (var icon in icons) icon.SetTexture(texture);
            PlayerSettings.SetPlatformIcons(BuildTargetGroup.iOS, kind, icons);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("POKER_APP_ICON_APPLIED: " + AssetPath);
    }
}
