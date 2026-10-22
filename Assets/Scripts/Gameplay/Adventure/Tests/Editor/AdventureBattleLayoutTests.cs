using System;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using NUnit.Framework;
using Poker.SingleBattle;
using PokerEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Poker.Adventure.Tests
{
    public sealed class AdventureBattleLayoutTests
    {
        private sealed class Fixture : IDisposable
        {
            private readonly string sceneCopy;
            private readonly Scene scene;
            private readonly TextAsset battleXml, adventureXml;
            public readonly AdventureSceneHost Host;
            public readonly SingleBattleSceneAdapter View;
            public readonly AdventureBattleLayout Layout;

            public Fixture(bool wrath = true, bool bodyLast = false)
            {
                sceneCopy = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/AdventureBattleLayoutTest.unity");
                Assert.IsTrue(AssetDatabase.CopyAsset("Assets/Scenes/SingleChapterAdventure.unity", sceneCopy));
                scene = EditorSceneManager.OpenScene(sceneCopy, OpenSceneMode.Additive);
                var roots = scene.GetRootGameObjects();
                Host = roots.SelectMany(root => root.GetComponentsInChildren<AdventureSceneHost>(true)).Single();
                View = Host.BattleView;
                Layout = Host.GetComponent<AdventureBattleLayout>();
                Assert.IsNotNull(Layout);
                var source = AssetDatabase.LoadAssetAtPath<TextAsset>(
                    "Packages/com.kaya.battle-engine/Content/StandardSingleBattle-2026-10-21.xml");
                Assert.IsNotNull(source);
                var xml = XDocument.Parse(source.text);
                string encounterId = wrath ? "standard-wrath-thousand-hands" : "standard-single-battle";
                var encounter = xml.Root.Element("Encounters").Elements("Encounter")
                    .Single(value => (string)value.Attribute("id") == encounterId);
                if (!wrath)
                {
                    encounter.Elements("Enemy").Remove();
                    encounter.Add(Enumerable.Range(0, 6).Select(index => new XElement("Enemy",
                        new XAttribute("id", "ordinary-" + index), new XAttribute("ref", "rotator"))));
                }
                else if (bodyLast)
                {
                    var body = encounter.Elements("Enemy").Single(value => (string)value.Attribute("ref") == "wrath");
                    body.Remove(); encounter.Add(body);
                }
                battleXml = new TextAsset(xml.ToString()) { name = "AdventureLayoutBattle" };
                adventureXml = new TextAsset("<AdventureContent schemaVersion='1' id='layout-test' version='1' " +
                    "startNode='fight' startingEncounter='" + encounterId + "' defaultTarot='fool'>" +
                    "<Tarots><Tarot id='fool' initiallyUnlocked='true'/></Tarots><Nodes>" +
                    "<Node id='fight' kind='" + (wrath ? "Boss" : "Battle") + "' chapter='1' encounter='" + encounterId + "'" +
                    (wrath ? "/>" : " next='finish'/><Node id='finish' kind='Boss' chapter='1' encounter='standard-wrath-thousand-hands'/>") +
                    "</Nodes><Shop/><Services/><Rest/></AdventureContent>") { name = "AdventureLayoutRun" };
                Host.Configure(battleXml, adventureXml, View,
                    roots.Where(root => root.name == "Gameplay" || root.name == "Battle Hand Backdrop").ToArray(), false);
                Host.Initialize();
                Assert.IsTrue(Host.NewRun("fool", 42), Host.Message);
                Assert.IsTrue(Host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.EnterNode, "fight")), Host.Message);
                Refresh();
                Assert.AreEqual(6, View.Enemies.Count);
            }

            public EnemyBattleActor Actor(ParticipantId id) => View.Enemies.Single(actor => actor.ParticipantId == id);
            public static Rect Area(EnemyBattleActor actor) => actor.GetComponent<BattleBillboardLayout>().ViewportArea;

            public void Refresh()
            {
                View.SynchronizeBattle();
                // The Editor test assembly deliberately has no card-UI dependency. Complete the real
                // authored hand's transitions before checking the adventure's presentation gate.
                foreach (var hand in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                    .Where(component => component != null && component.GetType().Name == "HandZoneView"))
                    hand.GetType().GetMethod("CompleteTransitions").Invoke(hand, null);
                typeof(AdventureSceneHost).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Host, null);
                typeof(AdventureBattleLayout).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Layout, null);
            }

            public void Edit(BattleAdminPatch patch)
            {
                var result = Host.ApplyAdmin(Host.Revision, patch);
                Assert.IsTrue(result.Accepted, Host.Message + " " + result.Error);
                Refresh();
            }

            public void Round()
            {
                var player = Host.Adventure.PlayerId;
                if (Host.Session.State.FlowFor(player).Window == BattlePlayerWindow.Draw) Execute(CardDuelAction.SkipDraw);
                Execute(Host.Session.State.Duel.IsActionBlocked(player) ? CardDuelAction.SkipPlayerTurn : CardDuelAction.EndTurn);
                if (Host.Session.State.FlowFor(player).Window == BattlePlayerWindow.Ending) Execute(CardDuelAction.FinishTurn);
            }

            private void Execute(CardDuelAction action)
            {
                var result = Host.Execute(Host.Revision, Host.Adventure.PlayerId, action);
                Assert.IsTrue(result.Accepted, Host.Message + " " + result.Error);
                Refresh();
            }

            public void Dispose()
            {
                foreach (var hand in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                    .Where(component => component != null && component.GetType().Name == "CardZonePlayHost"))
                    hand.GetType().GetMethod("StopSession").Invoke(hand, null);
                EditorSceneManager.CloseScene(scene, true);
                AssetDatabase.DeleteAsset(sceneCopy);
                UnityEngine.Object.DestroyImmediate(battleXml);
                UnityEngine.Object.DestroyImmediate(adventureXml);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WrathRosterUsesRemoteGridAndBossArtworkRegardlessOfRosterOrder(bool bodyLast)
        {
            using (var fixture = new Fixture(bodyLast: bodyLast))
            {
                var boss = WrathBossQuery.Read(fixture.View.PresentedState.Duel);
                Assert.IsNotNull(boss);
                var expected = new[] {
                    new Rect(.40f, .60f, .15f, .22f), new Rect(.56f, .60f, .15f, .22f), new Rect(.72f, .60f, .15f, .22f),
                    new Rect(.40f, .32f, .15f, .22f), new Rect(.56f, .32f, .15f, .22f), new Rect(.72f, .32f, .15f, .22f) };
                var artwork = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Characters/BattleBillboards/Enemy.mat");
                for (int i = 0; i < fixture.View.Enemies.Count; i++)
                {
                    var actor = fixture.View.Enemies[i];
                    var actual = Fixture.Area(actor);
                    string slot = "Remote six-enemy grid slot " + i + " (" + actor.ParticipantId + ")";
                    Assert.That(actual.x, Is.EqualTo(expected[i].x).Within(.000001f), slot + " x");
                    Assert.That(actual.y, Is.EqualTo(expected[i].y).Within(.000001f), slot + " y");
                    Assert.That(actual.width, Is.EqualTo(expected[i].width).Within(.000001f), slot + " width");
                    Assert.That(actual.height, Is.EqualTo(expected[i].height).Within(.000001f), slot + " height");
                    Assert.AreSame(artwork, actor.GetComponentInChildren<BattleDemoAshPresenter>().Artwork.sharedMaterial,
                        "Each boss participant retains the authored remote artwork.");
                }
            }
        }

        [Test]
        public void SevenStationBossKeepsRemoteEnemyHudWithoutEnablingTheLocalWrathPanel()
        {
            using (var fixture = new Fixture())
            {
                Assert.IsTrue(fixture.View.UsesExternalCommands);
                var hud = fixture.View.gameObject.scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<SingleBattleHud>(true)).Single();
                hud.Refresh();
                var panel = hud.GetComponentsInChildren<Transform>(true).FirstOrDefault(transform => transform.name == "Wrath Boss");
                Assert.IsTrue(panel == null || !panel.gameObject.activeInHierarchy,
                    "The seven-station scene must not enable the local standalone Wrath prototype panel.");
                var worldHud = fixture.View.GetComponent<BattleDemoEnemyWorldHud>();
                Assert.IsNotNull(worldHud, "Keep the authored remote per-enemy health, intent and status HUD.");
                Assert.IsTrue(worldHud.enabled);
            }
        }

        [Test]
        public void SeveredHandRetainsItsActorAndArtworkSlotThroughRegeneration()
        {
            using (var fixture = new Fixture())
            {
                var id = WrathBossQuery.Read(fixture.View.PresentedState.Duel).Hands[0].Id;
                var actor = fixture.Actor(id);
                var area = Fixture.Area(actor);
                fixture.Edit(new BattleAdminPatch(vitals: new[] {
                    new BattleAdminVitals(fixture.Host.Adventure.PlayerId, health: 60000, maxHealth: 60000),
                    new BattleAdminVitals(id, health: 0) }));
                Assert.IsFalse(actor.IsAlive); Assert.IsTrue(actor.gameObject.activeSelf);
                Assert.AreSame(actor, fixture.Actor(id)); Assert.AreEqual(area, Fixture.Area(actor));
                Assert.AreEqual(1, actor.GetComponentInChildren<BattleDemoAshPresenter>().Progress);
                for (int round = 0; round < 9; round++) fixture.Round();
                Assert.IsTrue(actor.IsAlive);
                Assert.AreSame(actor, fixture.Actor(id)); Assert.AreEqual(area, Fixture.Area(actor));
                Assert.AreEqual(0, actor.GetComponentInChildren<BattleDemoAshPresenter>().Progress);
                Assert.AreEqual(6, fixture.View.Enemies.Count);
            }
        }

        [Test]
        public void OrdinarySixEnemyEncounterRetainsItsGridAndUpdatedMinionArtwork()
        {
            using (var fixture = new Fixture(wrath: false))
            {
                Assert.IsNull(WrathBossQuery.Read(fixture.View.PresentedState.Duel));
                var areas = fixture.View.Enemies.Select(Fixture.Area).ToArray();
                Assert.AreEqual(3, areas.Select(area => area.center.x).Distinct().Count());
                Assert.AreEqual(2, areas.Select(area => area.center.y).Distinct().Count());
                Assert.AreEqual(1, areas.Select(area => area.size).Distinct().Count());
                var artwork = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Characters/BattleBillboards/Minion-Updated.mat");
                foreach (var actor in fixture.View.Enemies)
                    Assert.AreSame(artwork, actor.GetComponentInChildren<BattleDemoAshPresenter>().Artwork.sharedMaterial);
            }
        }
    }
}
