using System.Linq;
using CardInteraction;
using CardPresentation.Editor;
using NUnit.Framework;
using PokerEngine;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CardPresentation.Tests
{
    [TestFixture]
    public sealed class DeckSummaryTests
    {
        [Test] public void SummaryCountsCopiesRulesAndHiddenCardsWithoutInferringOrCountingDrawnCards()
        {
            DeckSummarySetup.Prepare();
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(DeckSummarySetup.PrefabPath));
            try
            {
                var view = root.GetComponent<DeckSummaryView>();
                DeckOverviewCard Entry(string id, Suit suit, Rank rank, bool remaining, CardEffects effects = default, bool disabled = false)
                {
                    var definition = StandardDeck.Cards().Single(card => card.Suit == suit && card.Rank == rank);
                    var card = new CardInstance(new CardInstanceId(id),definition.Id,effects:effects,isDisabled:disabled);
                    return new DeckOverviewCard(new CardZoneCardState(card,"clubs_02",true,Color.white,default).Project(),definition,remaining);
                }
                var data = new DeckOverviewData(new[] {
                    Entry("king1",Suit.Hearts,Rank.King,true,CardEffects.Ineffective),
                    Entry("king2",Suit.Hearts,Rank.King,true,disabled:true),
                    Entry("drawn",Suit.Spades,Rank.Ace,false),
                    Entry("two",Suit.Clubs,Rank.Two,true),
                    new DeckOverviewCard(new CardVisualData("hidden",null),null,true)
                });
                view.Present(data); view.Show(false);
                Assert.AreEqual("2",view.Cell(1,1).text); Assert.AreEqual("2",view.RankTotal(1).text);
                Assert.AreEqual("2",view.SuitTotal(1).text); Assert.AreEqual("1",view.Cell(2,12).text);
                Assert.AreEqual("0",view.Cell(0,0).text); Assert.Less(view.Cell(0,0).color.a,view.Cell(1,1).color.a);
                Assert.AreEqual(1,data.OtherRemainingCount); StringAssert.Contains("1",view.Other.text);
                Assert.AreEqual(4,data.RemainingCount); Assert.AreSame(data,view.Data);
                Assert.IsTrue(root.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget));
                Assert.IsTrue(root.GetComponentsInChildren<PixelPanelGraphic>(true).All(panel => panel.GetComponent<CanvasRenderer>() != null));
                Assert.IsFalse(root.GetComponent<CanvasGroup>().blocksRaycasts);
                view.Hide(false); Assert.IsFalse(view.IsOpen); Assert.IsFalse(root.activeSelf);
            }
            finally { Object.DestroyImmediate(root); }
        }

    }
}
