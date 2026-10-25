#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
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
        public IEnumerator UnaffordableRelicCardsKeepQualityAndOpenDetailsWithoutSpending()
        {
            yield return ReachBattle("battle-2");
            yield return WinBattle();
            yield return Click("继续旅程");
            yield return Click("商店");
            AssertPhase(AdventurePhase.Shop);
            var run = host.Adventure;
            Assert.IsTrue(run.ApplyAdmin(run.Revision, new BattleAdminPatch(gold: new[] {
                new BattleAdminGold(run.PlayerId, 1000) })).Accepted);
            screen.Refresh(); yield return null;
            var qualities = run.ShopItems.Select((offer, slot) => screen.GetComponentsInChildren<RectTransform>()
                .Single(rect => rect.name == "Stock " + slot).GetComponentsInChildren<TMP_Text>()
                .Single(text => text.text.StartsWith("品质 "))).Select(text => new { text.text, text.color }).ToArray();

            Assert.IsTrue(run.ApplyAdmin(run.Revision, new BattleAdminPatch(gold: new[] {
                new BattleAdminGold(run.PlayerId, 11) })).Accepted);
            screen.Refresh(); yield return null;
            Assert.IsTrue(run.ShopItems.All(offer => offer.Price > 11), "The 11-gold fixture must be unable to afford each item.");
            for (int slot = 0; slot < run.ShopItems.Count; slot++)
            {
                var offer = run.ShopItems[slot];
                var tile = screen.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Stock " + slot);
                var quality = tile.GetComponentsInChildren<TMP_Text>().Single(text => text.text.StartsWith("品质 "));
                Assert.AreEqual(qualities[slot].text, quality.text);
                Assert.AreEqual(qualities[slot].color, quality.color, "Quality keeps its funded-state color.");
                var warning = tile.GetComponentsInChildren<TMP_Text>().Single(text => text.text == "金币不足 · " + offer.Price + " 金币");
                Assert.AreEqual((Color)new Color32(232, 108, 102, 255), warning.color);
                Assert.IsTrue(tile.GetComponent<Button>().interactable, "The entire card remains available for inspection.");
            }
            yield return Capture("shop-relic-insufficient-gold");
            var item = run.ShopItems.First();
            var firstTile = screen.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Stock 0");
            long revision = run.Revision; int owned = run.Relics.Count;
            yield return ClickShopIcon(firstTile.Find("Relic " + item.RelicId).GetComponent<RectTransform>());
            Assert.IsTrue(VisibleText("道具详情"));
            Assert.AreEqual(11, run.Gold); Assert.AreEqual(revision, run.Revision); Assert.AreEqual(owned, run.Relics.Count);
            var confirm = screen.GetComponentsInChildren<Button>().Single(button => button.GetComponentsInChildren<TMP_Text>()
                .Any(text => text.text == "金币不足 · 需 " + item.Price + " 金币"));
            Assert.IsFalse(confirm.interactable);
            yield return Capture("shop-relic-unaffordable-detail");
            yield return Click("关闭");
            AssertOverlayClosed(); Assert.AreEqual(11, run.Gold); Assert.AreEqual(revision, run.Revision);
        }
    }
}
#endif
