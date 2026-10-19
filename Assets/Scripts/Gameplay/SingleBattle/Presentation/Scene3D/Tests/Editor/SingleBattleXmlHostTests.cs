using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CardInteraction;
using CardPresentation;
using NUnit.Framework;
using PokerEngine;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Poker.SingleBattle.Tests
{
    [TestFixture]
    public sealed class SingleBattleXmlHostTests
    {
        private sealed class Fixture : IDisposable
        {
            private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
            public readonly Scene Scene = EditorSceneManager.NewPreviewScene();
            public readonly CardMatchHost Match;
            public readonly SingleBattleSceneAdapter Host;
            public readonly CardZonePlayHost Hand;
            public readonly HandZoneView HandView;
            public readonly HandActionPresenter Actions;
            public readonly PlayerBattleActor[] Players;
            public readonly EnemyBattleActor[] Enemies;

            public Fixture(int players = 1, int enemies = 1)
            {
                var root = New("XML Match");
                Match = root.AddComponent<CardMatchHost>();
                Match.ConfigureRuntimeManaged();
                var hand = New("Hand");
                HandView = hand.AddComponent<HandZoneView>();
                HandView.Configure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameAssets/CardArt/Prefabs/Card.prefab"), CardZoneLayoutSettings.Default);
                Hand = hand.AddComponent<CardZonePlayHost>();
                Hand.ConfigureRuntimeBinding(Match);
                var ui = (GameObject)PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameAssets/CardPresentation/Prefabs/HandActionPanel.prefab"), Scene);
                var panel = ui.GetComponent<HandActionPanelView>();
                panel.Configure(panel.PlayButton, panel.DiscardButton, panel.RankSortButton, panel.SuitSortButton,
                    panel.PlayButton.GetComponentInChildren<TMP_Text>(), panel.DiscardButton.GetComponentInChildren<TMP_Text>(),
                    panel.RankSortButton.GetComponentInChildren<TMP_Text>(), panel.SuitSortButton.GetComponentInChildren<TMP_Text>());
                Actions = ui.GetComponent<HandActionPresenter>() ?? ui.AddComponent<HandActionPresenter>();
                Actions.Configure(Hand, panel);
                Host = root.AddComponent<SingleBattleSceneAdapter>();
                Host.Configure(Match, Actions, null, null);
                Players = Enumerable.Range(0, players).Select(index => New("Player " + index).AddComponent<PlayerBattleActor>()).ToArray();
                Enemies = Enumerable.Range(0, enemies).Select(index => New("Enemy " + index).AddComponent<EnemyBattleActor>()).ToArray();
                foreach (var actor in Players) actor.Configure(Host);
                foreach (var actor in Enemies) actor.Configure(Host);
                Host.ConfigureActors(Players, Enemies);
                Poker.SingleBattle.Editor.BattlePresentationSceneSetup.ConfigureHost(Host, includeFeedback: false);
            }

            private GameObject New(string name)
            {
                var go = new GameObject(name);
                SceneManager.MoveGameObjectToScene(go, Scene);
                return go;
            }

            public TextAsset Content(string text)
            {
                var asset = new TextAsset(text) { name = "xml-host-tests.xml" };
                owned.Add(asset);
                return asset;
            }

            public CardSelectionRequest SelectOne()
            {
                foreach (var card in Hand.Cards) Hand.SetInteraction(card.Id, default);
                Hand.SetInteraction(Hand.Cards[0].Id, new CardInteractionState(isSelected: true));
                Hand.Publish(); HandView.CompleteTransitions(); Actions.Refresh();
                return Hand.CaptureSelection();
            }

            public void Dispose()
            {
                Host.enabled = false;
                foreach (var actor in Players) actor.enabled = false;
                foreach (var actor in Enemies) actor.enabled = false;
                Hand.StopSession();
                EditorSceneManager.ClosePreviewScene(Scene);
                foreach (var item in owned) UnityEngine.Object.DestroyImmediate(item);
            }
        }

        private static string Xml(int players = 1, int enemies = 1, int health = 123, int damage = 13)
        {
            var hands = string.Concat(Enum.GetValues(typeof(HandCategory)).Cast<HandCategory>().Select(category =>
                "<Hand category=\"" + category + "\"><Effect id=\"damage/1\" kind=\"Damage\" amount=\"" +
                damage.ToString(CultureInfo.InvariantCulture) + "\" target=\"Opponents\"/></Hand>"));
            var playerSlots = string.Concat(Enumerable.Range(1, players).Select(index => "<Player id=\"p" + index + "\" ref=\"hero\"/>"));
            var enemySlots = string.Concat(Enumerable.Range(1, enemies).Select(index => "<Enemy id=\"e" + index + "\" ref=\"foe\"/>"));
            return "<BattleContent schemaVersion=\"1\" id=\"host-tests\" version=\"1\">" +
                "<ScoringProfiles><Scoring id=\"score\" preset=\"Default\"/></ScoringProfiles>" +
                "<BattleRules><Rules id=\"rules\" scoringRef=\"score\" roundLimit=\"20\" blockExpiry=\"Retain\"/></BattleRules>" +
                "<HandRules><HandRule id=\"hands\">" + hands + "</HandRule></HandRules>" +
                "<Players><Player id=\"hero\" maxHealth=\"" + health.ToString(CultureInfo.InvariantCulture) +
                "\" handRuleRef=\"hands\" handSize=\"8\" initialEnergy=\"3\" maxEnergy=\"3\" discards=\"2\"" +
                " playRefill=\"AfterAction\" discardRefill=\"AfterAction\" discardBlockPerCard=\"6\"/></Players>" +
                "<Enemies><Enemy id=\"foe\" maxHealth=\"1000\" damage=\"7\"/></Enemies>" +
                "<Encounters><Encounter id=\"fight\" rulesRef=\"rules\">" + playerSlots + enemySlots +
                "</Encounter></Encounters></BattleContent>";
        }

        private static BattleSession Assemble(string xml)
        {
            var compiled = BattleContentCompiler.CompileXml(xml, "attached.xml");
            Assert.That(compiled.Success, Is.True, Explain(compiled.Diagnostics));
            var result = BattleContentFactory.Create(compiled.Content,
                new BattleSetupRequest("fight", "attached", HmacDeckShuffler.FromSeed(new byte[32])));
            Assert.That(result.Success, Is.True, Explain(result.Diagnostics));
            return result.Session;
        }

        private static string Explain(IEnumerable<BattleDiagnostic> diagnostics) => string.Join("\n", diagnostics.Select(item => item.Code + ": " + item.Message));

        [TestCase("SingleBattleDemo", "single-battle", 800, 1200, 240, 20, true)]
        [TestCase("ForestRuins", "single-battle", 800, 1200, 240, 20, true)]
        [TestCase("TeamBattleDemo", "demo-battle", 40, 75, 7, 445, false)]
        public void MigratedScenesKeepTheirAuthoredRosterAndRules(string name, string encounter, int health, int enemyHealth, int damage, int rounds, bool unlimited)
        {
            var active = SceneManager.GetActiveScene();
            string path = "Assets/Scenes/" + name + ".unity";
            var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var adapter = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SingleBattleSceneAdapter>(true)).Single();
                Assert.That(adapter.ContentEncounterId, Is.EqualTo(encounter));
                Assert.That(adapter.ContentXml, Is.SameAs(AssetDatabase.LoadAssetAtPath<TextAsset>(Editor.SingleBattleSetup.SceneBattleContentPath)));
                var session = adapter.BuildConfiguredSession("migration", HmacDeckShuffler.FromSeed(new byte[32]));
                var state = session.State; var player = state.Duel.Players.Single(); var enemy = state.Duel.Enemies.Single();
                Assert.That(state.HasStarted, Is.False); Assert.That(adapter.Session, Is.Null);
                Assert.That(state.Duel.Combat.Get(player.Id).Health, Is.EqualTo(health));
                Assert.That(state.Duel.Combat.Get(enemy.Id).Health, Is.EqualTo(enemyHealth));
                Assert.That(session.Engine.Duel.Definition.RoundLimit, Is.EqualTo(rounds));
                Assert.That(player.Rules.UnlimitedDiscards, Is.EqualTo(unlimited));
                Assert.That(player.Rules.HandSize, Is.EqualTo(8)); Assert.That(player.Rules.MaxEnergy, Is.EqualTo(3));
                Assert.That(player.Rules.PlayEnergyCost, Is.EqualTo(1)); Assert.That(player.Rules.DiscardEnergyCost, Is.EqualTo(1));
                Assert.That(player.Rules.DamagePolicy, Is.InstanceOf<EqualScoreDamageRule>());
                Assert.That(session.Execute(state.Revision, player.Id, CardDuelAction.Start).Accepted, Is.True);
                var before = session.State;
                var preview = session.Engine.Duel.PreviewEnemyAction(before.Duel, enemy.Id);
                Assert.That(preview.Hits.Single().SettledDamage, Is.EqualTo(damage));
                var discarded = before.Zone(CardZoneKinds.Hand, player.Id).Cards.Take(1).ToArray();
                Assert.That(session.Execute(before.Revision, player.Id, CardDuelAction.Discard, discarded).Accepted, Is.True);
                Assert.That(session.State.Duel.Combat.Get(player.Id).Block, Is.EqualTo(unlimited ? 8 : 0));
                Assert.That(session.State.Zone(CardZoneKinds.Hand, player.Id).Cards.Count, Is.EqualTo(8));
                Assert.That(session.Execute(session.State.Revision, player.Id, CardDuelAction.EndTurn).Accepted, Is.True);
                Assert.That(session.State.Duel.Combat.Get(player.Id).Health, Is.EqualTo(health - damage + (unlimited ? 8 : 0)));
                Assert.That(session.State.Duel.Combat.Get(player.Id).Block, Is.Zero);
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(active));
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                        Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject), Is.Zero, transform.name);
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void GeneratedTeamEncounterBuildsIndependentTwoVersusThreeSlots()
        {
            var compiled = BattleContentCompiler.CompileXml(AssetDatabase.LoadAssetAtPath<TextAsset>(Editor.SingleBattleSetup.SceneBattleContentPath).text);
            Assert.That(compiled.Success, Is.True, Explain(compiled.Diagnostics));
            var built = BattleContentFactory.Create(compiled.Content, new BattleSetupRequest("team-battle", "team", HmacDeckShuffler.FromSeed(new byte[32])));
            Assert.That(built.Success, Is.True, Explain(built.Diagnostics));
            Assert.That(built.Session.State.Duel.Players.Count, Is.EqualTo(2));
            Assert.That(built.Session.State.Duel.Enemies.Count, Is.EqualTo(3));
            Assert.That(built.Session.State.Table.Cards.Count, Is.EqualTo(104));
        }

        [Test]
        public void DemoNewSceneBindsXmlContentAndBuildsFromXmlOnly()
        {
            var active = SceneManager.GetActiveScene();
            const string path = "Assets/Scenes/SingleBattleDemoNew.unity";
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var host = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SingleBattleSceneAdapter>(true)).Single();
                Assert.That(host.ContentXml, Is.SameAs(AssetDatabase.LoadAssetAtPath<TextAsset>(
                    "Packages/com.kaya.battle-engine/Content/SingleBattleDemoNew.xml")));
                Assert.That(host.ContentEncounterId, Is.EqualTo("single-battle-new"));
                var built = host.BuildConfiguredSession("scene-validation", HmacDeckShuffler.FromSeed(new byte[32]));
                Assert.That(built.State.HasStarted, Is.False);
                Assert.That(built.State.Duel.Players.Count, Is.EqualTo(host.Players.Count));
                Assert.That(built.State.Duel.Enemies.Count, Is.EqualTo(host.Enemies.Count));
                Assert.That(host.Session, Is.Null);
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(active));
            }
            finally
            {
                if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(active));
        }

        [Test]
        public void XmlBuildCreatesAnUndealtSessionWithoutPublishingIt()
        {
            using (var f = new Fixture())
            {
                var xml = f.Content(Xml());
                f.Host.ConfigureContent(xml, "fight");
                Assert.That(f.Host.ContentXml, Is.SameAs(xml));
                Assert.That(f.Host.ContentEncounterId, Is.EqualTo("fight"));

                var session = f.Host.BuildConfiguredSession("xml-built", HmacDeckShuffler.FromSeed(new byte[32]));
                Assert.That(session.State.HasStarted, Is.False);
                Assert.That(session.State.Table.MatchId.Value, Is.EqualTo("xml-built"));
                Assert.That(session.State.Duel.Players[0].Id.Value, Is.EqualTo("p1"));
                Assert.That(session.State.Duel.Enemies[0].Id.Value, Is.EqualTo("e1"));
                Assert.That(session.State.Duel.Combat.Get(new ParticipantId("p1")).Health, Is.EqualTo(123));
                Assert.That(session.State.Zone(CardZoneKinds.Hand, new ParticipantId("p1")).Cards, Is.Empty);
                Assert.That(f.Host.Session, Is.Null);
                Assert.That(f.Hand.Cards, Is.Empty);
                Assert.That(f.Host.ContentDiagnostics.Any(item => item.Severity == BattleDiagnosticSeverity.Error), Is.False);
            }
        }

        [Test]
        public void XmlRulesDriveOpeningPlayDiscardRefillAndEnemyTurnThroughTheExistingViews()
        {
            using (var f = new Fixture())
            {
                f.Host.ConfigureContent(f.Content(Xml()), "fight");
                f.Host.StartBattle();
                Assert.That(f.Host.State.HasStarted, Is.True);
                Assert.That(f.Host.Player.ParticipantId.Value, Is.EqualTo("p1"));
                Assert.That(f.Host.Enemy.ParticipantId.Value, Is.EqualTo("e1"));
                Assert.That(f.Hand.Cards.Count, Is.EqualTo(8));

                var played = f.SelectOne();
                Assert.That(f.Host.Player.Play(played, false), Is.EqualTo(1));
                Assert.That(f.Host.Enemy.Unit.Health, Is.EqualTo(987));
                Assert.That(f.Host.Player.Turn.Energy, Is.EqualTo(2));
                Assert.That(f.Hand.Cards.Count, Is.EqualTo(8));
                Assert.That(f.Host.PlayerZone(CardZoneKinds.Hand).Cards.Contains(played.Cards[0]), Is.False);
                Assert.That(f.Host.PlayerZone(CardZoneKinds.Played).Cards, Is.Empty);

                var discarded = f.SelectOne();
                Assert.That(f.Host.Player.Discard(discarded, false), Is.EqualTo(1));
                Assert.That(f.Host.Player.Unit.Block, Is.EqualTo(6));
                Assert.That(f.Host.Player.Turn.Energy, Is.EqualTo(1));
                Assert.That(f.Host.PlayerDiscards, Is.EqualTo(1));
                Assert.That(f.Hand.Cards.Count, Is.EqualTo(8));
                Assert.That(f.Host.PlayerZone(CardZoneKinds.Hand).Cards.Contains(discarded.Cards[0]), Is.False);

                f.HandView.CompleteTransitions();
                Assert.That(f.Host.EndTurn(false), Is.True);
                Assert.That(f.Host.State.Duel.CompletedRounds, Is.EqualTo(1));
                Assert.That(f.Host.Player.Unit.Health, Is.EqualTo(122));
                Assert.That(f.Host.Player.Unit.Block, Is.Zero);
                Assert.That(f.Host.Player.Turn.Energy, Is.EqualTo(3));
                Assert.That(f.Host.PlayerDiscards, Is.EqualTo(1));
                Assert.That(f.Hand.Cards.Count, Is.EqualTo(8));
                Assert.That(f.Host.State, Is.SameAs(f.Host.AuthorityState));
                foreach (var zone in f.Host.State.Table.Zones)
                    CollectionAssert.AreEquivalent(zone.Cards, f.Match.State.GetZone(zone.Id).Cards);
            }
        }

        [Test]
        public void ConfiguringNewXmlOnlyChangesTheNextBattle()
        {
            using (var f = new Fixture())
            {
                f.Host.ConfigureContent(f.Content(Xml()), "fight");
                f.Host.StartBattle();
                var originalSession = f.Host.Session;
                var originalState = f.Host.AuthorityState;
                f.Host.ConfigureContent(f.Content(Xml(health: 211, damage: 29)), "fight");
                Assert.That(f.Host.Session, Is.SameAs(originalSession));
                Assert.That(f.Host.AuthorityState, Is.SameAs(originalState));
                Assert.That(f.Host.Player.Unit.Health, Is.EqualTo(123));
                f.Host.RestartBattle();
                Assert.That(f.Host.Session, Is.Not.SameAs(originalSession));
                Assert.That(originalSession.State, Is.SameAs(originalState));
                Assert.That(f.Host.Player.Unit.Health, Is.EqualTo(211));
                Assert.That(f.Host.Player.Play(f.SelectOne(), false), Is.EqualTo(1));
                Assert.That(f.Host.Enemy.Unit.Health, Is.EqualTo(971));
            }
        }

        [TestCase("missing-xml")]
        [TestCase("empty-encounter")]
        [TestCase("unknown-encounter")]
        [TestCase("malformed-xml")]
        [TestCase("invalid-rules")]
        public void InvalidXmlRestartKeepsTheExistingAuthorityWithoutReplacingTheSession(string failure)
        {
            using (var f = new Fixture())
            {
                f.Host.ConfigureContent(f.Content(Xml()), "fight");
                f.Host.StartBattle();
                var session = f.Host.Session;
                var authority = f.Host.AuthorityState;
                var playback = f.Host.Playback;
                var mirror = f.Match.State;
                var hand = f.HandView.Data;
                var xml = failure == "missing-xml" ? null : f.Content(failure == "malformed-xml" ? "<BattleContent>" :
                    failure == "invalid-rules" ? Xml().Replace("handSize=\"8\"", "handSize=\"0\"") : Xml());
                f.Host.ConfigureContent(xml, failure == "empty-encounter" ? "" : failure == "unknown-encounter" ? "missing" : "fight");

                Assert.Throws<InvalidOperationException>(() => f.Host.RestartBattle());
                Assert.That(f.Host.ContentDiagnostics.Any(item => item.Severity == BattleDiagnosticSeverity.Error), Is.True, Explain(f.Host.ContentDiagnostics));
                Assert.That(f.Host.Session, Is.SameAs(session));
                Assert.That(f.Host.AuthorityState, Is.SameAs(authority));
                Assert.That(f.Host.State, Is.SameAs(authority));
                Assert.That(f.Host.Playback, Is.SameAs(playback));
                Assert.That(f.Match.State, Is.SameAs(mirror));
                Assert.That(f.HandView.Data, Is.SameAs(hand));
                Assert.That(f.Host.Player.Unit.Health, Is.EqualTo(123));
                f.Host.ConfigureContent(f.Content(Xml()), "fight");
                Assert.That(f.Host.Player.Play(f.SelectOne(), false), Is.EqualTo(1));
                Assert.That(f.Host.Enemy.Unit.Health, Is.EqualTo(987));
            }
        }

        [Test]
        public void InvalidXmlCannotStartAnUnconfiguredScene()
        {
            using (var f = new Fixture())
            {
                f.Host.ConfigureContent(f.Content("<BattleContent>"), "fight");
                Assert.Throws<InvalidOperationException>(() => f.Host.StartBattle());
                Assert.That(f.Host.Session, Is.Null);
                Assert.That(f.Match.IsRunning, Is.False);
                Assert.That(f.Hand.Cards, Is.Empty);
            }
        }

        [TestCase(2, 1)]
        [TestCase(1, 2)]
        public void XmlRosterSlotCountsMustMatchTheSceneBeforeAStartCanPublish(int players, int enemies)
        {
            using (var f = new Fixture())
            {
                f.Host.ConfigureContent(f.Content(Xml(players, enemies)), "fight");
                Assert.Throws<InvalidOperationException>(() => f.Host.StartBattle());
                Assert.That(f.Host.Session, Is.Null);
                Assert.That(f.Match.IsRunning, Is.False);
            }
        }

        [TestCase("player-order")]
        [TestCase("enemy-order")]
        [TestCase("player-count")]
        [TestCase("enemy-count")]
        public void AttachRejectsSessionsWhoseParticipantSlotsDoNotMatchTheConfiguredEncounter(string mismatch)
        {
            using (var f = new Fixture(players: 2, enemies: 2))
            {
                var xml = Xml(2, 2);
                f.Host.ConfigureContent(f.Content(xml), "fight");
                f.Host.StartBattle();
                var session = f.Host.Session;
                var state = f.Host.AuthorityState;
                var foreign = mismatch == "player-count" ? Xml(1, 2) : mismatch == "enemy-count" ? Xml(2, 1) :
                    mismatch == "player-order" ? xml.Replace("<Player id=\"p1\" ref=\"hero\"/><Player id=\"p2\" ref=\"hero\"/>",
                        "<Player id=\"p2\" ref=\"hero\"/><Player id=\"p1\" ref=\"hero\"/>") :
                    xml.Replace("<Enemy id=\"e1\" ref=\"foe\"/><Enemy id=\"e2\" ref=\"foe\"/>",
                        "<Enemy id=\"e2\" ref=\"foe\"/><Enemy id=\"e1\" ref=\"foe\"/>");
                var attached = Assemble(foreign);
                if (mismatch.EndsWith("-count", StringComparison.Ordinal))
                    Assert.Throws<ArgumentException>(() => f.Host.AttachSession(attached));
                else
                    Assert.Throws<InvalidOperationException>(() => f.Host.AttachSession(attached));
                Assert.That(f.Host.Session, Is.SameAs(session));
                Assert.That(f.Host.AuthorityState, Is.SameAs(state));
                CollectionAssert.AreEqual(new[] { "p1", "p2" }, f.Host.Players.Select(actor => actor.ParticipantId.Value));
                CollectionAssert.AreEqual(new[] { "e1", "e2" }, f.Host.Enemies.Select(actor => actor.ParticipantId.Value));
            }
        }

        [Test]
        public void MatchingXmlSessionCanBeAttachedWithoutRestartingItsRules()
        {
            using (var f = new Fixture())
            {
                var xml = Xml();
                f.Host.ConfigureContent(f.Content(xml), "fight");
                var session = Assemble(xml);
                Assert.That(session.Execute(session.State.Revision, new ParticipantId("p1"), CardDuelAction.Start).Accepted, Is.True);
                var snapshot = session.State;
                f.Host.AttachSession(session);
                Assert.That(f.Host.Session, Is.SameAs(session));
                Assert.That(f.Host.AuthorityState, Is.SameAs(snapshot));
                Assert.That(f.Host.State, Is.SameAs(snapshot));
                Assert.That(f.Hand.Cards.Count, Is.EqualTo(8));
                Assert.That(f.Host.Player.Unit.Health, Is.EqualTo(123));
            }
        }

        [Test]
        public void MissingXmlCannotStartAndValidXmlRecoversWithoutRecreatingSceneObjects()
        {
            using (var f = new Fixture())
            {
                Assert.Throws<InvalidOperationException>(() => f.Host.StartBattle());
                Assert.That(f.Host.Session, Is.Null);
                f.Host.ConfigureContent(f.Content(Xml()), "fight"); f.Host.StartBattle();
                Assert.That(f.Host.Player.Unit.Health, Is.EqualTo(123));
            }
        }
    }
}
