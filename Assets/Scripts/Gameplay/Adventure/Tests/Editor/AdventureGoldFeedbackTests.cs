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
    public sealed class AdventureGoldFeedbackTests
    {
        [Test]
        public void BattleWinNamesTheCommittedRewardAndBalanceWithoutPayingAgainOnRefresh()
        {
            using (var f = new Fixture("fight"))
            {
                f.Enter("fight"); f.Win(); f.ShowScreen();
                Assert.AreEqual(50, f.Host.Adventure.Gold);
                Assert.That(Texts(f.Screen.transform), Has.Some.Contains("战斗奖励"));
                Assert.That(Texts(f.Screen.transform), Has.Some.Contains("+20"));
                Assert.That(Texts(f.Screen.transform), Has.Some.Contains("余额 50"));
                AssertRefreshDoesNotChangeRun(f);
            }
        }

        [Test]
        public void ChestNamesItsAuthoritativeGoldAwardAndDoesNotRepeatItWhenRefreshed()
        {
            using (var f = new Fixture("chest"))
            {
                f.Enter("chest"); f.ShowScreen();
                Assert.AreEqual(130, f.Host.Adventure.Gold);
                Assert.That(Texts(f.Screen.transform), Has.Some.Contains("宝箱奖励"));
                Assert.That(Texts(f.Screen.transform), Has.Some.Contains("+100"));
                Assert.That(Texts(f.Screen.transform), Has.Some.Contains("余额 130"));
                AssertRefreshDoesNotChangeRun(f);
            }
        }

        [TestCase(0UL, 352, 382)]
        [TestCase(8UL, -30, 0)]
        public void RestLotteryReportsTheActualSignedOutcomeAndBalance(ulong seed, int delta, int balance)
        {
            using (var f = new Fixture("rest", seed: seed))
            {
                f.Enter("rest"); f.ShowScreen();
                Assert.IsTrue(f.Host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.DrawRestReward)), f.Host.Message);
                Assert.AreEqual(delta, f.Host.Adventure.LastLocationReward.GoldDelta);
                Assert.AreEqual(balance, f.Host.Adventure.Gold);
                string feedback = FeedbackText(f.Screen);
                Assert.That(feedback, Does.Contain("休息抽奖"));
                Assert.That(feedback, Does.Contain(delta > 0 ? "+" + delta : delta.ToString()));
                Assert.That(feedback, Does.Contain("余额 " + balance));
                if (delta < 0) Assert.That(feedback, Does.Not.Contain("获得"));
                AssertRefreshDoesNotChangeRun(f);
            }
        }

        [Test]
        public void LeavingShopNamesTheOwnedInterestItemAndShowsOneActualIncomeReceipt()
        {
            using (var f = new Fixture("shop", startingGold: 100, startingRelic: "11003"))
            {
                f.Enter("shop"); f.ShowScreen();
                Assert.IsTrue(f.Host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.Continue)), f.Host.Message);
                Assert.AreEqual(110, f.Host.Adventure.Gold);
                string feedback = FeedbackText(f.Screen);
                Assert.That(feedback, Does.Contain("存折"));
                Assert.That(feedback, Does.Contain("利息"));
                Assert.That(feedback, Does.Contain("+10"));
                Assert.That(feedback, Does.Contain("余额 110"));
                AssertRefreshDoesNotChangeRun(f);
                Assert.AreEqual(feedback, FeedbackText(f.Screen), "Redrawing must retain one receipt for the same committed income.");
                f.Host.ReturnToMenu(); Assert.IsTrue(f.Host.Resume());
                Assert.IsEmpty(Feedback(f.Screen), "Resuming must not announce the old income as a new award.");
                Assert.AreEqual(110, f.Host.Adventure.Gold);
            }
        }

        [Test]
        public void SellingAnOwnedItemReportsOnlyTheActualSaleIncome()
        {
            using (var f = new Fixture("shop", startingGold: 100, startingRelic: "11003"))
            {
                f.Enter("shop"); f.ShowScreen();
                string item = f.Host.Adventure.Relics.Single().Id;
                Assert.IsTrue(f.Host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.SellRelic, item)), f.Host.Message);
                Assert.AreEqual(120, f.Host.Adventure.Gold);
                string feedback = FeedbackText(f.Screen);
                Assert.That(feedback, Does.Contain("出售"));
                Assert.That(feedback, Does.Contain("存折"));
                Assert.That(feedback, Does.Contain("+20"));
                Assert.That(feedback, Does.Contain("余额 120"));
                Assert.That(feedback, Does.Not.Contain("利息"));
                AssertRefreshDoesNotChangeRun(f);
            }
        }

        [Test]
        public void RestoringASavedMapDoesNotReannounceOrReapplyThePriorInterest()
        {
            using (var original = new Fixture("shop", startingGold: 100, startingRelic: "11003"))
            {
                original.Enter("shop");
                Assert.IsTrue(original.Host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.Continue)), original.Host.Message);
                string snapshot = original.Host.Adventure.Export();
                using (var restored = new Fixture("shop", startingGold: 100, startingRelic: "11003", savePath: original.SavePath, restore: true))
                {
                    Assert.IsTrue(restored.Host.Resume(), restored.Host.Message); restored.ShowScreen();
                    Assert.AreEqual(snapshot, restored.Host.Adventure.Export());
                    Assert.IsEmpty(Feedback(restored.Screen));
                    AssertRefreshDoesNotChangeRun(restored, expectReceipt: false);
                }
            }
        }

        [Test]
        public void RefreshDoesNotInferInterestFromAnUnrelatedBalanceIncrease()
        {
            using (var f = new Fixture("shop", startingGold: 100, startingRelic: "11003"))
            {
                f.Enter("shop"); f.ShowScreen();
                var run = f.Host.Adventure;
                Assert.IsTrue(run.ApplyAdmin(run.Revision, new BattleAdminPatch(gold: new[] { new BattleAdminGold(run.PlayerId, 120) })).Accepted);
                f.Screen.Refresh();
                Assert.IsEmpty(Feedback(f.Screen));
                Assert.AreEqual(120, f.Host.Adventure.Gold);
                AssertRefreshDoesNotChangeRun(f, expectReceipt: false);
            }
        }

        private static void AssertRefreshDoesNotChangeRun(Fixture f, bool expectReceipt = true)
        {
            string snapshot = f.Host.Adventure.Export(); string saved = File.ReadAllText(f.SavePath);
            for (int i = 0; i < 3; i++) f.Screen.Refresh();
            Assert.AreEqual(snapshot, f.Host.Adventure.Export(), "Displaying a gold receipt must never submit an economy command.");
            Assert.AreEqual(saved, File.ReadAllText(f.SavePath));
            Assert.AreEqual(expectReceipt ? 1 : 0, Feedback(f.Screen).Length, "Repeated Refresh must not accumulate income notices.");
        }

        private static Transform[] Feedback(AdventureScreen screen) => screen.GetComponentsInChildren<Transform>(true)
            .Where(t => t.name == "Gold Income Feedback").ToArray();
        private static string FeedbackText(AdventureScreen screen)
        {
            var feedback = Feedback(screen);
            Assert.AreEqual(1, feedback.Length, "A committed gold change needs one compact source-and-balance receipt.");
            return string.Join("\n", Texts(feedback[0]));
        }
        private static string[] Texts(Transform parent) => parent.GetComponentsInChildren<Component>(true)
            .Where(c => c != null && c.GetType().GetProperty("text")?.PropertyType == typeof(string))
            .Select(c => (string)c.GetType().GetProperty("text").GetValue(c)).ToArray();

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root;
            private GameObject ui, view;
            private readonly TextAsset battle, adventure;
            private readonly bool ownsDirectory;
            public readonly string SavePath;
            public readonly AdventureSceneHost Host;
            public AdventureScreen Screen { get; private set; }

            public Fixture(string start, int startingGold = 30, string startingRelic = null, ulong seed = 42, string savePath = null, bool restore = false)
            {
                ownsDirectory = savePath == null;
                SavePath = savePath ?? Path.Combine(Path.GetTempPath(), "gold-feedback-" + Guid.NewGuid().ToString("N"), "run.json");
                Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
                var xml = XDocument.Load(Path.Combine(Application.dataPath, "../External/BattleToolkit/Engine/Content/SingleChapterBattle.xml"));
                foreach (var enemy in xml.Root.Element("Enemies").Elements())
                { enemy.SetAttributeValue("maxHealth", 1); enemy.SetAttributeValue("damage", 0); enemy.RemoveNodes(); }
                battle = new TextAsset(xml.ToString()) { name = "GoldFeedbackBattle" };
                string node = start == "fight" ? "kind='Battle' encounter='single-chapter-battle-1' gold='20'" :
                    start == "chest" ? "kind='Chest' gold='100'" : start == "shop" ? "kind='Shop'" : "kind='Rest'";
                adventure = new TextAsset("<AdventureContent schemaVersion='1' id='gold-feedback-test' version='1' startNode='" + start +
                    "' startingEncounter='single-chapter-battle-1' defaultTarot='fool' startingGold='" + startingGold + "' economyVersion='2'>" +
                    "<Tarots><Tarot id='fool' initiallyUnlocked='true'" + (startingRelic == null ? "" : " startingRelic='" + startingRelic + "'") +
                    "/></Tarots><Nodes><Node id='" + start + "' " + node + " chapter='1' next='boss'/>" +
                    "<Node id='boss' kind='Boss' chapter='1' encounter='single-chapter-wrath'/></Nodes><Shop/><Services>" +
                    "<Service kind='DeleteCard' price='45'/><Service kind='DuplicateCard' price='20'/>" +
                    "<Service kind='ChangeSuit' price='30'/></Services><Rest/></AdventureContent>")
                    { name = "GoldFeedbackAdventure" };
                root = new GameObject("Gold feedback host"); Host = root.AddComponent<AdventureSceneHost>();
                Host.Configure(battle, adventure, savePath: SavePath); Host.Initialize();
                if (!restore) Assert.IsTrue(Host.NewRun("fool", seed), Host.Message);
            }
            public void Enter(string id) => Assert.IsTrue(Host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.EnterNode, id)), Host.Message);
            public void Win()
            {
                var player = Host.Adventure.PlayerId;
                if (Host.Session.State.FlowFor(player).Window == BattlePlayerWindow.Draw)
                    Assert.IsTrue(Host.Execute(Host.Revision, player, CardDuelAction.SkipDraw).Accepted);
                Assert.IsTrue(Host.Execute(Host.Revision, player, CardDuelAction.Play,
                    Host.Session.State.Zone(CardZoneKinds.Hand, player).Cards.Take(1)).Accepted);
                Assert.AreEqual(AdventurePhase.Reward, Host.Adventure.Phase);
            }
            public void ShowScreen()
            {
                view = new GameObject("Gold feedback art lookup"); view.SetActive(false);
                var adapter = view.AddComponent(typeof(AdventureSceneHost).GetProperty("BattleView").PropertyType);
                typeof(AdventureSceneHost).GetField("battleView", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Host, adapter);
                ui = new GameObject("Gold feedback UI"); Screen = ui.AddComponent<AdventureScreen>();
                typeof(AdventureScreen).GetMethod("Configure").Invoke(Screen, new object[] { Host, null }); Screen.Refresh();
            }
            public void Dispose()
            {
                if (ui != null) UnityEngine.Object.DestroyImmediate(ui);
                if (view != null) UnityEngine.Object.DestroyImmediate(view);
                UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(battle); UnityEngine.Object.DestroyImmediate(adventure);
                if (ownsDirectory) Directory.Delete(Path.GetDirectoryName(SavePath), true);
            }
        }
    }
}
