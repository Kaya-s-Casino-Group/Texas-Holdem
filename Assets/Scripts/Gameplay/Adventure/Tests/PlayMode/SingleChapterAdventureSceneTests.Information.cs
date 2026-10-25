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
using UnityEngine.UI;

namespace Poker.Adventure.Tests
{
    public sealed partial class SingleChapterAdventureSceneTests
    {
        [UnityTest]
        public IEnumerator SevenStationInformationShowsScoringDiscardBlockAndCommittedComboWithoutChangingDamage()
        {
            yield return ReachBattle("battle-1");
            var view = host.BattleView; var player = host.Adventure.PlayerId; var enemy = view.Enemies.Single();
            Assert.IsTrue(view.Playback.ApplyAdmin(host.Revision, new BattleAdminPatch(
                vitals: new[] { new BattleAdminVitals(enemy.ParticipantId, health: 1000, maxHealth: 1000) },
                relics: new[] { new BattleAdminRelics(player, new BattleAdminRelic[0]) })).Accepted);
            yield return Idle();
            yield return InformationHand("standard:1:3", "standard:2:3", "standard:3:3", "standard:1:2", "standard:2:2");
            var outcome = Object.FindObjectOfType<BattleDemoOutcomePreview>();
            StringAssert.Contains("三条", outcome.OutcomeText); StringAssert.Contains("对子", outcome.OutcomeText);
            StringAssert.Contains("眩晕", outcome.OutcomeText); StringAssert.Contains("×", outcome.OutcomeText);
            var result = outcome.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(text => text.name == "Outcome Value");
            Assert.IsNotNull(result);
            yield return Capture("information-fullhouse");
            result.ForceMeshUpdate(); Assert.IsFalse(result.isTextOverflowing, "The authored preview rectangle must fit the new information: " + result.rectTransform.rect + " " + result.text);
            Assert.LessOrEqual(result.textBounds.size.x, result.rectTransform.rect.width + .01f);

            Assert.IsTrue(view.Playback.ApplyAdmin(host.Revision, new BattleAdminPatch(
                relics: new[] { new BattleAdminRelics(player, new[] { new BattleAdminRelic("13003") }) })).Accepted);
            yield return Idle();
            yield return InformationHand("standard:1:14", "standard:2:14", "standard:3:14", "standard:1:13", "standard:2:13");
            StringAssert.Contains("×8", outcome.OutcomeText);
            result.ForceMeshUpdate(); Assert.IsFalse(result.isTextOverflowing, "High-rank full house with a real multiplier relic must also fit.");
            Assert.LessOrEqual(result.textBounds.size.x, result.rectTransform.rect.width + .01f);
            yield return Capture("information-fullhouse-relic");
            Assert.IsTrue(view.Playback.ApplyAdmin(host.Revision, new BattleAdminPatch(
                relics: new[] { new BattleAdminRelics(player, new BattleAdminRelic[0]) })).Accepted);
            yield return Idle();

            yield return InformationHand("standard:1:7", "standard:2:7");
            var hand = view.Actions.Hand;
            foreach (var card in hand.Cards.Skip(1).ToArray()) hand.SetInteraction(card.Id, new CardInteractionState(isSelected: false));
            hand.Publish(); view.RefreshCandidate(); yield return null;
            var shield = Object.FindObjectsOfType<TMP_Text>().Single(text => text.name == "Shield Count");
            Assert.AreEqual("格挡+7", shield.text);
            long block = view.State.Duel.Combat.Get(player).Block;
            Assert.IsTrue(view.Actions.Panel.DiscardButton.interactable);
            yield return ClickButton(view.Actions.Panel.DiscardButton); yield return Idle();
            Assert.AreEqual(block + 7, view.State.Duel.Combat.Get(player).Block);

            yield return InformationHand("standard:1:2", "standard:2:3", "standard:3:4", "standard:4:5", "standard:1:6");
            StringAssert.Contains("中毒", outcome.OutcomeText); StringAssert.Contains("×", outcome.OutcomeText);
            long health = view.State.Duel.Combat.Get(enemy.ParticipantId).Health;
            bool sawCombo = false;
            Assert.IsTrue(view.Actions.Panel.PlayButton.interactable, "The real battle action must be enabled.");
            yield return ClickButton(view.Actions.Panel.PlayButton);
            float deadline = Time.realtimeSinceStartup + 20;
            do
            {
                if (!sawCombo && Object.FindObjectsOfType<TMP_Text>().Any(text => text.text.Contains("5段连击·总伤害60") && InformationTextIsRendered(text)))
                {
                    sawCombo = true;
                    yield return Capture("information-combo");
                }
                yield return null;
            } while ((view.IsPresenting || !sawCombo) && Time.realtimeSinceStartup < deadline);
            yield return Idle();
            Assert.IsTrue(sawCombo, "A real rendered committed combo summary must be readable.");
            Assert.AreEqual(health - 60, view.State.Duel.Combat.Get(enemy.ParticipantId).Health);
            Assert.AreEqual(5, view.LastPlayResolution.Events.Count(fact => fact.Effect.Kind == CombatEffectKind.Damage && fact.Effect.Source == player));
            Assert.AreEqual(5, view.State.Duel.Combat.Statuses.Single(status => status.DefinitionId.Value == "bleed").Stacks);
            AssertRemotePresentation();
        }

        [UnityTest]
        public IEnumerator SevenStationBossInformationTracksTwoImmunitiesThroughARealStraightAndTargetSwitch()
        {
            yield return ReachBattle("boss");
            var view = host.BattleView; var player = host.Adventure.PlayerId;
            var boss = WrathBossQuery.Read(view.State.Duel); var body = view.Enemies.Single(actor => actor.ParticipantId == boss.BodyId);
            // Keep the player decision windows available; the binding hand's actual severing/regen rules remain active.
            var binding = boss.Hands.Single(hand => hand.Role == WrathHandRole.Binding);
            Assert.IsTrue(view.Playback.ApplyAdmin(host.Revision, new BattleAdminPatch(vitals: new[] {
                new BattleAdminVitals(player, health: 10000, maxHealth: 10000), new BattleAdminVitals(binding.Id, health: 0) },
                relics: new[] { new BattleAdminRelics(player, new BattleAdminRelic[0]) })).Accepted);
            yield return Idle();
            for (int round = 0; round < 3; round++) yield return EndAdventureRound();
            Assert.AreEqual(2, view.State.Duel.Combat.Statuses.Single(status => status.DefinitionId.Value == "wrath-shell").Stacks);
            Assert.IsTrue(Object.FindObjectsOfType<TMP_Text>().Any(text => text.text == "免伤剩余2次"));
            yield return InformationHand("standard:1:14", "standard:2:2", "standard:3:3", "standard:4:4", "standard:1:5");
            var target = view.GetComponent<BattleDemoTargetSelection>();
            var blood = view.Enemies.First(actor => actor.ParticipantId != boss.BodyId && actor.IsAlive);
            Assert.IsTrue(target.ChooseTarget(blood)); yield return null;
            Assert.IsFalse(FindEnemyWorldHud(blood).GetComponentsInChildren<TMP_Text>().Any(text => text.text.Contains("目标")));
            Assert.IsTrue(blood.Visual.GetComponentsInChildren<MeshRenderer>(true).Any(renderer => renderer.name == "Selected enemy contour" && renderer.enabled));
            Assert.IsTrue(target.ChooseTarget(body)); yield return null;
            Assert.IsFalse(FindEnemyWorldHud(body).GetComponentsInChildren<TMP_Text>().Any(text => text.text.Contains("目标")));
            Assert.IsTrue(body.Visual.GetComponentsInChildren<MeshRenderer>(true).Any(renderer => renderer.name == "Selected enemy contour" && renderer.enabled));
            yield return Capture("information-boss-target-immunity");
            long health = view.State.Duel.Combat.Get(boss.BodyId).Health;
            bool sawOne = false, sawZero = false, sawImmune = false, sawSummary = false;
            Assert.IsTrue(view.Actions.Panel.PlayButton.interactable, "The real battle action must be enabled.");
            yield return ClickButton(view.Actions.Panel.PlayButton);
            float deadline = Time.realtimeSinceStartup + 25;
            do
            {
                var texts = Object.FindObjectsOfType<TMP_Text>().Select(text => text.text).ToArray();
                sawOne |= texts.Contains("免伤剩余1次"); sawZero |= texts.Contains("免伤剩余0次");
                sawImmune |= Object.FindObjectsOfType<TMP_Text>().Any(text => text.text == "免疫" && InformationTextIsRendered(text));
                sawSummary |= Object.FindObjectsOfType<TMP_Text>().Any(text => text.text == "5段连击·总伤害36" && InformationTextIsRendered(text));
                yield return null;
            } while ((view.IsPresenting || !sawSummary) && Time.realtimeSinceStartup < deadline);
            yield return Idle();
            // Zero-damage segments may settle within the same rendered frame. The final visible
            // count must be zero; a pair of separate real plays below also verifies visible one.
            Assert.IsTrue(sawZero, "The final rendered immunity count must be zero. one=" + sawOne + " immune=" + sawImmune + " summary=" + sawSummary);
            Assert.IsTrue(sawImmune && sawSummary, "Immunity and the committed combo sum must both be displayed.");
            var damage = view.LastPlayResolution.Events.Where(fact => fact.Effect.Kind == CombatEffectKind.Damage && fact.Effect.Source == player).ToArray();
            CollectionAssert.AreEqual(new long[] { 0, 0, 9, 12, 15 }, damage.Select(fact => fact.HealthLost).ToArray());
            Assert.AreEqual(health - 36, view.State.Duel.Combat.Get(boss.BodyId).Health);
            Assert.IsFalse(view.State.Duel.Combat.Statuses.Any(status => status.DefinitionId.Value == "wrath-shell"));
            yield return new WaitForSecondsRealtime(1.7f);
            Assert.IsFalse(Object.FindObjectsOfType<TMP_Text>().Any(text => text.text.Contains("段连击·总伤害")));

            // The next normal defense is on enemy round 7, regardless of the inserted ultimate.
            for (int round = 0; round < 4; round++) yield return EndAdventureRound();
            Assert.AreEqual(2, view.State.Duel.Combat.Statuses.Single(status => status.DefinitionId.Value == "wrath-shell").Stacks);
            yield return InformationHand("standard:1:2");
            Assert.IsTrue(target.ChooseTarget(body)); yield return null;
            yield return ClickButton(view.Actions.Panel.PlayButton); yield return Idle();
            Assert.AreEqual(1, view.State.Duel.Combat.Statuses.Single(status => status.DefinitionId.Value == "wrath-shell").Stacks);
            Assert.IsTrue(Object.FindObjectsOfType<TMP_Text>().Any(text => text.text == "免伤剩余1次"));
            yield return InformationHand("standard:2:3");
            Assert.IsTrue(target.ChooseTarget(body)); yield return null;
            yield return ClickButton(view.Actions.Panel.PlayButton);
            bool sawSeparateZero = false;
            deadline = Time.realtimeSinceStartup + 20;
            do
            {
                sawSeparateZero |= Object.FindObjectsOfType<TMP_Text>().Any(text => text.text == "免伤剩余0次");
                yield return null;
            } while (view.IsPresenting && Time.realtimeSinceStartup < deadline);
            Assert.IsTrue(sawSeparateZero);
            Assert.IsFalse(view.State.Duel.Combat.Statuses.Any(status => status.DefinitionId.Value == "wrath-shell"));
            AssertRemotePresentation();
        }

        [UnityTest]
        public IEnumerator SevenStationShopAffordabilityCanBeInspectedThroughItsIconWithoutSpending()
        {
            yield return ReachBattle("battle-2");
            Assert.IsTrue(host.BattleView.Playback.ApplyAdmin(host.Revision,
                new BattleAdminPatch(gold: new[] { new BattleAdminGold(host.Adventure.PlayerId, 0) })).Accepted);
            yield return Idle(); yield return WinBattle(); yield return Click("继续旅程"); yield return Click("商店");
            AssertPhase(AdventurePhase.Shop); Assert.AreEqual(0, host.Adventure.Gold);
            var item = host.Adventure.ShopItems.First();
            var tile = screen.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Stock 0");
            Assert.IsTrue(tile.GetComponentsInChildren<TMP_Text>().Any(text => text.text.Contains("金币不足")));
            yield return Capture("information-shop-insufficient");
            long revision = host.Revision;
            yield return ClickShopIcon(tile.Find("Relic " + item.RelicId).GetComponent<RectTransform>());
            var buy = screen.GetComponentsInChildren<Button>().Single(button => button.GetComponentsInChildren<TMP_Text>()
                .Any(text => text.text.Contains("金币不足") && text.text.Contains(item.Price.ToString())));
            Assert.IsFalse(buy.interactable);
            Assert.IsTrue(screen.GetComponentsInChildren<TMP_Text>().Any(text => text.isActiveAndEnabled && text.text.Contains("还差")));
            Assert.AreEqual(0, host.Adventure.Gold); Assert.AreEqual(revision, host.Revision);
            yield return Capture("information-shop-disabled-purchase");
            yield return Click("关闭"); yield return Click("离开商店 → 继续旅程"); AssertPhase(AdventurePhase.Map);
        }

        private IEnumerator InformationHand(params string[] cards)
        {
            var view = host.BattleView;
            Assert.IsTrue(view.Playback.ApplyAdmin(host.Revision, new BattleAdminPatch(zones: new[] {
                new BattleAdminZone(host.Adventure.PlayerId, CardZoneKinds.Hand, cards.Select(id => new BattleAdminCard(definitionId: id))) })).Accepted);
            yield return Idle();
            var hand = view.Actions.Hand;
            foreach (var card in hand.Cards.ToArray()) hand.SetInteraction(card.Id, new CardInteractionState(isSelected: true));
            hand.Publish(); hand.GetComponent<HandZoneView>().CompleteTransitions(); view.RefreshCandidate(); yield return null;
        }

        private static bool InformationTextIsRendered(TMP_Text text)
        {
            if (!text.isActiveAndEnabled || text.color.a < .75f ||
                text.GetComponentsInParent<CanvasGroup>().Any(group => group.alpha < .75f)) return false;
            text.ForceMeshUpdate();
            var canvas = text.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            bool visible = false;
            foreach (var glyph in text.textInfo.characterInfo.Take(text.textInfo.characterCount).Where(glyph => glyph.isVisible))
            {
                visible = true;
                foreach (var corner in new[] { glyph.bottomLeft, glyph.topRight })
                {
                    var point = RectTransformUtility.WorldToScreenPoint(camera, text.transform.TransformPoint(corner));
                    if (point.x < 0 || point.y < 0 || point.x > Screen.width || point.y > Screen.height) return false;
                }
            }
            return visible;
        }
    }
}
#endif
