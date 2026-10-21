using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using PokerEngine;
using UnityEngine;

namespace Poker.Adventure.Tests
{
    [TestFixture]
    public sealed class AdventureSceneHostTests
    {
        private sealed class Fixture : IDisposable
        {
            public readonly string DirectoryPath;
            public string SavePath => Path.Combine(DirectoryPath, "run.json");
            public readonly GameObject Root, BattleRoot;
            public readonly TextAsset BattleXml, AdventureXml;
            public readonly AdventureSceneHost Host;
            private readonly bool ownsDirectory;
            public Fixture(string directory = null)
            {
                ownsDirectory = directory == null;
                DirectoryPath = directory ?? Path.Combine(Path.GetTempPath(), "adventure-host-test-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(DirectoryPath);
                var xml = XDocument.Load(Path.Combine(Application.dataPath, "../External/BattleToolkit/Engine/Content/StandardSingleBattle.xml"));
                foreach (var enemy in xml.Root.Element("Enemies").Elements())
                { enemy.SetAttributeValue("maxHealth", 1); enemy.SetAttributeValue("damage", 0); enemy.RemoveNodes(); }
                BattleXml = new TextAsset(xml.ToString()) { name = "HostBattle" };
                AdventureXml = new TextAsset(@"<AdventureContent schemaVersion='1' id='host-test' version='1'
                    startNode='fight' startingEncounter='standard-single-battle' defaultTarot='fool' startingGold='30'>
                    <Tarots><Tarot id='fool' initiallyUnlocked='true'/></Tarots>
                    <Nodes>
                      <Node id='fight' kind='Battle' chapter='1' encounter='standard-single-battle' gold='20' next='shop'/>
                      <Node id='shop' kind='Shop' chapter='1' next='boss'/>
                      <Node id='boss' kind='Boss' chapter='1' encounter='standard-gluttony-boss'/>
                    </Nodes><Shop/><Services/><Rest/>
                    </AdventureContent>") { name = "HostAdventure" };
                Root = new GameObject("Adventure host test"); BattleRoot = new GameObject("Battle root test");
                Host = Root.AddComponent<AdventureSceneHost>();
                Host.Configure(BattleXml, AdventureXml, roots: new[] { BattleRoot }, savePath: SavePath);
                Host.Initialize();
            }
            public void StartBattle()
            {
                Assert.IsTrue(Host.NewRun("fool", 42), Host.Message);
                Assert.IsTrue(Host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.EnterNode, "fight")), Host.Message);
                Assert.AreEqual(AdventurePhase.Battle, Host.Adventure.Phase); Assert.IsNotNull(Host.Session);
            }
            public void Win()
            {
                var player = Host.Adventure.PlayerId;
                if (Host.Session.State.FlowFor(player).Window == BattlePlayerWindow.Draw)
                    Assert.IsTrue(Host.Execute(Host.Revision, player, CardDuelAction.SkipDraw).Accepted);
                var cards = Host.Session.State.Zone(CardZoneKinds.Hand, player).Cards.Take(1).ToArray();
                Assert.IsTrue(Host.Execute(Host.Revision, player, CardDuelAction.Play, cards).Accepted);
                Assert.AreEqual(AdventurePhase.Reward, Host.Adventure.Phase);
            }
            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Root); UnityEngine.Object.DestroyImmediate(BattleRoot);
                UnityEngine.Object.DestroyImmediate(BattleXml); UnityEngine.Object.DestroyImmediate(AdventureXml);
                if (ownsDirectory && Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, true);
            }
        }

        [Test] public void HeadlessCommandsPersistAndMenuResumeDoesNotReexecuteOpening()
        {
            using (var f = new Fixture())
            {
                f.StartBattle();
                string snapshot = File.ReadAllText(f.SavePath); long revision = f.Host.Revision;
                var state = f.Host.Session.State;
                f.Host.ReturnToMenu(); Assert.IsTrue(f.Host.IsAtMenu);
                Assert.IsFalse(f.Host.Execute(revision, f.Host.Adventure.PlayerId, CardDuelAction.EndTurn).Accepted);
                Assert.AreSame(state, f.Host.Session.State); Assert.AreEqual(snapshot, File.ReadAllText(f.SavePath));
                Assert.IsTrue(f.Host.Resume()); Assert.IsFalse(f.Host.IsAtMenu);
                Assert.AreEqual(revision, f.Host.Revision); Assert.AreSame(state, f.Host.Session.State);
                Assert.AreEqual(snapshot, File.ReadAllText(f.SavePath));
                Assert.IsFalse(f.Host.NewRun("fool", 10)); Assert.AreEqual(revision, f.Host.Revision);
                var stale = f.Host.Execute(revision - 1, f.Host.Adventure.PlayerId, CardDuelAction.EndTurn);
                Assert.AreEqual(CardDuelError.StaleRevision, stale.Error);
                f.Win(); Assert.IsFalse(f.Host.IsBusy);
                Assert.AreEqual(50, f.Host.Adventure.Gold);
            }
        }

        [Test] public void FreshHostRestoresBattleAndRewardWithoutDuplicatingRewards()
        {
            using (var original = new Fixture())
            {
                original.StartBattle(); long revision = original.Host.Revision;
                using (var restored = new Fixture(original.DirectoryPath))
                {
                    Assert.IsTrue(restored.Host.HasSavedRun); Assert.IsNull(restored.Host.Adventure);
                    Assert.IsTrue(restored.Host.Resume()); Assert.AreEqual(revision, restored.Host.Revision);
                    Assert.AreEqual(original.Host.Session.State.Revision, restored.Host.Session.State.Revision);
                    restored.Win();
                }
                using (var reward = new Fixture(original.DirectoryPath))
                {
                    Assert.IsTrue(reward.Host.Resume()); Assert.AreEqual(AdventurePhase.Reward, reward.Host.Adventure.Phase);
                    Assert.AreEqual(50, reward.Host.Adventure.Gold);
                    var previousBattle = reward.Host.Session;
                    Assert.IsTrue(reward.Host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.Continue)));
                    Assert.AreEqual(AdventurePhase.Map, reward.Host.Adventure.Phase);
                    Assert.AreSame(previousBattle, reward.Host.Session, "OnDisable may still read the completed battle after Continue.");
                    Assert.AreEqual(50, reward.Host.Adventure.Gold);
                    Assert.IsFalse(reward.Host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.Continue)));
                }
            }
        }

        [Test] public void FailedSaveLeavesAuthoritySnapshotAndBattleVisibilityUntouched()
        {
            using (var f = new Fixture())
            {
                f.StartBattle(); f.Win();
                f.BattleRoot.SetActive(true);
                var adventure = f.Host.Adventure; var battle = f.Host.Session;
                string snapshot = File.ReadAllText(f.SavePath);
                // A directory at the temporary-file path deterministically fails the atomic write.
                Directory.CreateDirectory(f.SavePath + ".tmp");
                Assert.IsFalse(f.Host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.Continue)));
                Assert.AreSame(adventure, f.Host.Adventure); Assert.AreSame(battle, f.Host.Session);
                Assert.IsTrue(f.BattleRoot.activeSelf); Assert.AreEqual(snapshot, File.ReadAllText(f.SavePath));
                Assert.IsNotEmpty(f.Host.Message);
                Directory.Delete(f.SavePath + ".tmp");
                Assert.IsTrue(f.Host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.Continue)));
                Assert.IsFalse(f.BattleRoot.activeSelf); Assert.AreSame(battle, f.Host.Session);
            }
        }

        [Test] public void InvalidSaveIsReportedAndPreserved()
        {
            string directory = Path.Combine(Path.GetTempPath(), "adventure-invalid-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string path = Path.Combine(directory, "run.json"); File.WriteAllText(path, "invalid save");
                using (var f = new Fixture(directory))
                {
                    Assert.IsFalse(f.Host.HasSavedRun); Assert.IsNotEmpty(f.Host.Message);
                    Assert.AreEqual("invalid save", File.ReadAllText(path));
                }
            }
            finally { Directory.Delete(directory, true); }
        }
    }
}
