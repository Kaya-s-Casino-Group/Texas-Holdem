using System;
using System.Collections.Generic;
using System.Linq;
using PokerEngine;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.SingleBattle
{
    /// <summary>Local administrator UI; all edits use the current session's atomic admin command.</summary>
    public sealed class BattleAdminPanel : MonoBehaviour
    {
        private SingleBattleSceneAdapter host;
        private TMP_FontAsset font;
        private GameObject overlay;
        private RectTransform content;
        private ScrollRect scroll;
        private TMP_Text notice, status;
        private Button apply, preview;
        private BattleAdminDraft draft;
        private int tab, player;
        private float top;
        private string feedback;
        private readonly List<GameObject> rows = new List<GameObject>();
        private readonly string[] tabs = { "血量 / 护盾 / 金币", "遗物", "手牌", "牌库", "弃牌堆" };
        private static readonly CardDefinition[] Faces = StandardDeck.Cards().ToArray();
        private static readonly Color Background = new Color32(24, 28, 35, 255);
        private static readonly Color TextColor = new Color32(238, 227, 201, 255);
        public bool IsOpen => overlay != null && overlay.activeSelf;
        public BattleAdminDraft Draft => draft;
        public string Feedback => feedback;
        public void Configure(SingleBattleSceneAdapter source, TMP_FontAsset textFont) { host = source; font = textFont; }

        public void Open()
        {
            if (host?.Session == null) return;
            Ensure(); overlay.SetActive(true); Read();
        }
        public void Close() { if (overlay != null) overlay.SetActive(false); draft = null; }
        private void OnDisable() => Close();
        public void Read()
        {
            if (host?.Session == null) return;
            draft = new BattleAdminDraft(host.Session); player = Mathf.Clamp(player, 0, draft.Players.Count - 1);
            feedback = "已读取当前对局。修改仅在点击“应用修改”后生效；关闭丢弃草稿。";
            Render();
        }
        private void Changed() { feedback = "草稿已修改，尚未应用。可先预览检查。"; if (notice != null) notice.text = feedback; }
        public bool Submit(bool previewOnly)
        {
            try
            {
                var result = host.EditBattle(draft, previewOnly);
                if (!result.Accepted) throw new InvalidOperationException("校验未通过：" + result.Error + "。请检查生命上限、遗物数量/等级及牌区内容。未应用任何修改。");
                string summary = string.Join("；", result.State.Duel.Combat.Combatants.Select(u =>
                    u.Id + "：生命 " + u.Health + "/" + u.MaxHealth + "，盾 " + u.Block));
                summary += "\n" + string.Join("；", result.State.Duel.Players.Select(p => p.Id + "：金币 " + result.State.GoldFor(p.Id) +
                    "，遗物 " + p.Relics.Relics.Count + "，手牌/牌库/弃牌 " + string.Join("/", BattleAdminDraft.ZoneKinds.Select(k => result.State.Zone(k, p.Id).Cards.Count))));
                if (!previewOnly) { Read(); host.RefreshCandidate(); }
                feedback = (previewOnly ? "预览通过（尚未应用）：" : "已应用并同步当前画面：") + summary;
                notice.text = feedback; return true;
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is OverflowException)
            { feedback = error.Message; if (notice != null) notice.text = feedback; return false; }
        }
        private void Update()
        {
            if (!IsOpen) return;
            bool current = draft != null && draft.IsCurrent(host.Session);
            bool ready = current && host.CanEditBattle;
            apply.interactable = preview.interactable = ready;
            status.text = !current ? "对局已变化：请重新读取，旧草稿不能应用。" : !ready ? "当前不可应用：等待玩家行动阶段和动画结束，终局不可修改。" :
                "当前对局修改器 · 不消耗行动点 · 生命设为 0 会改变胜负，不触发伤害/复活/奖励";
        }

        private void Ensure()
        {
            if (overlay != null) return;
            var root = new GameObject("Runtime Battle Administrator", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false); overlay = root;
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.overrideSorting = true; canvas.sortingOrder = 30000;
            // This canvas is nested under the HUD: inherit its scale and fill its rect.
            // A nested CanvasScaler cannot resize the default 100 x 100 RectTransform.
            Stretch((RectTransform)root.transform);
            var shade = Rect("Modal blocker", root.transform); Stretch(shade); shade.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .75f);
            var window = Rect("Administrator", shade); window.anchorMin = new Vector2(.035f, .065f); window.anchorMax = new Vector2(.965f, .935f); window.offsetMin = window.offsetMax = Vector2.zero;
            window.gameObject.AddComponent<Image>().color = Background;
            var header = Band(window, "Header", 12, 38);
            Label(header, "对局管理员", 0, .23f, 23);
            Button(header, "重新读取", .56f, .15f, Read);
            Button(header, "关闭（丢弃草稿）", .73f, .26f, Close);
            status = Label(Band(window, "State", 54, 30), "", 0, 1, 16);
            var nav = Band(window, "Tabs", 90, 40);
            for (int i = 0; i < tabs.Length; ++i) { int index = i; Button(nav, tabs[i], i / 5f, .195f, () => { tab = index; Render(); }); }
            var scroller = Rect("Scroll", window); scroller.anchorMin = new Vector2(0, 0); scroller.anchorMax = Vector2.one;
            scroller.offsetMin = new Vector2(18, 148); scroller.offsetMax = new Vector2(-18, -142);
            scroller.gameObject.AddComponent<Image>().color = new Color32(18, 21, 27, 255);
            scroll = scroller.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.scrollSensitivity = 40; scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = Rect("Viewport", scroller); Stretch(viewport); viewport.gameObject.AddComponent<RectMask2D>();
            content = Rect("Rows", viewport); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
            scroll.viewport = viewport; scroll.content = content;
            var footer = Rect("Footer", window); footer.anchorMin = footer.anchorMax = new Vector2(0, 0); footer.anchorMax = new Vector2(1, 0);
            footer.pivot = new Vector2(.5f, 0); footer.offsetMin = new Vector2(18, 10); footer.offsetMax = new Vector2(-18, 140);
            var commands = Band(footer, "Commands", 0, 38);
            preview = Button(commands, "预览修改", 0, .22f, () => Submit(true));
            apply = Button(commands, "应用修改", .24f, .22f, () => Submit(false));
            notice = Label(Band(footer, "Feedback", 44, 84), "", 0, 1, 15); notice.alignment = TextAlignmentOptions.TopLeft;
        }
        private void Clear()
        {
            foreach (var row in rows)
            {
                row.SetActive(false);
                if (Application.isPlaying) Destroy(row); else DestroyImmediate(row);
            }
            rows.Clear(); top = 8; scroll.StopMovement(); content.anchoredPosition = Vector2.zero;
        }
        private RectTransform Row(float height = 42)
        {
            var row = Band(content, "Row", top, height); rows.Add(row.gameObject); top += height + 8;
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, top + 8); return row;
        }
        private void Render()
        {
            if (draft == null || content == null) return;
            Clear(); notice.text = feedback;
            if (tab == 0) { RenderVitals(); return; }
            var owner = draft.Players[player];
            var heading = Row(); Label(heading, "玩家：" + owner.Id + " · " + tabs[tab], 0, .63f, 20);
            if (draft.Players.Count > 1) Button(heading, "切换玩家", .7f, .29f, () => { player = (player + 1) % draft.Players.Count; Render(); });
            if (tab == 1) RenderRelics(owner); else RenderCards(owner, BattleAdminDraft.ZoneKinds[tab - 2]);
        }
        private void RenderVitals()
        {
            foreach (var unit in draft.Units)
            {
                Label(Row(28), (unit.Gold != null ? "玩家 " : "敌人 ") + unit.Id, 0, 1, 20);
                var titles = Row(24); Label(titles, "当前生命", 0, .24f); Label(titles, "生命上限", .25f, .24f); Label(titles, "护盾", .5f, .24f);
                if (unit.Gold != null) Label(titles, "金币", .75f, .24f);
                var fields = Row(); Input(fields, unit.Health, 0, .24f, v => unit.Health = v); Input(fields, unit.Maximum, .25f, .24f, v => unit.Maximum = v);
                Input(fields, unit.Block, .5f, .24f, v => unit.Block = v); if (unit.Gold != null) Input(fields, unit.Gold, .75f, .24f, v => unit.Gold = v);
            }
        }
        private void RenderRelics(BattleAdminDraft.Player owner)
        {
            Label(Row(30), "按列表顺序结算；已有遗物保留实例与记忆。新增遗物使用当前对局配置。", 0, 1);
            Button(Row(), "+ 添加遗物", 0, .45f, () => ChooseRelic(owner));
            for (int i = 0; i < owner.Relics.Count; ++i)
            {
                var relic = owner.Relics[i]; int index = i;
                var row = Row(); Label(row, (i + 1) + "  " + host.ItemName(relic.DefinitionId) + "  [" + relic.DefinitionId + "]", 0, .55f, 18);
                Button(row, "上移", .57f, .12f, () => Move(owner.Relics, index, -1)).interactable = i > 0;
                Button(row, "下移", .71f, .12f, () => Move(owner.Relics, index, 1)).interactable = i + 1 < owner.Relics.Count;
                Button(row, "移除", .85f, .14f, () => { owner.Relics.Remove(relic); Changed(); Render(); });
                var captions = Row(24); var fields = Row();
                var names = new[] { "等级", "次数（空=沿用）", "成长值", "冷却回合", "本回合已用" };
                var values = new[] { relic.Level, relic.Charges, relic.Growth, relic.Cooldown, relic.Uses };
                Action<string>[] setters = { v => relic.Level = v, v => relic.Charges = v, v => relic.Growth = v, v => relic.Cooldown = v, v => relic.Uses = v };
                for (int j = 0; j < 5; ++j) { Label(captions, names[j], j / 5f, .19f); Input(fields, values[j], j / 5f, .19f, setters[j]); }
            }
        }
        private void ChooseRelic(BattleAdminDraft.Player owner)
        {
            Choose(host.Engine.AdminRelicCatalog.Select(item => new Choice(host.ItemName(item.Definition.Id) + " [" + item.Definition.Id + "]", () => {
                owner.Relics.Add(new BattleAdminDraft.Relic { DefinitionId = item.Definition.Id, Level = "1", Charges = item.Definition.Charges?.ToString() ?? "", Growth = "0", Cooldown = "0", Uses = "0" });
            })).ToArray());
        }
        private void RenderCards(BattleAdminDraft.Player owner, string kind)
        {
            var cards = owner.Zones[kind];
            Label(Row(30), "共 " + cards.Count + " 张 · " + (kind == CardZoneKinds.Deck ? "第 1 张是下一张摸到的牌；移入牌库放到牌顶。" : "修改牌面、复制、移动或移除；废牌标记可单独开关。"), 0, 1);
            Button(Row(), "+ 添加卡牌", 0, .45f, () => Choose(Faces.Select(face => new Choice(FaceName(face), () => cards.Insert(0, new BattleAdminDraft.Card { DefinitionId = face.Id.Value }))).ToArray()));
            for (int i = 0; i < cards.Count; ++i)
            {
                var card = cards[i]; int index = i; var row = Row();
                Button(row, (i + 1) + "  " + FaceName(Faces.First(f => f.Id.Value == card.DefinitionId)), 0, .21f,
                    () => Choose(Faces.Select(face => new Choice(FaceName(face), () => card.DefinitionId = face.Id.Value)).ToArray()));
                Button(row, "上移", .23f, .10f, () => Move(cards, index, -1)).interactable = i > 0;
                Button(row, "下移", .34f, .10f, () => Move(cards, index, 1)).interactable = i + 1 < cards.Count;
                Button(row, "复制", .45f, .10f, () => { cards.Insert(index + 1, card.Copy()); Changed(); Render(); });
                Button(row, "移除", .56f, .10f, () => { cards.Remove(card); Changed(); Render(); });
                var other = BattleAdminDraft.ZoneKinds.Where(k => k != kind).ToArray();
                for (int j = 0; j < other.Length; ++j) { string destination = other[j]; Button(row, "移至" + ZoneName(destination), .67f + j * .16f, .15f, () => { cards.Remove(card); owner.Zones[destination].Insert(0, card); Changed(); Render(); }); }
                var flags = Row(32);
                var effects = new[] { CardEffects.Unplayable, CardEffects.Ineffective, CardEffects.Enhanced, CardEffects.Laminated };
                var titles = new[] { "不可出牌", "不计分", "增强", "镀膜" };
                for (int j = 0; j < effects.Length; ++j)
                {
                    var effect = effects[j]; Button(flags, ((card.Effects & effect) != 0 ? "☑ " : "□ ") + titles[j], j * .2f, .19f,
                        () => { card.Effects ^= effect; Changed(); Render(); });
                }
                Button(flags, (card.Disabled ? "☑ " : "□ ") + "禁用", .8f, .19f, () => { card.Disabled = !card.Disabled; Changed(); Render(); });
            }
        }
        private void Move<T>(List<T> values, int index, int offset)
        { var value = values[index]; values.RemoveAt(index); values.Insert(index + offset, value); Changed(); Render(); }
        private sealed class Choice
        { public readonly string Label; public readonly Action Select; public Choice(string label, Action select) { Label = label; Select = select; } }
        private void Choose(Choice[] choices, string query = "")
        {
            Clear(); var header = Row(); Button(header, "返回", 0, .19f, Render);
            // Search filters only when submitted, so rebuilding cannot steal focus on every keystroke.
            var search = Input(header, query, .21f, .57f, _ => { }, false);
            Button(header, "搜索名称 / ID", .8f, .19f, () => Choose(choices, search.text));
            var filtered = choices.Where(c => c.Label.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            for (int i = 0; i < filtered.Length; i += 3)
            {
                var row = Row();
                for (int j = 0; j < 3 && i + j < filtered.Length; ++j)
                { var choice = filtered[i + j]; Button(row, choice.Label, j / 3f, .325f, () => { choice.Select(); Changed(); Render(); }); }
            }
            if (filtered.Length == 0) Label(Row(), "没有匹配项。", 0, 1);
        }
        private static string ZoneName(string kind) => kind == CardZoneKinds.Hand ? "手牌" : kind == CardZoneKinds.Deck ? "牌库" : "弃牌";
        private static string FaceName(CardDefinition face) => (face.Suit == Suit.Spades ? "黑桃 ♠ " : face.Suit == Suit.Hearts ? "红桃 ♥ " : face.Suit == Suit.Clubs ? "梅花 ♣ " : "方片 ♦ ") +
            (face.Rank == Rank.Ace ? "A" : face.Rank == Rank.King ? "K" : face.Rank == Rank.Queen ? "Q" : face.Rank == Rank.Jack ? "J" : ((int)face.Rank).ToString());
        private static RectTransform Rect(string name, Transform parent)
        { var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); rect.gameObject.layer = 5; return rect; }
        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static RectTransform Band(Transform parent, string name, float y, float height)
        { var rect = Rect(name, parent); rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1); rect.offsetMin = new Vector2(12, -y - height); rect.offsetMax = new Vector2(-12, -y); return rect; }
        private static RectTransform Cell(Transform parent, string name, float x, float width)
        { var rect = Rect(name, parent); rect.anchorMin = new Vector2(x, 0); rect.anchorMax = new Vector2(x + width, 1); rect.offsetMin = new Vector2(3, 0); rect.offsetMax = new Vector2(-3, 0); return rect; }
        private TMP_Text Label(Transform parent, string value, float x, float width, float size = 16)
        {
            var rect = Cell(parent, "Label", x, width); var text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.font = font;
            text.text = value; text.fontSize = size; text.color = TextColor; text.richText = false; text.enableWordWrapping = true;
            text.alignment = TextAlignmentOptions.MidlineLeft; text.raycastTarget = false; return text;
        }
        private Button Button(Transform parent, string title, float x, float width, Action action)
        {
            var rect = Cell(parent, title, x, width); var image = rect.gameObject.AddComponent<Image>(); image.color = new Color32(69, 62, 48, 255);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(() => action());
            var label = Label(rect, title, 0, 1); label.alignment = TextAlignmentOptions.Center; return button;
        }
        private TMP_InputField Input(Transform parent, string value, float x, float width, Action<string> change, bool edit = true)
        {
            var rect = Cell(parent, "Input", x, width); var image = rect.gameObject.AddComponent<Image>(); image.color = new Color32(42, 48, 58, 255);
            var input = rect.gameObject.AddComponent<TMP_InputField>(); input.targetGraphic = image;
            var area = Rect("Text Area", rect); Stretch(area); area.offsetMin = new Vector2(9, 4); area.offsetMax = new Vector2(-9, -4); area.gameObject.AddComponent<RectMask2D>();
            input.textViewport = area; input.textComponent = (TextMeshProUGUI)Label(area, "", 0, 1, 18); input.fontAsset = font; input.text = value;
            input.characterLimit = 64; input.onValueChanged.AddListener(v => { change(v); if (edit) Changed(); }); return input;
        }
    }
}
