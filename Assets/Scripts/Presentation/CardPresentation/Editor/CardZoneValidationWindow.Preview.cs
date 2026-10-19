#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using BattlePresentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CardPresentation.Editor
{
    public sealed partial class CardZoneValidationWindow
    {
        private CardZonePreviewSession preview;
        private double previewTick;
        private string[] lastPreviewEvents = Array.Empty<string>();
        private UnityEditor.Editor frontendEditor;
        [SerializeField] private string previewAction = CardZoneActions.Draw;
        [SerializeField] private bool advancedPreview;
        private static readonly System.Random sampleRandom = new System.Random();
        [SerializeField] private string targetIds = "preview-1";
        [SerializeField] private string afterJson;
        [SerializeField] private string parametersJson = "{\"items\":[]}";
        [Serializable] private sealed class PreviewCard
        { public string viewId, cardId, artKey; public bool hidden, selected, hovered; }
        [Serializable] private sealed class PreviewZone { public PreviewCard[] cards = Array.Empty<PreviewCard>(); }
        [Serializable] private sealed class Parameter { public string key, value; }
        [Serializable] private sealed class Parameters { public Parameter[] items = Array.Empty<Parameter>(); }
        private IEnumerable<ActionDescriptor> PreviewCapabilities => Presentation.Capabilities.Where(capability => capability.Key != CardZoneActions.State);
        public CardZonePreviewSession Preview => preview;
        public void BeginPreview()
        {
            if (preview != null) return;
            preview = new CardZonePreviewSession(frontend); lastPreviewEvents = Array.Empty<string>(); previewTick = EditorApplication.timeSinceStartup;
        }
        public void StopPreview()
        {
            var previous = preview; preview = null;
            if (previous != null)
            {
                try {
                    previous.Dispose(); status = "预览已停止，原展示已恢复。";
                    lastPreviewEvents = previous.Events.Concat(new[] { previous.Time.ToString("F3") + "s · " +
                        (previous.Operation?.Status.ToString() ?? "预览结束") + " · 原展示已恢复" }).ToArray();
                }
                catch (Exception exception) { error = "恢复预览失败：" + exception.Message; }
                SceneView.RepaintAll();
            }
        }
        private void OnSceneSaving(Scene scene, string path) => StopPreview();
        private void AdvanceEditorPreview()
        {
            if (preview == null) return;
            if (ChangingMode || Application.isPlaying || frontend == null || !frontend.isActiveAndEnabled) { StopPreview(); return; }
            double now = EditorApplication.timeSinceStartup;
            try { preview.Advance((float)Math.Min(.05, Math.Max(0, now - previewTick))); }
            catch (Exception exception) { StopPreview(); error = exception.Message; }
            previewTick = now; SceneView.RepaintAll(); EditorApplication.QueuePlayerLoopUpdate();
        }
        private void ReleaseFrontendEditor() { if (frontendEditor != null) DestroyImmediate(frontendEditor); frontendEditor = null; }
        private void DrawFrontendSettings()
        {
            if (frontend == null) return;
            EditorGUILayout.Space(); EditorGUILayout.LabelField("能力实现参数", EditorStyles.boldLabel);
            UnityEditor.Editor.CreateCachedEditor(frontend, null, ref frontendEditor);
            using (new EditorGUI.DisabledScope(preview != null || ChangingMode || Application.isPlaying)) frontendEditor.OnInspectorGUI();
        }
        private void DrawDisplaySnapshot()
        {
            var current = Presentation?.Current;
            if (current == null) { EditorGUILayout.LabelField("暂无展示快照"); return; }
            EditorGUILayout.LabelField("牌区 / 当前展示数量", current.Id + " / " + current.Cards.Count);
            foreach (var card in current.Cards)
                EditorGUILayout.LabelField(card.ViewId, card.Hidden ? "隐藏" : card.ArtKey ?? card.FaceKey);
        }
        private void DrawCapabilityDiagnostics()
        {
            if (Presentation == null) return;
            EditorGUILayout.LabelField("表现状态", Presentation.IsPresenting ? "进行中" : "空闲");
            foreach (var capability in PreviewCapabilities)
            {
                EditorGUILayout.LabelField(capability.Key, EditorStyles.boldLabel);
                EditorGUILayout.LabelField("信号", string.Join(", ", capability.Markers));
                EditorGUILayout.LabelField("通道", string.Join(", ", capability.Channels));
            }
        }
        public static CardZoneDisplayData RandomEightCards(CardZoneDisplayData current = null)
        {
            var faces = (from suit in new[] { "clubs", "diamonds", "hearts", "spades" }
                         from rank in Enumerable.Range(2, 13)
                         select suit + "_" + (rank == 14 ? "a" : rank == 13 ? "k" : rank == 12 ? "q" : rank == 11 ? "j" : rank.ToString("00"))).ToArray();
            for (int i = faces.Length - 1; i > 0; i--)
            { int j = sampleRandom.Next(i + 1); var value = faces[i]; faces[i] = faces[j]; faces[j] = value; }
            return new CardZoneDisplayData(current?.Id ?? "editor-preview", current?.OwnerId, current?.Kind ?? "hand",
                faces.Take(8).Select((art, i) => new CardDisplayData("preview-" + (i + 1), "preview-" + (i + 1), art, art)));
        }
        private CardZoneDisplayData Sample() => RandomEightCards(Presentation?.Current);
        public void LoadRandomHand()
        {
            BeginPreview(); var sample = Sample(); preview.Load(sample); afterJson = Json(sample);
            targetIds = string.Join(",", sample.Cards.Take(2).Select(card => card.ViewId));
        }
        private static string ActionLabel(string key)
        {
            switch (key)
            {
                case CardZoneActions.Draw: return "抽牌";
                case CardZoneActions.Arrange: return "理牌";
                case CardZoneActions.Prepare: return "出牌准备";
                case CardZoneActions.Play: return "出牌";
                case CardZoneActions.Discard: return "弃牌";
                case CardZoneActions.Selection: return "选牌 / 切换";
                case CardZoneActions.Settle: return "等待落定";
                case "cards.prepare-discard": return "弃牌准备";
                default: return key;
            }
        }
        public void PlayConfiguredAction(string key)
        {
            if (!PreviewCapabilities.Any(capability => capability.Key == key)) throw new ArgumentException("该表现未配置此动作。");
            if (preview == null || Presentation.Current == null || Presentation.Current.Cards.Count == 0) LoadRandomHand();
            previewAction = key;
            if (key == CardZoneActions.Draw)
            {
                var sample = Sample(); preview.Load(new CardZoneDisplayData(sample.Id, sample.OwnerId, sample.Kind, null));
                afterJson = Json(sample); targetIds = string.Join(",", sample.Cards.Select(card => card.ViewId));
            }
            else
            {
                var visible = Presentation.Current.Cards;
                var ids = RequestedIds().Where(id => visible.Any(card => card.ViewId == id)).ToArray();
                if (ids.Length == 0 && key != CardZoneActions.Selection) ids = visible.Take(1).Select(card => card.ViewId).ToArray();
                targetIds = string.Join(",", ids); BuildAfter();
            }
            var values = JsonUtility.FromJson<Parameters>(parametersJson)?.items ?? Array.Empty<Parameter>();
            preview.Play(key, ReadAfter(), RequestedIds(), values.ToDictionary(item => item.key, item => item.value));
        }
        private void DrawTargetCards()
        {
            if (preview == null || Presentation.Current == null) return;
            var cards = Presentation.Current.Cards;
            var selected = new HashSet<string>(RequestedIds());
            EditorGUILayout.LabelField("勾选动作目标牌", EditorStyles.boldLabel);
            for (int row = 0; row < cards.Count; row += 4)
            using (new EditorGUILayout.HorizontalScope())
                foreach (var card in cards.Skip(row).Take(4))
                {
                    bool on = GUILayout.Toggle(selected.Contains(card.ViewId), CardLabel(card), "Button");
                    if (on) selected.Add(card.ViewId); else selected.Remove(card.ViewId);
                }
            targetIds = string.Join(",", cards.Where(card => selected.Contains(card.ViewId)).Select(card => card.ViewId));
        }
        private static string CardLabel(CardDisplayData card)
        {
            if (card.Hidden) return "暗牌";
            var parts = (card.ArtKey ?? card.FaceKey ?? card.ViewId).Split('_');
            if (parts.Length != 2) return card.ArtKey ?? card.ViewId;
            string suit = parts[0] == "clubs" ? "梅花" : parts[0] == "diamonds" ? "方块" : parts[0] == "hearts" ? "红桃" : parts[0] == "spades" ? "黑桃" : parts[0];
            return suit + " " + parts[1].TrimStart('0').ToUpperInvariant();
        }
        private static string Json(CardZoneDisplayData zone) => JsonUtility.ToJson(new PreviewZone { cards = zone.Cards.Select(card =>
            new PreviewCard { viewId = card.ViewId, cardId = card.CardId, artKey = card.ArtKey, hidden = card.Hidden,
                selected = card.Properties.TryGetValue("selected", out var selected) && bool.TryParse(selected, out var s) && s,
                hovered = card.Properties.TryGetValue("hovered", out var hovered) && bool.TryParse(hovered, out var h) && h }).ToArray() }, true);
        private CardZoneDisplayData ReadAfter()
        {
            var before = Presentation.Current ?? throw new InvalidOperationException("先载入预览数据。");
            var dto = JsonUtility.FromJson<PreviewZone>(afterJson) ?? throw new ArgumentException("After JSON 无效。");
            if (dto.cards == null) throw new ArgumentException("After 必须包含 cards 数组。");
            return new CardZoneDisplayData(before.Id, before.OwnerId, before.Kind, dto.cards.Select(card =>
                new CardDisplayData(card.viewId, card.cardId, null, card.artKey, card.hidden, properties:
                    new Dictionary<string, string> { ["selected"] = card.selected.ToString(), ["hovered"] = card.hovered.ToString() })));
        }
        private string[] RequestedIds() => targetIds.Split(new[] { ',', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(id => id.Trim()).Where(id => id.Length > 0).ToArray();
        private void BuildAfter()
        {
            var current = Presentation.Current ?? throw new InvalidOperationException("先载入预览数据。");
            var ids = RequestedIds(); var cards = current.Cards.AsEnumerable();
            if (previewAction == CardZoneActions.Draw) { afterJson = Json(Sample()); return; }
            if (previewAction == CardZoneActions.Play || previewAction == CardZoneActions.Discard) cards = cards.Where(card => !ids.Contains(card.ViewId) && !ids.Contains(card.CardId));
            if (previewAction == CardZoneActions.Arrange) cards = cards.Reverse();
            if (previewAction == CardZoneActions.Selection) cards = cards.Select(card => new CardDisplayData(card.ViewId, card.CardId, card.FaceKey,
                card.ArtKey, card.Hidden, card.DisabledReason, card.Properties.Concat(new[] { new KeyValuePair<string, string>("selected", (ids.Contains(card.ViewId) || ids.Contains(card.CardId)).ToString()) })
                    .GroupBy(pair => pair.Key).ToDictionary(group => group.Key, group => group.Last().Value)));
            afterJson = Json(new CardZoneDisplayData(current.Id, current.OwnerId, current.Kind, cards));
        }
        private void DrawPresentationPreview()
        {
            if (Presentation == null) { EditorGUILayout.HelpBox("选择任意 ICardZonePresentation 实现。", MessageType.Info); return; }
            if (!(frontend is ICardZonePresentationPreview)) { EditorGUILayout.HelpBox("该实现需要提供 ICardZonePresentationPreview.AdvancePreview 手动时钟后才能编辑器预览。", MessageType.Info); return; }
            using (new EditorGUI.DisabledScope(Application.isPlaying || ChangingMode))
            {
                EditorGUILayout.HelpBox("点击下方已配置动作即可用八张随机牌预览，不需要填写 ID 或 JSON。停止时恢复原展示，不提交牌局命令。", MessageType.None);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(preview == null ? "载入八张随机牌" : "换一手八张随机牌")) Run(LoadRandomHand);
                    using (new EditorGUI.DisabledScope(preview == null))
                        if (GUILayout.Button("取消并恢复")) StopPreview();
                }
                EditorGUILayout.LabelField("固定八张 · 标准 52 张牌中不重复抽取", EditorStyles.miniLabel);
                DrawTargetCards();
                EditorGUILayout.Space(); EditorGUILayout.LabelField("已配置的表现动作 · 点击即播放", EditorStyles.boldLabel);
                foreach (var capability in PreviewCapabilities)
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(ActionLabel(capability.Key), GUILayout.Width(130))) Run(() => PlayConfiguredAction(capability.Key));
                    EditorGUILayout.LabelField(string.Join(" → ", capability.Markers), EditorStyles.miniLabel);
                }
                if (PreviewCapabilities.Any(capability => capability.Key == CardZoneActions.Selection) && GUILayout.Button("取消所有选牌"))
                    Run(() => { if (preview == null) LoadRandomHand(); targetIds = ""; PlayConfiguredAction(CardZoneActions.Selection); });
                advancedPreview = EditorGUILayout.Foldout(advancedPreview, "高级：自定义快照与参数", true);
                if (advancedPreview)
                {
                    var keys = PreviewCapabilities.Select(capability => capability.Key).ToArray();
                    if (keys.Length == 0) { EditorGUILayout.LabelField("此实现未声明动作能力"); return; }
                    previewAction = keys[EditorGUILayout.Popup("表现动作", Math.Max(0, Array.IndexOf(keys, previewAction)), keys)];
                    targetIds = EditorGUILayout.TextField("目标 ViewId（逗号分隔）", targetIds);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("按动作生成 After")) Run(BuildAfter);
                        if (GUILayout.Button("读取当前展示到 After")) Run(() => afterJson = Json(Presentation.Current ?? throw new InvalidOperationException("暂无展示数据。")));
                    }
                    EditorGUILayout.LabelField("After 展示数据（可修改牌面、选中、顺序等）");
                    afterJson = EditorGUILayout.TextArea(afterJson ?? "{\"cards\":[]}", GUILayout.MinHeight(100));
                    EditorGUILayout.LabelField("自定义参数 JSON：items[{key,value}]");
                    parametersJson = EditorGUILayout.TextArea(parametersJson, GUILayout.MinHeight(35));
                    if (GUILayout.Button("播放高级配置")) Run(() => {
                        var after = ReadAfter(); var values = JsonUtility.FromJson<Parameters>(parametersJson).items.ToDictionary(item => item.key, item => item.value);
                        BeginPreview(); preview.Play(previewAction, after, RequestedIds(), values);
                    });
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(preview == null))
                    {
                        if (GUILayout.Button(preview?.Paused == true ? "继续" : "暂停")) preview.Paused = !preview.Paused;
                        if (GUILayout.Button("单步 1/60s")) Run(() => { preview.Paused = true; preview.Advance(1f / 60, true); SceneView.RepaintAll(); });
                    }
                }
                if (preview != null)
                {
                    EditorGUILayout.LabelField("操作状态", preview.Operation?.Status.ToString() ?? "数据已载入");
                }
                foreach (var entry in preview != null ? preview.Events : lastPreviewEvents) EditorGUILayout.LabelField(entry, EditorStyles.wordWrappedMiniLabel);
            }
        }
    }
}
#endif
