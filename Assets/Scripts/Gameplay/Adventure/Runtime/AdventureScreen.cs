using System;
using System.Linq;
using PokerEngine;
using Poker.SingleBattle;
using CardArt;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UIFoundation;

namespace Poker.Adventure
{
    /// <summary>Single-scene run screens. All economic decisions are submitted to the run authority.</summary>
    [DisallowMultipleComponent]
    public sealed partial class AdventureScreen : MonoBehaviour
    {
        [SerializeField] private AdventureSceneHost host;
        [SerializeField] private TMP_FontAsset font;
        private RectTransform page, header, footer, overlay, safe, deskLayout;
        private Image backdrop;
        private RitualPicture sceneArt;
        [SerializeField] private Texture2D homeArt, heroArt;
        [SerializeField] private CardAtlasData cardAtlas;
        public void ConfigureArt(Texture2D home, Texture2D hero, CardAtlasData atlas) { homeArt = home; heroArt = hero; cardAtlas = atlas; }
        private string setupPage;
        private string selectedTarot;
        private string selectedCard;
        private AdventureCommandKind service;
        private bool subscribed;
        private static readonly Color Paper = new Color32(242, 230, 203, 255);
        private static readonly Color Gold = new Color32(219, 183, 112, 255);
        private static readonly Color Muted = new Color32(193, 179, 160, 255);
        private static readonly Color Panel = new Color32(36, 30, 39, 255);
        private static readonly Color Accent = new Color32(113, 48, 62, 255);
        public AdventureSceneHost Host => host;
        public void Configure(AdventureSceneHost owner, TMP_FontAsset typeface) { host = owner; font = typeface; }
        private void OnEnable() { Subscribe(); }
        private void Start() { Build(); Refresh(); }
        private void OnDisable() { if (subscribed) host.Changed -= Refresh; subscribed = false; }
        private void Subscribe()
        { if (host != null && !subscribed) { host.Changed += Refresh; subscribed = true; } }

        public void Build()
        {
            if (page != null) return;
            var canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 500;
            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
            backdrop = Box(transform, "Adventure Backdrop", new Rect(0, 0, 1, 1), new Color32(16, 14, 22, 248)).GetComponent<Image>();
            sceneArt = RitualSkin.Picture(backdrop.transform, "Scene Artwork", new Rect(0, 0, 1, 1), homeArt, true);
            safe = ScreenSafeArea.Ensure(canvas, fullBleed: backdrop.transform).Content;
            deskLayout = Rect(safe, "Desk Layout", new Rect(0, 0, 1, 1));
            var fit = deskLayout.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = 1920f / 1080f;
            sceneArt.transform.SetParent(deskLayout, false);
            RitualSkin.Place(sceneArt.transform, new Rect(0, 0, 1, 1));
            header = Rect(deskLayout, "Run Header", new Rect(.025f, .92f, .95f, .065f));
            page = Rect(deskLayout, "Run Page", new Rect(.035f, .12f, .93f, .77f));
            footer = Rect(deskLayout, "Run Footer", new Rect(.035f, .02f, .93f, .085f));
            overlay = Rect(safe, "Run Overlay", new Rect(0, 0, 1, 1));
            Subscribe();
        }
        public void Refresh()
        {
            if (host == null) return;
            Build(); UpdateBackdrop(); Clear(page); Clear(header); Clear(footer); Clear(overlay);
            header.SetParent(deskLayout, false);
            footer.SetParent(deskLayout, false);
            RitualSkin.Place(page, new Rect(.035f, .12f, .93f, .77f));
            RitualSkin.Place(header, new Rect(.025f, .92f, .95f, .065f));
            if (host.Definition == null)
            { Title(page, "旅程暂不可用", "检查场景的冒险内容配置后重试。"); ErrorFooter(); return; }
            bool battle = !host.IsAtMenu && host.Adventure != null && (host.Adventure.Phase == AdventurePhase.Battle || host.IsBusy);
            backdrop.gameObject.SetActive(!battle); page.gameObject.SetActive(!battle);
            if (battle)
            {
                // Leave the battle's hand and HUD unobstructed. The run menu never advances a battle.
                header.SetParent(safe, false);
                footer.SetParent(safe, false);
                deskLayout.gameObject.SetActive(false);
                RitualSkin.Place(header, new Rect(0, 0, 1, 1));
                DeskButton(header, "菜单", new Rect(.948f, .90f, .045f, .07f), host.ReturnToMenu, !host.IsBusy, "Button", 19);
                ErrorFooter(); return;
            }
            deskLayout.gameObject.SetActive(true);
            if (host.IsAtMenu || host.Adventure == null) { Menu(); ErrorFooter(); return; }
            setupPage = null;
            var run = host.Adventure;
            JourneyHeader();
            switch (run.Phase)
            {
                case AdventurePhase.Map: Map(); break;
                case AdventurePhase.Shop: Shop(); break;
                case AdventurePhase.Rest: Rest(); break;
                case AdventurePhase.Chest: Chest(); break;
                case AdventurePhase.Reward: Reward(); break;
                case AdventurePhase.Completed:
                case AdventurePhase.Failed: Result(); break;
            }
            if (run.PendingRelicRewards.Count > 0) PendingReward();
            ErrorFooter();
        }



        private void BackToMenu() => Button(footer, "返回主菜单", new Rect(0, .35f, .2f, .55f), () => { setupPage = null; Refresh(); });

        private static string NodeLabel(AdventureNodeDefinition n)
        {
            switch (n.Kind)
            {
                case AdventureNodeKind.Battle: return "普通战斗";
                case AdventureNodeKind.Elite: return "精英战斗";
                case AdventureNodeKind.Boss: return "首领战斗";
                case AdventureNodeKind.Shop: return "商店";
                case AdventureNodeKind.Rest: return "休息";
                case AdventureNodeKind.Chest: return "宝箱";
                default: return n.Name;
            }
        }

        private void ItemDetail(AdventureShopItem item)
        {
            var modal = Modal("道具详情");
            RelicIcon(modal, item.RelicId, new Rect(.07f, .67f, .12f, .16f));
            Label(modal, host.ItemName(item.RelicId), new Rect(.22f, .72f, .71f, .12f), 36, Gold);
            Label(modal, host.ItemDescription(item.RelicId), new Rect(.07f, .29f, .86f, .39f), 24, Paper);
            var command = new AdventureCommand(AdventureCommandKind.BuyRelic, item.Id);
            bool allowed = Preview(command);
            Button(modal, item.Price + " 金币 · 购买", new Rect(.07f, .08f, .58f, .12f), () => host.ExecuteAdventure(command), allowed, true);
            if (!allowed) Label(modal, "金币不足、栏位已满或持有数量已达上限", new Rect(.07f, .21f, .86f, .06f), 18, Muted);
        }


        private string RewardText()
        {
            var result = host.Adventure.LastLocationReward;
            if (result == null) return "";
            var lines = new System.Collections.Generic.List<string>();
            if (result.HealthDelta != 0) lines.Add("生命 " + Signed(result.HealthDelta));
            if (result.GoldDelta != 0) lines.Add("金币 " + Signed(result.GoldDelta));
            foreach (var id in result.GrantedRelicIds) lines.Add((host.Adventure.PendingRelicRewards.Contains(id) ? "待领取：" : "获得：") + host.ItemName(id));
            foreach (var id in result.LostRelicIds) lines.Add("失去：" + host.ItemName(id));
            return lines.Count > 0 ? string.Join("\n", lines) : "本次没有资源变化";
        }
        private static string Signed(long value) => value > 0 ? "+" + value : value.ToString();


        private void ContinueButton(string caption) => Button(footer, caption, new Rect(.68f, .35f, .32f, .60f), () => host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.Continue)), host.Adventure.CanContinue, true);
        private bool Preview(AdventureCommand command) => host.Adventure.Execute(host.Adventure.Revision, command, preview: true).Accepted;
        private void Inventory(RectTransform parent, Rect area, bool sell)
        {
            var bar = Rect(parent, "Inventory", area); var run = host.Adventure;
            if (run.Relics.Count == 0) { Label(bar, "尚未携带道具", new Rect(0, .2f, 1, .6f), 22, Muted); return; }
            for (int i = 0; i < run.Relics.Count; i++)
            {
                var relic = run.Relics[i]; float width = 1f / Math.Max(5, run.Relics.Count);
                var itemButton = Button(bar, host.ItemName(relic.DefinitionId) + (sell ? "\n出售 " + run.SellPrice(relic.Id) + " 金币" : ""), new Rect(i * width, .05f, width - .01f, .90f), () => {
                    var modal = Modal(sell ? "出售道具" : "持有道具");
                    Label(modal, host.ItemName(relic.DefinitionId), new Rect(.07f, .70f, .86f, .14f), 34, Gold);
                    Label(modal, host.ItemDescription(relic.DefinitionId), new Rect(.07f, .26f, .86f, .39f), 24, Paper);
                    if (sell) Button(modal, "确认出售 · " + run.SellPrice(relic.Id) + " 金币", new Rect(.07f, .08f, .58f, .13f), () => host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.SellRelic, relic.Id)), true, true);
                });
                RelicIcon(itemButton.transform, relic.DefinitionId, new Rect(.09f, .29f, .18f, .42f));
                var caption = itemButton.GetComponentInChildren<TMP_Text>();
                RitualSkin.Place(caption.transform, new Rect(.34f, .20f, .53f, .57f));
                caption.fontSize = caption.fontSizeMax = 18; caption.fontSizeMin = 12;
            }
        }
        private void PendingReward()
        {
            string id = host.Adventure.PendingRelicRewards[0];
            var modal = Modal("选择如何携带奖励", false);
            RelicIcon(modal, id, new Rect(.16f, .74f, .16f, .12f));
            Label(modal, host.ItemName(id), new Rect(.06f, .62f, .36f, .11f), 32, Gold, TextAlignmentOptions.Center);
            Label(modal, host.ItemDescription(id), new Rect(.07f, .24f, .35f, .35f), 23, Paper);
            Label(modal, "替换一件已有道具，\n或放弃这件奖励。", new Rect(.47f, .70f, .46f, .14f), 23, Paper);
            var viewport = Box(modal, "Reward Choices", new Rect(.47f, .22f, .46f, .46f), Color.clear);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect(viewport, "Choices", new Rect(0, 0, 1, 1));
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = new Vector2(0, host.Adventure.Relics.Count * 76);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            int i = 0;
            foreach (var owned in host.Adventure.Relics)
            {
                var command = new AdventureCommand(AdventureCommandKind.ClaimRelicReward, owned.Id);
                var button = Button(content, "替换 " + host.ItemName(owned.DefinitionId), new Rect(0, 0, 1, 1), () => host.ExecuteAdventure(command), Preview(command));
                var row = (RectTransform)button.transform;
                row.anchorMin = new Vector2(0, 1); row.anchorMax = Vector2.one; row.pivot = new Vector2(.5f, 1);
                row.sizeDelta = new Vector2(0, 68); row.anchoredPosition = new Vector2(0, -i * 76);
                RelicIcon(button.transform, owned.DefinitionId, new Rect(.045f, .20f, .12f, .60f));
                RitualSkin.Place(button.GetComponentInChildren<TMP_Text>().transform, new Rect(.22f, .18f, .64f, .64f)); i++;
            }
            var claim = new AdventureCommand(AdventureCommandKind.ClaimRelicReward);
            if (Preview(claim)) Button(modal, "直接领取", new Rect(.07f, .06f, .40f, .10f), () => host.ExecuteAdventure(claim), true, true);
            Button(modal, "放弃这件奖励", new Rect(.53f, .06f, .40f, .10f), () => host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.SkipRelicReward)));
        }
        private static string ServiceName(AdventureCommandKind kind) => kind == AdventureCommandKind.DeleteCard ? "撕牌" : kind == AdventureCommandKind.DuplicateCard ? "加牌" : "改花色";
        private void OpenService(AdventureCommandKind kind)
        {
            service = kind; selectedCard = null; RenderService();
        }
        private void RenderService()
        {
            var previousScroll = overlay.GetComponentInChildren<ScrollRect>();
            float scrollPosition = previousScroll != null ? previousScroll.verticalNormalizedPosition : 1;
            var modal = Modal(ServiceName(service) + " · 选择永久牌组中的一张牌");
            var viewport = Box(modal, "Deck Scroll", new Rect(.05f, .24f, .90f, .58f), new Color32(20, 18, 26, 255));
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.viewport = viewport;
            var content = Rect(viewport, "Cards", new Rect(0, 0, 1, 1)); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1);
            const int columns = 10, rowHeight = 172;
            int rows = (host.Adventure.Deck.Count + columns - 1) / columns;
            content.sizeDelta = new Vector2(0, Math.Max(340, rows * rowHeight)); content.anchoredPosition = Vector2.zero; scroll.content = content;
            int i = 0;
            foreach (var card in host.Adventure.Deck)
            {
                var face = StandardDeck.Cards().FirstOrDefault(c => c.Id == card.DefinitionId);
                string label = face == null ? card.DefinitionId.ToString() : FaceName(face);
                string id = card.Id;
                var button = Button(content, label, new Rect(0, 0, 1, 1), () => { selectedCard = id; RenderService(); }, true, id == selectedCard);
                // These cells display the actual poker-card artwork, with a plain selection backing.
                var cardBacking = button.GetComponent<Image>(); cardBacking.sprite = null;
                cardBacking.color = id == selectedCard ? Accent : Panel;
                var cellColors = button.colors; cellColors.normalColor = cardBacking.color;
                cellColors.selectedColor = cellColors.pressedColor = Accent;
                cellColors.highlightedColor = new Color32(73, 56, 63, 255); button.colors = cellColors;
                var r = button.GetComponent<RectTransform>(); r.anchorMin = new Vector2(i % columns / (float)columns, 1); r.anchorMax = new Vector2((i % columns + 1) / (float)columns, 1); r.pivot = new Vector2(.5f, 1);
                r.offsetMin = new Vector2(4, -((i / columns + 1) * rowHeight) + 5); r.offsetMax = new Vector2(-4, -(i / columns * rowHeight) - 5); i++;
                if (face != null && cardAtlas != null && cardAtlas.TryGetCard(AtlasCardId(face), out var art))
                {
                    var picture = RitualSkin.Picture(button.transform, "FourBeasts Card", new Rect(.07f, .24f, .86f, .71f), cardAtlas.texture);
                    picture.Configure(cardAtlas.texture, false, new Rect(art.uv.x, art.uv.y, art.uv.z, art.uv.w));
                    var caption = button.GetComponentInChildren<TMP_Text>(); caption.text = label.Replace('\n', ' ');
                    RitualSkin.Place(caption.transform, new Rect(.02f, .015f, .96f, .23f));
                    caption.fontSize = caption.fontSizeMax = 17; caption.fontSizeMin = 12;
                }
            }
            var selected = host.Adventure.Deck.FirstOrDefault(card => card.Id == selectedCard);
            var selectedFace = selected != null ? StandardDeck.Cards().FirstOrDefault(card => card.Id == selected.DefinitionId) : null;
            Label(modal, "本次 " + host.Adventure.ServicePrice(service) + " 金币 · 牌组 " + host.Adventure.Deck.Count + " 张 · " +
                (selectedFace != null ? "已选 " + FaceName(selectedFace).Replace('\n', ' ') : "滚动查看牌库，点击选择"), new Rect(.05f, .15f, .9f, .07f), 22, Muted);
            if (service == AdventureCommandKind.ChangeSuit)
            {
                var suits = new[] { Suit.Spades, Suit.Hearts, Suit.Clubs, Suit.Diamonds };
                for (int j = 0; j < suits.Length; j++)
                {
                    var suit = suits[j]; var command = new AdventureCommand(service, selectedCard, suit);
                    Button(modal, SuitName(suit), new Rect(.05f + j * .225f, .04f, .21f, .09f), () => ApplyService(command), selectedCard != null && Preview(command), true);
                }
            }
            else
            {
                var command = new AdventureCommand(service, selectedCard);
                Button(modal, "确认" + ServiceName(service), new Rect(.56f, .04f, .39f, .10f), () => ApplyService(command), selectedCard != null && Preview(command), true);
            }
            Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = scrollPosition;
        }
        private void ApplyService(AdventureCommand command)
        { if (host.ExecuteAdventure(command)) { selectedCard = null; RenderService(); } }
        private static string FaceName(CardDefinition card) => SuitName(card.Suit) + "\n" + (card.Rank == Rank.Ace ? "A" : card.Rank == Rank.King ? "K" : card.Rank == Rank.Queen ? "Q" : card.Rank == Rank.Jack ? "J" : ((int)card.Rank).ToString());
        private static string AtlasCardId(CardDefinition card) => card.Suit.ToString().ToLowerInvariant() + "_" +
            (card.Rank == Rank.Ace ? "a" : card.Rank == Rank.King ? "k" : card.Rank == Rank.Queen ? "q" : card.Rank == Rank.Jack ? "j" : ((int)card.Rank).ToString("00"));
        private static string SuitName(Suit suit) => suit == Suit.Spades ? "黑桃" : suit == Suit.Hearts ? "红桃" : suit == Suit.Clubs ? "梅花" : "方块";
        private void ConfirmAbandon()
        {
            var modal = Modal("放弃旅程");
            Label(modal, "本局的生命、金币和构筑将结束。\n已解锁的塔罗会保留。", new Rect(.08f, .32f, .84f, .40f), 30, Paper, TextAlignmentOptions.Center);
            Button(modal, "确认放弃", new Rect(.20f, .08f, .60f, .13f), () => host.Abandon(), true, true);
        }
        private RectTransform Modal(string title, bool close = true)
        {
            Clear(overlay);
            Box(overlay, "Modal Shield", new Rect(0, 0, 1, 1), new Color(0, 0, 0, .80f));
            var frame = DeskPanel(overlay, "Dialog", new Rect(.15f, .08f, .70f, .84f));
            Label(frame, title, new Rect(.05f, .87f, .72f, .09f), 29, Paper);
            if (close) Button(frame, "关闭", new Rect(.80f, .86f, .15f, .09f), () => Clear(overlay));
            return frame;
        }
        private void ErrorFooter()
        {
            if (!string.IsNullOrEmpty(host.Message)) Label(footer, host.Message, new Rect(0, 0, .98f, .3f), 20, new Color32(255, 160, 146, 255));
        }
        private void Title(RectTransform parent, string title, string subtitle)
        {
            Label(parent, title, new Rect(0, .86f, 1, .13f), 46, Paper);
            Label(parent, subtitle, new Rect(0, .77f, 1, .08f), 24, Muted);
        }
        private RectTransform Rect(Transform parent, string name, Rect area)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
            var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = area.min; rect.anchorMax = area.max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
        private RectTransform Box(Transform parent, string name, Rect area, Color color)
        {
            var rect = Rect(parent, name, area); var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = true;
            return rect;
        }
        private TMP_Text Label(Transform parent, string text, Rect area, int size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Left)
        {
            var rect = Rect(parent, "Text", area); var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font; label.text = text; label.fontSize = size; label.color = color; label.alignment = align;
            label.enableWordWrapping = true; label.enableAutoSizing = true; label.fontSizeMax = size; label.fontSizeMin = Mathf.Max(12, size * .6f); label.overflowMode = TextOverflowModes.Ellipsis; label.raycastTarget = false;
            return label;
        }
        private Button Button(Transform parent, string text, Rect area, Action onClick, bool enabled = true, bool primary = false, string sprite = null)
        {
            var rect = Box(parent, text.Replace('\n', ' '), area, primary ? Accent : Panel);
            var outline = rect.GetComponent<Outline>() ?? rect.gameObject.AddComponent<Outline>(); outline.effectColor = primary ? Gold : new Color32(83, 71, 83, 255); outline.effectDistance = Vector2.one;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>(); button.interactable = enabled;
            var colors = button.colors; colors.highlightedColor = new Color(1.25f, 1.2f, 1.1f); colors.disabledColor = new Color(.44f, .44f, .44f); button.colors = colors;
            sprite = sprite ?? (rect.rect.width > rect.rect.height * 4.2f ? "MenuButton" : "Button");
            if (DeskUISkin.Get(sprite) != null) DeskUISkin.Button(button, sprite);
            else RitualSkin.Button(button, primary);
            AdventureAudioController.BindButton(button);
            button.onClick.AddListener(() => onClick());
            Label(rect, text, new Rect(.10f, .18f, .80f, .64f), 24, enabled ? Paper : Muted, TextAlignmentOptions.Center);
            return button;
        }
        private static void Clear(RectTransform root)
        {
            if (root == null) return;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i).gameObject; child.SetActive(false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
        }
    }
}
