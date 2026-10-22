using System.Linq;
using Poker.BattlePresentation;
using PokerEngine;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Poker.SingleBattle
{
    public sealed partial class SingleBattleHud
    {
        private RectTransform relicHoverPanel, relicHoverAnchor;
        private TMP_Text relicHoverName, relicHoverDescription, relicHoverRuntime;
        private Image relicHoverIcon;
        private Button relicHoverUse;
        private readonly System.Collections.Generic.List<Button> relicReplacementButtons = new System.Collections.Generic.List<Button>();
        private string relicHoverId, relicReplacementId, relicHoverMatch, relicHoverObserver;
        private CardDuelState relicHoverState;
        private float relicHoverCloseAt = float.PositiveInfinity;
        private bool relicUseOpen;
        internal bool IsRelicUseOpen(string id) => relicUseOpen && relicHoverId == id;

        private void BindRelicHover(Image icon, BattleRelicState relic, BattleRelicLoadout loadout)
        {
            icon.raycastTarget = true;
            var hover = icon.GetComponent<BattleDemoRelicHoverTarget>() ?? icon.gameObject.AddComponent<BattleDemoRelicHoverTarget>();
            var count = icon.transform.Find("Remaining Uses")?.GetComponent<TMP_Text>();
            if (count == null)
            {
                count = Instantiate(message, icon.transform); count.name = "Remaining Uses";
                count.rectTransform.anchorMin = count.rectTransform.anchorMax = count.rectTransform.pivot = new Vector2(1, 0);
                count.rectTransform.anchoredPosition = new Vector2(3, -2); count.rectTransform.sizeDelta = new Vector2(28, 22);
                count.fontSize = 16; count.enableAutoSizing = false; count.fontStyle = FontStyles.Bold;
                count.alignment = TextAlignmentOptions.BottomRight; count.raycastTarget = false; count.enableWordWrapping = false;
            }
            BattleBadgeText.Apply(count);
            var activation = loadout.RulesFor(relic.InstanceId).Item?.Activation;
            bool active = activation.HasValue && activation.Value != BattleItemActivation.None;
            hover.Configure(this, relic.InstanceId, active);
            count.text = relic.RemainingCharges?.ToString() ?? "";
            count.gameObject.SetActive(active && relic.RemainingCharges.HasValue);
        }

        internal void KeepRelicHover() => relicHoverCloseAt = float.PositiveInfinity;
        internal void LeaveRelicHover()
        { if (!relicUseOpen) relicHoverCloseAt = Time.unscaledTime + .18f; }
        private void HideRelicHover()
        {
            if (relicHoverPanel != null) relicHoverPanel.gameObject.SetActive(false);
            relicHoverId = relicReplacementId = null; relicHoverState = null;
            relicUseOpen = false;
        }

        internal void ShowRelicHover(string id, RectTransform anchor, bool forUse = false)
        {
            if (IsHandGuideOpen) return;
            if (relicUseOpen && !forUse) return;
            if (GetComponent<BattleDemoRelicArt>() == null || host?.State == null || host.Player == null) return;
            var relic = host.State.Duel.Player(host.Player.ParticipantId).Relics.Relics.FirstOrDefault(item => item.InstanceId == id);
            if (relic == null) return;
            if (relicHoverId != id) relicReplacementId = null;
            relicUseOpen = forUse;
            relicHoverId = id; relicHoverAnchor = anchor; KeepRelicHover();
            relicHoverMatch = host.State.Table.MatchId.ToString(); relicHoverObserver = host.Player.ParticipantId.Value;
            EnsureRelicHover(); relicHoverPanel.gameObject.SetActive(true); relicHoverPanel.SetAsLastSibling();
            RebuildRelicHover();
        }

        private void EnsureRelicHover()
        {
            if (relicHoverPanel != null) return;
            relicHoverPanel = new GameObject("Relic Hover", typeof(RectTransform), typeof(Image), typeof(BattleDemoRelicHoverTarget)).GetComponent<RectTransform>();
            relicHoverPanel.SetParent(transform, false); relicHoverPanel.gameObject.layer = gameObject.layer;
            Place(relicHoverPanel, 0, 0, 300, 200);
            var background = relicHoverPanel.GetComponent<Image>(); background.color = new Color32(25, 23, 32, 250);
            background.raycastTarget = true; GetComponent<BattleDemoUITheme>()?.StylePanel(background);
            relicHoverPanel.GetComponent<BattleDemoRelicHoverTarget>().Configure(this, null);
            relicHoverIcon = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            relicHoverIcon.transform.SetParent(relicHoverPanel, false); relicHoverIcon.gameObject.layer = gameObject.layer;
            relicHoverIcon.preserveAspect = true; relicHoverIcon.raycastTarget = false; Place(relicHoverIcon.rectTransform, 14, -12, 38, 38);
            relicHoverName = RelicHoverText("Item Name", 18); Place(relicHoverName.rectTransform, 62, -14, 224, 28);
            relicHoverDescription = RelicHoverText("Effect Description", 14);
            relicHoverRuntime = RelicHoverText("Availability", 12); relicHoverRuntime.color = new Color32(204, 187, 157, 255);
            relicHoverUse = CreateToolsButton(relicHoverPanel, "使用", UseHoveredRelic, 0, 36);
            relicHoverUse.name = "Use Item";
        }

        private TMP_Text RelicHoverText(string name, int size)
        {
            var text = Instantiate(message, relicHoverPanel); text.gameObject.SetActive(true); text.name = name;
            text.fontSize = size; text.enableAutoSizing = false; text.fontStyle = FontStyles.Normal;
            text.alignment = TextAlignmentOptions.TopLeft; text.enableWordWrapping = true; text.richText = false;
            text.overflowMode = TextOverflowModes.Overflow; text.raycastTarget = false;
            text.color = new Color32(242, 230, 203, 255); return text;
        }

        private void RebuildRelicHover()
        {
            var loadout = host.State.Duel.Player(host.Player.ParticipantId).Relics;
            var relic = loadout.Relics.FirstOrDefault(item => item.InstanceId == relicHoverId);
            if (relic == null) { HideRelicHover(); return; }
            relicHoverState = host.State;
            var rules = loadout.RulesFor(relic.InstanceId); var spec = rules.Item;
            bool active = relicUseOpen && spec != null && spec.Activation != BattleItemActivation.None;
            relicHoverIcon.sprite = ResolveRelicIcon(relic.DefinitionId);
            relicHoverName.text = host.ItemName(relic.DefinitionId) + "  Lv." + relic.Level;
            relicHoverDescription.text = BattlePresentationRuleText.RelicDescription(rules);
            float descriptionHeight = Mathf.Max(24, relicHoverDescription.GetPreferredValues(relicHoverDescription.text, 272, Mathf.Infinity).y);
            Place(relicHoverDescription.rectTransform, 14, -60, 272, descriptionHeight);
            relicHoverRuntime.text = BattlePresentationRuleText.RelicRuntime(relic);
            float runtimeHeight = Mathf.Max(20, relicHoverRuntime.GetPreferredValues(relicHoverRuntime.text, 272, Mathf.Infinity).y);
            float top = 70 + descriptionHeight; Place(relicHoverRuntime.rectTransform, 14, -top, 272, runtimeHeight); top += runtimeHeight + 12;
            foreach (var button in relicReplacementButtons) { button.gameObject.SetActive(false); Destroy(button.gameObject); }
            relicReplacementButtons.Clear();
            if (active && spec.Activation == BattleItemActivation.ReplaceHeld)
            {
                var targets = loadout.Relics.Where(item => item.InstanceId != relic.InstanceId).ToArray();
                if (!targets.Any(item => item.InstanceId == relicReplacementId)) relicReplacementId = null;
                for (int i = 0; i < targets.Length; i++)
                {
                    var target = targets[i];
                    var button = CreateToolsButton(relicHoverPanel,
                        (relicReplacementId == target.InstanceId ? "✓ " : "替换 ") + host.ItemName(target.DefinitionId),
                        () => { relicReplacementId = target.InstanceId; RebuildRelicHover(); }, top + i / 2 * 34, 30);
                    Place((RectTransform)button.transform, 14 + i % 2 * 140, -(top + i / 2 * 34), 132, 30);
                    button.GetComponentInChildren<TMP_Text>().fontSize = 12;
                    button.interactable = host.CanUseItem(relic.InstanceId); relicReplacementButtons.Add(button);
                }
                top += Mathf.CeilToInt(targets.Length / 2f) * 34 + 8;
            }
            relicHoverUse.gameObject.SetActive(active);
            relicHoverUse.interactable = active && CanUseHoveredRelic();
            if (active)
            {
                Place((RectTransform)relicHoverUse.transform, 14, -top, 272, 36);
                relicHoverUse.GetComponentInChildren<TMP_Text>().text = "使用 " + host.ItemName(relic.DefinitionId);
                top += 48;
            }
            relicHoverPanel.sizeDelta = new Vector2(300, top + 2); PositionRelicHover();
        }

        private void PositionRelicHover()
        {
            if (relicHoverAnchor == null) return;
            var root = (RectTransform)transform;
            var corners = new Vector3[4]; relicHoverAnchor.GetWorldCorners(corners);
            Vector2 corner = root.InverseTransformPoint(corners[1]);
            float width = relicHoverPanel.rect.width, height = relicHoverPanel.rect.height;
            float x = corner.x - width - 6;
            if (x < root.rect.xMin + 8) x = root.InverseTransformPoint(corners[2]).x + 6;
            float y = Mathf.Clamp(corner.y, root.rect.yMin + height + 8, root.rect.yMax - 8);
            x = Mathf.Clamp(x, root.rect.xMin + 8, root.rect.xMax - width - 8);
            relicHoverPanel.anchoredPosition = new Vector2(x - root.rect.xMin, y - root.rect.yMax);
        }

        private void UseHoveredRelic()
        {
            if (!CanUseHoveredRelic()) return;
            host.UseItem(relicHoverId, relicReplacementId);
            if (relicHoverId != null) RebuildRelicHover();
        }

        private bool CanUseHoveredRelic()
        {
            if (relicHoverId == null || !host.CanUseItem(relicHoverId)) return false;
            var spec = host.State.Duel.Player(host.Player.ParticipantId).Relics.RulesFor(relicHoverId).Item;
            return spec?.Activation != BattleItemActivation.ReplaceHeld || relicReplacementId != null;
        }

        private void RefreshRelicHover()
        {
            if (relicHoverId == null) return;
            if (host?.State == null || host.Player == null || relicHoverMatch != host.State.Table.MatchId.ToString() ||
                relicHoverObserver != host.Player.ParticipantId.Value || relicHoverAnchor == null || !relicHoverAnchor.gameObject.activeInHierarchy ||
                Time.unscaledTime >= relicHoverCloseAt) { HideRelicHover(); return; }
            var pointer = Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame)
            {
                var canvas = GetComponentInParent<Canvas>();
                var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                var position = pointer.position.ReadValue();
                if (!RectTransformUtility.RectangleContainsScreenPoint(relicHoverPanel, position, camera) &&
                    !RectTransformUtility.RectangleContainsScreenPoint(relicHoverAnchor, position, camera))
                { HideRelicHover(); return; }
            }
            if (!ReferenceEquals(relicHoverState, host.State)) RebuildRelicHover();
            if (relicHoverId == null) return;
            relicHoverUse.interactable = CanUseHoveredRelic();
            foreach (var button in relicReplacementButtons) button.interactable = host.CanUseItem(relicHoverId);
            PositionRelicHover();
        }
    }
}
