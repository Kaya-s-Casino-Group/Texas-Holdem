#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CardPresentation;
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
        public IEnumerator SevenStationPoisonBadgeCanBeHoveredThroughTheActualWorldHudRaycast()
        {
            yield return ReachBattle("elite");
            var view = host.BattleView; var enemy = view.Enemies.Single();
            Assert.IsTrue(view.Playback.ApplyAdmin(host.Revision, new BattleAdminPatch(
                vitals: new[] { new BattleAdminVitals(enemy.ParticipantId, health: 1000, maxHealth: 1000) },
                relics: new[] { new BattleAdminRelics(host.Adventure.PlayerId, new BattleAdminRelic[0]) })).Accepted);
            yield return Idle();
            yield return InformationHand("standard:1:2", "standard:2:3", "standard:3:4", "standard:4:5", "standard:1:6", "standard:2:9");
            var hand = view.Actions.Hand;
            var spare = hand.Cards.Single(card => view.State.Table.GetCard(new CardInstanceId(card.Id)).DefinitionId.Value == "standard:2:9");
            foreach (var card in hand.Cards.ToArray()) hand.SetInteraction(card.Id, new CardInteractionState(isSelected: false));
            foreach (var card in hand.Cards.Where(card => card.Id != spare.Id).ToArray())
                hand.SetInteraction(card.Id, new CardInteractionState(isSelected: true));
            hand.Publish(); hand.GetComponent<HandZoneView>().CompleteTransitions(); view.RefreshCandidate(); yield return null;
            Assert.AreEqual(5, hand.CaptureSelection().Cards.Count);
            Assert.IsFalse(hand.CaptureSelection().Cards.Contains(new CardInstanceId(spare.Id)));
            var preview = view.PreviewSelectedPlay(); Assert.IsNotNull(preview); Assert.IsTrue(preview.Accepted);
            Assert.AreEqual(HandCategory.Straight, preview.Score.Category, "The poison fixture must play 23456 and retain its spare 9.");
            yield return ClickButton(view.Actions.Panel.PlayButton); yield return Idle();
            Assert.AreEqual(1, hand.Cards.Count); Assert.AreEqual(spare.Id, hand.Cards.Single().Id);
            Assert.AreEqual(5, view.State.Duel.Combat.Statuses.Single(status => status.DefinitionId.Value == "bleed" && status.Target.Id == enemy.DisplayId).Stacks);
            // Let the existing damage/status visual tail finish before capturing the status explanation.
            yield return new WaitForSecondsRealtime(2);
            var hud = FindEnemyWorldHud(enemy);
            var badge = hud.Find("Statuses").GetComponentsInChildren<BattleDemoEnemyBadgeTooltip>().Single(item => item.Description.Contains("中毒"));
            AssertEnlargedEnemyStatus(badge, "Icons/icon_poison");
            var pointer = EnterEnemyBadgeThroughRaycast(badge);
            yield return null; Canvas.ForceUpdateCanvases();
            var popup = hud.Find("Enemy Badge Tooltip"); Assert.IsTrue(popup.gameObject.activeInHierarchy);
            var text = popup.GetComponentInChildren<TMP_Text>(); StringAssert.Contains("中毒", text.text);
            Assert.IsTrue(InformationTextIsRendered(text), "The poison explanation must be rendered after actual hover routing.");
            ExecuteEvents.Execute(badge.gameObject, pointer, ExecuteEvents.pointerExitHandler);
            Assert.IsFalse(popup.gameObject.activeSelf);
            pointer = EnterEnemyBadgeThroughRaycast(badge, hoverText: true); yield return null;
            Assert.IsTrue(popup.gameObject.activeInHierarchy); Assert.IsTrue(InformationTextIsRendered(text));
            yield return Capture("enemy-poison-badge-hover");
            ExecuteEvents.Execute(badge.gameObject, pointer, ExecuteEvents.pointerExitHandler);
            Assert.IsFalse(popup.gameObject.activeSelf);
            AssertRemotePresentation();
        }

        [UnityTest]
        public IEnumerator SevenStationEnemyStatusBadgesGrowWithTheirExistingArtAndRemainClearOfHealthAndIntents()
        {
            yield return ReachBattle("boss");
            var view = host.BattleView; var player = host.Adventure.PlayerId; var boss = WrathBossQuery.Read(view.State.Duel);
            var body = view.Enemies.Single(actor => actor.ParticipantId == boss.BodyId);
            var binding = boss.Hands.Single(hand => hand.Role == WrathHandRole.Binding);
            Assert.IsTrue(view.Playback.ApplyAdmin(host.Revision, new BattleAdminPatch(vitals: new[] {
                new BattleAdminVitals(player, health: 10000, maxHealth: 10000), new BattleAdminVitals(binding.Id, health: 0) },
                relics: new[] { new BattleAdminRelics(player, new BattleAdminRelic[0]) })).Accepted);
            yield return Idle();
            for (int round = 0; round < 3; round++) yield return EndAdventureRound();
            var hud = FindEnemyWorldHud(body); var row = hud.Find("Statuses").GetComponent<RectTransform>();
            var rowPosition = row.anchoredPosition; var rowAnchor = row.anchorMin;
            var badge = row.GetComponentsInChildren<BattleDemoEnemyBadgeTooltip>().Single(item => item.Description.Contains("免伤剩余2次"));
            AssertEnlargedEnemyStatus(badge, "Icons/icon_immunity");
            var pointer = EnterEnemyBadgeThroughRaycast(badge); yield return null;
            Assert.IsTrue(hud.Find("Enemy Badge Tooltip").gameObject.activeInHierarchy);
            StringAssert.Contains("仅保护暴怒本体", hud.Find("Enemy Badge Tooltip").GetComponentInChildren<TMP_Text>().text);
            Assert.AreEqual(rowPosition, row.anchoredPosition); Assert.AreEqual(rowAnchor, row.anchorMin);
            Assert.IsFalse(ScreenArea(badge.GetComponent<RectTransform>()).Overlaps(ScreenArea(hud.Find("Health Bar/Concept Track").GetComponent<RectTransform>())));
            foreach (var intent in hud.Find("Intents").GetComponentsInChildren<BattleDemoEnemyBadgeTooltip>())
                Assert.IsFalse(ScreenArea(badge.GetComponent<RectTransform>()).Overlaps(ScreenArea(intent.GetComponent<RectTransform>())));
            yield return Capture("enemy-immunity-badge-enlarged-hover");
            ExecuteEvents.Execute(badge.gameObject, pointer, ExecuteEvents.pointerExitHandler);
            AssertRemotePresentation();
        }

        [UnityTest]
        public IEnumerator SevenStationTargetSelectionKeepsItsGoldContourAndRulesWithOnlyHealthInTheBloodbar()
        {
            yield return ReachBattle("boss"); yield return InformationHand("standard:1:2");
            var view = host.BattleView; var boss = WrathBossQuery.Read(view.State.Duel);
            var selector = view.GetComponent<BattleDemoTargetSelection>();
            var hand = view.Enemies.First(actor => actor.ParticipantId != boss.BodyId && actor.IsAlive);
            var body = view.Enemies.Single(actor => actor.ParticipantId == boss.BodyId);
            foreach (var actor in new[] { hand, body })
            {
                Assert.IsTrue(selector.ChooseTarget(actor)); yield return null;
                var preview = view.PreviewSelectedPlay(); Assert.IsTrue(preview.Accepted);
                Assert.IsTrue(preview.Events.Any(fact => fact.Effect.Kind == CombatEffectKind.Damage &&
                    fact.Effect.Source == host.Adventure.PlayerId && fact.Effect.Target == actor.ParticipantId),
                    "The real preview must resolve damage against the selected actor.");
                var health = FindEnemyWorldHud(actor).Find("Health Bar/Health Value").GetComponent<TMP_Text>();
                StringAssert.DoesNotContain("目标", health.text); Assert.IsTrue(InformationTextIsRendered(health));
                var contour = actor.Visual.GetComponentsInChildren<MeshRenderer>(true).Single(renderer => renderer.name == "Selected enemy contour");
                Assert.IsTrue(contour.enabled); var properties = new MaterialPropertyBlock(); contour.GetPropertyBlock(properties);
                var gold = properties.GetColor("_OutlineColor"); Assert.Greater(gold.r, .95f); Assert.Greater(gold.g, .8f); Assert.Less(gold.b, .4f);
            }
            yield return Capture("enemy-selected-target-outline-with-health-only");
            AssertRemotePresentation();
        }

        private static PointerEventData EnterEnemyBadgeThroughRaycast(BattleDemoEnemyBadgeTooltip badge, bool hoverText = false)
        {
            Canvas.ForceUpdateCanvases();
            var canvas = badge.GetComponentInParent<Canvas>(); var raycaster = canvas.GetComponent<GraphicRaycaster>();
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode); Assert.IsNull(raycaster.eventCamera);
            var rect = badge.transform.Find(hoverText ? "Value" : "Concept Icon") as RectTransform ?? badge.GetComponent<RectTransform>();
            var point = RectTransformUtility.WorldToScreenPoint(raycaster.eventCamera, rect.TransformPoint(rect.rect.center));
            var pointer = new PointerEventData(EventSystem.current) { position = point }; var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.IsNotEmpty(hits, "No raycast at the displayed enemy badge icon " + point);
            var entered = ExecuteEvents.GetEventHandler<IPointerEnterHandler>(hits[0].gameObject);
            Assert.AreEqual(badge.gameObject, entered, "Enemy badge at " + point + " is blocked by " +
                string.Join(" | ", hits.Take(6).Select(hit => hit.gameObject.name + " order=" + hit.sortingOrder + " camera=" + (hit.module.eventCamera == null ? "overlay" : hit.module.eventCamera.name))));
            pointer.pointerCurrentRaycast = hits[0]; ExecuteEvents.Execute(entered, pointer, ExecuteEvents.pointerEnterHandler);
            return pointer;
        }

        private static void AssertEnlargedEnemyStatus(BattleDemoEnemyBadgeTooltip badge, string sprite)
        {
            var rect = badge.GetComponent<RectTransform>();
            Assert.GreaterOrEqual(rect.rect.width, 74 * 1.25f); Assert.GreaterOrEqual(rect.rect.height, 24 * 1.25f);
            var art = badge.GetComponentsInChildren<Image>().Single(image => image.name == "Concept Icon");
            Assert.AreSame(ConceptBattleSkin.Get(sprite), art.sprite); Assert.IsTrue(art.preserveAspect);
            var value = badge.GetComponentInChildren<TMP_Text>(); value.ForceMeshUpdate();
            // The full six-character immunity label previously auto-sized down to 8 within this slot.
            Assert.GreaterOrEqual(value.fontSizeMin, 8 * 1.25f); Assert.GreaterOrEqual(value.fontSizeMax, 12 * 1.25f);
            Assert.GreaterOrEqual(value.fontSize, 8 * 1.25f); Assert.IsTrue(InformationTextIsRendered(value));
        }
    }
}
#endif
