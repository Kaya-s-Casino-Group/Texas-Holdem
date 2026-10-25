using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PokerEngine;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.SingleBattle.Tests
{
    public sealed class BattleResourceWidgetTests
    {
        [Test]
        public void GeneratedBloodIsBelowTheFrameAndTheNumber()
        {
            var root = new GameObject("Orb", typeof(RectTransform), typeof(Image));
            try
            {
                var label = new GameObject("Energy", typeof(RectTransform)); label.transform.SetParent(root.transform, false);
                BattleResourceWidgets.PresentActions(root.transform, 3);
                var liquid = BattleResourceWidgets.PresentActions(root.transform, 2);
                Assert.That(root.GetComponent<Image>().enabled, Is.False);
                Assert.That(liquid.FillAmount, Is.EqualTo(2f / 3).Within(.0001f));
                Assert.That(liquid.mainTexture, Is.SameAs(Resources.Load<Texture2D>("BattleConcept/Textures/action_blood_liquid")));
                Assert.That(liquid.mainTexture, Is.Not.SameAs(Texture2D.whiteTexture));
                CollectionAssert.AreEqual(new[] { "Reservoir Background", "Action Liquid", "Action Frame Overlay", "Energy" },
                    root.transform.Cast<Transform>().Select(t => t.name).ToArray());
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void DiscardPreviewShowsOnlyTheGainAndDoesNotCommit(int selectedCount)
        {
            var player = new ParticipantId("player");
            var engine = new CardDuelEngine(new BattleRules("preview", 10));
            var initial = engine.Create("preview", HmacDeckShuffler.FromSeed(new byte[32]), new[] {
                new BattlePlayerSetup(player, new CombatantDefinition("hero", 100, initialBlock: 25),
                    rules: new BattlePlayerRules(discardReward: new PerCardBlockRewardRule(6)))
            }, new[] { new BattleEnemySetup(new ParticipantId("enemy"), new CombatantDefinition("foe", 100), 1) });
            var state = engine.Execute(initial, initial.Revision, player, CardDuelAction.Start).State;
            var selected = state.Zone(CardZoneKinds.Hand, player).Cards.Take(selectedCount).ToArray();
            var preview = engine.PreviewDiscard(state, state.Revision, player, selected);
            Assert.That(preview.Accepted, Is.True);
            Assert.That(BattleResourceWidgets.DiscardShieldGain(preview, player), Is.EqualTo(selectedCount * 6));
            Assert.That(state.Duel.Combat.Get(player).Block, Is.EqualTo(25));
            var actual = engine.Execute(state, state.Revision, player, CardDuelAction.Discard, selected);
            Assert.That(BattleResourceWidgets.DiscardShieldGain(actual, player), Is.EqualTo(selectedCount * 6));
            Assert.That(BattleResourceWidgets.DiscardShieldGain(null, player), Is.Zero);
            Assert.That(BattleResourceWidgets.DiscardShieldGain(engine.PreviewDiscard(state, state.Revision, player,
                new CardInstanceId[0]), player), Is.Zero);
        }

        [TestCase(-1, 0f)]
        [TestCase(0, 0f)]
        [TestCase(1, 1f / 3)]
        [TestCase(2, 2f / 3)]
        [TestCase(3, 1f)]
        [TestCase(5, 1f)]
        public void ActionLevelClipsInsideTheCircle(int actions, float expected)
        {
            var root = new GameObject("Liquid", typeof(RectTransform), typeof(BattleActionPointLiquid));
            try
            {
                ((RectTransform)root.transform).sizeDelta = new Vector2(100, 100);
                var liquid = root.GetComponent<BattleActionPointLiquid>(); liquid.SetActions(actions);
                Assert.That(liquid.FillAmount, Is.EqualTo(expected).Within(.0001f));
                using (var mesh = new VertexHelper())
                {
                    typeof(BattleActionPointLiquid).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                        .Invoke(liquid, new object[] { mesh });
                    Assert.That(mesh.currentVertCount, Is.EqualTo(expected == 0 ? 0 : 97));
                    float highestLiquid = float.MinValue;
                    for (int i = 0; i < mesh.currentVertCount; i++)
                    {
                        var vertex = UIVertex.simpleVert; mesh.PopulateUIVertex(ref vertex, i);
                        Assert.That(((Vector2)vertex.position).magnitude, Is.LessThanOrEqualTo(50.01f));
                        highestLiquid = Mathf.Max(highestLiquid, vertex.position.y);
                    }
                    if (expected > 0) Assert.That(highestLiquid, Is.EqualTo(-50 + 100 * expected).Within(.01f));
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ShieldUpdatesTheCountAndKeepsAnEditedPosition()
        {
            var root = new GameObject("Discard", typeof(RectTransform));
            try
            {
                BattleResourceWidgets.PresentShield(root.transform, null, 12);
                var row = (RectTransform)root.transform.Find("Discard Shield Preview");
                row.anchoredPosition = new Vector2(13, -28);
                BattleResourceWidgets.PresentShield(root.transform, null, 0);
                Assert.That(row.anchoredPosition, Is.EqualTo(new Vector2(13, -28)));
                Assert.That(row.Find("Shield Count").GetComponent<TMP_Text>().text, Is.EqualTo("格挡+0"));
                Assert.That(row.Find("Shield Icon").GetComponent<Image>().sprite, Is.Not.Null);
                Assert.That(root.GetComponentsInChildren<TMP_Text>(true).Length, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
