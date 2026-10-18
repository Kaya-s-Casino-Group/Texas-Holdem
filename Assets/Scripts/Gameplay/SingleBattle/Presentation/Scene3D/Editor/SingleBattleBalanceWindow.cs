using System;
using System.Collections.Generic;
using System.Linq;
using PokerEngine;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Poker.SingleBattle.Editor
{
    /// <summary>Scene-bound authoring only: never starts or modifies a running battle.</summary>
    public sealed class SingleBattleBalanceWindow : EditorWindow
    {
        private static readonly string[] Tabs = { "配置总览", "公共规则", "玩家配置", "敌人配置" };
        private static readonly string[] HandNames = { "高牌", "一对", "两对", "三条", "顺子", "同花", "葫芦", "四条", "同花顺", "五条" };
        [SerializeField] private SingleBattleSceneAdapter host;
        [SerializeField] private int tab;
        [SerializeField] private int playerIndex;
        [SerializeField] private int enemyIndex;
        [SerializeField] private Vector2 scroll;
        private SingleBattleSceneAdapter[] hosts = Array.Empty<SingleBattleSceneAdapter>();
        private int activeSceneHandle;
        private readonly Dictionary<Object, UnityEditor.Editor> editors = new Dictionary<Object, UnityEditor.Editor>();
        private readonly Dictionary<string, bool> folds = new Dictionary<string, bool>();
        private string validation;
        private MessageType validationType;

        [MenuItem("工具/卡牌战斗/数值配置", priority = 0)]
        public static void Open()
        {
            var window = GetWindow<SingleBattleBalanceWindow>("卡牌战斗数值配置");
            window.minSize = new Vector2(560, 560);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshHosts();
            EditorApplication.hierarchyChanged += RefreshHosts;
            Undo.undoRedoPerformed += OnUndo;
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= RefreshHosts;
            Undo.undoRedoPerformed -= OnUndo;
            foreach (var editor in editors.Values) if (editor != null) DestroyImmediate(editor);
            editors.Clear();
        }

        private void OnUndo() { validation = null; Repaint(); }

        private void RefreshHosts()
        {
            var scene = SceneManager.GetActiveScene();
            activeSceneHandle = scene.handle;
            hosts = scene.IsValid() && scene.isLoaded
                ? scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SingleBattleSceneAdapter>(true)).ToArray()
                : Array.Empty<SingleBattleSceneAdapter>();
            if (!hosts.Contains(host)) host = hosts.Length == 1 ? hosts[0] : null;
            validation = null;
            Repaint();
        }

        private void OnGUI()
        {
            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Clamp(position.width * 0.42f, 220, 320);
            try { DrawWindow(); }
            finally { EditorGUIUtility.labelWidth = labelWidth; }
        }

        private void DrawWindow()
        {
            if (activeSceneHandle != SceneManager.GetActiveScene().handle || hosts.Any(item => item == null)) RefreshHosts();
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("单局数值配置", EditorStyles.largeLabel);
            EditorGUILayout.LabelField("当前场景：" + SceneManager.GetActiveScene().name, EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                var names = new[] { "请选择当前场景中的战局" }.Concat(hosts.Select(item => HierarchyPath(item.transform))).ToArray();
                int current = Array.IndexOf(hosts, host) + 1;
                int next = EditorGUILayout.Popup("战局入口", current, names);
                if (next != current) { host = next == 0 ? null : hosts[next - 1]; validation = null; scroll = Vector2.zero; }
                if (GUILayout.Button("刷新", GUILayout.Width(50))) RefreshHosts();
            }
            if (host == null)
            {
                EditorGUILayout.HelpBox(hosts.Length == 0 ? "当前场景没有单局战斗入口。请打开配置好 SingleBattleSceneAdapter 的对局场景。" : "场景中存在多个战局，请先选择要配置的入口。", MessageType.Info);
                return;
            }
            if (host.UsesXmlContent) { DrawXmlContent(); return; }
            EditorGUILayout.HelpBox("编辑的是当前场景绑定的原始配置资产；所有共用它的角色或场景都会受影响。修改在下次开战生效，支持撤销。保存按钮仅保存配置资产，场景绑定仍在 Inspector 中管理。", MessageType.Info);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                EditorGUILayout.HelpBox("运行期间只读。请退出运行模式后修改数值。", MessageType.Warning);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                int next = GUILayout.Toolbar(tab, Tabs, EditorStyles.toolbarButton);
                if (next != tab) { tab = next; scroll = Vector2.zero; }
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                switch (tab)
                {
                    case 0: DrawOverview(); break;
                    case 1: DrawRules(); break;
                    case 2: DrawPlayer(); break;
                    case 3: DrawEnemy(); break;
                }
            }
            EditorGUILayout.EndScrollView();
            if (!string.IsNullOrEmpty(validation)) EditorGUILayout.HelpBox(validation, validationType);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("校验当前战局配置", GUILayout.Height(28))) ValidateConfiguration();
                if (GUILayout.Button("保存当前战局配置资产", GUILayout.Height(28)) && ValidateConfiguration())
                {
                    int count = 0;
                    foreach (var asset in ConfigAssets())
                    {
                        if (!EditorUtility.IsDirty(asset)) continue;
                        AssetDatabase.SaveAssetIfDirty(asset);
                        count++;
                    }
                    validation = $"校验通过，已保存 {count} 份修改过的配置资产。";
                }
            }
            EditorGUILayout.Space(5);
        }

        private static string HierarchyPath(Transform item) => item.parent == null ? item.name : HierarchyPath(item.parent) + "/" + item.name;

        private void DrawXmlContent()
        {
            EditorGUILayout.HelpBox("当前场景使用 XML 内容配置。使用 External/BattleToolkit/Tools/BattleContentEditor 外部工具导入此文件、配置并导出覆盖；Unity 刷新后重新开战生效。", MessageType.Info);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("XML 内容包", host.ContentXml, typeof(TextAsset), false);
                EditorGUILayout.TextField("编成 ID", host.ContentEncounterId ?? "");
            }
            if (host.ContentXml != null)
            {
                EditorGUILayout.SelectableLabel(AssetDatabase.GetAssetPath(host.ContentXml), EditorStyles.wordWrappedLabel, GUILayout.Height(40));
                Locate(host.ContentXml, "定位 XML 文件");
            }
            Locate(host, "定位场景入口 / 修改编成");
            EditorGUILayout.HelpBox("场景玩家和敌人按 XML 编成的各自顺序绑定；属性、牌型、状态、遗物、牌组均从 XML 组装。", MessageType.None);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("统一校验内容和场景阵容（不启动对局）"))
                {
                    try
                    {
                        host.BuildConfiguredSession();
                        validation = "校验通过。未启动对局或修改当前会话。";
                        validationType = MessageType.Info;
                    }
                    catch (Exception error) { validation = error.Message; validationType = MessageType.Error; }
                }
            if (!string.IsNullOrEmpty(validation)) EditorGUILayout.HelpBox(validation, validationType);
        }

        private void DrawOverview()
        {
            Section("配置归属");
            EditorGUILayout.HelpBox("公共规则：回合上限、格挡清除、基础计分与每级成长。\n玩家：生命、能量、牌数、个人牌型等级、出牌伤害、弃牌收益、目标策略及伤害修正。\n敌人：生命、基础攻击、目标策略及伤害修正。", MessageType.None);
            AssetHeader(host.RulesProfile, "公共战局规则");
            Section("玩家阵容 · 按场景绑定顺序");
            for (int i = 0; i < host.Players.Count; i++)
            {
                var actor = host.Players[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"{i + 1}. {(actor == null ? "缺少角色" : actor.name)}");
                    if (GUILayout.Button("配置", GUILayout.Width(60))) { playerIndex = i; tab = 2; }
                }
                AssetHeader(actor == null ? null : actor.Profile, "玩家配置资产");
            }
            Section("敌人阵容 · 顺序即行动顺序");
            for (int i = 0; i < host.Enemies.Count; i++)
            {
                var actor = host.Enemies[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"{i + 1}. {(actor == null ? "缺少角色" : actor.name)}");
                    if (GUILayout.Button("配置", GUILayout.Width(60))) { enemyIndex = i; tab = 3; }
                }
                AssetHeader(actor == null ? null : actor.Profile, "敌人配置资产");
            }
            Section("保存范围");
            var assets = ConfigAssets();
            EditorGUILayout.LabelField($"关联配置 {assets.Count} 份，其中未保存 {assets.Count(EditorUtility.IsDirty)} 份。");
            EditorGUILayout.HelpBox("本窗口不修改场景阵容、开局牌组或 UI / 动画 / 音效。手牌选择上限由玩家的出牌与弃牌张数上限共同决定；开局发牌读取玩家的手牌上限。", MessageType.None);
            Locate(host, "定位场景战局入口");
        }

        private void DrawRules()
        {
            var profile = host.RulesProfile;
            if (!AssetHeader(profile, "公共战局规则")) { Locate(host, "定位战局入口并绑定公共规则"); return; }
            Edit(profile, data =>
            {
                Section("回合与格挡");
                Field(data, "version", "规则版本");
                Field(data, "roundLimit", "回合上限");
                Choice(data, "playerBlockExpiry", "玩家格挡清除时机", "保留至被消耗", "敌人阶段结束后清空");
            });
            Child(profile, "scoring", "共享计分基础表", "未绑定时使用引擎默认计分表。绑定计分资产后可在此编辑。", true);
        }

        private T ActorSelector<T>(IReadOnlyList<T> actors, ref int index, string label) where T : Component
        {
            if (actors.Count == 0) { EditorGUILayout.HelpBox("该战局尚未绑定" + label + "。", MessageType.Warning); return null; }
            index = Mathf.Clamp(index, 0, actors.Count - 1);
            index = EditorGUILayout.Popup(label, index, actors.Select((actor, i) => $"{i + 1}. {(actor == null ? "缺少角色" : actor.name)}").ToArray());
            var selected = actors[index];
            if (selected == null) EditorGUILayout.HelpBox("此阵容位置缺少角色，请在战局入口修复绑定。", MessageType.Error);
            else Locate(selected, "定位场景角色 / 更换配置资产");
            return selected;
        }

        private void DrawPlayer()
        {
            var actor = ActorSelector(host.Players, ref playerIndex, "玩家");
            if (actor == null || !AssetHeader(actor.Profile, "玩家配置资产")) return;
            var profile = actor.Profile;
            Edit(profile, data =>
            {
                Vitals(data);
                Section("能量与操作费用");
                Field(data, "maxEnergy", "能量上限 / 每轮恢复值");
                Field(data, "initialEnergy", "初始能量（可超过上限）");
                Field(data, "playEnergyCost", "出牌消耗能量");
                Field(data, "discardEnergyCost", "弃牌消耗能量");
                Section("手牌与弃牌次数");
                Field(data, "handSize", "手牌上限 / 开局发牌数");
                Field(data, "maxPlayCards", "单次出牌张数上限");
                Field(data, "maxDiscardCards", "单次弃牌张数上限");
                Field(data, "unlimitedDiscards", "不限制整场弃牌次数");
                using (new EditorGUI.DisabledScope(data.FindProperty("unlimitedDiscards").boolValue))
                    Field(data, "discards", "整场弃牌次数");
                EditorGUILayout.HelpBox("不限次数仍消耗能量。实际动作张数不超过手牌上限。", MessageType.None);
                Choice(data, "playRefillTiming", "出牌后补牌", "下个玩家回合", "出牌后立即补牌");
                Choice(data, "discardRefillTiming", "弃牌后补牌", "下个玩家回合", "弃牌后立即补牌");
                DrawHandLevels(data);
            });
            Section("玩家出牌与弃牌规则");
            Child(profile, "damage", "出牌伤害", "必须绑定出牌伤害配置。", true);
            Child(profile, "discardReward", "弃牌收益", "未绑定：弃牌无额外收益。", true);
            Child(profile, "supportTargets", "友方收益目标", "未绑定：自身或命令指定的存活队友。");
            ActorPolicies(profile);
        }

        private void DrawEnemy()
        {
            var actor = ActorSelector(host.Enemies, ref enemyIndex, "敌人");
            if (actor == null || !AssetHeader(actor.Profile, "敌人配置资产")) return;
            Edit(actor.Profile, data => { Vitals(data); Field(data, "attackDamage", "基础攻击伤害"); });
            EditorGUILayout.HelpBox("敌人意图与实际伤害都经过：基础攻击 → 自身攻击修正 → 目标承伤修正 → 格挡。", MessageType.None);
            ActorPolicies(actor.Profile);
        }

        private void ActorPolicies(BattleActorProfile profile)
        {
            Section("角色独立规则");
            Child(profile, "attackTargets", "攻击目标策略", "未绑定：首个存活对手。", true);
            Child(profile, "attackModifier", "攻击修正", "未绑定：攻击伤害不修正。", true);
            Child(profile, "incomingModifier", "承伤修正", "未绑定：承受伤害不修正。", true);
        }

        private static void Vitals(SerializedObject data)
        {
            Section("基础属性");
            Field(data, "unitId", "单位定义 ID");
            Field(data, "maxHealth", "最大生命");
            Field(data, "initialBlock", "初始格挡");
        }

        private static void DrawHandLevels(SerializedObject data)
        {
            Section("个人牌型等级");
            EditorGUILayout.HelpBox("最低 1 级，未配置的牌型按 1 级。候选预览和实际计分读取同一份玩家等级；基础值与每级成长在公共规则中配置。", MessageType.None);
            var levels = data.FindProperty("handLevels");
            for (int category = 1; category <= HandNames.Length; category++)
            {
                SerializedProperty entry = null;
                for (int i = 0; i < levels.arraySize; i++)
                {
                    var candidate = levels.GetArrayElementAtIndex(i);
                    if (candidate.FindPropertyRelative("category").intValue == category) { entry = candidate; break; }
                }
                EditorGUI.BeginChangeCheck();
                int value = EditorGUILayout.IntField(HandNames[category - 1], entry == null ? 1 : entry.FindPropertyRelative("level").intValue);
                if (!EditorGUI.EndChangeCheck()) continue;
                if (entry == null)
                {
                    entry = levels.GetArrayElementAtIndex(levels.arraySize++);
                    entry.FindPropertyRelative("category").intValue = category;
                }
                entry.FindPropertyRelative("level").intValue = Math.Max(1, value);
            }
        }

        private void Child(ScriptableObject parent, string property, string title, string empty, bool expanded = false)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                string key = parent.GetInstanceID() + "/" + property;
                bool open = folds.TryGetValue(key, out var previous) ? previous : expanded;
                open = EditorGUILayout.Foldout(open, title, true);
                folds[key] = open;
                if (!open) return;
                ScriptableObject child = null;
                Edit(parent, data =>
                {
                    var reference = data.FindProperty(property);
                    EditorGUILayout.PropertyField(reference, new GUIContent("配置资产"));
                    child = reference.objectReferenceValue as ScriptableObject;
                });
                if (child == null) { EditorGUILayout.HelpBox(empty, property == "damage" ? MessageType.Error : MessageType.Info); return; }
                AssetHeader(child, "当前编辑资产");
                if (child is ScaledAttackDamageProfile || child is ScaledIncomingDamageProfile)
                {
                    EditorGUILayout.HelpBox("输出 = 向下取整（最大值（0，输入 × 倍率 + 固定增减））。承伤修正在格挡前应用。", MessageType.None);
                    Edit(child, data => { Field(data, "version", "规则版本"); Field(data, "multiplier", "倍率"); Field(data, "flatAdjustment", "固定增减值"); });
                }
                else if (child is OpponentTargetPolicyProfile)
                    Edit(child, data => { Choice(data, "selection", "目标选择方式", "首个存活对手", "当前生命最低的对手", "当前生命最高的对手", "全部存活对手"); Field(data, "allowRequestedTargets", "允许命令指定目标"); });
                else if (child is PerCardBlockRewardProfile)
                    Edit(child, data => { Field(data, "version", "规则版本"); Field(data, "blockPerCard", "每张弃牌获得格挡"); });
                else if (child is SelfOrRequestedAllyProfile)
                    EditorGUILayout.HelpBox("收益给予自己或命令指定的存活队友。此策略没有额外数值。", MessageType.None);
                else if (child is ScoreDamageProfile || child is NoDiscardRewardProfile)
                {
                    EditorGUILayout.HelpBox(child is ScoreDamageProfile ? "出牌得分等额转换为基础伤害，再应用攻击与承伤修正。" : "弃牌不产生额外收益。", MessageType.None);
                    Edit(child, data => Field(data, "version", "规则版本"));
                }
                else
                {
                    // Reuse the scoring asset's Chinese inspector, and support future profile implementations.
                    if (!editors.TryGetValue(child, out var editor) || editor == null) editors[child] = editor = UnityEditor.Editor.CreateEditor(child);
                    EditorGUI.BeginChangeCheck();
                    editor.OnInspectorGUI();
                    if (EditorGUI.EndChangeCheck()) validation = null;
                }
            }
        }

        private void Edit(ScriptableObject target, Action<SerializedObject> draw)
        {
            using (var data = new SerializedObject(target))
            {
                data.Update();
                draw(data);
                if (data.ApplyModifiedProperties()) validation = null;
            }
        }

        private static void Field(SerializedObject data, string property, string label) => EditorGUILayout.PropertyField(data.FindProperty(property), new GUIContent(label));
        private static void Choice(SerializedObject data, string property, string label, params string[] choices)
        {
            var field = data.FindProperty(property);
            EditorGUI.BeginChangeCheck();
            int value = EditorGUILayout.Popup(label, field.intValue, choices);
            if (EditorGUI.EndChangeCheck()) field.intValue = value;
        }
        private static void Section(string text) { EditorGUILayout.Space(10); EditorGUILayout.LabelField(text, EditorStyles.boldLabel); }
        private static void Locate(Object item, string label)
        {
            if (item != null && GUILayout.Button(label)) { Selection.activeObject = item; EditorGUIUtility.PingObject(item); }
        }
        private static bool AssetHeader(Object asset, string title)
        {
            if (asset == null) { EditorGUILayout.HelpBox(title + "：未绑定。", MessageType.Warning); return false; }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField(title, asset, asset.GetType(), false);
                if (GUILayout.Button("定位", GUILayout.Width(45))) { Selection.activeObject = asset; EditorGUIUtility.PingObject(asset); }
            }
            EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(asset), EditorStyles.wordWrappedMiniLabel);
            return true;
        }

        private List<ScriptableObject> ConfigAssets()
        {
            var assets = new HashSet<ScriptableObject>();
            Action<ScriptableObject> collect = null;
            collect = asset =>
            {
                if (asset == null || !assets.Add(asset)) return;
                using (var data = new SerializedObject(asset))
                {
                    var property = data.GetIterator();
                    while (property.Next(true))
                        if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue is ScriptableObject child) collect(child);
                }
            };
            collect(host.RulesProfile);
            foreach (var actor in host.Players) if (actor != null) collect(actor.Profile);
            foreach (var actor in host.Enemies) if (actor != null) collect(actor.Profile);
            return assets.OrderBy(AssetDatabase.GetAssetPath).ToList();
        }

        private bool ValidateConfiguration()
        {
            var errors = new List<string>();
            void Check(string label, Action build)
            {
                try { build(); }
                catch (Exception error) { errors.Add(label + "：" + error.Message); }
            }
            Check("公共规则", () =>
            {
                if (host.RulesProfile == null) throw new InvalidOperationException("未绑定公共规则资产。");
                host.RulesProfile.BuildRules(); host.RulesProfile.BuildScoring();
            });
            if (host.Players.Count == 0 || host.Enemies.Count == 0) errors.Add("阵容：玩家和敌人均至少需要一位。");
            if (host.Players.Count + host.Enemies.Count > 64) errors.Add("阵容：双方总单位数不能超过 64。");
            var seen = new HashSet<Component>();
            for (int i = 0; i < host.Players.Count; i++)
            {
                var actor = host.Players[i];
                Check($"玩家 {i + 1}", () =>
                {
                    if (actor == null || actor.Profile == null) throw new InvalidOperationException("角色或配置未绑定。");
                    if (!seen.Add(actor)) throw new InvalidOperationException("同一角色在阵容中重复绑定。");
                    actor.Profile.BuildSetup(new ParticipantId("config/player/" + i));
                });
            }
            for (int i = 0; i < host.Enemies.Count; i++)
            {
                var actor = host.Enemies[i];
                Check($"敌人 {i + 1}", () =>
                {
                    if (actor == null || actor.Profile == null) throw new InvalidOperationException("角色或配置未绑定。");
                    if (!seen.Add(actor)) throw new InvalidOperationException("同一角色在阵容中重复绑定。");
                    actor.Profile.BuildSetup(new ParticipantId("config/enemy/" + i));
                });
            }
            validationType = errors.Count == 0 ? MessageType.Info : MessageType.Error;
            validation = errors.Count == 0 ? "静态配置校验通过。未启动对局或执行战斗动作。" : "配置校验未通过：\n" + string.Join("\n", errors);
            return errors.Count == 0;
        }
    }
}
