using System;
using System.Linq;
using PokerEngine;
using UnityEditor;
using UnityEngine;

namespace CardPresentation.Editor
{
    public sealed partial class CardZoneValidationWindow
    {
        [SerializeField] private string insertArtId = "hearts_q", inspectedDeckId;
        [SerializeField] private int insertCount = 1, insertPosition = -1;
        [SerializeField] private bool showSamples;
        private Vector2 handScroll, deckScroll;

        private void DrawHandPage(string[] artIds)
        {
            DrawDiscardAction();
            EditorGUILayout.LabelField("添加卡牌", EditorStyles.boldLabel);
            insertArtId = CardPicker("牌面", insertArtId, artIds);
            insertCount = Mathf.Clamp(EditorGUILayout.IntField("数量", insertCount), 1, CurrentCapacity);
            insertPosition = InsertionPicker("插入位置", insertPosition, CurrentModel.Cards.Count);
            using (new EditorGUI.DisabledScope(insertCount > CurrentCapacity - CurrentModel.Cards.Count))
                if (GUILayout.Button(Hand != null ? "插入手牌" : "插入牌区")) Mutate("插入卡牌", () => {
                    int index = insertPosition < 0 ? CurrentModel.Cards.Count : insertPosition;
                    CurrentModel.Insert(index, insertArtId, insertCount); inspectedCardId = CurrentModel.Cards[index].Id;
                });
            EditorGUILayout.Space(8);
            inspectedCardId = DrawEditableList(CurrentModel, inspectedCardId, artIds, false, ref handScroll);
            showSamples = EditorGUILayout.Foldout(showSamples, "示例预设（替换当前列表）", true);
            if (showSamples)
            {
                if (GUILayout.Button("5 张基础示例")) Mutate("恢复示例牌", ResetSample);
                using (new EditorGUI.DisabledScope(CurrentCapacity < 8))
                if (GUILayout.Button("8 张 Shader 效果对照")) Mutate("效果对照", () => CurrentModel.LoadEffectSamples(artIds.Contains("hearts_q") ? "hearts_q" : artIds[0]));
                using (new EditorGUI.DisabledScope(CurrentCapacity < 12))
                if (GUILayout.Button("12 张重叠示例")) Mutate("重叠示例", () => {
                    CurrentModel.Clear(); CurrentModel.Insert(0, artIds.Contains("spades_a") ? "spades_a" : artIds[0], 12); layout.Size.x = 0.26f;
                });
            }
        }

        private string DrawEditableList(CardZonePreviewModel list, string editingId, string[] artIds, bool deck, ref Vector2 listScroll)
        {
            var display = new CardZonePreviewList(list, topFirst: deck);
            EditorGUILayout.LabelField((deck ? "测试牌堆" : Hand != null ? "手牌列表" : "卡牌列表") + " · " + list.Cards.Count + " 张", EditorStyles.boldLabel);
            if (deck) EditorGUILayout.LabelField("从牌顶到牌底显示：第一行是下一张；抽 N 张依次取前 N 行。", EditorStyles.miniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("花色排序")) Mutate("花色排序", display.SortBySuit);
                if (GUILayout.Button("点数排序")) Mutate("点数排序", display.SortByRank);
                if (GUILayout.Button("反转")) Mutate("反转列表", list.Reverse);
                if (GUILayout.Button("打乱")) Mutate("打乱列表", list.Shuffle);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("全部正面")) Mutate("全部正面", () => { foreach (var card in list.Cards) card.FaceUp = true; });
                if (GUILayout.Button("全部牌背")) Mutate("全部牌背", () => { foreach (var card in list.Cards) card.FaceUp = false; });
                if (GUILayout.Button("清空列表")) Mutate("清空列表", list.Clear);
            }
            if (list.Cards.Count == 0) { EditorGUILayout.HelpBox("列表为空，可在上方添加。", MessageType.None); return null; }
            if (!list.Cards.Any(card => card.Id == editingId)) editingId = display.Cards[0].Id;
            float height = Mathf.Min((list.Cards.Count + 1) * 26 + 8, Mathf.Clamp(position.height * 0.5f, 156, 520));
            using (var area = new EditorGUILayout.ScrollViewScope(listScroll, GUILayout.Height(height)))
            {
                listScroll = area.scrollPosition;
                DrawCardListHeader(deck);
                var rows = display.Cards.ToArray();
                var faceLabels = artIds.Select(id => CardZonePreviewCatalog.LabelFor(id).Replace(" ", "/")).ToArray();
                for (int index = 0; index < rows.Length; index++)
                {
                    var card = rows[index]; bool changed = false;
                    {
                        var cells = CardRowCells(deck);
                        var previous = GUI.backgroundColor;
                        if (card.Id == editingId) GUI.backgroundColor = new Color(0.5f, 0.75f, 1);
                        string rowLabel = (index + 1) + (deck && index == 0 ? " · 牌顶" : "");
                        if (GUI.Button(cells[0], new GUIContent(rowLabel, (deck && index == 0 ? "下一张抽取的牌\n" : "") + card.Id))) editingId = card.Id;
                        GUI.backgroundColor = previous;
                        if (DrawInlineCardProperties(list, card, artIds, faceLabels, cells)) editingId = card.Id;
                        int destination = EditorGUI.DelayedIntField(cells[10], index + 1) - 1;
                        if (destination != index)
                        { Mutate("移动卡牌到指定位置", () => display.Move(index, destination)); changed = true; }
                        using (new EditorGUI.DisabledScope(index == 0))
                            if (GUI.Button(cells[11], "↑")) { Mutate("上移卡牌", () => display.Move(index, index - 1)); changed = true; }
                        using (new EditorGUI.DisabledScope(index == list.Cards.Count - 1))
                            if (GUI.Button(cells[12], "↓")) { Mutate("下移卡牌", () => display.Move(index, index + 1)); changed = true; }
                        using (new EditorGUI.DisabledScope(list.Cards.Count >= (deck ? CardZonePreviewModel.MaximumCards : CurrentCapacity)))
                            if (GUI.Button(cells[13], new GUIContent("复制", "复制到本行后面，生成新身份并清空悬停/选中")))
                            { Mutate("复制卡牌", () => display.DuplicateAfter(index)); changed = true; }
                        if (GUI.Button(cells[14], new GUIContent("清除", "清除强化、塑封、失效和禁用；保留牌面、可见性、交互和乘色")))
                            Mutate("清除效果", () => list.SetCardProperties(new CardInstanceId(card.Id), card.ToCard().DefinitionId, CardEffects.None, false));
                        if (deck)
                        {
                            using (new EditorGUI.DisabledScope(CurrentModel.Cards.Count >= CurrentCapacity))
                                if (GUI.Button(cells[15], "抽出"))
                                { Mutate("抽出指定卡牌", () => { CurrentExperiment.DrawCard(CurrentModel, new CardInstanceId(card.Id), DrawInsertIndex); InspectDrawnCard(); }); changed = true; }
                        }
                        if (GUI.Button(cells[cells.Length - 1], new GUIContent("×", "删除这一张牌")))
                        { Mutate("删除卡牌", () => display.RemoveAt(index)); changed = true; }
                    }
                    if (changed) break;
                }
            }
            if (!list.Cards.Any(card => card.Id == editingId)) editingId = display.Cards.FirstOrDefault()?.Id;
            return editingId;
        }

        private static Rect[] CardRowCells(bool deck)
        {
            // Header and controls share the same geometry, independent of IMGUI style margins.
            var row = GUILayoutUtility.GetRect(deck ? 900 : 854, 24, GUILayout.MinWidth(deck ? 900 : 854),
                GUILayout.Height(24), GUILayout.ExpandWidth(true));
            float[] widths = deck
                ? new float[] { 64, 0, 38, 38, 38, 38, 38, 38, 38, 56, 48, 25, 25, 40, 42, 42, 25 }
                : new float[] { 64, 0, 38, 38, 38, 38, 38, 38, 38, 56, 48, 25, 25, 40, 42, 25 };
            widths[1] = Mathf.Max(108, row.width - widths.Sum() - (widths.Length - 1) * 4);
            var cells = new Rect[widths.Length];
            float x = row.x;
            for (int i = 0; i < widths.Length; i++)
            {
                cells[i] = new Rect(x, row.y + 1, widths[i], 22);
                x += widths[i] + 4;
            }
            return cells;
        }

        private static void DrawCardListHeader(bool deck)
        {
            var centered = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter };
            var cells = CardRowCells(deck);
            string[] labels = { "序号", "牌面", "正面", "强化", "塑封", "失效", "禁用", "悬停", "选中", "乘色", "移到" };
            string[] tips = { "点击序号定位诊断中的卡牌；悬停查看实例 ID", "修改牌面", "当前观察者可见；取消后显示牌背", "强化", "塑封",
                "可打出/丢弃并参与牌型，但自身不计分", "不能打出或丢弃", "模拟本地悬停状态", "待提交；禁用牌不能新选中，已有选中可取消",
                "逐牌乘色", "输入最终显示序号，回车或失焦确认" };
            for (int i = 0; i < labels.Length; i++) EditorGUI.LabelField(cells[i], new GUIContent(labels[i], tips[i]), centered);
            var actions = cells[11]; actions.xMax = cells[cells.Length - 1].xMax;
            EditorGUI.LabelField(actions, "操作", centered);
        }

        private bool DrawInlineCardProperties(CardZonePreviewModel list, PreviewCard sample, string[] artIds, string[] faceLabels, Rect[] cells)
        {
            EditorGUI.BeginChangeCheck();
            int originalFace = Array.IndexOf(artIds, sample.ArtId);
            int nextFace;
            using (new EditorGUI.DisabledScope(artIds.Length == 0))
                nextFace = EditorGUI.Popup(cells[1], originalFace, faceLabels);
            string face = nextFace >= 0 && nextFace < artIds.Length ? artIds[nextFace] : sample.ArtId;
            bool faceUp = CardRowToggle(cells[2], sample.FaceUp, "观察者可见（正面）");
            bool enhanced = CardRowToggle(cells[3], (sample.Effects & CardEffects.Enhanced) != 0, "强化");
            bool laminated = CardRowToggle(cells[4], (sample.Effects & CardEffects.Laminated) != 0, "塑封");
            bool ineffective = CardRowToggle(cells[5], (sample.Effects & CardEffects.Ineffective) != 0, "失效：自身不计分");
            bool disabled = CardRowToggle(cells[6], sample.IsDisabled, "禁用出牌 / 丢弃");
            bool hovered = CardRowToggle(cells[7], sample.IsHovered, "模拟悬停");
            bool pending;
            using (new EditorGUI.DisabledScope(disabled && !sample.IsSelected))
                pending = CardRowToggle(cells[8], sample.IsSelected, "选中 / 待提交");
            var tint = EditorGUI.ColorField(cells[9], GUIContent.none, sample.Tint, true, true, false);
            if (!EditorGUI.EndChangeCheck()) return false;
            Mutate("修改单牌数据", () => {
                var effects = (enhanced ? CardEffects.Enhanced : CardEffects.None) | (laminated ? CardEffects.Laminated : CardEffects.None) |
                    (ineffective ? CardEffects.Ineffective : CardEffects.None);
                list.SetCardProperties(new CardInstanceId(sample.Id), CardZonePreviewCatalog.DefinitionFor(face), effects, disabled);
                sample.FaceUp = faceUp; sample.Tint = tint; sample.SetInteraction(new CardInteractionState(hovered, pending));
            });
            return true;
        }

        private static bool CardRowToggle(Rect cell, bool value, string tooltip)
        {
            var toggle = new Rect(cell.x + (cell.width - 18) * 0.5f, cell.y + (cell.height - 18) * 0.5f, 18, 18);
            return EditorGUI.Toggle(toggle, new GUIContent("", tooltip), value);
        }

        private static string CardPicker(string label, string value, string[] artIds)
        {
            if (artIds.Length == 0) { EditorGUILayout.LabelField(label, "无可用牌面"); return value; }
            int original = Array.IndexOf(artIds, value);
            int next = EditorGUILayout.Popup(label, Mathf.Max(0, original), artIds.Select(id => CardZonePreviewCatalog.LabelFor(id).Replace(" ", "/")).ToArray());
            return artIds[next];
        }
        private static int InsertionPicker(string label, int value, int count, bool deck = false)
        {
            var options = new[] { deck ? "牌底（末尾）" : "末尾" }.Concat(Enumerable.Range(0, count)
                .Select(i => deck && i == 0 ? "牌顶（第 1 张之前）" : "第 " + (i + 1) + " 张之前")).ToArray();
            int index = value < 0 || value >= count ? 0 : value + 1;
            return EditorGUILayout.Popup(label, index, options) - 1;
        }
        private void InspectDrawnCard() { inspectedCardId = CurrentExperiment.Drawn.FirstOrDefault(); }

        private void DrawDiagnostics()
        {
            if (GUILayout.Button("检查视觉实例复用")) Run(CheckReuse);
            if (CurrentModel.Cards.Count > 0)
            {
                int index = CurrentModel.Cards.ToList().FindIndex(card => card.Id == inspectedCardId);
                index = EditorGUILayout.Popup("检查卡牌", Mathf.Max(0, index), CurrentModel.Cards.Select((card, i) => (i + 1) + ". " + CardZonePreviewCatalog.LabelFor(card.ArtId)).ToArray());
                var sample = CurrentModel.Cards[index]; inspectedCardId = sample.Id;
                var card = sample.ToCard();
                EditorGUILayout.SelectableLabel(card.Id.Value, EditorStyles.miniLabel, GUILayout.Height(18));
                EditorGUILayout.LabelField("定义", card.DefinitionId.Value);
                EditorGUILayout.LabelField("规则效果 / 禁用", card.Effects + " / " + card.IsDisabled);
                EditorGUILayout.LabelField("可打出 / 丢弃 / 计分", card.CanPlay + " / " + card.CanDiscard + " / " + card.CanScore);
                if (target != null && target.TryGetView(sample.Id, out var view))
                {
                    EditorGUILayout.LabelField("投影牌面", view.Data.FaceArtId ?? "隐藏");
                    EditorGUILayout.LabelField("投影效果 / 禁用", view.Data.Effects + " / " + view.Data.IsDisabled);
                    var surface = view.Surface;
                    EditorGUILayout.LabelField("火漆 / 镭射 / 红叉", surface.SealEnabled + " / " + surface.FoilEnabled + " / " + surface.RedCrossEnabled);
                    using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ColorField("Shader 乘色", surface.Tint);
                    EditorGUILayout.LabelField("视觉实例 ID", view.gameObject.GetInstanceID().ToString());
                }
            }
            else EditorGUILayout.HelpBox("牌区为空。", MessageType.None);
            DrawDrawDiagnostics();
        }
    }
}
