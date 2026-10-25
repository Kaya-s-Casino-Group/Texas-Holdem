#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using CardPresentation;
using NUnit.Framework;
using Poker.SingleBattle;
using PokerEngine;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Poker.Adventure.Tests
{
    public sealed partial class SingleChapterAdventureSceneTests
    {
        [UnityTest]
        public IEnumerator DiscardBlockHintRemainsReadableWithoutCoveringBattleActions()
        {
            yield return ReachBattle("battle-1");
            var view = host.BattleView; var player = host.Adventure.PlayerId;
            Assert.IsTrue(view.Playback.ApplyAdmin(host.Revision, new BattleAdminPatch(
                relics: new[] { new BattleAdminRelics(player, new BattleAdminRelic[0]) })).Accepted);
            yield return Idle();
            yield return InformationHand("standard:1:9", "standard:2:2");
            var hand = view.Actions.Hand;
            foreach (var card in hand.Cards.Skip(1).ToArray()) hand.SetInteraction(card.Id, new CardInteractionState(isSelected: false));
            hand.Publish(); view.RefreshCandidate(); yield return null;
            var label = Object.FindObjectsOfType<TMP_Text>().Single(text => text.name == "Shield Count");
            Assert.AreEqual("格挡+9", label.text);
            Assert.IsTrue(InformationTextIsRendered(label));
            var canvas = label.GetComponentInParent<Canvas>();
            var row = (RectTransform)label.transform.parent;
            var displayedScale = row.localScale;
            float effectiveSize = label.fontSize * label.rectTransform.lossyScale.y / canvas.scaleFactor;
            Rect displayedBounds = ScreenArea(row);
            // Compare the actual rendered hint with its former unscaled presentation in this authored hierarchy.
            // Ancestor scales vary with the scene; a fixed TMP fontSize does not measure screen readability.
            row.localScale = Vector3.one; Canvas.ForceUpdateCanvases();
            float originalSize = label.fontSize * label.rectTransform.lossyScale.y / canvas.scaleFactor;
            Rect originalBounds = ScreenArea(row);
            row.localScale = displayedScale; Canvas.ForceUpdateCanvases();
            Assert.GreaterOrEqual(effectiveSize, originalSize * 1.25f, "The displayed hint text must grow after the authored layout applies.");
            Assert.GreaterOrEqual(displayedBounds.width, originalBounds.width * 1.25f);
            Assert.GreaterOrEqual(displayedBounds.height, originalBounds.height * 1.25f);
            TestContext.WriteLine("Rendered discard hint size: " + originalSize + " -> " + effectiveSize);
            Assert.LessOrEqual(label.textBounds.size.x, label.rectTransform.rect.width + .1f);
            Assert.IsFalse(label.raycastTarget);
            var hud = Object.FindObjectsOfType<SingleBattleHud>().Single(item => item.gameObject.scene == view.gameObject.scene);
            Assert.IsFalse(ScreenArea(label.rectTransform).Overlaps(ScreenArea((RectTransform)hud.EndTurnButton.transform)),
                "The hint cannot cover End Turn.");
            yield return Capture("discard-block-hint-enlarged");
            long block = view.State.Duel.Combat.Get(player).Block;
            yield return ClickButton(view.Actions.Panel.DiscardButton); yield return Idle();
            Assert.AreEqual(block + 9, view.State.Duel.Combat.Get(player).Block);
            AssertRemotePresentation();
        }
    }
}
#endif
