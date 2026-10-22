#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using Poker.Adventure;
using Poker.SingleBattle;
using PokerEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// A one-way snapshot: the workshop never writes layout changes into gameplay.
public static class BattleLayoutWorkshopSetup
{
    public const string Path = "Assets/Scenes/BattleLayoutWorkshop.unity";
    const string Source = "Assets/Scenes/SingleChapterAdventure.unity";
    static readonly string[] Names = { "01 普通战斗", "02 精英战斗", "03 暴怒 Boss" };
    static readonly string[] Encounters = { "single-chapter-battle-1", "single-chapter-gatekeeper", "single-chapter-wrath" };
    static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
    static void Invoke(object owner, string name) => owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);

    [MenuItem("工具/战斗布局工作台/创建或打开布局场景")]
    public static void Open()
    {
        if (Application.isPlaying) throw new InvalidOperationException("请先退出 Play 模式。");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Path) != null) { EditorSceneManager.OpenScene(Path); return; }
        var previous = SceneManager.GetActiveScene();
        if (previous.isDirty) throw new InvalidOperationException("请先保存当前场景，再创建布局工作台。");
        var output = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            for (int i = 0; i < Names.Length; i++) Bake(output, i);
            SceneManager.SetActiveScene(output);
            EditorSceneManager.SaveScene(output, Path);
            foreach (var scene in Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray())
                if (scene != output && !scene.isDirty) EditorSceneManager.CloseScene(scene, true);
            Selection.activeGameObject = output.GetRootGameObjects()[0];
            Debug.Log("布局工作台已保存：三组敌人、血条和背景可直接编辑；原始战斗场景未改动。");
        }
        catch { if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); throw; }
    }

    static void Bake(Scene output, int index)
    {
        string scratch = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/__BattleLayoutSource.unity");
        if (!AssetDatabase.CopyAsset(Source, scratch)) throw new InvalidOperationException("Cannot copy source scene.");
        var scene = EditorSceneManager.OpenScene(scratch, OpenSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            var host = All<SingleBattleSceneAdapter>(scene).Single();
            for (var t = host.transform; t != null; t = t.parent) t.gameObject.SetActive(true);
            var content = AssetDatabase.LoadAssetAtPath<TextAsset>("Packages/com.kaya.battle-engine/Content/SingleChapterBattle.xml");
            var compiled = BattleContentCompiler.CompileXml(content.text, content.name);
            var assembled = BattleContentFactory.Create(compiled.Content, new BattleSetupRequest(Encounters[index], "layout-preview", HmacDeckShuffler.CreateRandomState()));
            if (!assembled.Success) throw new InvalidOperationException("Cannot assemble " + Encounters[index]);
            var session = assembled.Session;
            var opening = session.Execute(session.State.Revision, session.State.Duel.PlayerId, CardDuelAction.Start);
            if (!opening.Accepted) throw new InvalidOperationException("Cannot start " + Encounters[index]);
            host.ConfigureContent(content, Encounters[index]);
            host.AttachCommands(new BattleSessionCommands(session), opening, false);
            host.Actions.Hand.GetComponent<CardPresentation.HandZoneView>().CompleteTransitions();
            var adventureLayout = All<AdventureBattleLayout>(scene).Single();
            var materialField = new SerializedObject(adventureLayout).FindProperty(index == 1 ? "eliteArtwork" : "minionArtwork");
            var material = materialField.objectReferenceValue as Material;
            if (index == 2) host.GetComponent<WrathBossArtworkLayout>().TryApply(host.Display);
            else
            {
                // Use the same artwork and viewport rectangles as AdventureBattleLayout.
                Invoke(adventureLayout, "LateUpdate");
                typeof(AdventureBattleLayout).GetMethod("ApplyArtwork", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { host.Enemies[0], material });
            }
            foreach (var layout in host.Enemies.SelectMany(a => a.GetComponentsInChildren<BattleBillboardLayout>(true))) layout.Refresh();
            All<BattleBackgroundSwitcher>(scene).Single().Select(index == 0 ? BattleBackground.Minion : index == 1 ? BattleBackground.Elite : BattleBackground.Boss);
            var world = host.GetComponent<BattleDemoEnemyWorldHud>();
            Invoke(world, "Start");
            Canvas.ForceUpdateCanvases(); Invoke(world, "LateUpdate"); Canvas.ForceUpdateCanvases();

            var group = new GameObject(Names[index]); SceneManager.MoveGameObjectToScene(group, output);
            var environment = new GameObject("Environment - 背景与镜头"); environment.transform.SetParent(group.transform, false);
            foreach (var root in scene.GetRootGameObjects().Where(r => r.GetComponent<Camera>() != null || r.GetComponent<Light>() != null || r.name == "Battle Backgrounds"))
                Copy(root, environment.transform);
            var enemies = new GameObject("Enemies - 调整敌人位置和大小"); enemies.transform.SetParent(group.transform, false);
            foreach (var actor in host.Enemies)
            {
                var copy = Copy(actor.gameObject, enemies.transform);
                copy.name = actor.DisplayId + " - " + actor.name;
            }
            var hud = scene.GetRootGameObjects().Single(r => r.name == "BattleDemo Enemy World UI");
            var hudCopy = Copy(hud, group.transform); hudCopy.name = "Enemy HUD - 调整血条与意图";
            // IDs remain stable even when multiple source actors share a template name.
            for (int j = 0; j < host.Enemies.Count; j++) hudCopy.transform.GetChild(j).name = host.Enemies[j].DisplayId + " - HUD";
            group.SetActive(index == 0);
            Debug.Log(Names[index] + ": " + host.Enemies.Count + " enemies / " + hudCopy.transform.childCount + " HUDs baked.");
        }
        finally { EditorSceneManager.CloseScene(scene, true); AssetDatabase.DeleteAsset(scratch); }
    }

    static GameObject Copy(GameObject original, Transform parent)
    {
        var clone = UnityEngine.Object.Instantiate(original);
        clone.name = original.name;
        SceneManager.MoveGameObjectToScene(clone, parent.gameObject.scene);
        clone.transform.SetParent(parent, true);
        foreach (var script in clone.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (script == null) continue;
            var ns = script.GetType().Namespace ?? "";
            // Retain rendering/UI, including the procedural badge graphic, but remove gameplay and layout writers.
            if (ns.StartsWith("UnityEngine") || ns.StartsWith("TMPro") || script is BattleDemoEnemyIcon) continue;
            UnityEngine.Object.DestroyImmediate(script);
        }
        return clone;
    }

    [MenuItem("工具/战斗布局工作台/显示普通战斗")] public static void Normal() => Show(0);
    [MenuItem("工具/战斗布局工作台/显示精英战斗")] public static void Elite() => Show(1);
    [MenuItem("工具/战斗布局工作台/显示暴怒 Boss")] public static void Boss() => Show(2);
    static void Show(int index)
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != Path) throw new InvalidOperationException("请先打开 BattleLayoutWorkshop。");
        foreach (var root in scene.GetRootGameObjects())
        {
            int i = Array.IndexOf(Names, root.name); if (i < 0) continue;
            Undo.RecordObject(root, "切换布局预览"); root.SetActive(i == index);
            if (i == index) Selection.activeGameObject = root;
        }
        EditorSceneManager.MarkSceneDirty(scene);
    }
}
#endif
