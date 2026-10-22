using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using CardPresentation;
using global::BattlePresentation;
using NUnit.Framework;
using Poker.BattlePresentation;
using PokerEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Poker.SingleBattle.Tests
{
    public sealed class BattleCardEffectBridgeTests
    {
        private static readonly ParticipantId Player = new ParticipantId("player");
        private sealed class Frontend : IBattlePresentationFrontend
        {
            public PresentationBatch Batch;
            public bool IsPresenting => false;
            public void Initialize(BattleDisplaySnapshot snapshot) { }
            public IPresentationOperation Present(PresentationBatch batch, PlaybackMode mode) { Batch = batch; return PresentationOperation.Completed(); }
            public void Synchronize(BattleDisplaySnapshot snapshot) { }
        }
        private sealed class Markers : IMarkerSink
        { internal readonly List<string> Values = new List<string>(); public bool Emit(string marker) { Values.Add(marker); return true; } }
        private sealed class Fixture : IDisposable
        {
            internal readonly Scene Scene = EditorSceneManager.NewPreviewScene();
            internal readonly GameObject Root;
            internal readonly BattleCardEffectBridge Bridge;
            internal readonly BattleSession Session;
            internal readonly BattleSessionResult Result;
            internal readonly PresentationBatch Batch;
            internal Fixture(int[] ranks, int enemies = 1, bool flush = false, int health = 100000, bool chooseTarget = false)
            {
                Root = new GameObject("Scene effect bridge test"); SceneManager.MoveGameObjectToScene(Root, Scene);
                Bridge = Root.AddComponent<BattleCardEffectBridge>();
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>("Packages/com.kaya.battle-engine/Content/StandardSingleBattle-2026-10-21.xml");
                var xml = XDocument.Parse(asset.text);
                xml.Root.Element("Decks").Elements().First().ReplaceNodes(ranks.Select((rank, index) => new XElement("Card",
                    new XAttribute("id", "c" + index), new XAttribute("face", "standard:" + (flush ? 1 : index % 4 + 1) + ":" + rank))));
                var enemy = xml.Root.Element("Enemies").Elements().First();
                enemy.RemoveNodes(); enemy.SetAttributeValue("maxHealth", health); enemy.SetAttributeValue("damage", 0);
                var encounter = xml.Root.Element("Encounters").Elements().First();
                for (int index = 1; index < enemies; index++) encounter.Add(new XElement("Enemy", new XAttribute("id", "enemy-" + index), new XAttribute("ref", "rotator")));
                var compiled = BattleContentCompiler.CompileXml(xml.ToString());
                Assert.IsTrue(compiled.Success, string.Join(";", compiled.Diagnostics.Select(item => item.Message)));
                var built = BattleContentFactory.Create(compiled.Content, new BattleSetupRequest("standard-single-battle", "visual-cues", HmacDeckShuffler.FromSeed(new byte[32])));
                Assert.IsTrue(built.Success);
                Session = built.Session;
                Assert.IsTrue(Session.Execute(0, Player, CardDuelAction.Start).Accepted);
                var before = Session.State;
                Result = Session.Execute(before.Revision, Player, CardDuelAction.Play, before.Zone(CardZoneKinds.Hand, Player).Cards,
                    chooseTarget ? new[] { new ParticipantId("enemy") } : null);
                Assert.IsTrue(Result.Accepted, Result.Error.ToString());
                Bridge.Prepare(Result, Player);
                var frontend = new Frontend();
                var shared = new BattlePresentationBridge(Session.Engine, Player, frontend);
                shared.Initialize(before); shared.Present(Result, true); Batch = frontend.Batch;
            }
            internal BattleDisplayChange[] Attacks => Batch.Changes.Where(change => Bridge.TryGet(change.Sequence, out _)).ToArray();
            public void Dispose() => EditorSceneManager.ClosePreviewScene(Scene);
        }

        [TestCase(false, false)][TestCase(true, false)][TestCase(false, true)]
        public void StraightUsesOneAuthoritativeCardPerHitIncludingRetargeting(bool retarget, bool flush)
        {
            using (var fixture = new Fixture(new[] { 6, 2, 5, 3, 4 }, retarget ? 3 : 1, flush, health: retarget ? 3 : 100000))
            {
                var attacks = fixture.Attacks; Assert.AreEqual(retarget ? 3 : 5, attacks.Length);
                var cards = new List<string>();
                foreach (var change in attacks)
                {
                    Assert.IsTrue(fixture.Bridge.TryGet(change.Sequence, out var cue));
                    Assert.IsTrue(cue.Flight.Sequential);
                    Assert.AreEqual(attacks.Length, cue.Flight.Shots.Count);
                    var shot = cue.Flight.Shots.Single(item => item.CueId == cue.Id); cards.Add(shot.CardId);
                    Assert.AreEqual(cards.Count - 1, shot.Order);
                    fixture.Bridge.TryGet(attacks[0].Sequence, out var first);
                    Assert.AreSame(first.Flight, cue.Flight);
                    Assert.AreEqual(change.Effect.TargetId, shot.TargetId);
                }
                var ranks = cards.Select(id => (int)fixture.Session.Engine.Face(fixture.Result.Before.Table.GetCard(new CardInstanceId(id))).Rank);
                CollectionAssert.AreEqual(retarget ? new[] { 2, 3, 4 } : new[] { 2, 3, 4, 5, 6 }, ranks);
                if (retarget) CollectionAssert.AreEqual(new[] { "enemy", "enemy-1", "enemy-2" }, attacks.Select(change => change.Effect.TargetId));
                Assert.AreEqual(cards.Count, cards.Distinct().Count());
                foreach (var change in fixture.Batch.Changes.Where(change => change.Effect?.Kind != "Damage"))
                    Assert.IsFalse(fixture.Bridge.TryGet(change.Sequence, out _));
                Assert.AreSame(fixture.Result.State, fixture.Session.State, "Building visual cues cannot mutate authority.");
            }
        }

        [Test]
        public void PairKeepsOnlyItsAuthoritativeScoringCardsInFlightAndDropsItsKickersOnce()
        {
            using (var fixture = new Fixture(new[] { 7, 7, 2, 4, 9 }))
            {
                Assert.AreEqual(1, fixture.Attacks.Length);
                fixture.Bridge.TryGet(fixture.Attacks[0].Sequence, out var cue);
                Assert.AreEqual(2, cue.Flight.Shots.Count); Assert.AreEqual(3, cue.Flight.UnscoredCards.Count);
                var pattern = fixture.Result.Steps.First(step => step.Transition.Pattern != null).Transition.Pattern;
                CollectionAssert.AreEquivalent(pattern.ScoringCards.Select(card => card.Value), cue.Flight.Shots.Select(shot => shot.CardId));
                CollectionAssert.AreEquivalent(pattern.SubmittedCards.Except(pattern.ScoringCards).Select(card => card.Value), cue.Flight.UnscoredCards);
            }
        }

        [TestCase(3)][TestCase(4)]
        public void GroupAttackSharesOneFlightWithBranchesForEveryActualTarget(int count)
        {
            using (var fixture = new Fixture(Enumerable.Repeat(7, count).ToArray(), 3))
            {
                Assert.AreEqual(3, fixture.Attacks.Length);
                fixture.Bridge.TryGet(fixture.Attacks[0].Sequence, out var first);
                fixture.Bridge.TryGet(fixture.Attacks[1].Sequence, out var second);
                Assert.AreSame(first.Flight, second.Flight);
                Assert.AreEqual(count * 3, first.Flight.Shots.Count);
                CollectionAssert.AreEquivalent(new[] { "enemy", "enemy-1", "enemy-2" }, first.Flight.Shots.Select(shot => shot.TargetId).Distinct());
                Assert.IsTrue(first.Flight.Shots.GroupBy(shot => shot.CardId).All(group => group.Count() == 3));
            }
        }

        [Test]
        public void FullHouseSeparatesTripleGroupAndRequestedPairWithoutSendingControlAsCards()
        {
            using (var fixture = new Fixture(new[] { 3, 3, 3, 2, 2 }, 2, chooseTarget: true))
            {
                var waves = fixture.Attacks.Select(change => { fixture.Bridge.TryGet(change.Sequence, out var cue); return cue.Flight; }).Distinct().ToArray();
                Assert.AreEqual(2, waves.Length);
                Assert.AreEqual(6, waves.Single(wave => wave.Category == "ThreeOfAKind").Shots.Count);
                var pair = waves.Single(wave => wave.Category == "OnePair"); Assert.AreEqual(2, pair.Shots.Count);
                Assert.IsTrue(pair.Shots.All(shot => shot.TargetId == "enemy"));
                foreach (var change in fixture.Batch.Changes.Where(change => change.Effect == null || change.Effect.Kind != "Damage"))
                    Assert.IsFalse(fixture.Bridge.TryGet(change.Sequence, out _));
            }
        }

        [Test]
        public void RoyalEliminationFansToAllTargetsAndKeepsHealingOutOfTheVolley()
        {
            using (var fixture = new Fixture(new[] { 10, 11, 12, 13, 14 }, 2, true))
            {
                Assert.AreEqual(2, fixture.Attacks.Length);
                foreach (var change in fixture.Attacks)
                {
                    Assert.AreEqual("Eliminate", change.Effect.Kind);
                    fixture.Bridge.TryGet(change.Sequence, out var cue); Assert.IsTrue(cue.Flight.Royal);
                    Assert.AreEqual(10, cue.Flight.Shots.Count);
                }
                foreach (var change in fixture.Batch.Changes.Where(change => change.Effect?.Kind == "Heal"))
                    Assert.IsFalse(fixture.Bridge.TryGet(change.Sequence, out _));
            }
        }

        [TestCase(false)][TestCase(true)]
        public void SceneFlightSpawnsAllTargetBranchesOnceAndOwnsRoyalImpactAndCancellation(bool royal)
        {
            using (var fixture = new Fixture(royal ? new[] { 10, 11, 12, 13, 14 } : new[] { 7, 7, 7 }, 2, royal))
            {
                var handRoot = new GameObject("Hand"); handRoot.transform.SetParent(fixture.Root.transform);
                var hand = handRoot.AddComponent<HandZoneView>();
                hand.Configure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameAssets/CardArt/Prefabs/Card.prefab"), CardZoneLayoutSettings.Default);
                var cameraRoot = new GameObject("Camera"); cameraRoot.transform.SetParent(fixture.Root.transform); cameraRoot.transform.position = new Vector3(0, 0, -10);
                hand.SetCameraFacing(true, cameraRoot.AddComponent<Camera>());
                var world = handRoot.AddComponent<WorldCardZonePresentation>();
                var zone = fixture.Batch.Initial.Zones.Single(item => item.Kind == "hand" && item.OwnerId == "player"); world.Synchronize(zone);
                var actionRoot = new GameObject("Flight"); actionRoot.transform.SetParent(fixture.Root.transform);
                var action = actionRoot.AddComponent<BattleCardProjectileAction>();
                action.Configure(world, AssetDatabase.LoadAssetAtPath<Material>("Assets/GameAssets/SingleBattle/Presentation/Scene3D/CardProjectileTail.mat"));
                var routines = new List<IEnumerator>(); action.DeltaTimeProvider = () => .02f;
                action.StartPresentationRoutine = routine => { routines.Add(routine); return () => routines.Remove(routine); };
                var resolver = new DictionaryTargetResolver();
                for (int index = 0; index < 2; index++)
                { var target = new GameObject("Target" + index); target.transform.SetParent(fixture.Root.transform); target.transform.position = new Vector3(index * 4 - 2, 2, 0); resolver.Bind(index == 0 ? "enemy" : "enemy-1", target.transform); }
                using (var firstScope = new PresentationScope("first"))
                using (var secondScope = new PresentationScope("second"))
                {
                    var first = fixture.Attacks[0]; var second = fixture.Attacks[1];
                    var markers = new Markers();
                    var op = action.Play(new ActionContext(fixture.Batch.Id, 1, "first", first, first.Before, resolver, firstScope), markers);
                    Assert.AreEqual(PresentationStatus.Pending, op.Status, op.Error?.ToString());
                    Assert.AreEqual(royal ? 10 : 6, action.GetComponentsInChildren<TrailRenderer>().Length);
                    for (int frame = 0; frame < 60 && !markers.Values.Contains("Impact"); frame++)
                        foreach (var routine in routines.ToArray()) if (!routine.MoveNext()) routines.Remove(routine);
                    CollectionAssert.Contains(markers.Values, "Impact");
                    if (royal) Assert.AreEqual(2, action.GetComponentsInChildren<LineRenderer>().Length);
                    var otherMarkers = new Markers();
                    var other = action.Play(new ActionContext(fixture.Batch.Id, 1, "second", second, second.Before, resolver, secondScope), otherMarkers);
                    Assert.AreNotEqual(PresentationStatus.Failed, other.Status, other.Error?.ToString());
                    Assert.AreEqual(royal ? 10 : 6, action.GetComponentsInChildren<TrailRenderer>().Length, "Later target facts reuse the original flight, without respawning cards.");
                    CollectionAssert.Contains(otherMarkers.Values, "Impact");
                    world.Synchronize(zone);
                    Assert.IsEmpty(action.GetComponentsInChildren<TrailRenderer>());
                    Assert.IsEmpty(action.GetComponentsInChildren<LineRenderer>());
                    Assert.IsTrue(hand.Views.All(card => !card.Surface.GetComponent<Renderer>().forceRenderingOff));
                    Assert.AreEqual(0, routines.Count);
                }
            }
        }

        [TestCase(false)][TestCase(true)]
        public void NonScoringCardsMatchHandDiscardPoseTimingAndOrderBeforeReleasingThePlay(bool prepared)
        {
            using (var fixture = new Fixture(new[] { 7, 7, 2, 4, 9 }))
            {
                var cameraRoot = new GameObject("Camera"); cameraRoot.transform.SetParent(fixture.Root.transform);
                cameraRoot.transform.position = new Vector3(0, 0, -10);
                var camera = cameraRoot.AddComponent<Camera>();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameAssets/CardArt/Prefabs/Card.prefab");
                HandZoneView CreateHand(string name)
                {
                    var root = new GameObject(name); root.transform.SetParent(fixture.Root.transform);
                    root.transform.position = new Vector3(1, 2, 3); root.transform.localScale = Vector3.one * 1.7f;
                    var result = root.AddComponent<HandZoneView>(); result.Configure(prefab, CardZoneLayoutSettings.Default);
                    var motion = result.MotionSettings; motion.Enabled = false; result.SetMotionSettings(motion);
                    result.SetCameraFacing(true, camera);
                    result.SetDiscardSettings(new CardZoneDiscardSettings { FlightSeconds = .6f, Interval = .15f, FlightLift = .035f });
                    return result;
                }
                var hand = CreateHand("Played hand"); var reference = CreateHand("Ordinary discard");
                var world = hand.gameObject.AddComponent<WorldCardZonePresentation>();
                var zone = fixture.Batch.Initial.Zones.Single(item => item.Kind == "hand" && item.OwnerId == "player");
                world.Synchronize(new CardZoneDisplayData(zone.Id, zone.OwnerId, zone.Kind, zone.Cards.Reverse()));
                if (prepared)
                    hand.Present(new CardZoneViewData(hand.Data.ZoneId, hand.Data.Cards.Select(card => new CardVisualData(
                        card.ViewId, card.FaceArtId, card.CardId, card.Tint, card.Effects, card.IsDisabled,
                        new CardInteractionState(isSelected: true), card.ArtBindings))), false);
                reference.Present(hand.Data, false);
                if (prepared)
                {
                    var ids = hand.Data.Cards.Select(card => card.ViewId).ToArray();
                    hand.PresentSelectionAction(ids, _ => { }); reference.PresentSelectionAction(ids, _ => { });
                    hand.AdvanceMotion(.09f); reference.AdvanceMotion(.09f);
                    Assert.Greater(hand.Views[0].InteractionOffset.magnitude, 0);
                }
                fixture.Bridge.TryGet(fixture.Attacks[0].Sequence, out var cue);
                var unscored = hand.Views.Where(card => cue.Flight.UnscoredCards.Contains(card.Data.CardId.Value.Value)).Select(card => card.ViewId).ToArray();
                Assert.AreEqual(3, unscored.Length);
                var remaining = new CardZoneViewData(hand.Data.ZoneId, hand.Data.Cards.Where(card => !unscored.Contains(card.ViewId)));
                var flight = new GameObject("Flight"); flight.transform.SetParent(fixture.Root.transform);
                var action = flight.AddComponent<BattleCardProjectileAction>();
                action.Configure(world, AssetDatabase.LoadAssetAtPath<Material>("Assets/GameAssets/SingleBattle/Presentation/Scene3D/CardProjectileTail.mat"));
                var routines = new List<IEnumerator>(); action.DeltaTimeProvider = () => .02f;
                action.StartPresentationRoutine = routine => { routines.Add(routine); return () => routines.Remove(routine); };
                var target = new GameObject("Target"); target.transform.SetParent(fixture.Root.transform); target.transform.position = Vector3.up * 2;
                var resolver = new DictionaryTargetResolver(); resolver.Bind("enemy", target.transform);
                using (var scope = new PresentationScope("unscored-discard"))
                {
                    var attack = fixture.Attacks[0]; var markers = new Markers();
                    var operation = action.Play(new ActionContext(fixture.Batch.Id, 1, "play", attack, attack.Before, resolver, scope), markers);
                    Assert.AreEqual(PresentationStatus.Pending, operation.Status, operation.Error?.ToString());
                    reference.Present(remaining, unscored, true);
                    var ghosts = action.GetComponentsInChildren<MeshRenderer>().Where(renderer => renderer.name.StartsWith("Unscored Card "))
                        .ToDictionary(renderer => renderer.name.Substring("Unscored Card ".Length));
                    CollectionAssert.AreEqual(unscored, ghosts.Keys);
                    Assert.AreEqual(2, action.GetComponentsInChildren<TrailRenderer>().Length);
                    for (int frame = 0; frame < 48; frame++)
                    {
                        foreach (var routine in routines.ToArray()) if (!routine.MoveNext()) routines.Remove(routine);
                        reference.AdvanceMotion(frame == 0 ? 0 : .02f);
                        foreach (var card in reference.DepartingViews)
                        {
                            var ghost = ghosts[card.ViewId];
                            Assert.IsTrue(ghost.enabled);
                            Assert.Less(Vector3.Distance(card.Surface.transform.position, ghost.transform.position), .0001f, "Discard position: frame " + frame);
                            Assert.Less(Quaternion.Angle(card.Surface.transform.rotation, ghost.transform.rotation), .05f, "Discard flip: frame " + frame);
                        }
                        if (frame < 44) CollectionAssert.DoesNotContain(markers.Values, "Impact", "The slow final discard must finish before the hand can compact.");
                    }
                    CollectionAssert.Contains(markers.Values, "Impact");
                    Assert.IsEmpty(reference.DepartingViews);
                    world.Synchronize(zone);
                    Assert.IsEmpty(action.GetComponentsInChildren<MeshRenderer>());
                    Assert.IsTrue(hand.Views.All(card => !card.Surface.GetComponent<Renderer>().forceRenderingOff));
                }
            }
        }
    }
}
