using System;
using System.Collections.Generic;
using System.Linq;
using CardArt;
using CardInteraction;
using CardPresentation;
using Poker.BattlePresentation;
using PokerEngine;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Poker.SingleBattle
{
    public sealed partial class SingleBattleHud
    {
        [Header("牌型指南入口")]
        [SerializeField] private Button guideEntry;
        private Button guideClose, guideFlushButton;
        private bool ownsGuideEntry;
        private RectTransform guideOverlay, guideCardRow, guideFormulaContent;
        private TMP_Text guideHeading, guideAvailability, guideTags, guideIndex, guideNotice, guideFormula;
        private ScrollRect guideFormulaScroll;
        private HandGuideType guideType = HandGuideType.HighCard;
        private bool guideFlush;
        private CardDuelState guideState;
        private HandGuideResult guideResult;
        private CardOutlineCycle guideCycle;
        private Material guideOutlineMaterial;
        private double guideStarted;
        private int guideShownIndex = -1;
        private GameObject guidePreviousFocus;
        private readonly Dictionary<HandGuideType, Button> guideTypeButtons = new Dictionary<HandGuideType, Button>();
        private readonly Dictionary<CardZoneMouseInput, bool> guideInputs = new Dictionary<CardZoneMouseInput, bool>();
        private readonly List<CardBreathingOutline> guideOutlines = new List<CardBreathingOutline>();
        private readonly List<GameObject> guideCards = new List<GameObject>();
        private readonly Dictionary<string, BattleHandGuideText> guideTexts = new Dictionary<string, BattleHandGuideText>();
        private static readonly Color GuidePaper = new Color32(238, 225, 196, 255);
        private static readonly Color GuidePanel = new Color32(22, 25, 29, 252);
        private static readonly Color[] GuideColors = {
            new Color32(244, 195, 97, 255), new Color32(85, 210, 222, 255),
            new Color32(219, 135, 240, 255), new Color32(127, 222, 156, 255), new Color32(244, 140, 111, 255) };
        public bool IsHandGuideOpen => guideOverlay != null && guideOverlay.gameObject.activeSelf;

        private void UpdateHandGuide()
        {
            if (!Application.isPlaying || host == null || message == null) return;
            EnsureHandGuideEntry();
            if (guideEntry == null) return;
            RefreshHandGuideEntryLayout();
            bool available = host.State != null && host.State.HasStarted && !host.State.Duel.IsFinished &&
                ReferenceEquals(host.State, host.AuthorityState) && !host.IsPresenting && host.Player != null;
            guideEntry.interactable = available && !IsHandGuideOpen && adminPanel?.IsOpen != true;
            if (!IsHandGuideOpen) return;
            if (!available) { CloseHandGuide(); return; }
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true) { CloseHandGuide(); return; }
            if (!ReferenceEquals(guideState, host.State)) RebuildHandGuide();
            if (guideCycle == null) return;
            guideCycle.Tick(Time.unscaledTimeAsDouble - guideStarted + .35);
            if (guideShownIndex != guideCycle.ActiveIndex) ShowGuideCandidate(guideCycle.ActiveIndex);
        }

        private void EnsureHandGuideEntry()
        {
            if (guideEntry != null || host.DrawPile?.View == null) return;
            var icon = HandGuidePileIcon();
            if (icon == null) return;
            guideEntry = GuideButton(icon.parent, "Hand Guide", "", OpenHandGuide, new Rect(0, 0, .04f, .08f));
            ownsGuideEntry = true;
            var image = guideEntry.GetComponent<Image>();
            image.sprite = Resources.Load<Sprite>("BattleConcept/Icons/icon_hand_guide");
            image.color = Color.white; image.preserveAspect = true;
            guideEntry.GetComponentInChildren<TMP_Text>().gameObject.SetActive(false);
        }
        private RectTransform HandGuidePileIcon()
        {
            var root = host?.DrawPile?.View?.transform;
            return (root?.Find("Icon") ?? root?.Find("Safe Area/Icon")) as RectTransform;
        }

        public void RefreshHandGuideEntryLayout()
        {
            var icon = HandGuidePileIcon();
            if (icon == null || guideEntry == null) return;
            var rect = (RectTransform)guideEntry.transform;
            if (rect.parent != icon.parent) rect.SetParent(icon.parent, false);
            // Keep placement valid even while the battle Canvas is inactive in the adventure scene.
            var shift = new Vector2(icon.anchorMax.x - icon.anchorMin.x, 0);
            rect.anchorMin = icon.anchorMin - shift; rect.anchorMax = icon.anchorMax - shift; rect.pivot = icon.pivot;
            rect.sizeDelta = icon.sizeDelta; rect.anchoredPosition = icon.anchoredPosition - new Vector2(icon.sizeDelta.x + 14, 0);
            rect.localScale = Vector3.one;
        }

        public void OpenHandGuide()
        {
            if (host?.State == null || host.IsPresenting || host.State.Duel.IsFinished || adminPanel?.IsOpen == true || IsHandGuideOpen) return;
            EnsureHandGuidePanel();
            host.DeckOverview?.Close(false); HideRelicHover();
            battleToolsExpanded = false;
            if (battleToolsBody != null) battleToolsBody.gameObject.SetActive(false);
            guidePreviousFocus = EventSystem.current?.currentSelectedGameObject;
            // World card raycasters outrank Canvas graphics, so the full-screen blocker alone is insufficient.
            foreach (var input in gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CardZoneMouseInput>(true)))
            {
                guideInputs[input] = input.enabled;
                input.Controller?.CancelPointer(); input.enabled = false;
            }
            host.SetHandGuideOpen(true);
            guideOverlay.gameObject.SetActive(true); guideOverlay.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
            EventSystem.current?.SetSelectedGameObject(guideClose.gameObject);
            RebuildHandGuide();
        }
        public void CloseHandGuide()
        {
            ClearGuideHighlights();
            if (guideOverlay != null) guideOverlay.gameObject.SetActive(false);
            foreach (var pair in guideInputs) if (pair.Key != null) pair.Key.enabled = pair.Value;
            guideInputs.Clear(); guideState = null; guideResult = null; guideTexts.Clear();
            if (host != null && host.IsHandGuideOpen) host.SetHandGuideOpen(false);
            if (EventSystem.current != null && guidePreviousFocus != null && guidePreviousFocus.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(guidePreviousFocus);
            guidePreviousFocus = null;
        }

        private void DestroyHandGuide()
        {
            CloseHandGuide();
            if (ownsGuideEntry && guideEntry != null) Destroy(guideEntry.gameObject);
            if (guideOverlay != null) Destroy(guideOverlay.gameObject);
        }

        private void EnsureHandGuidePanel()
        {
            if (guideOverlay != null) return;
            guideOverlay = GuideRect(transform, "Hand Guide Modal", new Rect(0, 0, 1, 1));
            var canvas = guideOverlay.gameObject.AddComponent<Canvas>(); canvas.overrideSorting = true; canvas.sortingOrder = 32700;
            guideOverlay.gameObject.AddComponent<GraphicRaycaster>();
            // No outside-click close: only panel controls / Escape are accepted.
            guideOverlay.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .12f);
            var border = GuideRect(guideOverlay, "Panel Border", new Rect(.24f, .055f, .56f, .57f));
            border.gameObject.AddComponent<Image>().color = new Color32(161, 136, 88, 255);
            var panel = GuideRect(border, "Panel", new Rect(0, 0, 1, 1));
            panel.offsetMin = new Vector2(2, 2); panel.offsetMax = new Vector2(-2, -2);
            panel.gameObject.AddComponent<Image>().color = GuidePanel;
            GuideLabel(panel, "Title", "牌型指南", new Rect(.025f, .02f, .7f, .08f), 26);
            guideClose = GuideButton(panel, "Close", "关闭 ×", CloseHandGuide, new Rect(.86f, .022f, .115f, .075f));
            var body = GuideRect(panel, "Body", new Rect(.025f, .13f, .95f, .83f));
            var left = GuideRect(body, "Types", new Rect(0, 0, .225f, 1));
            var types = (HandGuideType[])Enum.GetValues(typeof(HandGuideType));
            for (int i = 0; i < types.Length; i++)
            {
                var type = types[i];
                guideTypeButtons[type] = GuideButton(left, type.ToString(), BattleHandGuideText.TypeName(type),
                    () => { guideType = type; RebuildHandGuide(); }, new Rect(0, i * .080f, 1, .068f));
            }
            guideFlushButton = GuideButton(left, "Flush Filter", "□ 只看同花组合", () => { guideFlush = !guideFlush; RebuildHandGuide(); }, new Rect(0, .87f, 1, .10f));
            var divider = GuideRect(body, "Divider", new Rect(.245f, 0, .0015f, 1));
            divider.gameObject.AddComponent<Image>().color = new Color32(89, 80, 62, 255);
            var right = GuideRect(body, "Details", new Rect(.27f, 0, .73f, 1));
            guideHeading = GuideLabel(right, "Heading", "", new Rect(0, 0, .70f, .09f), 23);
            guideIndex = GuideLabel(right, "Combination", "", new Rect(.70f, 0, .30f, .09f), 16, TextAlignmentOptions.Right);
            guideAvailability = GuideLabel(right, "Availability", "", new Rect(0, .11f, 1, .075f), 18);
            guideTags = GuideLabel(right, "Effects", "", new Rect(0, .20f, 1, .085f), 17);
            guideCardRow = GuideRect(right, "Combination Cards", new Rect(0, .31f, 1, .27f));
            guideNotice = GuideLabel(right, "Notice", "", new Rect(0, .60f, 1, .06f), 13);
            var viewport = GuideRect(right, "Formula Viewport", new Rect(0, .69f, 1, .31f));
            viewport.gameObject.AddComponent<Image>().color = new Color32(15, 18, 23, 255);
            viewport.gameObject.AddComponent<RectMask2D>();
            guideFormulaScroll = viewport.gameObject.AddComponent<ScrollRect>();
            guideFormulaContent = GuideRect(viewport, "Content", new Rect(0, 0, 1, 1));
            guideFormulaContent.anchorMin = new Vector2(0, 1); guideFormulaContent.anchorMax = Vector2.one;
            guideFormulaContent.pivot = new Vector2(0, 1); guideFormulaContent.anchoredPosition = Vector2.zero;
            guideFormula = GuideLabel(guideFormulaContent, "Formula", "", new Rect(0, 0, 1, 1), 16);
            guideFormula.enableAutoSizing = false; guideFormula.enableWordWrapping = true; guideFormula.alignment = TextAlignmentOptions.TopLeft;
            guideFormula.overflowMode = TextOverflowModes.Overflow;
            guideFormula.margin = new Vector4(10, 8, 10, 8);
            guideFormulaScroll.viewport = viewport; guideFormulaScroll.content = guideFormulaContent;
            guideFormulaScroll.horizontal = false; guideFormulaScroll.vertical = true;
            guideFormulaScroll.movementType = ScrollRect.MovementType.Clamped; guideFormulaScroll.scrollSensitivity = 24;
            guideOverlay.gameObject.SetActive(false);
        }

        private void RebuildHandGuide()
        {
            ClearGuideHighlights(); guideTexts.Clear(); ClearGuideCards();
            guideState = host.State; guideShownIndex = -1;
            guideResult = host.Engine.QueryHandGuide(guideState, host.Player.ParticipantId, new HandGuideRequest(guideType, guideFlush));
            foreach (var pair in guideTypeButtons)
            {
                pair.Value.GetComponent<Image>().color = pair.Key == guideType ? new Color32(104, 77, 40, 255) : new Color32(42, 44, 47, 255);
                pair.Value.GetComponentInChildren<TMP_Text>().text = (pair.Key == guideType ? "● " : "") + BattleHandGuideText.TypeName(pair.Key);
            }
            guideFlushButton.GetComponentInChildren<TMP_Text>().text = (guideFlush ? "☑ " : "□ ") + "只看同花组合";
            if (guideResult.Candidates.Count == 0)
            {
                guideHeading.text = BattleHandGuideText.TypeName(guideType) + (guideFlush ? " · 同花" : "");
                guideIndex.text = ""; guideAvailability.text = BattleHandGuideText.AvailabilityText(guideResult); guideTags.text = "";
                guideNotice.text = "";
                SetGuideFormula(guideResult.Availability == HandGuideAvailability.Impossible ?
                    "当前手牌、牌库和可洗回的弃牌中，没有符合此条件的组合。" : "当前组合查询尚未完成，可切换牌型或同花条件。", true);
                return;
            }
            guideOutlineMaterial = Resources.Load<Material>("BattleConcept/Materials/hand_guide_outline");
            var hand = host.Actions.Hand.GetComponent<HandZoneView>();
            var outlines = new Dictionary<string, CardBreathingOutline>();
            foreach (var id in guideResult.Candidates.SelectMany(c => c.Cards).Where(c => !c.IsMissing).Select(c => c.Card.Id.Value).Distinct())
            {
                var card = hand.Views.FirstOrDefault(v => v.CardId?.Value == id);
                if (card == null || card.Surface == null || guideOutlineMaterial == null) continue;
                var outline = card.GetComponent<CardBreathingOutline>() ?? card.gameObject.AddComponent<CardBreathingOutline>();
                outline.enabled = true;
                BindGuideOutline(outline, card); outlines[id] = outline; guideOutlines.Add(outline);
            }
            guideCycle = new CardOutlineCycle(guideResult.Candidates.Select(c => c.Cards.Where(k => !k.IsMissing && outlines.ContainsKey(k.Card.Id.Value))
                .Select(k => outlines[k.Card.Id.Value]).ToArray()).ToArray(),
                guideResult.Candidates.Select((c, i) => GuideColors[i % GuideColors.Length]).ToArray());
            guideStarted = Time.unscaledTimeAsDouble;
            guideCycle.Tick(.35); ShowGuideCandidate(0);
        }

        private void BindGuideOutline(CardBreathingOutline outline, CardInstanceView card)
        {
            // Card artwork prefabs are normalized to XZ. Measure in SwayRoot space, never world AABB.
            var surface = card.Surface.transform; var mesh = surface.GetComponent<MeshFilter>().sharedMesh;
            var matrix = card.SwayRoot.worldToLocalMatrix * surface.localToWorldMatrix;
            var bounds = new Bounds(matrix.MultiplyPoint3x4(mesh.bounds.center), Vector3.zero);
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
                bounds.Encapsulate(matrix.MultiplyPoint3x4(mesh.bounds.center + Vector3.Scale(mesh.bounds.extents, new Vector3(x, y, z))));
            float unit = Mathf.Min(bounds.size.x, bounds.size.z);
            outline.Bind(card.SwayRoot, guideOutlineMaterial, new Vector2(bounds.size.x, bounds.size.z),
                new Vector3(bounds.center.x, bounds.max.y, bounds.center.z), Quaternion.Euler(-90, 0, 0),
                unit * .025f, unit * .045f, unit * .045f, unit * .003f);
        }
        private void ClearGuideHighlights()
        {
            guideCycle?.Dispose(); guideCycle = null;
            foreach (var outline in guideOutlines) if (outline != null) outline.enabled = false;
            guideOutlines.Clear();
        }
        private void ShowGuideCandidate(int index)
        {
            if (guideResult == null || index < 0 || index >= guideResult.Candidates.Count) return;
            guideShownIndex = index; var candidate = guideResult.Candidates[index];
            if (!guideTexts.TryGetValue(candidate.Id, out var text))
            {
                var preview = host.Engine.PreviewHandGuide(guideState, candidate);
                // Requested effect slots (e.g. the pair hit in a full house) use the current target,
                // or the first living target. ActorPolicy effects retain their configured auto targeting.
                var slots = new List<KeyValuePair<string, IEnumerable<ParticipantId>>>();
                foreach (var step in (preview.BaseEffects?.Steps ?? Array.Empty<BattleEffectStep>()).Where(s => s.Target.Selection == EffectTargetSelection.Requested).GroupBy(s => s.Target.RequestSlot))
                {
                    var target = step.First().Target;
                    var candidates = target.Group == EffectTargetGroup.Opponents ? guideState.Duel.Enemies.Select(e => e.Id) : guideState.Duel.Players.Select(p => p.Id);
                    var living = candidates.Where(id => guideState.Duel.Combat.Get(id).IsAlive).ToArray();
                    string selected = host.PlayTargetChoices.FirstOrDefault(c => c.Slot == step.Key)?.SelectedId;
                    if (living.Length > 0) slots.Add(new KeyValuePair<string, IEnumerable<ParticipantId>>(step.Key,
                        new[] { living.FirstOrDefault(id => id.Value == selected).IsValid ? living.First(id => id.Value == selected) : living[0] }));
                }
                preview = host.Engine.PreviewHandGuide(guideState, candidate, true, targetBindings: slots.Count == 0 ? null : new BattleTargetBindings(slots));
                text = new BattleHandGuideText(preview, host.Engine.Duel.Definition.StatusCatalog); guideTexts[candidate.Id] = text;
            }
            var color = GuideColors[index % GuideColors.Length];
            guideHeading.text = text.Heading; guideHeading.color = color;
            guideIndex.text = "方案 " + (index + 1) + " / " + guideResult.Candidates.Count + (guideResult.Truncated ? "+" : ""); guideIndex.color = color;
            guideAvailability.text = text.Availability; guideTags.text = string.Join("  ", text.EffectTags.Select(tag => "[" + tag + "]"));
            guideNotice.text = candidate.MissingCount > 0 ? "补齐示例 · 来自牌库 / 弃牌，不代表下次抽牌" : "对应颜色的手牌正在高亮 · 多个方案自动轮换";
            SetGuideFormula(string.Join("\n", text.FormulaLines) + "\n\n当前目标结算\n" + string.Join("\n", text.SettlementLines), index == 0);
            PresentGuideCards(candidate, color);
        }
        private void PresentGuideCards(HandGuideCandidate candidate, Color color)
        {
            ClearGuideCards();
            var surface = host.Actions.Hand.CardPrefab.GetComponentInChildren<CardView>(true);
            var material = host.DeckOverview?.View?.CardMaterial ?? host.DrawPile?.View?.CardMaterial;
            if (surface == null || material == null) return;
            float height = guideCardRow.rect.height;
            float width = height * surface.Atlas.cardSize.x / surface.Atlas.cardSize.y;
            float plusWidth = candidate.MissingCount > 0 ? 28 : 0;
            float scale = Mathf.Min(1, (guideCardRow.rect.width - plusWidth) / (candidate.Cards.Count * (width + 10)));
            width *= scale; height *= scale; float x = 0; bool plus = false;
            foreach (var entry in candidate.Cards.OrderBy(c => c.IsMissing))
            {
                if (entry.IsMissing && !plus)
                {
                    var label = GuideLabel(guideCardRow, "Plus", "+", new Rect(0, 0, 1, 1), 26, TextAlignmentOptions.Center);
                    GuidePixelRect(label.rectTransform, x, 0, 28, height); guideCards.Add(label.gameObject); x += 28; plus = true;
                }
                var frame = GuideRect(guideCardRow, entry.IsMissing ? "Completion sample" : "Held card", new Rect(0, 0, 1, 1));
                GuidePixelRect(frame, x, 0, width + 4, height + 4); frame.gameObject.AddComponent<Image>().color = entry.IsMissing ? new Color32(101, 105, 112, 255) : color;
                var cardRect = GuideRect(frame, "Card", new Rect(0, 0, 1, 1)); cardRect.offsetMin = new Vector2(2, 2); cardRect.offsetMax = new Vector2(-2, -2);
                var graphic = cardRect.gameObject.AddComponent<CardUIGraphic>(); graphic.raycastTarget = false;
                graphic.Bind(surface.Atlas, material); graphic.SetArtProfile(surface.ArtProfile);
                graphic.Present(new CardVisualData("guide/" + entry.Card.Id.Value, BattleDisplayProjector.StandardArt(entry.Face), entry.Card.Id,
                    effects: entry.Card.Effects, isDisabled: entry.Card.IsDisabled, artBindings: CardStateArtBindings.Standard));
                guideCards.Add(frame.gameObject); x += width + 10;
            }
        }
        private void ClearGuideCards()
        { foreach (var card in guideCards) if (card != null) { card.SetActive(false); Destroy(card); } guideCards.Clear(); }
        private void SetGuideFormula(string value, bool reset)
        {
            guideFormula.text = value;
            guideFormulaContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                Mathf.Max(guideFormulaScroll.viewport.rect.height, guideFormula.GetPreferredValues(value, Mathf.Max(100, guideFormulaScroll.viewport.rect.width - 20), Mathf.Infinity).y + 20));
            if (reset) { guideFormulaScroll.StopMovement(); guideFormulaContent.anchoredPosition = Vector2.zero; }
        }
        private RectTransform GuideRect(Transform parent, string name, Rect area)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.gameObject.layer = gameObject.layer; ConceptBattleSkin.Place(rect, area); return rect;
        }
        private TMP_Text GuideLabel(Transform parent, string name, string value, Rect area, int size, TextAlignmentOptions align = TextAlignmentOptions.Left)
        {
            var label = GuideRect(parent, name, area).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = message.font; label.text = value; label.color = GuidePaper; label.raycastTarget = false;
            label.fontSize = label.fontSizeMax = size; label.fontSizeMin = Mathf.Max(11, size * .7f);
            label.enableAutoSizing = true; label.enableWordWrapping = false; label.richText = false;
            label.alignment = align; label.overflowMode = TextOverflowModes.Ellipsis; return label;
        }
        private Button GuideButton(Transform parent, string name, string title, UnityEngine.Events.UnityAction action, Rect area)
        {
            var rect = GuideRect(parent, name, area); var image = rect.gameObject.AddComponent<Image>(); image.color = new Color32(42, 44, 47, 255);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.onClick.AddListener(action); button.navigation = new Navigation { mode = Navigation.Mode.None };
            GuideLabel(rect, "Label", title, new Rect(.04f, .08f, .92f, .84f), 17, TextAlignmentOptions.Center);
            return button;
        }
        private static void GuidePixelRect(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
        }
    }
}
