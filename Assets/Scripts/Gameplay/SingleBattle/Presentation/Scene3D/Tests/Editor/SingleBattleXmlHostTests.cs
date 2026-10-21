using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using CardInteraction;
using CardPresentation;
using NUnit.Framework;
using PokerEngine;
using TMPro;
using UIFoundation;
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

        private static string Xml(int players = 1, int enemies = 1, long health = 123, long damage = 13)
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

        [Test]
        public void Int64XmlVitalsAndEffectsReachActorsAndShieldsWithoutNarrowing()
        {
            using (var f = new Fixture())
            {
                var xml = Xml(health: 6000000000L, damage: 3000000000L)
                    .Replace("maxHealth=\"1000\"", "maxHealth=\"7000000000\"")
                    .Replace("discardBlockPerCard=\"6\"", "discardBlockPerCard=\"3000000000\"");
                f.Host.ConfigureContent(f.Content(xml), "fight");
                f.Host.StartBattle();
                Assert.AreEqual(6000000000L, f.Host.Player.Unit.Health);
                Assert.AreEqual(7000000000L, f.Host.Enemy.Unit.Health);
                Assert.AreEqual(1, f.Host.Player.Play(f.SelectOne(), false));
                Assert.AreEqual(4000000000L, f.Host.Enemy.Unit.Health);
                Assert.AreEqual(1, f.Host.Player.Discard(f.SelectOne(), false));
                Assert.AreEqual(3000000000L, f.Host.Player.Unit.Block);
                Assert.AreEqual(3000000000L, f.Host.Player.Shields.TotalBlock);
                f.HandView.CompleteTransitions();
                Assert.IsTrue(f.Host.EndTurn(false));
                Assert.AreEqual(6000000000L, f.Host.Player.Unit.Health);
                Assert.AreEqual(2999999993L, f.Host.Player.Shields.TotalBlock);
                f.Host.SynchronizeBattle();
                Assert.AreEqual(6000000000L, f.Host.Player.DisplayUnit.Health);
                Assert.AreEqual(4000000000L, f.Host.Enemy.DisplayUnit.Health);
                Assert.AreEqual(2999999993L, f.Host.Player.Shields.TotalBlock);
            }
        }

        [Test]
        public void AdministratorPatchPublishesThroughPlaybackToActorsAndHandWithoutPlayingCards()
        {
            using (var f = new Fixture())
            {
                f.Host.ConfigureContent(f.Content(Xml()), "fight");
                f.Host.StartBattle(); f.HandView.CompleteTransitions();
                var before = f.Host.AuthorityState;
                var owner = f.Host.Player.ParticipantId;
                var kept = before.Zone(CardZoneKinds.Hand, owner).Cards.Take(2).ToArray();
                var patch = new BattleAdminPatch(
                    vitals: new[] { new BattleAdminVitals(owner, health: 5000000000L, maxHealth: 6000000000L, block: 3000000000L) },
                    zones: new[] { new BattleAdminZone(owner, CardZoneKinds.Hand, kept.Select(id => new BattleAdminCard(instanceId: id.Value))) },
                    gold: new[] { new BattleAdminGold(owner, 9000000000L) }, reason: "Unity 管理员接口同步验证");
                var result = f.Host.Playback.ApplyAdmin(before.Revision, patch);
                Assert.IsTrue(result.Accepted, result.Error.ToString());
                Assert.AreEqual(before.Revision + 1, result.State.Revision);
                Assert.AreSame(result.State, f.Host.State);
                Assert.AreSame(result.State, f.Host.AuthorityState);
                Assert.AreEqual(5000000000L, f.Host.Player.Unit.Health);
                Assert.AreEqual(5000000000L, f.Host.Player.DisplayUnit.Health);
                Assert.AreEqual(3000000000L, f.Host.Player.Shields.TotalBlock);
                Assert.AreEqual(9000000000L, result.State.GoldFor(owner));
                CollectionAssert.AreEqual(kept, f.Hand.Cards.Select(card => card.Card.Id));
                Assert.AreEqual(before.Duel.Player(owner).Energy.Energy, result.State.Duel.Player(owner).Energy.Energy);
                Assert.AreEqual(before.Duel.CompletedRounds, result.State.Duel.CompletedRounds);
                Assert.AreEqual(before.Duel.Scores.Count, result.State.Duel.Scores.Count);
                Assert.IsNull(f.Host.LastPlayResolution);
                Assert.IsNull(f.Host.LastDiscardResolution);
            }
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
        public void BattleDemoBindsCurrentLocalRulesAndBuildsFromXmlOnly()
        {
            var active = SceneManager.GetActiveScene();
            const string path = "Assets/Scenes/BattleDemo.unity";
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var host = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SingleBattleSceneAdapter>(true)).Single();
                Assert.That(host.ContentXml, Is.SameAs(AssetDatabase.LoadAssetAtPath<TextAsset>(
                    "Assets/GameAssets/SingleBattle/Presentation/Scene3D/BattleDemoValidation.xml")));
                Assert.That(host.Enemies.Count, Is.EqualTo(3));
                Assert.That(host.ContentEncounterId, Is.EqualTo("standard-single-battle"));
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
        public void OpeningDiscardCanSelectAndDiscardAllEightCards()
        {
            using (var f = new Fixture())
            {
                f.Host.ConfigureContent(AssetDatabase.LoadAssetAtPath<TextAsset>("Packages/com.kaya.battle-engine/Content/StandardSingleBattle.xml"), "standard-single-battle");
                f.Host.StartBattle(); f.HandView.CompleteTransitions();
                var cards = f.Hand.Cards.ToArray();
                foreach (var card in cards)
                {
                    Assert.That(f.Hand.CanSelect(card.Id), Is.True);
                    f.Hand.SetInteraction(card.Id, new CardInteractionState(isSelected: true));
                }
                f.Hand.Publish();
                Assert.That(f.Hand.CaptureSelection().Cards.Count, Is.EqualTo(8));
                Assert.That(f.Host.DiscardBeforeDraw(false), Is.True);
                f.HandView.CompleteTransitions();
                Assert.That(f.Hand.Cards, Is.Empty);
                CollectionAssert.IsSubsetOf(cards.Select(card => card.Card.Id), f.Host.PlayerZone(CardZoneKinds.Discard).Cards);
                Assert.That(f.Host.DrawCards(5, false), Is.True);
                f.HandView.CompleteTransitions();
                Assert.That(f.Host.State.FlowFor(f.Host.Player.ParticipantId).Window, Is.EqualTo(BattlePlayerWindow.Action));
                Assert.That(f.HandView.MaxSelectedCards, Is.EqualTo(5));
            }
        }

        [Test]
        public void OpeningStageHidesScoresAndActionStageRestoresThem()
        {
            using (var f = new Fixture())
            {
                f.Host.ConfigureContent(AssetDatabase.LoadAssetAtPath<TextAsset>("Packages/com.kaya.battle-engine/Content/StandardSingleBattle.xml"), "standard-single-battle");
                f.Host.StartBattle(); f.HandView.CompleteTransitions();
                var root = new GameObject("Candidate");
                SceneManager.MoveGameObjectToScene(root, f.Scene);
                TMP_Text Label(string name)
                {
                    var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
                    go.transform.SetParent(root.transform, false);
                    var text = go.GetComponent<TextMeshProUGUI>();
                    text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansCJK/TMP/NotoSansCJKSCRegular.asset");
                    return text;
                }
                var title = Label("Title"); var details = Label("Details");
                var chips = Label("Chips").gameObject.AddComponent<RollingNumberText>();
                var mult = Label("Mult").gameObject.AddComponent<RollingNumberText>();
                var times = Label("Times").gameObject;
                chips.Configure("N0"); mult.Configure("0.##");
                var view = root.AddComponent<HandCandidatePresenter>();
                view.Configure(f.Host, title, details, chips, mult);
                view.ConfigurePhaseDisplay(chips.gameObject, mult.gameObject, times);
                view.Refresh();
                Assert.That(view.HandText, Is.EqualTo("弃牌阶段"));
                Assert.That(view.DetailText, Does.Not.Contain("计分"));
                Assert.That(chips.gameObject.activeSelf || mult.gameObject.activeSelf || times.activeSelf, Is.False);
                foreach (var card in f.Hand.Cards)
                    f.Hand.SetInteraction(card.Id, new CardInteractionState(isSelected: true));
                f.Hand.Publish(); view.Refresh();
                Assert.That(view.DetailText, Does.Contain("8 张"));
                Assert.That(f.Host.SkipDraw(false), Is.True);
                f.HandView.CompleteTransitions(); f.SelectOne(); view.Refresh();
                Assert.That(view.HandText, Is.Not.EqualTo("弃牌阶段"));
                Assert.That(view.DetailText, Does.Contain("计分"));
                Assert.That(chips.gameObject.activeSelf && mult.gameObject.activeSelf && times.activeSelf, Is.True);
                f.Host.RestartBattle(); f.HandView.CompleteTransitions(); view.Refresh();
                Assert.That(view.HandText, Is.EqualTo("弃牌阶段"));
                Assert.That(times.activeSelf, Is.False);
            }
        }

        [TestCase(5)]
        [TestCase(3)]
        public void ActionSelectionUsesRuleLimitAndUnlocksAfterDeselecting(int limit)
        {
            using (var f = new Fixture())
            {
                var xml = AssetDatabase.LoadAssetAtPath<TextAsset>("Packages/com.kaya.battle-engine/Content/StandardSingleBattle.xml").text;
                f.Host.ConfigureContent(f.Content(xml.Replace("maxPlayCards=\"5\"", "maxPlayCards=\"" + limit + "\"")), "standard-single-battle");
                f.Host.StartBattle(); f.HandView.CompleteTransitions();
                foreach (var card in f.Hand.Cards)
                    f.Hand.SetInteraction(card.Id, new CardInteractionState(isSelected: true));
                f.Hand.Publish();
                Assert.That(f.Hand.CaptureSelection().Cards.Count, Is.EqualTo(8));
                Assert.That(f.Host.SkipDraw(false), Is.True);
                f.HandView.CompleteTransitions();
                Assert.That(f.HandView.MaxSelectedCards, Is.EqualTo(limit));
                Assert.That(f.Hand.CaptureSelection().Cards, Is.Empty, "Discard-stage selections exceeding the play limit are cleared.");
                foreach (var card in f.Hand.Cards.Take(limit))
                    f.Hand.SetInteraction(card.Id, new CardInteractionState(isSelected: true));
                var extra = f.Hand.Cards[limit];
                Assert.That(f.Hand.CanSelect(extra.Id), Is.False);
                f.Hand.SetInteraction(extra.Id, new CardInteractionState(isSelected: true));
                Assert.That(f.Hand.CaptureSelection().Cards.Count, Is.EqualTo(limit));
                f.Hand.SetInteraction(f.Hand.Cards[0].Id, default);
                Assert.That(f.Hand.CanSelect(extra.Id), Is.True);
                f.Hand.SetInteraction(extra.Id, new CardInteractionState(isSelected: true));
                Assert.That(f.Hand.CaptureSelection().Cards.Count, Is.EqualTo(limit));
                f.Host.RestartBattle(); f.HandView.CompleteTransitions();
                Assert.That(f.HandView.MaxSelectedCards, Is.EqualTo(8));
            }
        }

        [Test]
        public void StandardXmlDrawAndDefenseCommandsDriveTheSceneHand()
        {
            using (var f = new Fixture())
            {
                f.Host.ConfigureContent(AssetDatabase.LoadAssetAtPath<TextAsset>("Packages/com.kaya.battle-engine/Content/StandardSingleBattle.xml"), "standard-single-battle");
                f.Host.StartBattle(); f.HandView.CompleteTransitions();
                Assert.That(f.Hand.Cards.Count, Is.EqualTo(8));
                Assert.That(f.Host.SkipDraw(false), Is.True); f.HandView.CompleteTransitions();
                Assert.That(f.Host.State.FlowFor(f.Host.Player.ParticipantId).Window, Is.EqualTo(BattlePlayerWindow.Action));
                var discarded = f.SelectOne();
                Assert.That(f.Host.Player.Discard(discarded, false), Is.EqualTo(1)); f.HandView.CompleteTransitions();
                Assert.That(f.Host.State.Duel.CompletedRounds, Is.EqualTo(1));
                Assert.That(f.Hand.Cards.Count, Is.EqualTo(7));
                Assert.That(f.Host.State.FlowFor(f.Host.Player.ParticipantId).Window, Is.EqualTo(BattlePlayerWindow.Draw));
                Assert.That(f.Host.SkipDraw(false), Is.True);
                Assert.That(f.Hand.Cards.Count, Is.EqualTo(7));
                Assert.That(f.Host.State, Is.SameAs(f.Host.AuthorityState));
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void CompatibleRulePreviewAndScenePlaybackUseOneHighestScore(bool straightFlush)
        {
            using (var f = new Fixture())
            {
                var doc = XDocument.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(
                    "Packages/com.kaya.battle-engine/Content/StandardSingleBattle.xml").text);
                int[] ranks = straightFlush ? new[] { 2, 3, 4, 5, 6 } : new[] { 7, 7, 7, 8, 8 };
                doc.Root.Element("Decks").Elements().First().ReplaceNodes(ranks.Select((rank, i) => new XElement("Card",
                    new XAttribute("id", "c" + i), new XAttribute("face", "standard:" + (straightFlush ? 1 : i % 4 + 1) + ":" + rank))));
                var foe = doc.Root.Element("Enemies").Elements().First(e => (string)e.Attribute("id") == "rotator");
                foe.SetAttributeValue("maxHealth", 10000);
                f.Host.ConfigureContent(f.Content(doc.ToString()), "standard-single-battle");
                f.Host.StartBattle(); f.HandView.CompleteTransitions();
                Assert.IsTrue(f.Host.SkipDraw(false)); f.HandView.CompleteTransitions();
                foreach (var card in f.Hand.Cards)
                    f.Hand.SetInteraction(card.Id, new CardInteractionState(isSelected: true));
                f.Hand.Publish(); f.Actions.Refresh(); f.Host.RefreshCandidate();
                var before = f.Host.AuthorityState;
                var candidate = f.Host.Candidate;
                Assert.AreEqual(straightFlush ? "StraightFlush" : "FullHouse", candidate.PatternKey);
                Assert.AreEqual(straightFlush ? 160 : 148, candidate.Total);
                StringAssert.Contains(straightFlush ? "同花" : "三条", candidate.Labels["additionalMechanisms"]);
                StringAssert.Contains(straightFlush ? "顺子" : "一对", candidate.Labels["additionalMechanisms"]);
                Assert.AreSame(before, f.Host.AuthorityState, "A candidate preview must not mutate authority.");
                Assert.AreEqual(5, f.Host.Player.Play(f.Hand.CaptureSelection(), false));
                f.HandView.CompleteTransitions();
                Assert.AreEqual(10000 - candidate.Total, f.Host.Enemy.Unit.Health,
                    "Compatible effects must not duplicate the highest hand's damage budget.");
                Assert.That(f.Host.AuthorityState.Duel.Combat.Statuses.Any(s =>
                    s.DefinitionId.Value == (straightFlush ? "bleed" : "stun")), Is.True);
                Assert.AreSame(f.Host.AuthorityState, f.Host.State);
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
