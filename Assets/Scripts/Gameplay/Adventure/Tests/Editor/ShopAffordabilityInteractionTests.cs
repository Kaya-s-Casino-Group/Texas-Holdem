using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using NUnit.Framework;
using PokerEngine;
using UnityEngine;

namespace Poker.Adventure.Tests
{
    public sealed class ShopAffordabilityInteractionTests
    {
        [Test]
        public void UnaffordableOfferRemainsInspectableAndExplainsWhyPurchaseIsBlocked()
        {
            using (var fixture = new Fixture(0))
            {
                var run = fixture.Host.Adventure;
                var item = run.ShopItems.First();
                var tile = fixture.Screen.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Stock 0");
                Assert.That(Texts(tile), Has.Some.Contains("金币不足"), "The shelf must distinguish affordability from inspectability.");
                var button = tile.GetComponent("Button");
                Assert.IsTrue((bool)button.GetType().GetProperty("interactable").GetValue(button));
                long revision = run.Revision;
                InvokeClick(button);
                var confirm = fixture.Screen.GetComponentsInChildren<Component>(true).Where(c => c != null && c.GetType().Name == "Button")
                    .Single(c => Texts(c.transform).Contains("金币不足 · 需 " + item.Price + " 金币"));
                Assert.IsFalse((bool)confirm.GetType().GetProperty("interactable").GetValue(confirm));
                Assert.That(Texts(fixture.Screen.transform), Has.Some.Contains("还差"));
                Assert.AreEqual(0, run.Gold); Assert.AreEqual(revision, run.Revision);
                Assert.IsEmpty(run.PurchasedItemIds);
                Assert.IsFalse(run.Execute(revision, new AdventureCommand(AdventureCommandKind.BuyRelic, item.Id)).Accepted);
                Assert.AreEqual(0, run.Gold); Assert.AreEqual(revision, run.Revision);
            }
        }

        [Test]
        public void AffordableOfferKeepsNormalConfirmationAndChargesExactlyOnce()
        {
            using (var fixture = new Fixture(1000))
            {
                var run = fixture.Host.Adventure; var item = run.ShopItems.First();
                var tile = fixture.Screen.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Stock 0");
                Assert.That(Texts(tile).Any(t => t.Contains("金币不足")), Is.False);
                InvokeClick(tile.GetComponent("Button"));
                var confirm = fixture.Screen.GetComponentsInChildren<Component>(true).Where(c => c != null && c.GetType().Name == "Button")
                    .Single(c => Texts(c.transform).Contains("确认购买 · " + item.Price + " 金币"));
                Assert.IsTrue((bool)confirm.GetType().GetProperty("interactable").GetValue(confirm));
                long revision = run.Revision;
                InvokeClick(confirm);
                Assert.AreEqual(1000 - item.Price, fixture.Host.Adventure.Gold);
                Assert.That(fixture.Host.Adventure.PurchasedItemIds, Does.Contain(item.Id));
                Assert.IsFalse(fixture.Host.Adventure.Execute(revision, new AdventureCommand(AdventureCommandKind.BuyRelic, item.Id)).Accepted);
                Assert.AreEqual(1000 - item.Price, fixture.Host.Adventure.Gold);
            }
        }

        [Test]
        public void ChangingGoldMovesTheRedWarningToTheDetailButtonWhileQualityAndInspectionStayAvailable()
        {
            using (var fixture = new Fixture(1000))
            {
                var run = fixture.Host.Adventure; var item = run.ShopItems.First();
                Func<Transform> tile = () => fixture.Screen.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Stock 0");
                var quality = TextComponents(tile()).Single(c => Text(c).StartsWith("品质 "));
                string originalQuality = Text(quality); Color originalQualityColor = TextColor(quality);
                var affordableLabel = TextComponents(tile()).Single(c => Text(c) == "查看详情 · " + item.Price + " 金币");
                Color originalButtonColor = TextColor(affordableLabel);

                Assert.IsTrue(run.ApplyAdmin(run.Revision, new BattleAdminPatch(gold: new[] {
                    new BattleAdminGold(run.PlayerId, 0) })).Accepted);
                fixture.Screen.Refresh();
                quality = TextComponents(tile()).Single(c => Text(c).StartsWith("品质 "));
                Assert.AreEqual(originalQuality, Text(quality), "Insufficient gold must not change the quality/risk description.");
                Assert.AreEqual(originalQualityColor, TextColor(quality));
                var warning = TextComponents(tile()).Single(c => Text(c) == "金币不足 · " + item.Price + " 金币");
                Assert.AreEqual((Color)new Color32(232, 108, 102, 255), TextColor(warning), "Both the caption and price must be red.");
                Assert.IsTrue((bool)tile().GetComponent("Button").GetType().GetProperty("interactable").GetValue(tile().GetComponent("Button")));
                long revision = run.Revision; int owned = run.Relics.Count;
                InvokeClick(tile().GetComponent("Button"));
                Assert.That(Texts(fixture.Screen.transform), Has.Some.EqualTo("道具详情"));
                Assert.AreEqual(0, run.Gold); Assert.AreEqual(revision, run.Revision); Assert.AreEqual(owned, run.Relics.Count);
                var confirm = fixture.Screen.GetComponentsInChildren<Component>(true).Where(c => c != null && c.GetType().Name == "Button")
                    .Single(c => Texts(c.transform).Contains("金币不足 · 需 " + item.Price + " 金币"));
                Assert.IsFalse((bool)confirm.GetType().GetProperty("interactable").GetValue(confirm));
                var close = fixture.Screen.GetComponentsInChildren<Component>(true).Where(c => c != null && c.GetType().Name == "Button")
                    .Single(c => Texts(c.transform).Contains("关闭"));
                InvokeClick(close);

                Assert.IsTrue(run.ApplyAdmin(run.Revision, new BattleAdminPatch(gold: new[] {
                    new BattleAdminGold(run.PlayerId, 1000) })).Accepted);
                fixture.Screen.Refresh();
                Assert.AreEqual(originalQuality, Text(TextComponents(tile()).Single(c => Text(c).StartsWith("品质 "))));
                Assert.AreEqual(originalButtonColor, TextColor(TextComponents(tile()).Single(c => Text(c) == "查看详情 · " + item.Price + " 金币")));
                Assert.IsFalse(Texts(tile()).Any(t => t.Contains("金币不足")), "Funding the same shop must remove the warning.");
            }
        }

        [TestCase(AdventureCommandKind.DuplicateCard, "加牌", 1000)]
        [TestCase(AdventureCommandKind.ChangeSuit, "改花色", 1000)]
        [TestCase(AdventureCommandKind.DeleteCard, "撕牌", 1000)]
        [TestCase(AdventureCommandKind.DuplicateCard, "加牌", 0)]
        [TestCase(AdventureCommandKind.ChangeSuit, "改花色", 0)]
        [TestCase(AdventureCommandKind.DeleteCard, "撕牌", 0)]
        public void LowerServiceCardsOpenSelectionWithoutApplyingOrCharging(AdventureCommandKind kind, string caption, int gold)
        {
            using (var fixture = new Fixture(gold))
            {
                var run = fixture.Host.Adventure;
                var tile = fixture.Screen.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Service " + kind);
                var priceButton = tile.GetComponentsInChildren<Component>(true)
                    .Single(c => c != null && c.GetType().Name == "Button" && c.transform != tile);
                var priceRect = (RectTransform)priceButton.transform;
                Assert.That(Vector2.Distance(new Vector2(.10f, .06f), priceRect.anchorMin), Is.LessThan(.00001f));
                Assert.That(Vector2.Distance(new Vector2(.90f, .23f), priceRect.anchorMax), Is.LessThan(.00001f));
                Assert.AreEqual(gold > 0, (bool)priceButton.GetType().GetProperty("interactable").GetValue(priceButton));
                if (gold == 0)
                    Assert.AreEqual((Color)new Color32(232, 108, 102, 255), TextColor(TextComponents(priceButton.transform).Single()));

                string state = run.Export();
                var tileButton = tile.GetComponent("Button");
                Assert.IsNotNull(tileButton, "The lower service card must be inspectable outside its price button.");
                Assert.IsTrue((bool)tileButton.GetType().GetProperty("interactable").GetValue(tileButton));
                InvokeClick(tileButton);
                Assert.That(Texts(fixture.Screen.transform), Has.Some.EqualTo(caption + " · 选择永久牌组中的一张牌"));
                Assert.AreEqual(state, run.Export(), "Opening the permanent-deck picker must not execute the service or charge gold.");

                var card = fixture.Screen.GetComponentsInChildren<Component>(true)
                    .First(c => c != null && c.GetType().Name == "Button" && c.transform.parent.name == "Cards");
                InvokeClick(card);
                Assert.That(Texts(fixture.Screen.transform), Has.Some.Contains("已选"));
                Assert.AreEqual(state, run.Export(), "Selecting a card still requires explicit confirmation before any run change.");
                if (gold == 0)
                {
                    var confirm = fixture.Screen.GetComponentsInChildren<Component>(true)
                        .Where(c => c != null && c.GetType().Name == "Button" && c.transform.parent.name == "Dialog" && !Texts(c.transform).Contains("关闭"))
                        .ToArray();
                    Assert.IsNotEmpty(confirm);
                    Assert.IsTrue(confirm.All(c => !(bool)c.GetType().GetProperty("interactable").GetValue(c)),
                        "Insufficient gold must keep every service confirmation disabled even after a card is selected.");
                    Assert.IsFalse(run.Execute(run.Revision, new AdventureCommand(kind, run.Deck.First().Id, Suit.Hearts)).Accepted);
                    Assert.AreEqual(state, run.Export());
                }
            }
        }

        private static Component[] TextComponents(Transform parent) => parent.GetComponentsInChildren<Component>(true)
            .Where(c => c != null && c.GetType().GetProperty("text")?.PropertyType == typeof(string)).ToArray();
        private static string Text(Component component) => (string)component.GetType().GetProperty("text").GetValue(component);
        private static Color TextColor(Component component) => (Color)component.GetType().GetProperty("color").GetValue(component);

        private static string[] Texts(Transform parent) => parent.GetComponentsInChildren<Component>(true)
            .Where(c => c != null && c.GetType().GetProperty("text")?.PropertyType == typeof(string))
            .Select(c => (string)c.GetType().GetProperty("text").GetValue(c)).ToArray();
        private static void InvokeClick(Component button)
        {
            var click = button.GetType().GetProperty("onClick").GetValue(button);
            click.GetType().GetMethod("Invoke", Type.EmptyTypes).Invoke(click, null);
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root, ui, view;
            private readonly TextAsset battle, adventure;
            public readonly AdventureSceneHost Host;
            public readonly AdventureScreen Screen;
            public Fixture(int gold)
            {
                var xml = XDocument.Load(Path.Combine(Application.dataPath, "../External/BattleToolkit/Engine/Content/StandardSingleBattle.xml"));
                foreach (var enemy in xml.Root.Element("Enemies").Elements())
                { enemy.SetAttributeValue("maxHealth", 1); enemy.SetAttributeValue("damage", 0); enemy.RemoveNodes(); }
                battle = new TextAsset(xml.ToString());
                adventure = new TextAsset("<AdventureContent schemaVersion='1' id='shop-ui-test' version='1' startNode='fight' " +
                    "startingEncounter='standard-single-battle' defaultTarot='fool' startingGold='" + gold + "' economyVersion='2' shopOfferCount='4'>" +
                    "<Tarots><Tarot id='fool' initiallyUnlocked='true'/></Tarots><Nodes>" +
                    "<Node id='fight' kind='Battle' chapter='1' encounter='standard-single-battle' next='shop'/>" +
                    "<Node id='shop' kind='Shop' chapter='1' next='boss'/>" +
                    "<Node id='boss' kind='Boss' chapter='1' encounter='standard-gluttony-boss'/>" +
                    "</Nodes><Shop/><Services><Service kind='DeleteCard' price='45'/>" +
                    "<Service kind='DuplicateCard' price='20'/><Service kind='ChangeSuit' price='30'/></Services><Rest/></AdventureContent>");
                root = new GameObject("Shop affordability host");
                Host = root.AddComponent<AdventureSceneHost>(); Host.Configure(battle, adventure, autoSave: false); Host.Initialize();
                Assert.IsTrue(Host.NewRun("fool", 42), Host.Message);
                Assert.IsTrue(Host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.EnterNode, "fight")), Host.Message);
                var player = Host.Adventure.PlayerId;
                if (Host.Session.State.FlowFor(player).Window == BattlePlayerWindow.Draw)
                    Assert.IsTrue(Host.Execute(Host.Revision, player, CardDuelAction.SkipDraw).Accepted);
                Assert.IsTrue(Host.Execute(Host.Revision, player, CardDuelAction.Play,
                    Host.Session.State.Zone(CardZoneKinds.Hand, player).Cards.Take(1)).Accepted);
                Assert.AreEqual(AdventurePhase.Reward, Host.Adventure.Phase);
                Assert.IsTrue(Host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.Continue)), Host.Message);
                Assert.IsTrue(Host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.EnterNode, "shop")), Host.Message);
                // This fixture runs economy headlessly; the shop still uses the real view's optional art lookup.
                view = new GameObject("Shop art lookup"); view.SetActive(false);
                var adapter = view.AddComponent(typeof(AdventureSceneHost).GetProperty("BattleView").PropertyType);
                typeof(AdventureSceneHost).GetField("battleView", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Host, adapter);
                ui = new GameObject("Shop affordability UI"); Screen = ui.AddComponent<AdventureScreen>();
                typeof(AdventureScreen).GetMethod("Configure").Invoke(Screen, new object[] { Host, null }); Screen.Refresh();
            }
            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(ui); UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(view);
                UnityEngine.Object.DestroyImmediate(battle); UnityEngine.Object.DestroyImmediate(adventure);
            }
        }
    }
}
