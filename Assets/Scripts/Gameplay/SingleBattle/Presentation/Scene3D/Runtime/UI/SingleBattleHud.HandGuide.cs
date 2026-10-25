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
        private Button guideClose, guideFlushButton, guidePureFlushButton, guideStraightFlushButton;
        private bool ownsGuideEntry;
        private RectTransform guideOverlay, guideCardRow, guideFormulaContent;
        private TMP_Text guideHeading, guideAvailability, guideTags, guideIndex, guideNotice, guideFormula, guideRuleSummary;
        private ScrollRect guideFormulaScroll;
        private HandGuideType guideType = HandGuideType.HighCard;
        private bool guideFlush, guideAttributeFlush;
        private CardDuelState guideState;
        private CardDuelState guideCacheState;
        private CardDuelEngine guideCacheEngine;
        private ParticipantId guideCacheActor;
        private string guideCacheTargetSelection;
        private HandGuideResult guideResult;
        private CardOutlineCycle guideCycle;
        private Material guideOutlineMaterial;
        private double guideStarted;
        private int guideShownIndex = -1;
        private GameObject guidePreviousFocus;
        private readonly Dictionary<HandGuideType, Button> guideTypeButtons = new Dictionary<HandGuideType, Button>();
        private readonly Dictionary<CardZoneMouseInput, bool> guideInputs = new Dictionary<CardZoneMouseInput, bool>();
        private readonly List<CardBreathingOutline> guideOutlines = new List<CardBreathingOutline>();
        private readonly List<GuideCardSlot> guideCards = new List<GuideCardSlot>();
        private TMP_Text guidePlus;
        private readonly Dictionary<int, HandGuideResult> guideQueries = new Dictionary<int, HandGuideResult>();
        private readonly Dictionary<string, BattleHandGuideText> guideTexts = new Dictionary<string, BattleHandGuideText>();
        private sealed class GuideCardSlot
        {
            public RectTransform Root;
            public Image Frame;
            public CardUIGraphic Graphic;
        }
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
            guideEntry.interactable = available && !host.IsHandGuideOpen && !IsHandGuideOpen && adminPanel?.IsOpen != true;
            if (!IsHandGuideOpen) return;
            if (!available) { CloseHandGuide(); return; }
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true) { CloseHandGuide(); return; }
            if (!ReferenceEquals(guideState, host.State)) RebuildHandGuide();
            AdvanceHandGuidePreloads();
            if (guideCycle == null) return;
            guideCycle.Tick(Time.unscaledTimeAsDouble - guideStarted + .35);
            if (guideShownIndex != guideCycle.ActiveIndex) ShowGuideCandidate(guideCycle.ActiveIndex);
        }

        private void EnsureHandGuideEntry()
        {
            if (guideEntry != null || host.DrawPile?.View == null) return;
            var icon = HandGuidePileIcon();
            if (icon == null) return;
            guideEntry = GuideButton(HandGuideEntryParent, "Hand Guide", "", OpenHandGuide, new Rect(0, 0, .04f, .08f));
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

        // Keep the button outside DrawPileView: pointer-enter events also reach its ancestors.
        private Transform HandGuideEntryParent => transform.Find("Safe Area") ?? transform;

        public void RefreshHandGuideEntryLayout()
        {
            var icon = HandGuidePileIcon();
            if (icon == null || guideEntry == null) return;
            var rect = (RectTransform)guideEntry.transform;
            if (rect.parent != HandGuideEntryParent) rect.SetParent(HandGuideEntryParent, false);
            // Keep placement valid even while the battle Canvas is inactive in the adventure scene.
            var shift = new Vector2(icon.anchorMax.x - icon.anchorMin.x, 0);
            rect.anchorMin = icon.anchorMin - shift; rect.anchorMax = icon.anchorMax - shift; rect.pivot = icon.pivot;
            rect.sizeDelta = icon.sizeDelta; rect.anchoredPosition = icon.anchoredPosition - new Vector2(icon.sizeDelta.x + 14, 0);
            rect.localScale = Vector3.one;
        }

        public void OpenHandGuide()
        {
            if (host?.State == null || host.IsHandGuideOpen || host.IsPresenting || host.State.Duel.IsFinished || adminPanel?.IsOpen == true || IsHandGuideOpen) return;
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
            bool ownedGate = IsHandGuideOpen;
            CancelHandGuidePreloads(); ClearGuideHighlights(true); ClearGuideCards();
            if (guideOverlay != null) guideOverlay.gameObject.SetActive(false);
            foreach (var pair in guideInputs) if (pair.Key != null) pair.Key.enabled = pair.Value;
            guideInputs.Clear(); guideState = null; guideResult = null;
            if (ownedGate && host != null && host.IsHandGuideOpen) host.SetHandGuideOpen(false);
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
            guideClose = GuideButton(panel, "Close", "关闭", CloseHandGuide, new Rect(.86f, .022f, .115f, .075f));
            var body = GuideRect(panel, "Body", new Rect(.025f, .13f, .95f, .83f));
            var left = GuideRect(body, "Types", new Rect(0, 0, .225f, 1));
            var types = (HandGuideType[])Enum.GetValues(typeof(HandGuideType));
            for (int i = 0; i < types.Length; i++)
            {
                var type = types[i];
                guideTypeButtons[type] = GuideButton(left, type.ToString(), BattleHandGuideText.TypeName(type),
                    () => {
                        bool ordinaryEntry = guideAttributeFlush && (type == HandGuideType.HighCard || type == HandGuideType.Straight);
                        if (guideType == type && !(ordinaryEntry && guideFlush)) return;
                        guideType = type; if (ordinaryEntry) guideFlush = false;
                        RebuildHandGuide();
                    }, new Rect(0, i * .080f, 1, .068f));
            }
            guidePureFlushButton = GuideButton(left, "Flush", "同花",
                () => { guideType = HandGuideType.HighCard; guideFlush = true; RebuildHandGuide(); }, new Rect(0, 0, 1, .061f));
            guideStraightFlushButton = GuideButton(left, "Straight Flush", "同花顺",
                () => { guideType = HandGuideType.Straight; guideFlush = true; RebuildHandGuide(); }, new Rect(0, 0, 1, .061f));
            guideFlushButton = GuideButton(left, "Flush Filter", "同花筛选：关", () => { guideFlush = !guideFlush; RebuildHandGuide(); }, new Rect(0, .87f, 1, .10f));
            var divider = GuideRect(body, "Divider", new Rect(.245f, 0, .0015f, 1));
            divider.gameObject.AddComponent<Image>().color = new Color32(89, 80, 62, 255);
            var right = GuideRect(body, "Details", new Rect(.27f, 0, .73f, 1));
            guideHeading = GuideLabel(right, "Heading", "", new Rect(0, 0, .70f, .09f), 23);
            guideIndex = GuideLabel(right, "Combination", "", new Rect(.70f, 0, .30f, .09f), 16, TextAlignmentOptions.Right);
            guideAvailability = GuideLabel(right, "Availability", "", new Rect(0, .11f, 1, .075f), 18);
            guideTags = GuideLabel(right, "Effects", "", new Rect(0, .20f, 1, .085f), 17);
            guideCardRow = GuideRect(right, "Combination Cards", new Rect(0, .31f, 1, .27f));
            guideRuleSummary = GuideLabel(right, "Basic Rule Summary", "", new Rect(0, .31f, 1, .27f), 18, TextAlignmentOptions.TopLeft);
            guideRuleSummary.enableWordWrapping = true;
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
            ClearGuideHighlights(); ClearGuideCards();
            guideState = host.State; guideShownIndex = -1; guideResult = null;
            var actor = host.Player.ParticipantId;
            if (!ReferenceEquals(guideCacheState, guideState) || !ReferenceEquals(guideCacheEngine, host.Engine) || guideCacheActor != actor)
            {
                CancelHandGuidePreloads(); guideExpandedQueries.Clear();
                guideQueries.Clear(); guideTexts.Clear(); ClearGuideHighlights(true);
                guideCacheState = guideState; guideCacheEngine = host.Engine; guideCacheActor = actor;
            }
            // Target selection can change without a new rules snapshot while the window is closed.
            // Reuse expensive settled text on reopen only when both snapshot and target choices match.
            string targets = string.Join("|", host.PlayTargetChoices.Select(choice => choice.Slot + ":" + choice.SelectedId));
            if (guideCacheTargetSelection != targets)
            { guideCacheTargetSelection = targets; guideTexts.Clear(); }
            var rule = guideState.Duel.Player(actor).Rules.EffectRule as ConfiguredHandEffectRule;
            guideAttributeFlush = rule?.ModernScoring == true && rule.FlushAsAttribute;
            LayoutGuideTypes();
            foreach (var pair in guideTypeButtons)
            {
                bool selected = pair.Key == guideType && !(guideAttributeFlush && guideFlush && (guideType == HandGuideType.HighCard || guideType == HandGuideType.Straight));
                pair.Value.GetComponent<Image>().color = selected ? new Color32(104, 77, 40, 255) : new Color32(42, 44, 47, 255);
                pair.Value.GetComponentInChildren<TMP_Text>().text = BattleHandGuideText.TypeName(pair.Key);
            }
            guidePureFlushButton.GetComponent<Image>().color = guideFlush && guideType == HandGuideType.HighCard ? new Color32(104, 77, 40, 255) : new Color32(42, 44, 47, 255);
            guideStraightFlushButton.GetComponent<Image>().color = guideFlush && guideType == HandGuideType.Straight ? new Color32(104, 77, 40, 255) : new Color32(42, 44, 47, 255);
            guideFlushButton.GetComponentInChildren<TMP_Text>().text = "同花筛选：" + (guideFlush ? "开" : "关");
            guideFlushButton.GetComponent<Image>().color = guideFlush ? new Color32(104, 77, 40, 255) : new Color32(42, 44, 47, 255);
            QueueHandGuidePreloads();
            if (guideQueries.TryGetValue(CurrentGuideQueryKey, out var cached)) { PresentHandGuideResult(cached); return; }
            PresentHandGuideBasicRules();
        }

        private void LayoutGuideTypes()
        {
            // Legacy content treats Flush as a separate scoring category; these two queries are
            // valid shortcuts only for the attribute-based rules, so retain its original list.
            guidePureFlushButton.gameObject.SetActive(guideAttributeFlush);
            guideStraightFlushButton.gameObject.SetActive(guideAttributeFlush);
            int row = 0;
            float step = guideAttributeFlush ? .070f : .080f, height = guideAttributeFlush ? .061f : .068f;
            foreach (HandGuideType type in Enum.GetValues(typeof(HandGuideType)))
            {
                ConceptBattleSkin.Place(guideTypeButtons[type].transform, new Rect(0, row++ * step, 1, height));
                if (!guideAttributeFlush || type != HandGuideType.Straight) continue;
                ConceptBattleSkin.Place(guidePureFlushButton.transform, new Rect(0, row++ * step, 1, height));
                ConceptBattleSkin.Place(guideStraightFlushButton.transform, new Rect(0, row++ * step, 1, height));
            }
        }

        private void PresentHandGuideBasicRules(HandGuideResult result = null)
        {
            ClearGuideHighlights(); ClearGuideCards();
            guideHeading.text = guideAttributeFlush ? BattleHandGuideText.TypeName(guideType, guideFlush) :
                BattleHandGuideText.TypeName(guideType) + (guideFlush ? " · 同花" : "");
            guideHeading.color = GuidePaper;
            guideIndex.text = ""; guideTags.text = "基本规则";
            guideAvailability.text = result == null ? "基本规则" : result.Availability == HandGuideAvailability.Impossible ? "当前牌组无法补齐" : "暂未找到组合";
            guideNotice.text = result?.Availability == HandGuideAvailability.Impossible ? "牌型条件与效果见下方" :
                FindGuidePreload(CurrentGuideQueryKey) != null ? "找到可用组合后会自动显示示例" : "暂未找到可用示例，可先查看牌型规则";
            var player = guideState.Duel.Player(host.Player.ParticipantId);
            guideRuleSummary.gameObject.SetActive(true);
            guideRuleSummary.text = BattleHandGuideText.BasicRuleSummary(guideType, guideFlush, player);
            SetGuideFormula(string.Join("\n", BattleHandGuideText.BasicRuleLines(guideType, guideFlush, player,
                host.Engine.Scoring, host.Engine.Duel.Definition.StatusCatalog)), true);
        }

        private void PresentHandGuideResult(HandGuideResult result)
        {
            string previousId = guideResult != null && guideShownIndex >= 0 && guideShownIndex < guideResult.Candidates.Count ? guideResult.Candidates[guideShownIndex].Id : null;
            double phase = guideCycle != null ? (Time.unscaledTimeAsDouble - guideStarted + .35) % 2 : .35;
            ClearGuideHighlights();
            guideResult = result;
            if (guideResult.Candidates.Count == 0)
            {
                PresentHandGuideBasicRules(result); return;
            }
            guideRuleSummary.gameObject.SetActive(false);
            if (guideOutlineMaterial == null) guideOutlineMaterial = Resources.Load<Material>("BattleConcept/Materials/hand_guide_outline");
            var hand = host.Actions.Hand.GetComponent<HandZoneView>();
            var outlines = new Dictionary<string, CardBreathingOutline>();
            foreach (var id in guideResult.Candidates.SelectMany(c => c.Cards).Where(c => !c.IsMissing).Select(c => c.Card.Id.Value).Distinct())
            {
                var card = hand.Views.FirstOrDefault(v => v.CardId?.Value == id);
                if (card == null || card.Surface == null || guideOutlineMaterial == null) continue;
                var outline = card.GetComponent<CardBreathingOutline>() ?? card.gameObject.AddComponent<CardBreathingOutline>();
                if (!guideOutlines.Contains(outline) || outline.OutlineRenderer == null)
                {
                    outline.enabled = true; BindGuideOutline(outline, card);
                    if (!guideOutlines.Contains(outline)) guideOutlines.Add(outline);
                }
                outlines[id] = outline;
            }
            guideCycle = new CardOutlineCycle(guideResult.Candidates.Select(c => c.Cards.Where(k => !k.IsMissing && outlines.ContainsKey(k.Card.Id.Value))
                .Select(k => outlines[k.Card.Id.Value]).ToArray()).ToArray(),
                guideResult.Candidates.Select((c, i) => GuideColors[i % GuideColors.Length]).ToArray());
            int index = 0;
            for (int i = 0; i < result.Candidates.Count; i++) if (result.Candidates[i].Id == previousId) { index = i; break; }
            guideStarted = Time.unscaledTimeAsDouble - (index * 2 + phase - .35);
            guideCycle.Tick(index * 2 + phase); ShowGuideCandidate(index, previousId == null);
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
                unit * .05f, unit * .06f, unit * .045f, unit * .003f);
        }
        private void ClearGuideHighlights(bool release = false)
        {
            guideCycle?.Dispose(); guideCycle = null;
            if (!release) return;
            foreach (var outline in guideOutlines) if (outline != null) outline.enabled = false;
            guideOutlines.Clear();
        }
        private BattleHandGuideText GetGuideCandidateText(HandGuideCandidate candidate)
        {
            if (!guideTexts.TryGetValue(candidate.Id, out var text))
            {
                var preview = host.Engine.PreviewHandGuide(guideState, candidate, true, targetBindingResolver: ResolveGuideTargets);
                text = new BattleHandGuideText(preview, host.Engine.Duel.Definition.StatusCatalog); guideTexts[candidate.Id] = text;
            }
            return text;
        }
        private BattleTargetBindings ResolveGuideTargets(BattleEffectSequence effects)
        {
            // Requested slots use the selected target or first living target. ActorPolicy effects
            // retain their configured targeting; the engine still performs authoritative settlement.
            var slots = new List<KeyValuePair<string, IEnumerable<ParticipantId>>>();
            foreach (var step in (effects?.Steps ?? Array.Empty<BattleEffectStep>()).Where(s => s.Target.Selection == EffectTargetSelection.Requested).GroupBy(s => s.Target.RequestSlot))
            {
                var target = step.First().Target;
                var candidates = target.Group == EffectTargetGroup.Opponents ? guideState.Duel.Enemies.Select(e => e.Id) : guideState.Duel.Players.Select(p => p.Id);
                var living = candidates.Where(id => guideState.Duel.Combat.Get(id).IsAlive).ToArray();
                string selected = host.PlayTargetChoices.FirstOrDefault(c => c.Slot == step.Key)?.SelectedId;
                if (living.Length > 0) slots.Add(new KeyValuePair<string, IEnumerable<ParticipantId>>(step.Key,
                    new[] { living.FirstOrDefault(id => id.Value == selected).IsValid ? living.First(id => id.Value == selected) : living[0] }));
            }
            return slots.Count == 0 ? null : new BattleTargetBindings(slots);
        }
        private void ShowGuideCandidate(int index, bool resetScroll = false)
        {
            if (guideResult == null || index < 0 || index >= guideResult.Candidates.Count) return;
            guideShownIndex = index; var candidate = guideResult.Candidates[index];
            var text = GetGuideCandidateText(candidate);
            var color = GuideColors[index % GuideColors.Length];
            guideHeading.text = text.Heading; guideHeading.color = color;
            guideIndex.text = "方案 " + (index + 1) + " / " + guideResult.Candidates.Count + (guideResult.Truncated ? "+" : ""); guideIndex.color = color;
            guideAvailability.text = text.Availability; guideTags.text = string.Join("  ", text.EffectTags.Select(tag => "[" + tag + "]"));
            guideNotice.text = candidate.MissingCount > 0 ? "补齐示例 · 来自牌库 / 弃牌，不代表下次抽牌" : "对应颜色的手牌正在高亮 · 多个方案自动轮换";
            var player = guideState.Duel.Player(host.Player.ParticipantId);
            var basicRules = BattleHandGuideText.BasicRuleLines(guideType, guideFlush, player,
                host.Engine.Scoring, host.Engine.Duel.Definition.StatusCatalog);
            SetGuideFormula("本局组合预览\n" + string.Join("\n", text.FormulaLines) + "\n\n当前目标结算\n" + string.Join("\n", text.SettlementLines) +
                "\n\n基础规则（与本局数值对照）\n" + string.Join("\n", basicRules), resetScroll);
            PresentGuideCards(candidate, color);
        }
        private void PresentGuideCards(HandGuideCandidate candidate, Color color)
        {
            var surface = host.Actions.Hand.CardPrefab.GetComponentInChildren<CardView>(true);
            var material = host.DeckOverview?.View?.CardMaterial ?? host.DrawPile?.View?.CardMaterial;
            if (surface == null || material == null) { ClearGuideCards(); return; }
            float height = guideCardRow.rect.height;
            float width = height * surface.Atlas.cardSize.x / surface.Atlas.cardSize.y;
            float plusWidth = candidate.MissingCount > 0 ? 28 : 0;
            float scale = Mathf.Min(1, (guideCardRow.rect.width - plusWidth) / (candidate.Cards.Count * (width + 10)));
            width *= scale; height *= scale; float x = 0; bool plus = false; int slotIndex = 0;
            if (guidePlus != null) guidePlus.gameObject.SetActive(false);
            foreach (var entry in candidate.Cards.OrderBy(c => c.IsMissing))
            {
                if (entry.IsMissing && !plus)
                {
                    if (guidePlus == null) guidePlus = GuideLabel(guideCardRow, "Plus", "+", new Rect(0, 0, 1, 1), 26, TextAlignmentOptions.Center);
                    guidePlus.gameObject.SetActive(true); GuidePixelRect(guidePlus.rectTransform, x, 0, 28, height); x += 28; plus = true;
                }
                if (slotIndex == guideCards.Count)
                {
                    var frame = GuideRect(guideCardRow, "Guide Card", new Rect(0, 0, 1, 1));
                    var cardRect = GuideRect(frame, "Card", new Rect(0, 0, 1, 1)); cardRect.offsetMin = new Vector2(2, 2); cardRect.offsetMax = new Vector2(-2, -2);
                    var cardGraphic = cardRect.gameObject.AddComponent<CardUIGraphic>(); cardGraphic.raycastTarget = false;
                    guideCards.Add(new GuideCardSlot { Root = frame, Frame = frame.gameObject.AddComponent<Image>(), Graphic = cardGraphic });
                }
                var slot = guideCards[slotIndex++]; slot.Root.gameObject.SetActive(true);
                GuidePixelRect(slot.Root, x, 0, width + 4, height + 4); slot.Frame.color = entry.IsMissing ? new Color32(101, 105, 112, 255) : color;
                var graphic = slot.Graphic;
                if (graphic.Atlas != surface.Atlas || graphic.material != material) graphic.Bind(surface.Atlas, material);
                if (graphic.ArtProfile != surface.ArtProfile) graphic.SetArtProfile(surface.ArtProfile);
                graphic.Present(new CardVisualData("guide/" + entry.Card.Id.Value, BattleDisplayProjector.StandardArt(entry.Face), entry.Card.Id,
                    effects: entry.Card.Effects, isDisabled: entry.Card.IsDisabled, artBindings: CardStateArtBindings.Standard));
                x += width + 10;
            }
            for (int i = slotIndex; i < guideCards.Count; i++) guideCards[i].Root.gameObject.SetActive(false);
        }
        private void ClearGuideCards()
        {
            foreach (var card in guideCards) if (card.Root != null) card.Root.gameObject.SetActive(false);
            if (guidePlus != null) guidePlus.gameObject.SetActive(false);
        }
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
