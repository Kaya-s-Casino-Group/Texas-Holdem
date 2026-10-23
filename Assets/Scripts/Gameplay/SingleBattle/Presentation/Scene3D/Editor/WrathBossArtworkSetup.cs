using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Poker.SingleBattle.Editor
{
    public static class WrathBossArtworkSetup
    {
        public const string Folder = "Assets/Art/Characters/BattleBillboards/WrathBoss/";
        public const string DefinitionPath = Folder + "WrathBossArtwork.asset";

        [MenuItem("工具/卡牌战斗/暴怒美术/按 PSD 布局接入两个场景")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请先退出播放模式。");
            if (SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("请先保存当前场景。");
            BuildDefinition();
            var original = SceneManager.GetActiveScene();
            foreach (var path in new[] { WrathBossSetup.ScenePath, "Assets/Scenes/SingleChapterAdventure.unity" })
            {
                var scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.IsValid() || !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    if (scene.isDirty) throw new InvalidOperationException("场景有未保存修改：" + path);
                    var host = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SingleBattleSceneAdapter>(true)).Single();
                    Configure(host);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("无法保存：" + path);
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            AssetDatabase.SaveAssets();
            Debug.Log("暴怒美术已按 5000×5000 PSD 原坐标接入：本体、五只独立手、预警红光；七站旅程与独立 Boss 场景已保存。");
        }

        public static void Configure(SingleBattleSceneAdapter host)
        {
            var definition = AssetDatabase.LoadAssetAtPath<WrathBossArtwork>(DefinitionPath);
            if (definition == null) throw new InvalidOperationException("先执行暴怒 PSD 美术接入。");
            var camera = host.gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .First(value => value.CompareTag("MainCamera"));
            // The imported standalone scene has duplicate URP camera data. SceneView cannot copy
            // the second disallowed component and throws before any battle can be previewed.
            foreach (var duplicate in camera.GetComponents<MonoBehaviour>().Where(component => component != null &&
                component.GetType().FullName == "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData").Skip(1).ToArray())
                UnityEngine.Object.DestroyImmediate(duplicate);
            var ashMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/GameAssets/SingleBattle/Presentation/Scene3D/BattleDemoAsh.mat");
            foreach (var actor in host.Enemies)
            {
                var previous = actor.Presentation as BattleActorPresenter;
                var ash = previous as BattleDemoAshPresenter;
                if (ash == null)
                {
                    if (previous == null) throw new InvalidOperationException("缺少角色表现：" + actor.name);
                    var settings = JsonUtility.ToJson(previous); var visual = previous.gameObject;
                    UnityEngine.Object.DestroyImmediate(previous);
                    ash = visual.AddComponent<BattleDemoAshPresenter>();
                    JsonUtility.FromJsonOverwrite(settings, ash);
                }
                ash.ConfigureAsh(actor.Visual.Find("Artwork").GetComponent<MeshRenderer>(), ashMaterial);
                actor.ConfigurePresentation(ash);
            }
            var composition = host.GetComponent<WrathBossArtworkLayout>() ?? host.gameObject.AddComponent<WrathBossArtworkLayout>();
            composition.Configure(host, camera, definition);
            composition.Preview();
            EditorUtility.SetDirty(composition);
            foreach (var actor in host.Enemies)
            {
                EditorUtility.SetDirty(actor.transform);
                if (actor.GetComponent<BattleBillboardLayout>() is BattleBillboardLayout layout) EditorUtility.SetDirty(layout);
                foreach (var component in actor.GetComponentsInChildren<BattleDemoAshPresenter>(true)) EditorUtility.SetDirty(component);
                foreach (var renderer in actor.GetComponentsInChildren<MeshRenderer>(true)) EditorUtility.SetDirty(renderer);
            }
        }

        private static void BuildDefinition()
        {
            var template = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Characters/BattleBillboards/Enemy.mat");
            WrathBossArtwork.Part Part(string name, Rect pixels, int order, Vector2? hudPixel = null)
            {
                string path = Folder + name + ".png";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true; importer.mipmapEnabled = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.isReadable = name != "WarningGlow"; // Alpha-aware selection of overlapping hands.
                importer.wrapMode = TextureWrapMode.Clamp; importer.maxTextureSize = 2048;
                Poker.EditorTools.PokerBuildAssetPolicy.ApplyStandalone(importer);
                importer.SaveAndReimport();
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                string materialPath = Folder + name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null) { material = new Material(template) { name = "Wrath " + name }; AssetDatabase.CreateAsset(material, materialPath); }
                material.SetTexture("_BaseMap", texture); material.SetTexture("_MainTex", texture);
                EditorUtility.SetDirty(material);
                return new WrathBossArtwork.Part { Name = name, Pixels = pixels, Order = order, Material = material,
                    HudPixel = hudPixel ?? pixels.center };
            }
            var parts = new[] {
                Part("Body", new Rect(1281, 443, 2447, 4026), 10),
                Part("Hand1", new Rect(957, 2012, 1262, 1878), 12, new Vector2(1700, 2850)),
                Part("Hand2", new Rect(3062, 1591, 1513, 1683), 8, new Vector2(4200, 2700)),
                Part("Hand3", new Rect(2812, 443, 1415, 1410), 6, new Vector2(3240, 900)),
                Part("Hand4", new Rect(2764, 2107, 985, 1350), 4, new Vector2(3150, 2530)),
                Part("Hand5", new Rect(799, 576, 1211, 1667), 2, new Vector2(1250, 1760)) };
            var warning = Part("WarningGlow", new Rect(671, 351, 4030, 4228), 0);
            var definition = AssetDatabase.LoadAssetAtPath<WrathBossArtwork>(DefinitionPath);
            if (definition == null) { definition = ScriptableObject.CreateInstance<WrathBossArtwork>(); AssetDatabase.CreateAsset(definition, DefinitionPath); }
            definition.Configure(new Vector2(5000, 5000), parts, warning);
            EditorUtility.SetDirty(definition);
        }
    }
}
