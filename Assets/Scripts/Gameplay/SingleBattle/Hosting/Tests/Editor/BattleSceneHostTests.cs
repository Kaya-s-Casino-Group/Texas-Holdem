using System;
using System.Linq;
using NUnit.Framework;
using Poker.BattleHosting.Editor;
using Poker.BattlePresentation;
using PokerEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using global::BattlePresentation.Unity;

namespace Poker.BattleHosting.Tests
{
    public sealed class BattleSceneHostTests
    {
        private Scene scene;
        [SetUp] public void OpenPreview() => scene = EditorSceneManager.NewPreviewScene();
        [TearDown] public void ClosePreview() { if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene); }
        private BattleSceneHost Instance()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BattleSceneHostSetup.PrefabPath);
            Assert.NotNull(prefab, "The shipped minimal prefab must exist before testing.");
            return ((GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)).GetComponent<BattleSceneHost>();
        }
        [Test] public void MinimalPrefabStartsWithoutActorsCardsUiOrCamera()
        {
            var host = Instance();
            Assert.AreEqual(0, host.transform.childCount);
            CollectionAssert.AreEquivalent(new[] { typeof(Transform), typeof(BattleSceneHost), typeof(XmlBattleSessionSource),
                typeof(BattleTimelineSceneAdapter), typeof(ScenePresentationDirector) }, host.GetComponents<Component>().Select(c => c.GetType()));
            host.StartBattle();
            var adapter = (BattleTimelineSceneAdapter)host.SceneAdapter;
            Assert.IsTrue(host.AuthorityState.HasStarted);
            Assert.AreEqual(BattlePlayerWindow.Draw, host.AuthorityState.FlowFor(adapter.ObserverId).Window);
            Assert.AreEqual(0, adapter.Display.Zones.Single(z => z.Kind == CardZoneKinds.Hand && z.OwnerId == adapter.ObserverId.Value).Cards.Count,
                "The current XML waits for a manual draw; attaching a frontend must not deal cards.");
            Assert.IsTrue(adapter.CanAct);
            Assert.IsFalse(host.IsPresenting);
            var count = host.AuthorityState.Duel.Player(adapter.ObserverId).Rules.Flow.DrawCount;
            Assert.IsTrue(adapter.SubmitAction(CardDuelAction.Draw, drawCount: count, animate: false).Accepted);
            Assert.AreEqual(BattlePlayerWindow.Action, host.AuthorityState.FlowFor(adapter.ObserverId).Window);
            Assert.AreEqual(count, adapter.Display.Zones.Single(z => z.Kind == CardZoneKinds.Hand && z.OwnerId == adapter.ObserverId.Value).Cards.Count);
            Assert.IsFalse(typeof(BattleSceneHost).Assembly.GetReferencedAssemblies().Any(a => a.Name.Contains("SingleBattle") || a.Name.Contains("CardPresentation")));
        }
        [Test] public void TwoBattleInstancesKeepSessionsDisplaysSelectionsAndCommandsIndependent()
        {
            var first = Instance(); var second = Instance(); first.StartBattle(); second.StartBattle();
            var a = (BattleTimelineSceneAdapter)first.SceneAdapter; var b = (BattleTimelineSceneAdapter)second.SceneAdapter;
            Assert.AreNotSame(first.Session, second.Session);
            Assert.AreNotEqual(first.AuthorityState.Table.MatchId, second.AuthorityState.Table.MatchId);
            var untouched = second.AuthorityState; var secondDisplay = b.Display;
            Assert.IsTrue(a.SubmitAction(CardDuelAction.Draw,
                drawCount: first.AuthorityState.Duel.Player(a.ObserverId).Rules.Flow.DrawCount, animate: false).Accepted);
            a.SelectCards(first.AuthorityState.Zone(CardZoneKinds.Hand, a.ObserverId).Cards.Take(1).Select(id => id.Value));
            Assert.IsTrue(a.Candidate.CanPlay); Assert.AreEqual(0, b.Candidate.SelectedIds.Count);
            Assert.IsTrue(a.PlaySelected(false).Accepted);
            Assert.AreSame(untouched, second.AuthorityState); Assert.AreSame(secondDisplay, b.Display);
            first.RestartBattle(); Assert.AreSame(untouched, second.AuthorityState);
        }
        [Test] public void AttachDoesNotStartAndSynchronizeDoesNotExecuteRules()
        {
            var host = Instance(); var source = (XmlBattleSessionSource)host.SessionSource;
            var session = source.Build("attached-battle", HmacDeckShuffler.FromSeed(new byte[32]));
            host.AttachSession(session);
            Assert.AreSame(session, host.Session); Assert.IsFalse(session.State.HasStarted);
            host.Playback.Start(false); var state = host.AuthorityState;
            host.SynchronizeBattle(); Assert.AreSame(state, host.AuthorityState);
            host.RestartBattle(); Assert.AreNotSame(session, host.Session); Assert.AreSame(state, session.State);
        }
        [Test] public void InvalidSourceLeavesCurrentSessionIntactAndReportsDiagnostics()
        {
            var host = Instance(); host.StartBattle(); var session = host.Session;
            var source = (XmlBattleSessionSource)host.SessionSource; var oldXml = source.ContentXml; var oldId = source.EncounterId;
            source.Configure(oldXml, "missing-encounter");
            Assert.Throws<InvalidOperationException>(() => host.RestartBattle());
            Assert.AreSame(session, host.Session); Assert.IsNotEmpty(source.Diagnostics);
            source.Configure(oldXml, oldId); host.RestartBattle(); Assert.AreNotSame(session, host.Session);
        }
        [Test] public void AnotherBattleCannotBorrowSceneServices()
        {
            var first = Instance(); var second = Instance();
            Assert.Throws<InvalidOperationException>(() => second.Configure(first.SessionSource, first.SceneAdapter));
            Assert.AreNotSame(first.SceneAdapter, second.SceneAdapter);
            first.StartBattle(); second.StartBattle();
        }
        [Test] public void DisabledHostCannotAcceptCommandsOrRestartUntilEnabled()
        {
            var host = Instance(); host.StartBattle(); var adapter = (BattleTimelineSceneAdapter)host.SceneAdapter;
            var state = host.AuthorityState; host.enabled = false;
            Assert.IsFalse(adapter.CanAct); host.RestartBattle(); Assert.AreSame(state, host.AuthorityState);
            Assert.Throws<InvalidOperationException>(() => adapter.EndTurn());
            host.enabled = true; Assert.IsTrue(adapter.CanAct); Assert.IsTrue(adapter.EndTurn(false).Accepted);
        }
        [Test] public void AddToolIsIdempotentAndExplicitNewBattleDoesNotReplaceExistingObjects()
        {
            var unrelated = new GameObject("Existing Scene Object"); SceneManager.MoveGameObjectToScene(unrelated, scene);
            var first = BattleSceneHostSetup.AddToScene(scene);
            Assert.AreSame(first, BattleSceneHostSetup.AddToScene(scene));
            var second = BattleSceneHostSetup.AddToScene(scene, null, true);
            Assert.AreNotSame(first, second); Assert.NotNull(unrelated);
            Assert.AreSame(first, BattleSceneHostSetup.AddToScene(scene, first.gameObject));
            Assert.Throws<InvalidOperationException>(() => BattleSceneHostSetup.AddToScene(scene));
            Assert.AreEqual(2, scene.GetRootGameObjects().Count(go => go.GetComponent<BattleSceneHost>() != null));
        }
    }
}
