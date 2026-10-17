using System;
using System.Linq;
using CardInteraction;
using CardPresentation;
using RetroRendering;
using Poker.DrawBattle;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Poker.SingleBattle.Editor
{
    public static class SingleBattleSetup
    {
        public const string ScenePath = "Assets/Scenes/SingleBattleDemo.unity";
        private static readonly Color Ink = new Color32(25, 30, 35, 238);
        private static readonly Color Paper = new Color32(238, 232, 213, 255);
        private static TMP_FontAsset font;
        [MenuItem("工具/卡牌战斗/示例场景/配置单人战斗")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != ScenePath) throw new InvalidOperationException("Open the dedicated SingleBattleDemo scene in Edit mode first.");
            var roots = scene.GetRootGameObjects();
            var actions = roots.SelectMany(root => root.GetComponentsInChildren<HandActionPresenter>(true)).Single(item => item.isActiveAndEnabled);
            var match = actions.Hand.Match;
            foreach (var old in roots.SelectMany(root => root.GetComponentsInChildren<DrawBattleHost>(true))) UnityEngine.Object.DestroyImmediate(old);
            var host = match.GetComponent<SingleBattleHost>() ?? match.gameObject.AddComponent<SingleBattleHost>();
            var overview = roots.SelectMany(root => root.GetComponentsInChildren<DeckOverviewPresenter>(true)).FirstOrDefault(item => item.Match == match);
            var pile = roots.SelectMany(root => root.GetComponentsInChildren<DrawPilePresenter>(true)).FirstOrDefault(item => item.Source == actions.Hand);
            host.Configure(match, actions, overview, pile);
            if (host.RulesProfile == null) host.ConfigureRules(AssetDatabase.LoadAssetAtPath<BattleRulesProfile>("Assets/GameAssets/SingleBattle/Configuration/Profiles/SingleBattleRules.asset"));
            PrepareRuntimeCardZones(scene, host);
            ConnectActors(scene, host);
            var oldHud = roots.SelectMany(root => root.GetComponentsInChildren<SingleBattleHud>(true)).SingleOrDefault();
            var hudParent = oldHud != null ? oldHud.transform.parent : null;
            if (oldHud != null) UnityEngine.Object.DestroyImmediate(oldHud.gameObject);
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/FusionPixelFont/TMP/FusionPixel12pxProportional.asset");
            var go = new GameObject("Battle HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(go, scene); go.layer = 5;
            if (hudParent != null) go.transform.SetParent(hudParent, false);
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 120;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            var topLeft = Panel(go.transform, "Player", new Vector2(0, 1), new Vector2(24, -24), new Vector2(245, 176));
            Label(topLeft, "Heading", "旅人 / PLAYER", 14, new Vector2(14, -10), new Vector2(215, 22), Paper);
            var player = Label(topLeft, "Health", "你   40 / 40\n格挡 0", 21, new Vector2(14, -36), new Vector2(215, 56), Paper);
            var playerBar = Bar(topLeft, new Color32(93, 180, 148, 255));
            var energy = Label(topLeft, "Energy", "能量 3 / 3\n出牌 1 · 弃牌 1", 16, new Vector2(14, -124), new Vector2(215, 44), new Color32(233, 193, 112, 255));
            var topRight = Panel(go.transform, "Enemy", new Vector2(1, 1), new Vector2(-24, -24), new Vector2(245, 126));
            Label(topRight, "Heading", "守卫 / ENEMY", 14, new Vector2(14, -10), new Vector2(215, 22), Paper);
            var enemy = Label(topRight, "Health", "守卫   75 / 75\n格挡 0", 21, new Vector2(14, -36), new Vector2(215, 56), Paper);
            var enemyBar = Bar(topRight, new Color32(205, 107, 95, 255));
            var middle = Panel(go.transform, "Turn", new Vector2(.5f, 1), new Vector2(0, -24), new Vector2(500, 126));
            Label(middle, "Heading", "单局战斗  /  THE GUARDIAN", 14, new Vector2(12, -8), new Vector2(476, 24), Paper, true);
            var turn = Label(middle, "Round", "第 1 / 8 回合 · 玩家行动", 25, new Vector2(12, -34), new Vector2(476, 40), Paper, true);
            var intent = Label(middle, "Intent", "敌人意图：攻击 7", 17, new Vector2(12, -82), new Vector2(476, 30), new Color32(237, 156, 129, 255), true);
            var info = Panel(go.transform, "Battle Info", new Vector2(.5f, 1), new Vector2(0, -162), new Vector2(700, 100));
            var message = Label(info, "Message", "选择 1–5 张牌组成牌型 · 能量用尽或手动结束回合", 20, new Vector2(12, -8), new Vector2(676, 32), Paper, true);
            var score = Label(info, "Score", "牌型得分 = 基础伤害", 16, new Vector2(12, -40), new Vector2(676, 26), new Color32(233, 193, 112, 255), true);
            var counts = Label(info, "Counts", "抽牌堆 44  ·  弃牌堆 0  ·  剩余弃牌 3", 16, new Vector2(12, -68), new Vector2(676, 26), Paper, true);
            var endTurnRoot = Panel(go.transform, "End Turn", new Vector2(0, 1), new Vector2(24, -192), new Vector2(245, 44));
            var endTurn = MakeButton(endTurnRoot, "结束回合");
            var restartRoot = Panel(go.transform, "Restart", new Vector2(0, 1), new Vector2(24, -248), new Vector2(150, 40));
            var restart = MakeButton(restartRoot, "重新开始");
            var result = Panel(go.transform, "Result", new Vector2(.5f, .5f), new Vector2(0, 0), new Vector2(450, 250));
            result.GetComponent<Image>().raycastTarget = true;
            var resultTitle = Label(result, "Title", "战斗胜利", 36, new Vector2(16, -28), new Vector2(418, 56), Paper, true);
            var details = Label(result, "Details", "", 20, new Vector2(16, -96), new Vector2(418, 62), Paper, true);
            var resultButton = Panel(result, "Again", new Vector2(.5f, 0), new Vector2(0, 22), new Vector2(190, 44));
            var again = MakeButton(resultButton, "再战一场");
            var hud = go.AddComponent<SingleBattleHud>();
            hud.Configure(host, player, enemy, turn, intent, message, counts, score,
                playerBar, enemyBar, result.gameObject, resultTitle, details, restart, again, energy, endTurn);
            BattleHudLayout.Apply(hud);
            HandCandidateSetup.Apply(hud);
            result.gameObject.SetActive(false);
            EditorUtility.SetDirty(host); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Selection.activeObject = host;
            Debug.Log("SingleBattleDemo configured and saved. Enter Play: select cards, play/discard, defeat the guardian, restart.");
        }
        [MenuItem("工具/卡牌战斗/示例场景/连接战斗角色")]
        public static void ConnectActors()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != ScenePath) throw new InvalidOperationException("Open SingleBattleDemo in Edit mode first.");
            var host = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SingleBattleHost>(true)).Single();
            ConnectActors(scene, host); EditorUtility.SetDirty(host); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Selection.activeObject = host.Player;
            Debug.Log("PlayerBattleActor and EnemyBattleActor connected and saved; existing HUD, models and layout preserved.");
        }
        private static void ConnectActors(Scene scene, SingleBattleHost host)
        {
            var roots = scene.GetRootGameObjects();
            var actors = host.Player != null ? host.Player.transform.parent.gameObject : FindSceneObject(scene, "Battle Actors");
            if (actors == null) { actors = new GameObject("Battle Actors"); SceneManager.MoveGameObjectToScene(actors, scene); }
            var playerRoot = actors.transform.Find("Player");
            if (playerRoot == null)
            {
                var playerObject = new GameObject("Player"); playerObject.transform.SetParent(actors.transform, false);
                playerObject.transform.position = host.Actions.Hand.transform.position; playerRoot = playerObject.transform;
            }
            var player = playerRoot.GetComponent<PlayerBattleActor>() ?? playerRoot.gameObject.AddComponent<PlayerBattleActor>();
            var playerModel = playerRoot.Find("Model");
            if (playerModel == null) { var child = new GameObject("Model"); child.transform.SetParent(playerRoot, false); playerModel = child.transform; }
            var blockout = FindSceneObject(scene, "TabletopCombat_Blockout");
            var oldEnemy = host.Enemy;
            var sourceModel = oldEnemy != null ? oldEnemy.Visual : null;
            if (!HasVisibleModel(sourceModel) && blockout != null) sourceModel = blockout.transform.Find("Enemy");
            if (!HasVisibleModel(sourceModel) && blockout != null) sourceModel = blockout.transform.Find("DragonGargoyle");
            if (!HasVisibleModel(sourceModel)) throw new InvalidOperationException("Bind a visible Enemy or DragonGargoyle renderer before connecting the enemy actor.");
            var enemyRoot = actors.transform.Find("Enemy");
            if (enemyRoot == null)
            {
                var enemyObject = new GameObject("Enemy"); enemyObject.transform.SetParent(actors.transform, false);
                enemyObject.transform.position = sourceModel.position; enemyRoot = enemyObject.transform;
            }
            var enemyModel = enemyRoot.Find("Model");
            if (enemyModel == null) { var child = new GameObject("Model"); child.transform.SetParent(enemyRoot, false); enemyModel = child.transform; }
            if (oldEnemy != null && oldEnemy.transform != enemyRoot) UnityEngine.Object.DestroyImmediate(oldEnemy);
            if (sourceModel != enemyModel)
            {
                if (!HasVisibleModel(enemyModel)) enemyRoot.position = sourceModel.position;
                sourceModel.SetParent(enemyModel, true); // Preserve the existing mesh/VAT world pose and components.
            }
            var enemy = enemyRoot.GetComponent<EnemyBattleActor>() ?? enemyRoot.gameObject.AddComponent<EnemyBattleActor>();
            player.Configure(host, playerModel); enemy.Configure(host, enemyModel);
            if (player.Profile == null) player.ConfigureProfile(AssetDatabase.LoadAssetAtPath<PlayerBattleProfile>("Assets/GameAssets/SingleBattle/Configuration/Profiles/SingleBattlePlayer.asset"));
            if (enemy.Profile == null) enemy.ConfigureProfile(AssetDatabase.LoadAssetAtPath<EnemyBattleProfile>("Assets/GameAssets/SingleBattle/Configuration/Profiles/SingleBattleEnemy.asset"));
            ConnectPresentation(player, playerModel); ConnectPresentation(enemy, enemyModel);
            host.ConfigureActors(player, enemy);
            EditorUtility.SetDirty(player); EditorUtility.SetDirty(enemy);
        }
        private static bool HasVisibleModel(Transform model) => model != null && model.gameObject.activeInHierarchy &&
            model.GetComponentsInChildren<Renderer>(true).Any(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy);
        private static void ConnectPresentation(BattleActor actor, Transform model)
        {
            if (actor.Presentation != null)
            {
                if (actor.Presentation is VatBattleActorPresenter existing) ConnectHealthCracks(actor, model, existing);
                if (actor.Presentation is BattleActorPresenter current) ConnectAudio(model, current);
                return; // Keep an explicitly chosen protocol implementation on repeated setup.
            }
            var vat = model.GetComponentInChildren<RigidVATPlayer>(true);
            BattleActorPresenter presenter;
            if (vat != null)
            {
                var timelinePresenter = model.GetComponent<VatBattleActorPresenter>() ?? model.gameObject.AddComponent<VatBattleActorPresenter>();
                timelinePresenter.Configure(model, vat); presenter = timelinePresenter;
                ConnectHealthCracks(actor, model, timelinePresenter);
                var settings = new SerializedObject(vat); settings.FindProperty("playOnStart").boolValue = false; settings.ApplyModifiedPropertiesWithoutUndo();
            }
            else { presenter = model.GetComponent<BattleActorPresenter>() ?? model.gameObject.AddComponent<BattleActorPresenter>(); presenter.Configure(model); }
            ConnectAudio(model, presenter);
            actor.ConfigurePresentation(presenter); EditorUtility.SetDirty(actor); EditorUtility.SetDirty(presenter);
        }
        private static void ConnectAudio(Transform model, BattleActorPresenter presenter)
        {
            if (presenter.Audio != null) return;
            var root = ChildGroup(model, "Audio");
            var audio = root.GetComponent<BattleActorAudio>() ?? root.gameObject.AddComponent<BattleActorAudio>();
            var attack = Voice(root, "Attack"); var hit = Voice(root, "Hit"); var death = Voice(root, "Death");
            audio.Configure(attack, hit, death); presenter.ConfigureAudio(audio);
            EditorUtility.SetDirty(audio); EditorUtility.SetDirty(presenter);
            EditorUtility.SetDirty(attack); EditorUtility.SetDirty(hit); EditorUtility.SetDirty(death);
        }
        private static AudioSource Voice(Transform root, string name)
        {
            var child = ChildGroup(root, name);
            var source = child.GetComponent<AudioSource>();
            if (source == null) source = child.gameObject.AddComponent<AudioSource>();
            return source;
        }
        private static void ConnectHealthCracks(BattleActor actor, Transform model, VatBattleActorPresenter presenter)
        {
            bool firstSetup = presenter.Cracks == null;
            var cracks = model.GetComponent<HealthCrackPresenter>() ?? model.gameObject.AddComponent<HealthCrackPresenter>();
            cracks.Configure(model); presenter.ConfigureCracks(cracks);
            if (firstSetup) presenter.ConfigureAttackOffset(new Vector3(actor is PlayerBattleActor ? .06f : -.06f, 0, 0));
            EditorUtility.SetDirty(cracks); EditorUtility.SetDirty(presenter);
        }
        [MenuItem("工具/卡牌战斗/示例场景/刷新能量界面")]
        public static void RefreshEnergyHud()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != ScenePath) throw new InvalidOperationException("Open SingleBattleDemo in Edit mode first.");
            var hud = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SingleBattleHud>(true)).Single();
            var panel = (RectTransform)hud.transform.Find("Player");
            var label = panel.Find("Energy").GetComponent<TMP_Text>();
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, Mathf.Max(panel.sizeDelta.y, 176));
            label.rectTransform.sizeDelta = new Vector2(label.rectTransform.sizeDelta.x, Mathf.Max(label.rectTransform.sizeDelta.y, 44));
            label.text = "能量 3 / 3\n出牌 1 · 弃牌 1";
            EditorUtility.SetDirty(label); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        [MenuItem("工具/卡牌战斗/示例场景/准备运行时牌区")]
        public static void PrepareRuntimeCardZones()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != ScenePath) throw new InvalidOperationException("Open SingleBattleDemo in Edit mode first.");
            var host = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SingleBattleHost>(true)).Single();
            PrepareRuntimeCardZones(scene, host);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("SingleBattleDemo saved without editor card seeds. The battle supplies zones and deals at the first player turn.");
        }
        [MenuItem("工具/卡牌战斗/场景接入/移除旧出牌区表现")]
        public static void RemovePlayedZonePresentation()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode before removing the old played zone.");
            var scene = SceneManager.GetActiveScene();
            var hosts = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SingleBattleHost>(true)).ToArray();
            if (hosts.Length == 0) throw new InvalidOperationException("当前场景没有 SingleBattleHost。");
            Undo.SetCurrentGroupName("移除出牌区表现"); int group = Undo.GetCurrentGroup();
            foreach (var host in hosts) RemovePlayedPresentation(host);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("已移除出牌区及飞行路径；出牌只更新手牌与战斗数据，后续表现待配置。");
        }
        internal static void RemovePlayedPresentation(SingleBattleHost host)
        {
            var actions = host.Actions;
            if (actions == null || actions.Hand == null) throw new InvalidOperationException("Bind the hand action presenter first.");
            var scene = host.gameObject.scene;
            var zones = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PlayedZoneView>(true))
                .Where(zone => zone.GetComponent<CardZonePlayHost>()?.Match == host.Match).ToArray();
            var routes = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CardZoneTransferPresenter>(true))
                .Where(route => route.Source == actions.Hand.GetComponent<HandZoneView>() || zones.Contains(route.Destination)).ToArray();
            foreach (var route in routes) route.CompleteTransfers();
            Undo.RecordObject(actions, "解除出牌路径绑定"); actions.Configure(actions.Hand, actions.Panel);
            EditorUtility.SetDirty(actions); PrefabUtility.RecordPrefabInstancePropertyModifications(actions);
            foreach (var route in routes) Undo.DestroyObjectImmediate(route);
            foreach (var zone in zones)
            {
                zone.GetComponent<CardZonePlayHost>().StopSession(); zone.Clear();
                Undo.DestroyObjectImmediate(zone.gameObject);
            }
        }

        private static void PrepareRuntimeCardZones(Scene scene, SingleBattleHost host)
        {
            RemovePlayedPresentation(host);
            host.Actions.Hand.StopSession();
            host.Match.ConfigureRuntimeManaged(); EditorUtility.SetDirty(host.Match);
            foreach (var zone in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CardZonePlayHost>(true)).Where(zone => zone.Match == host.Match))
            {
                zone.ConfigureRuntimeBinding(host.Match); EditorUtility.SetDirty(zone);
            }
            foreach (var overview in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DeckOverviewPresenter>(true)).Where(overview => overview.Match == host.Match))
            {
                overview.ClearAuthoringRoster(); EditorUtility.SetDirty(overview);
            }
            foreach (var pile in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DrawPilePresenter>(true)).Where(pile => pile.Source != null && pile.Source.Match == host.Match))
                pile.Refresh();
        }
        private static GameObject FindSceneObject(Scene scene, string name) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).SingleOrDefault(item => item.name == name)?.gameObject;

        public const string FoxPrefabPath = "Assets/Art/Models/Creatures/FoxStatue/VAT/FoxStatue_Collapse_VAT.prefab";
        public const string DragonPrefabPath = "Assets/Art/Models/Creatures/DragonGargoyle/VAT/DragonGargoyle_Collapse_VAT.prefab";

        [MenuItem("工具/卡牌战斗/示例场景/整理场景层级")]
        public static void Organize()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != ScenePath) throw new InvalidOperationException("Open SingleBattleDemo in Edit mode first.");
            var roots = scene.GetRootGameObjects();
            var host = roots.SelectMany(root => root.GetComponentsInChildren<SingleBattleHost>(true)).Single();
            if (host.Player == null || host.Enemy == null) throw new InvalidOperationException("Connect the battle actors first.");
            var fox = FindSceneObject(scene, "FoxStatue_Collapse_VAT");
            var dragon = host.Enemy.Visual.GetComponentsInChildren<MeshFilter>(true).Single().gameObject;
            var foxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FoxPrefabPath);
            var dragonAsset = AssetDatabase.LoadAssetAtPath<GameObject>(DragonPrefabPath);
            if (fox == null || foxAsset == null || dragonAsset == null) throw new InvalidOperationException("Both prepared VAT prefabs and the placed Fox model are required.");
            // Keep existing scene object/component identities and record their settings as prefab overrides.
            var before = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(item => item != host.Player.transform && item != host.Player.Visual)
                .ToDictionary(item => item, item => item.localToWorldMatrix);
            ConnectModelPrefab(fox, foxAsset);
            ConnectModelPrefab(dragon, dragonAsset);
            if (host.Player.Visual.childCount == 0) host.Player.transform.position = fox.transform.position;
            fox.transform.SetParent(host.Player.Visual, true);

            var battle = Group(scene, "Battle");
            var cards = Group(scene, "Card Table");
            var environment = Group(scene, "Environment");
            var cameras = Group(scene, "Cameras");
            var ui = Group(scene, "UI");
            var lighting = ChildGroup(environment, "Lighting");
            foreach (var root in roots)
            {
                if (root == host.gameObject || root == host.Player.transform.parent.gameObject) root.transform.SetParent(battle, true);
                else if (root.GetComponent<HandZoneView>() != null) { root.name = "Hand Zone"; root.transform.SetParent(cards, true); }
                else if (root.GetComponent<PlayedZoneView>() != null) { root.name = "Played Zone"; root.transform.SetParent(cards, true); }
                else if (root.GetComponent<Camera>() != null) root.transform.SetParent(cameras, true);
                else if (root.GetComponent<Light>() != null) root.transform.SetParent(lighting, true);
                else if (root.name == "TabletopCombat_Blockout") root.transform.SetParent(environment, true);
                else if (root.GetComponent<Canvas>() != null || root.GetComponent<UnityEngine.EventSystems.EventSystem>() != null || root.GetComponent<CardHoverPresenter>() != null)
                    root.transform.SetParent(ui, true);
            }
            // Retain the old empty enemy locator as a scene marker rather than a visual child.
            var marker = host.Enemy.Visual.Find("Enemy");
            if (marker != null && marker.childCount == 0 && marker.GetComponents<Component>().Length == 1)
            { marker.name = "Enemy Anchor"; marker.SetParent(ChildGroup(environment, "Markers"), true); }
            var groups = new[] { battle, cards, environment, cameras, ui };
            for (int i = 0; i < groups.Length; ++i) groups[i].SetSiblingIndex(i);
            foreach (var entry in before)
                for (int i = 0; i < 16; ++i)
                    if (Mathf.Abs(entry.Key.localToWorldMatrix[i] - entry.Value[i]) > .0001f)
                        throw new InvalidOperationException("World pose changed during organization: " + entry.Key.name);
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(fox) != FoxPrefabPath ||
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(dragon) != DragonPrefabPath)
                throw new InvalidOperationException("VAT prefab connection was not retained.");
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = host.Player.gameObject;
            Debug.Log("SingleBattleDemo organized and saved: five groups, Fox hero and Dragon enemy prefab instances; " + before.Count + " existing world poses preserved.");
        }
        private static void ConnectModelPrefab(GameObject model, GameObject asset)
        {
            var currentPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(model);
            if (currentPath == AssetDatabase.GetAssetPath(asset)) return;
            if (!string.IsNullOrEmpty(currentPath))
            {
                // The scene Dragon started as an FBX instance with its mesh/material replaced by VAT.
                // Retain those overrides while moving the instance to the prepared art prefab.
                if (!currentPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) ||
                    PrefabUtility.GetOutermostPrefabInstanceRoot(model) != model)
                    throw new InvalidOperationException("The model is already connected to another prefab: " + currentPath);
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }
            PrefabUtility.ConvertToPrefabInstance(model, asset, new ConvertToPrefabInstanceSettings
            {
                objectMatchMode = ObjectMatchMode.ByHierarchy, changeRootNameToAssetName = false,
                componentsNotMatchedBecomesOverride = true, gameObjectsNotMatchedBecomesOverride = true,
                recordPropertyOverridesOfMatches = true
            }, InteractionMode.AutomatedAction);
        }
        private static Transform Group(Scene scene, string name)
        {
            var go = scene.GetRootGameObjects().SingleOrDefault(root => root.name == name);
            if (go == null) { go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene); }
            return go.transform;
        }
        private static Transform ChildGroup(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child == null) { var go = new GameObject(name); go.transform.SetParent(parent, false); child = go.transform; }
            return child;
        }
        private static RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.layer = 5; go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = rect.pivot = anchor; rect.anchoredPosition = position; rect.sizeDelta = size;
            var background = go.GetComponent<Image>(); background.color = Ink; background.raycastTarget = false; return rect;
        }
        private static TMP_Text Label(Transform parent, string name, string value, int size, Vector2 position, Vector2 dimensions, Color color, bool center = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.layer = 5; go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = dimensions;
            var text = go.GetComponent<TextMeshProUGUI>(); text.font = font; text.fontSize = size; text.text = value; text.color = color;
            text.alignment = center ? TextAlignmentOptions.Center : TextAlignmentOptions.TopLeft; text.raycastTarget = false;
            text.enableWordWrapping = false; return text;
        }
        private static Image Bar(RectTransform parent, Color color)
        {
            var back = Panel(parent, "Health Bar", new Vector2(0, 1), new Vector2(14, -104), new Vector2(217, 8)); back.GetComponent<Image>().color = new Color32(52, 56, 59, 255);
            var fill = Panel(back, "Fill", Vector2.zero, Vector2.zero, Vector2.zero); fill.anchorMax = Vector2.one; fill.offsetMin = fill.offsetMax = Vector2.zero;
            fill.GetComponent<Image>().color = color; return fill.GetComponent<Image>();
        }
        private static Button MakeButton(RectTransform rect, string title)
        {
            var image = rect.GetComponent<Image>(); image.color = new Color32(89, 72, 49, 255); image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var text = Label(rect, "Label", title, 20, new Vector2(0, -4), rect.sizeDelta - new Vector2(0, 8), Paper, true); text.alignment = TextAlignmentOptions.Center;
            return button;
        }
        [MenuItem("工具/卡牌战斗/截图/捕获游戏画面")]
        public static void Capture()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play first.");
            System.IO.Directory.CreateDirectory("Library/SingleBattleTests");
            ScreenCapture.CaptureScreenshot("Library/SingleBattleTests/demo.png");
        }
    }
}
