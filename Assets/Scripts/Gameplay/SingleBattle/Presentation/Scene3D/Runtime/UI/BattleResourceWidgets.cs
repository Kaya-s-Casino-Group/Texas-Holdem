using System;
using System.Linq;
using PokerEngine;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.SingleBattle
{
    public static class BattleResourceWidgets
    {
        private static Texture2D bloodTexture;
        public static BattleActionPointLiquid PresentActions(Transform frame, long current)
        {
            var backing = frame.Find("Reservoir Background") as RectTransform;
            if (backing == null)
            {
                backing = ConceptBattleSkin.Node(frame, "Reservoir Background");
                ConceptBattleSkin.Place(backing, new Rect(.16f, .16f, .68f, .68f));
                var graphic = backing.gameObject.AddComponent<BattleActionPointBacking>();
                graphic.color = new Color32(48, 28, 18, 255); graphic.raycastTarget = false;
            }
            var node = frame.Find("Action Liquid") as RectTransform;
            if (node == null)
            {
                node = ConceptBattleSkin.Node(frame, "Action Liquid");
                ConceptBattleSkin.Place(node, new Rect(.16f, .16f, .68f, .68f));
                node.SetAsFirstSibling();
            }
            var liquid = node.GetComponent<BattleActionPointLiquid>();
            if (liquid == null) liquid = node.gameObject.AddComponent<BattleActionPointLiquid>();
            if (bloodTexture == null) bloodTexture = Resources.Load<Texture2D>("BattleConcept/Textures/action_blood_liquid");
            liquid.SetTexture(bloodTexture);
            liquid.raycastTarget = false; liquid.SetActions(current);
            var overlay = frame.Find("Action Frame Overlay");
            if (overlay == null)
                overlay = ConceptBattleSkin.Art(frame, "Action Frame Overlay", "Frames/frame_action_points", new Rect(0, 0, 1, 1), false).transform;
            // uGUI draws children above their parent. Move the frame art into its own child so it occludes the liquid.
            var originalFrame = frame.GetComponent<Image>(); if (originalFrame != null) originalFrame.enabled = false;
            overlay.SetAsFirstSibling(); node.SetAsFirstSibling(); backing.SetAsFirstSibling();
            return liquid;
        }

        public static long DiscardShieldGain(CardDuelTransition preview, ParticipantId player)
        {
            if (preview == null || !preview.Accepted) return 0;
            return preview.Events.Where(e => e.Effect.Kind == CombatEffectKind.GainBlock && e.Effect.Target == player)
                .Sum(e => Math.Max(0, e.After.Block - e.Before.Block));
        }

        public static void PresentShield(Transform discard, TMP_FontAsset font, long gain)
        {
            var row = discard.Find("Discard Shield Preview") ?? discard.Find("Current Shield");
            if (row == null)
            {
                row = ConceptBattleSkin.Node(discard, "Discard Shield Preview");
                ConceptBattleSkin.Place(row, new Rect(.20f, 1.07f, .60f, .40f));
                ConceptBattleSkin.Art(row, "Shield Icon", "Icons/icon_shield", new Rect(0, 0, .38f, 1));
                BattleBadgeText.Apply(ConceptBattleSkin.Text(row, "Shield Count", font,
                    new Rect(.42f, 0, .58f, 1), "+" + gain, 20, TextAlignmentOptions.Left));
            }
            row.name = "Discard Shield Preview";
            var count = row.Find("Shield Count").GetComponent<TMP_Text>();
            count.text = "+" + gain;
        }
    }
}
