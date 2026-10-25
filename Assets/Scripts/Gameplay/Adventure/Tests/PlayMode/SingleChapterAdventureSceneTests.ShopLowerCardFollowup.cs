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
        public IEnumerator LowerShopServiceCardAndIconOpenTheirPickerWithoutSpending()
        {
            yield return ReachBattle("battle-2");
            yield return WinBattle();
            yield return Click("继续旅程");
            yield return Click("商店");
            AssertPhase(AdventurePhase.Shop);
            var run = host.Adventure;
            var kinds = new[] { AdventureCommandKind.DuplicateCard, AdventureCommandKind.ChangeSuit, AdventureCommandKind.DeleteCard };
            var captions = new[] { "加牌", "改花色", "撕牌" };
            foreach (int gold in new[] { 1000, 0 })
            {
                Assert.IsTrue(run.ApplyAdmin(run.Revision, new BattleAdminPatch(gold: new[] {
                    new BattleAdminGold(run.PlayerId, gold) })).Accepted);
                screen.Refresh(); yield return null;
                for (int index = 0; index < kinds.Length; index++)
                {
                    var kind = kinds[index];
                    var tile = screen.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Service " + kind);
                    var priceButton = tile.GetComponentsInChildren<Button>().Single(button => button.transform != tile);
                    Assert.That(Vector2.Distance(new Vector2(.10f, .06f), ((RectTransform)priceButton.transform).anchorMin), Is.LessThan(.00001f));
                    Assert.That(Vector2.Distance(new Vector2(.90f, .23f), ((RectTransform)priceButton.transform).anchorMax), Is.LessThan(.00001f));
                    Assert.AreEqual(gold > 0, priceButton.interactable);
                    if (gold == 0)
                        Assert.AreEqual((Color)new Color32(232, 108, 102, 255), priceButton.GetComponentInChildren<TMP_Text>().color);

                    string state = run.Export();
                    yield return ClickLowerServiceSurface(tile, new Vector2(.13f, .80f));
                    Assert.IsTrue(VisibleText(captions[index] + " · 选择永久牌组中的一张牌"));
                    Assert.AreEqual(state, run.Export(), "The card surface may only open its existing picker.");
                    yield return Click("关闭"); AssertOverlayClosed();

                    var icon = tile.Find("Card Service").GetComponent<RectTransform>();
                    var iconCenter = tile.InverseTransformPoint(icon.TransformPoint(icon.rect.center));
                    yield return ClickLowerServiceSurface(tile, new Vector2(
                        (iconCenter.x - tile.rect.xMin) / tile.rect.width,
                        (iconCenter.y - tile.rect.yMin) / tile.rect.height));
                    Assert.IsTrue(VisibleText(captions[index] + " · 选择永久牌组中的一张牌"));
                    Assert.AreEqual(state, run.Export(), "The service icon may only open its existing picker.");
                    var card = screen.GetComponentsInChildren<Button>().First(button => button.transform.parent.name == "Cards");
                    yield return ClickButton(card);
                    Assert.AreEqual(state, run.Export(), "Choosing a permanent card must still wait for confirmation.");
                    if (gold == 0)
                    {
                        var confirmations = screen.GetComponentsInChildren<Button>()
                            .Where(button => button.transform.parent.name == "Dialog" && !button.GetComponentsInChildren<TMP_Text>().Any(text => text.text == "关闭")).ToArray();
                        Assert.IsNotEmpty(confirmations);
                        Assert.IsTrue(confirmations.All(button => !button.interactable));
                        Assert.IsFalse(run.Execute(run.Revision, new AdventureCommand(kind, run.Deck.First().Id, Suit.Hearts)).Accepted);
                        Assert.AreEqual(state, run.Export());
                    }
                    yield return Capture("shop-service-" + kind + (gold == 0 ? "-insufficient" : "-picker"));
                    yield return Click("关闭"); AssertOverlayClosed();
                    if (gold > 0)
                    {
                        yield return ClickButton(priceButton);
                        Assert.IsTrue(VisibleText(captions[index] + " · 选择永久牌组中的一张牌"));
                        Assert.AreEqual(state, run.Export(), "The existing price button keeps its selection-only behavior.");
                        yield return Click("关闭"); AssertOverlayClosed();
                    }
                }
            }
        }

        private IEnumerator ClickLowerServiceSurface(RectTransform tile, Vector2 normalizedPosition)
        {
            Canvas.ForceUpdateCanvases();
            var local = new Vector2(tile.rect.xMin + tile.rect.width * normalizedPosition.x,
                tile.rect.yMin + tile.rect.height * normalizedPosition.y);
            var pointer = new PointerEventData(EventSystem.current) {
                position = RectTransformUtility.WorldToScreenPoint(null, tile.TransformPoint(local)),
                button = PointerEventData.InputButton.Left, clickCount = 1 };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Assert.IsNotEmpty(hits, "The lower service card must be reachable by the UI raycaster.");
            var clickable = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.AreEqual(tile.gameObject, clickable, "Clicking a lower service card or its icon must reach that card's selection action.");
            ExecuteEvents.Execute(clickable, pointer, ExecuteEvents.pointerClickHandler);
            yield return null;
        }
    }
}
#endif
