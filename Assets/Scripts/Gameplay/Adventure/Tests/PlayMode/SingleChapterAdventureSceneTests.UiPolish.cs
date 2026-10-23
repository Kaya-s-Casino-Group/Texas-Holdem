#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
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
        public IEnumerator OnlyUnlockedTarotIsReadyBeforeStartingJourney()
        {
            yield return Click("开始新旅程");
            Assert.IsNull(host.Adventure, "Viewing the preparation page must not start or save a run.");
            Assert.IsTrue(screen.GetComponentsInChildren<TMP_Text>().Any(text =>
                text.isActiveAndEnabled && text.text.Contains("120") && text.text.Contains("144")),
                "The Fool's resulting health must be visible before starting the run.");
            yield return Capture("journey-preparation");
            yield return Click("踏上旅程");
            AssertPhase(AdventurePhase.Map);
            Assert.AreEqual("fool", host.Adventure.TarotId);
            Assert.AreEqual(144, host.Adventure.Health);
            Assert.AreEqual(144, host.Adventure.MaxHealth);
        }

        [UnityTest]
        public IEnumerator ShopIconPreviewsWithoutSpendingAndConfirmedPurchaseCanContinueJourney()
        {
            yield return ReachBattle("battle-2");
            Assert.IsTrue(host.BattleView.Playback.ApplyAdmin(host.Revision,
                new BattleAdminPatch(gold: new[] { new BattleAdminGold(host.Adventure.PlayerId, 10000) })).Accepted);
            yield return Idle();
            yield return WinBattle();
            yield return Click("继续旅程");
            yield return Click("商店");
            AssertPhase(AdventurePhase.Shop);

            var item = host.Adventure.ShopItems.First(offer => host.Adventure.Execute(host.Revision,
                new AdventureCommand(AdventureCommandKind.BuyRelic, offer.Id), preview: true).Accepted);
            int slot = host.Adventure.ShopItems.ToList().IndexOf(item);
            var tile = screen.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Stock " + slot);
            var icon = tile.Find("Relic " + item.RelicId).GetComponent<RectTransform>();
            long gold = host.Adventure.Gold, revision = host.Revision;
            int owned = host.Adventure.Relics.Count;
            yield return ClickShopIcon(icon);
            Assert.IsTrue(VisibleText("道具详情"));
            Assert.AreEqual(gold, host.Adventure.Gold, "Opening an item from its icon must not charge gold.");
            Assert.AreEqual(revision, host.Revision);
            Assert.AreEqual(owned, host.Adventure.Relics.Count);
            yield return Capture("shop-icon-detail");
            yield return Click("关闭");
            AssertOverlayClosed();
            Assert.AreEqual(gold, host.Adventure.Gold);
            tile = screen.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Stock " + slot);
            icon = tile.Find("Relic " + item.RelicId).GetComponent<RectTransform>();
            yield return ClickShopIcon(icon);
            yield return Click("确认购买 · " + item.Price + " 金币");
            Assert.AreEqual(gold - item.Price, host.Adventure.Gold);
            Assert.AreEqual(owned + 1, host.Adventure.Relics.Count);
            Assert.IsTrue(host.Adventure.PurchasedItemIds.Contains(item.Id));
            yield return Capture("shop-clear-navigation");
            yield return Click("离开商店 → 继续旅程");
            AssertPhase(AdventurePhase.Map);
            Assert.IsFalse(host.IsAtMenu, "Leaving the shop continues the journey instead of returning home.");
            Assert.IsTrue(host.Adventure.AvailableNodes.Any(node => node.Kind == AdventureNodeKind.Elite));
        }

        [UnityTest]
        public IEnumerator RestRiskLotteryOnlyAppliesOutcomeOnce()
        {
            yield return ReachBattle("battle-2");
            yield return WinBattle();
            yield return Click("继续旅程");
            yield return Click("休息");
            AssertPhase(AdventurePhase.Rest);
            yield return Capture("rest-lottery-risk");
            long before = host.Adventure.Gold;
            yield return Click("免费抽奖 · 有风险");
            Assert.IsTrue(host.Adventure.RestLotteryUsed);
            Assert.IsFalse(host.Adventure.CanDrawRestReward);
            Assert.AreEqual(before + host.Adventure.LastLocationReward.GoldDelta, host.Adventure.Gold,
                "A free draw must apply only the announced outcome, without a separate entry fee.");
            Assert.GreaterOrEqual(host.Adventure.Gold, 0);
            long settledGold = host.Adventure.Gold, revision = host.Revision;
            Assert.IsFalse(host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.DrawRestReward)),
                "The authority must reject a second draw even if an old UI event is repeated.");
            Assert.AreEqual(settledGold, host.Adventure.Gold);
            Assert.AreEqual(revision, host.Revision);
        }

        private IEnumerator ClickShopIcon(RectTransform icon)
        {
            Canvas.ForceUpdateCanvases();
            var point = RectTransformUtility.WorldToScreenPoint(null, icon.TransformPoint(icon.rect.center));
            var pointer = new PointerEventData(EventSystem.current)
                { position = point, button = PointerEventData.InputButton.Left, clickCount = 1 };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.IsNotEmpty(hits);
            var clickable = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.IsNotNull(clickable, "The visible shop item icon must open its details.");
            var button = clickable.GetComponent<Button>();
            Assert.IsNotNull(button, "The shop icon must route to the item detail button.");
            Assert.IsTrue(button.interactable);
            ExecuteEvents.Execute(clickable, pointer, ExecuteEvents.pointerClickHandler);
            yield return null;
        }
    }
}
#endif
