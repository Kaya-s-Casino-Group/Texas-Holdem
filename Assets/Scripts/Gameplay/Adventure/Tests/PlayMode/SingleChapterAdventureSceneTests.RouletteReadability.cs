#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Poker.SingleBattle;
using PokerEngine;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Poker.Adventure.Tests
{
    public sealed partial class SingleChapterAdventureSceneTests
    {
        [UnityTest]
        public IEnumerator SevenStationRouletteRequiresActiveUseAndRendersItsActualGrowingMultiplier()
        {
            bool safeFixture = false;
            for (ulong seed = 312; seed < 332; seed++)
            {
                Assert.IsTrue(host.NewRun(host.AvailableTarots.First().Id, seed), host.Message);
                Assert.IsTrue(host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.EnterNode, "battle-1")), host.Message);
                yield return Idle();
                var candidateView = host.BattleView; var candidatePlayer = host.Adventure.PlayerId;
                Assert.IsTrue(candidateView.Playback.ApplyAdmin(host.Revision, new BattleAdminPatch(
                    vitals: new[] { new BattleAdminVitals(candidatePlayer, health: 10000, maxHealth: 10000) },
                    relics: new[] { new BattleAdminRelics(candidatePlayer, new[] {
                        new BattleAdminRelic("15001"), new BattleAdminRelic("13004") }) })).Accepted);
                yield return Idle();
                var beforePreview = candidateView.AuthorityState;
                var candidateItem = candidateView.State.Duel.Player(candidatePlayer).Relics.Relics.Single(relic => relic.DefinitionId == "15001");
                var candidatePreview = candidateView.PreviewItem(candidateItem.InstanceId);
                Assert.IsNotNull(candidatePreview); Assert.IsTrue(candidatePreview.Accepted);
                long bomb = candidatePreview.State.Duel.Player(candidatePlayer).Relics.Relics
                    .Single(relic => relic.InstanceId == candidateItem.InstanceId).Memory.CounterValue(BattleItemRuntime.RouletteBombKey);
                Assert.AreSame(beforePreview, candidateView.AuthorityState, "Fixture selection must use only a read-only item preview.");
                TestContext.WriteLine("Roulette readability fixture: seed=" + seed + ", bomb=" + bomb + (bomb >= 3 ? " (selected)" : " (skipped)"));
                if (bomb >= 3) { safeFixture = true; break; }
                Assert.IsTrue(host.Abandon(), host.Message); yield return Idle();
            }
            Assert.IsTrue(safeFixture, "No fixture among seeds 312..331 survived both multiplier stages; the production random rules remain unchanged.");
            var view = host.BattleView; var player = host.Adventure.PlayerId;
            var icon = Object.FindObjectsOfType<Image>().Single(image => image.name == "俄罗斯转盘" &&
                image.GetComponent<BattleDemoRelicHoverTarget>() != null);
            var item = view.State.Duel.Player(player).Relics.Relics.Single(relic => relic.DefinitionId == "15001");
            var inactive = view.AuthorityState;
            int actions = view.State.FlowFor(player).ActionsRemaining;
            var cards = view.State.Zone(CardZoneKinds.Hand, player).Cards.ToArray();
            AssertRenderedRoulette(icon, null);
            Assert.AreEqual(0, item.Memory.CounterValue(BattleItemRuntime.RouletteActiveKey));
            yield return ClickRouletteIcon(icon);
            var availability = RouletteAvailability();
            StringAssert.Contains("主动道具", availability.text); StringAssert.Contains("点击使用", availability.text);
            Assert.IsTrue(InformationTextIsRendered(availability));
            Assert.AreSame(inactive, view.AuthorityState, "Opening the real item panel must not activate it or consume AP.");
            Assert.AreEqual(actions, view.State.FlowFor(player).ActionsRemaining);
            yield return Capture("roulette-inactive");

            // This deterministic run must survive two multiplier stages. Preview is read-only;
            // the real visible Use Item button below performs the actual activation.
            var preview = view.PreviewItem(item.InstanceId);
            Assert.IsNotNull(preview); Assert.IsTrue(preview.Accepted);
            var previewItem = preview.State.Duel.Player(player).Relics.Relics.Single(relic => relic.InstanceId == item.InstanceId);
            Assert.GreaterOrEqual(previewItem.Memory.CounterValue(BattleItemRuntime.RouletteBombKey), 3,
                "Choose a deterministic fixture seed with at least two safe stages; never change the roulette's random rules.");
            Assert.AreSame(inactive, view.AuthorityState);
            var use = Object.FindObjectsOfType<Button>().Single(button => button.name == "Use Item" && button.isActiveAndEnabled);
            Assert.IsTrue(use.interactable);
            yield return ClickButton(use); yield return Idle();
            Assert.AreEqual(10, BattleItemRuntime.HandMultiplier(view.State.Duel.Player(player).Relics));
            Assert.AreEqual(actions, view.State.FlowFor(player).ActionsRemaining);
            CollectionAssert.AreEqual(cards, view.State.Zone(CardZoneKinds.Hand, player).Cards);
            AssertRenderedRoulette(icon, "×10");
            StringAssert.Contains("当前牌型伤害 ×10", RouletteAvailability().text);
            Assert.IsFalse(use.interactable, "An active roulette cannot be activated a second time.");
            yield return Capture("roulette-active");

            yield return EndAdventureRound();
            Assert.AreEqual(100, BattleItemRuntime.HandMultiplier(view.State.Duel.Player(player).Relics));
            AssertRenderedRoulette(icon, "×100");
            StringAssert.Contains("当前牌型伤害 ×100", RouletteAvailability().text);
            yield return Capture("roulette-next-turn");
            AssertRemotePresentation();
        }

        private static TMP_Text RouletteAvailability() => Object.FindObjectsOfType<TMP_Text>()
            .Single(text => text.name == "Availability" && text.isActiveAndEnabled);

        private static void AssertRenderedRoulette(Image icon, string multiplier)
        {
            var labels = icon.GetComponentsInChildren<TMP_Text>();
            var active = labels.Single(label => label.text == "主动");
            Assert.IsTrue(InformationTextIsRendered(active)); Assert.IsFalse(active.raycastTarget);
            var values = labels.Where(label => label.text.StartsWith("×", System.StringComparison.Ordinal)).ToArray();
            if (multiplier == null) Assert.IsEmpty(values, "Owning the item must not imply that its multiplier is active.");
            else
            {
                Assert.AreEqual(multiplier, values.Single().text);
                Assert.IsTrue(InformationTextIsRendered(values.Single())); Assert.IsFalse(values.Single().raycastTarget);
            }
        }

        private IEnumerator ClickRouletteIcon(Image icon)
        {
            Canvas.ForceUpdateCanvases();
            var point = RectTransformUtility.WorldToScreenPoint(null, icon.rectTransform.TransformPoint(icon.rectTransform.rect.center));
            var pointer = new PointerEventData(EventSystem.current)
                { position = point, button = PointerEventData.InputButton.Left, clickCount = 1 };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Assert.IsNotEmpty(hits, "The actual item icon must be reachable by the scene's UI raycaster.");
            var clickable = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.IsNotNull(clickable);
            Assert.IsNotNull(clickable.GetComponent<BattleDemoRelicHoverTarget>(), "The item cue cannot intercept its use-panel click.");
            ExecuteEvents.Execute(clickable, pointer, ExecuteEvents.pointerClickHandler);
            yield return null;
        }
    }
}
#endif
