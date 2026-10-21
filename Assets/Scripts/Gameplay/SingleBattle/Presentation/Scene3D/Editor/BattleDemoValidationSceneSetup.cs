using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Poker.SingleBattle.Editor
{
    public static class BattleDemoValidationSceneSetup
    {
        [MenuItem("工具/卡牌战斗/BattleDemo/配置小怪世界空间UI")]
        public static void ConfigureEnemyWorldUI()
        { ConfigureEnemyWorldUI(SceneManager.GetActiveScene()); }

        public static void ConfigureEnemyWorldUI(Scene scene)
        {
            if (Application.isPlaying || (scene.path != "Assets/Scenes/BattleDemo.unity" && scene.path != "Assets/Scenes/SingleChapterAdventure.unity"))
                throw new System.InvalidOperationException("Open BattleDemo or SingleChapterAdventure in Edit Mode first.");
            var host = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SingleBattleSceneAdapter>(true)).Single();
            var hud = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SingleBattleHud>(true)).Single();
            var settings = new SerializedObject(hud);
            foreach (var path in new[] { "enemy", "intent" })
            {
                var text = settings.FindProperty(path).objectReferenceValue as TMP_Text;
                if (text == null) continue;
                var frame = text.transform.parent.gameObject;
                var hidden = frame.GetComponent<CanvasGroup>();
                if (hidden == null) hidden = frame.AddComponent<CanvasGroup>();
                hidden.alpha = 0; hidden.interactable = false; hidden.blocksRaycasts = false;
                EditorUtility.SetDirty(hidden);
            }
            var font = settings.FindProperty("enemy").objectReferenceValue as TMP_Text;
            var world = host.GetComponent<BattleDemoEnemyWorldHud>();
            if (world == null) world = host.gameObject.AddComponent<BattleDemoEnemyWorldHud>();
            world.Configure(host, host.Actions.Hand.GetComponent<CardPresentation.HandZoneView>().FacingCamera, font.font);
            EditorUtility.SetDirty(world);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log(scene.name + ": per-enemy world-space health, intent and status icons configured.");
        }
        public const string ContentPath = "Assets/GameAssets/SingleBattle/Presentation/Scene3D/BattleDemoValidation.xml";
        [MenuItem("工具/卡牌战斗/BattleDemo/配置三敌与目标选择")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != "Assets/Scenes/BattleDemo.unity")
                throw new System.InvalidOperationException("Open BattleDemo in Edit Mode first.");
            var host = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SingleBattleSceneAdapter>(true)).Single();
            var enemies = host.Enemies.ToList();
            while (enemies.Count < 3)
            {
                var clone = Object.Instantiate(enemies[0], enemies[0].transform.parent);
                clone.name = "Enemy " + enemies.Count;
                enemies.Add(clone);
            }
            var camera = host.Actions.Hand.GetComponent<CardPresentation.HandZoneView>().FacingCamera;
            for (int i = 0; i < 3; i++)
            {
                var layout = enemies[i].GetComponent<BattleBillboardLayout>();
                var serialized = new SerializedObject(layout);
                float aspect = serialized.FindProperty("artworkAspect").floatValue;
                layout.Configure(camera, new Rect(.37f + i * .175f, .35f, .17f, .42f), 3.2f, aspect);
                // The old single-enemy artwork was offset far behind its logical plane.
                // Three portraits share their logical plane so hit areas and layout match the visible artwork.
                foreach (var mesh in enemies[i].Visual.GetComponentsInChildren<MeshFilter>())
                    mesh.transform.localPosition = Vector3.zero;
                EditorUtility.SetDirty(layout);
            }
            var intentPanel = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<RectTransform>(true))
                .FirstOrDefault(rect => rect.name == "Enemy Intent");
            if (intentPanel != null)
            {
                intentPanel.localRotation = Quaternion.identity;
                intentPanel.anchorMin = new Vector2(.72f, .79f); intentPanel.anchorMax = new Vector2(.88f, .90f);
            }
            host.ConfigureActors(host.Players, enemies);
            host.ConfigureContent(AssetDatabase.LoadAssetAtPath<TextAsset>(ContentPath), "standard-single-battle");
            var font = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TMP_Text>(true)).First(text => text.font != null);
            var selector = host.GetComponent<BattleDemoTargetSelection>() ?? host.gameObject.AddComponent<BattleDemoTargetSelection>();
            const string outlinePath = "Assets/GameAssets/SingleBattle/Presentation/Scene3D/BattleDemoTargetOutline.mat";
            var outline = AssetDatabase.LoadAssetAtPath<Material>(outlinePath);
            if (outline == null)
            {
                outline = new Material(Shader.Find("Poker/BattleDemo Target Outline"));
                AssetDatabase.CreateAsset(outline, outlinePath);
            }
            selector.Configure(host, font.canvas.rootCanvas, camera, font.font, outline);
            EditorUtility.SetDirty(host); EditorUtility.SetDirty(selector);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("BattleDemo: three enemies, portrait target selection and scene-local content configured.");
        }
    }
}
