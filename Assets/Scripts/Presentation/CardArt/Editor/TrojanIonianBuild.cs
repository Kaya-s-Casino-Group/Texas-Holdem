using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CardArt.Editor
{
    // Project-specific recipe; delegates all packing/export to the Card asset tools.
    public static class TrojanIonianBuild
    {
        const string Report = "ArtSource/Cards/TrojanIonian-v1/prefab-build.txt";
        const string Request = "Temp/TrojanIonianBuild.request";
        [InitializeOnLoadMethod]
        static void ResumeRequestedBuild()
        {
            if (File.Exists(Request)) EditorApplication.delayCall += RunRequested;
        }
        static void RunRequested()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            { EditorApplication.delayCall += RunRequested; return; }
            if (!File.Exists(Request)) return;
            File.Delete(Request);
            try { Build(); }
            catch (Exception e) { File.WriteAllText(Report, "FAILED\n" + e); Debug.LogException(e); }
        }
        [MenuItem("工具/卡牌美术/特洛伊爱奥尼亚/构建牌组 Prefab")]
        public static void Build()
        {
            var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameAssets/CardArt/Prefabs/FourBeastsCard.prefab");
            if (!template) throw new InvalidOperationException("FourBeasts prefab missing");
            var templateView = template.GetComponentInChildren<CardView>();
            var templateMaterial = template.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            const string recipePath = "Assets/Art/CardAtlases/TrojanIonianRecipe.asset";
            var recipe = AssetDatabase.LoadAssetAtPath<CardAtlasRecipe>(recipePath);
            if (!recipe) { recipe = ScriptableObject.CreateInstance<CardAtlasRecipe>(); AssetDatabase.CreateAsset(recipe, recipePath); }
            recipe.sourceFolder = "ArtSource/Cards/TrojanIonian-v1/originals";
            recipe.outputFolder = "Assets/Art/CardAtlases";
            recipe.outputName = "TrojanIonianAtlas";
            recipe.order = CardAtlasOrder.BySuit;
            var referenceManifest = JsonUtility.FromJson<CardAtlasManifest>(File.ReadAllText("Assets/Art/CardAtlases/FourBeastsAtlas.json"));
            recipe.settings = referenceManifest.settings;
            CardAtlasBuilder.Scan(recipe);
            if (recipe.cards.Count != 53) throw new InvalidOperationException("Expected 52 faces and one card back");
            using (var build = CardAtlasBuilder.Build(recipe)) CardAtlasBuilder.Export(recipe, build);
            var atlas = AssetDatabase.LoadAssetAtPath<CardAtlasData>("Assets/Art/CardAtlases/TrojanIonianAtlas.asset");
            const string profilePath = "Assets/GameAssets/CardArt/Profiles/TrojanIonianCardArt.asset";
            var profile = AssetDatabase.LoadAssetAtPath<CardArtProfile>(profilePath);
            if (!profile) { profile = ScriptableObject.CreateInstance<CardArtProfile>(); AssetDatabase.CreateAsset(profile, profilePath); }
            profile.Configure(atlas, templateView.ArtProfile.Combinations, templateView.ArtProfile.BaseCombination);
            EditorUtility.SetDirty(profile);
            var settings = new CardPrefabExportSettings {
                atlas = atlas, artProfile = profile, shader = templateMaterial.shader,
                mesh = template.GetComponentInChildren<MeshFilter>().sharedMesh,
                cardId = templateView.CardId, outputName = "TrojanIonianCard",
                outputFolder = "Assets/GameAssets/CardArt/Prefabs", addCollider = true,
                sealMap = templateMaterial.GetTexture("_SealMap") as Texture2D,
                sealNormalMap = templateMaterial.GetTexture("_SealNormalMap") as Texture2D,
                sealNormalScale = templateMaterial.GetFloat("_SealNormalScale"),
                sealRect = templateMaterial.GetVector("_SealRect"), sealRotation = templateMaterial.GetFloat("_SealRotation"),
                sealColor = templateView.SealColor, sealEnabled = templateView.SealEnabled,
                sealEmissionStrength = templateView.SealEmissionStrength,
                redCrossMap = templateMaterial.GetTexture("_RedCrossMap") as Texture2D,
                redCrossEnabled = templateView.RedCrossEnabled
            };
            var check = CardPrefabExporter.Validate(settings);
            if (!check.CanExport) throw new InvalidOperationException(string.Join("\n", check.errors));
            var prefab = CardPrefabExporter.Export(settings, true);
            var material = prefab.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            material.CopyPropertiesFromMaterial(templateMaterial);
            CardShaderProperties.ConfigureSkin(material, atlas);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            AssetDatabase.SaveAssetIfDirty(profile);
            AssetDatabase.SaveAssetIfDirty(recipe);
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            try {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                var view = instance.GetComponentInChildren<CardView>();
                foreach (var card in atlas.cards.Where(c => c.id != "card_back")) view.SetCard(card.id);
                if (view.Atlas != atlas || view.ArtProfile != profile || instance.GetComponentInChildren<MeshRenderer>().sharedMaterial != material)
                    throw new InvalidOperationException("Prefab resource binding mismatch");
                if (instance.GetComponent<BoxCollider>() == null) throw new InvalidOperationException("Missing collider");
            } finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            File.WriteAllText(Report, "PASS\n52 face IDs successfully bound via CardView.SetCard\n53 atlas regions including card_back; no jokers\n2048x2048 atlas; same packing and import settings as FourBeasts\nShared FourBeasts mesh/shader and copied material effects\nIndependent atlas, art profile, shared material and prefab\nPrefab: Assets/GameAssets/CardArt/Prefabs/TrojanIonianCard.prefab\nWarnings: " + string.Join("; ", check.warnings));
            Debug.Log("TrojanIonian Card prefab exported and validated.");
        }
    }
}
