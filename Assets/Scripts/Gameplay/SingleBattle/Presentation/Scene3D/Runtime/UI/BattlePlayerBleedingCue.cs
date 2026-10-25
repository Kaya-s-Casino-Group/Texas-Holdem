using System.Globalization;
using System.Linq;
using Poker.BattlePresentation;
using PokerEngine;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.SingleBattle
{
    /// <summary>Read-only current bleeding cue in the existing player status slot.</summary>
    [DisallowMultipleComponent]
    public sealed class BattlePlayerBleedingCue : MonoBehaviour
    {
        private Image icon;
        private RectTransform details;
        private TMP_Text detailsText;
        private BattleDemoEnemyBadgeTooltip hover;

        public static void Present(TMP_Text label, TeamBattleState presented, string playerId,
            CombatStatusCatalog catalog, string fallback)
        {
            if (label == null || presented == null) return;
            var statuses = presented.Combat.Statuses.Where(status => status.Target.Kind == EntityKind.Participant && status.Target.Id == playerId).ToArray();
            var bleeding = statuses.Where(status => IsBleeding(status, catalog)).ToArray();
            var cue = label.GetComponent<BattlePlayerBleedingCue>();
            if (bleeding.Length == 0)
            {
                if (cue != null) cue.HideCue();
                SingleBattleHud.PresentActorStatuses(label, fallback);
                return;
            }
            if (cue == null) cue = label.gameObject.AddComponent<BattlePlayerBleedingCue>();
            cue.Ensure(label);
            decimal stacks = bleeding.Sum(status => (decimal)status.Stacks);
            decimal increase = bleeding.Sum(status => status.Stacks * catalog.Find(status.DefinitionId).Behaviors
                .OfType<IncomingAttackDamagePerStackSpec>().Sum(behavior => behavior.Rate));
            string summary = "流血 " + stacks.ToString("0", CultureInfo.InvariantCulture) + " 层 · 加伤已计入意图";
            var other = statuses.Where(status => !IsBleeding(status, catalog)).Select(status => BattlePresentationRuleText.Status(status, catalog)).ToArray();
            string text = "<line-height=28px><indent=30px><size=16><color=#EF7D7B>" + summary + "</color></size></indent></line-height>";
            if (other.Length > 0) text += "\n" + string.Join("\n", other);
            SingleBattleHud.PresentActorStatuses(label, text);
            cue.icon.gameObject.SetActive(true);
            string explanation = "流血 " + stacks.ToString("0", CultureInfo.InvariantCulture) + " 层 · 承受敌方攻击伤害 +" +
                (increase * 100).ToString("0.##", CultureInfo.InvariantCulture) + "%（护盾抵挡前）\n" +
                "已计入敌方伤害预告，无需再次计算；格挡另算。\n" +
                string.Join("\n", bleeding.Select(status => BattlePresentationRuleText.Status(status, catalog)));
            if (cue.hover.Description != explanation) cue.HideDetails();
            cue.hover.Description = explanation;
        }

        private static bool IsBleeding(StatusInstance status, CombatStatusCatalog catalog) =>
            catalog?.Find(status.DefinitionId)?.Behaviors.Any(behavior => behavior is IncomingAttackDamagePerStackSpec) == true;

        private void Ensure(TMP_Text owner)
        {
            if (icon != null) return;
            var iconRect = ConceptBattleSkin.Node(transform, "Bleeding Status Icon");
            iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0, 1);
            iconRect.anchoredPosition = new Vector2(0, -2); iconRect.sizeDelta = new Vector2(24, 24);
            icon = iconRect.gameObject.AddComponent<Image>();
            icon.sprite = ConceptBattleSkin.Get("Icons/icon_bleeding"); icon.preserveAspect = true; icon.raycastTarget = true;
            hover = iconRect.gameObject.AddComponent<BattleDemoEnemyBadgeTooltip>();
            hover.Show = description =>
            {
                EnsureDetails(owner); detailsText.text = description;
                details.gameObject.SetActive(true); details.SetAsLastSibling();
            };
            hover.Hide = HideDetails;
        }
        private void EnsureDetails(TMP_Text owner)
        {
            if (details != null) return;
            details = ConceptBattleSkin.Node(transform, "Bleeding Status Details");
            details.anchorMin = details.anchorMax = details.pivot = new Vector2(0, 1);
            details.anchoredPosition = new Vector2(0, -30); details.sizeDelta = new Vector2(350, 112);
            var background = details.gameObject.AddComponent<Image>();
            background.color = new Color(.08f, .065f, .09f, .96f); background.raycastTarget = false;
            detailsText = ConceptBattleSkin.Text(details, "Description", owner.font, new Rect(.03f, .04f, .94f, .92f), "", 13, TextAlignmentOptions.TopLeft);
            detailsText.enableWordWrapping = true; detailsText.enableAutoSizing = false;
        }
        private void HideCue()
        {
            if (icon != null) icon.gameObject.SetActive(false);
            HideDetails();
        }
        private void HideDetails() { if (details != null) details.gameObject.SetActive(false); }
        private void OnDisable() => HideDetails();
    }
}
