using System;
using System.Linq;
using Poker.SingleBattle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Poker.Adventure.Editor
{
    public static class AdventureArtworkSetup
    {
        private const string Backgrounds = "Assets/Art/Environment/BattleBackgrounds/";
        private const string Characters = "Assets/Art/Characters/BattleBillboards/";
        public const string MinionMaterialPath = Characters + "Minion-Updated.mat";
        public const string EliteMaterialPath = Characters + "Elite-Updated.mat";

        [MenuItem("工具/局外冒险/应用更新背景与小怪立绘")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出播放模式。");
            var minion = Import(Backgrounds + "Sources/Minion_Updated.jpg", false);
            var elite = Import(Backgrounds + "Sources/Elite_Updated.jpg", false);
            var portrait = Import(Characters + "Minion-Updated.png", true);
            var elitePortrait = Import(Characters + "Elite-Updated.png", true);
            var original = AssetDatabase.LoadAssetAtPath<Material>(Characters + "Enemy.mat");
            var minionMaterial = CopyMaterial(MinionMaterialPath, original, portrait);
            var eliteMaterial = CopyMaterial(EliteMaterialPath, original, elitePortrait);
            var backgroundTemplate = AssetDatabase.LoadAssetAtPath<Material>(Backgrounds + "Materials/Minion_Back.mat");
            var minionBackground = CopyMaterial(Backgrounds + "Materials/Minion_Updated.mat", backgroundTemplate, minion);
            var eliteBackground = CopyMaterial(Backgrounds + "Materials/Elite_Updated.mat", backgroundTemplate, elite);
            var previous = SceneManager.GetActiveScene();
            try
            {
                foreach (var path in new[] { "Assets/Scenes/BattleDemo.unity", AdventureSceneSetup.ScenePath })
                {
                    var scene = SceneManager.GetSceneByPath(path);
                    bool opened = !scene.IsValid() || !scene.isLoaded;
                    if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    try
                    {
                        SceneManager.SetActiveScene(scene);
                        var roots = scene.GetRootGameObjects();
                        var switcher = roots.SelectMany(r => r.GetComponentsInChildren<BattleBackgroundSwitcher>(true)).Single();
                        var view = roots.SelectMany(r => r.GetComponentsInChildren<SingleBattleSceneAdapter>(true)).Single();
                        var camera = roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).First(c => c.CompareTag("MainCamera"));
                        var settings = new SerializedObject(switcher);
                        // Keep each existing root/reference; disable only its old environmental layers.
                        UpdateBackground((GameObject)settings.FindProperty("minion").objectReferenceValue, camera, minionBackground, minion);
                        UpdateBackground((GameObject)settings.FindProperty("elite").objectReferenceValue, camera, eliteBackground, elite);
                        var run = roots.SelectMany(r => r.GetComponentsInChildren<AdventureBattleLayout>(true)).FirstOrDefault();
                        if (run != null)
                        {
                            run.ConfigureArtwork(minionMaterial, eliteMaterial, original);
                            EditorUtility.SetDirty(run);
                        }
                        else
                        {
                            foreach (var actor in view.Enemies)
                            {
                                var renderer = actor.Visual.Find("Artwork").GetComponent<MeshRenderer>();
                                renderer.sharedMaterial = minionMaterial;
                                EditorUtility.SetDirty(renderer);
                                if (actor.Presentation is BattleDemoAshPresenter ash)
                                {
                                    ash.ConfigureArtwork(minionMaterial);
                                    EditorUtility.SetDirty(ash);
                                }
                            }
                        }
                        var playerLayout = view.Player.GetComponent<BattleBillboardLayout>();
                        var playerArtwork = view.Player.Visual.Find("Artwork");
                        playerArtwork.localScale = Vector3.one;
                        EditorUtility.SetDirty(playerArtwork);
                        playerLayout.SetViewportArea(new Rect(-.012f, -.03f, .36f, .84f));
                        EditorUtility.SetDirty(playerLayout);
                        var overlay = view.Player.GetComponent<PlayerPortraitOverlay>();
                        if (overlay != null) UnityEngine.Object.DestroyImmediate(overlay);
                        playerArtwork.GetComponent<MeshRenderer>().forceRenderingOff = false;
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                    }
                    finally
                    {
                        if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                        if (opened) EditorSceneManager.CloseScene(scene, true);
                    }
                }
                AssetDatabase.SaveAssets();
                Debug.Log("已替换普通/精英背景及立绘；主角使用常规场景面片；Boss保持原样。");
            }
            finally { if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); }
        }

        private static Texture2D Import(string path, bool alpha)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new InvalidOperationException("缺少素材：" + path);
            importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true;
            importer.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = alpha; importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = true; importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear; importer.maxTextureSize = 4096;
            importer.textureCompression = alpha ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material CopyMaterial(string path, Material template, Texture texture)
        {
            if (template == null) throw new InvalidOperationException("缺少现有材质模板。");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(template); AssetDatabase.CreateAsset(material, path); }
            material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            EditorUtility.SetDirty(material); return material;
        }

        private static void UpdateBackground(GameObject root, Camera camera, Material material, Texture texture)
        {
            var image = root.transform.Find("Updated Artwork");
            foreach (Transform child in root.transform) if (child != image) child.gameObject.SetActive(false);
            if (image == null)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Updated Artwork"; UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
                image = quad.transform; image.SetParent(root.transform, false);
            }
            image.gameObject.SetActive(true);
            var renderer = image.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            var backdrop = root.GetComponent<PaintedBattleBackdrop>();
            if (backdrop == null) backdrop = root.AddComponent<PaintedBattleBackdrop>();
            backdrop.ConfigureImage(camera, image, (float)texture.width / texture.height);
            EditorUtility.SetDirty(backdrop); EditorUtility.SetDirty(renderer);
        }
    }
}
