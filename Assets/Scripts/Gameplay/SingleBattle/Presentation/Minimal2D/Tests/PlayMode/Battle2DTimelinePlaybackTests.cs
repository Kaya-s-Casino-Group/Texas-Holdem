using System.Collections;
using System.Linq;
using global::BattlePresentation;
using global::BattlePresentation.Unity;
using NUnit.Framework;
using Poker.BattleHosting;
using PokerEngine;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Poker.Battle2DValidation.Tests
{
    public sealed class Battle2DTimelinePlaybackTests
    {
        private static IEnumerator Open()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Battle2DValidation.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
        }
        private static IEnumerator Idle(Battle2DView view)
        {
            float until = Time.realtimeSinceStartup + 10;
            while (view.IsPresenting && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsFalse(view.IsPresenting); Assert.IsNull(view.SceneDirector.LastError);
        }
        private static void Match(Battle2DSceneAdapter adapter)
        {
            var state = adapter.AuthorityState; var view = adapter.View;
            Assert.AreEqual(state.Revision, view.PresentedState.RuleRevision);
            CollectionAssert.AreEqual(state.Zone(CardZoneKinds.Hand, adapter.PlayerId).Cards.Select(id => id.Value), view.Cards.Select(c => c.Id));
            foreach (var unit in view.PresentedState.Units)
                Assert.AreEqual(state.Duel.Combat.Get(new ParticipantId(unit.Id)).Health, unit.Health);
        }
        [UnityTest] public IEnumerator XmlSceneSelectionCommandsCancellationAndRestartUseOneHost()
        {
            yield return Open();
            var adapter = Object.FindObjectsOfType<Battle2DSceneAdapter>().Single(); var host = adapter.SceneHost; var view = adapter.View;
            Assert.AreSame(adapter, host.SceneAdapter); Assert.IsInstanceOf<XmlBattleSessionSource>(host.SessionSource);
            Assert.AreEqual("single-battle-new", ((XmlBattleSessionSource)host.SessionSource).EncounterId);
            yield return Idle(view); Assert.IsTrue(host.AuthorityState.HasStarted); Match(adapter);
            var before = host.AuthorityState; var ids = view.Cards.Take(2).Select(c => c.Id).ToArray();
            view.ToggleSelection(ids[0]); var version = view.Candidate.SelectionVersion;
            view.ToggleSelection(ids[1]); Assert.Greater(view.Candidate.SelectionVersion, version);
            view.ToggleSelection(ids[0]); view.ToggleSelection(ids[1]);
            Assert.IsEmpty(view.Selected); Assert.AreSame(before, host.AuthorityState);
            view.ToggleSelection(view.Cards[0].Id); view.PlayButton.onClick.Invoke();
            var committed = host.AuthorityState; Assert.AreNotSame(before, committed); Assert.IsTrue(view.IsPresenting);
            Assert.IsFalse(view.PlayButton.interactable); yield return Idle(view);
            Assert.AreSame(committed, host.AuthorityState); Match(adapter);
            view.ToggleSelection(view.Cards[0].Id); view.DiscardButton.onClick.Invoke();
            yield return Idle(view); Match(adapter);
            var enemyHome = view.EnemyActor.localPosition;
            view.EndTurnButton.onClick.Invoke(); committed = host.AuthorityState;
            Assert.IsTrue(view.IsPresenting); yield return Idle(view);
            Assert.AreSame(committed, host.AuthorityState); Match(adapter); Assert.AreEqual(enemyHome, view.EnemyActor.localPosition);
            view.EndTurnButton.onClick.Invoke(); committed = host.AuthorityState;
            Assert.IsTrue(view.IsPresenting); view.enabled = false;
            Assert.IsFalse(view.IsPresenting); Assert.AreSame(committed, host.AuthorityState); Match(adapter);
            view.enabled = true; yield return null;
            Assert.IsEmpty(view.Selected); Assert.IsTrue(view.EndTurnButton.interactable);
            host.enabled = false; Assert.IsFalse(view.EndTurnButton.interactable);
            Assert.Throws<System.InvalidOperationException>(() => adapter.EndTurn());
            host.enabled = true; yield return null;
            view.RestartButton.onClick.Invoke(); yield return Idle(view);
            Assert.AreNotSame(committed, host.AuthorityState); Assert.AreEqual(0, host.AuthorityState.Duel.CompletedRounds); Match(adapter);
        }

        [UnityTest] public IEnumerator XmlEnemyQueueSharesTheMinimalRectangleWithoutOverlappingActions()
        {
            yield return Open();
            var adapter = Object.FindObjectsOfType<Battle2DSceneAdapter>().Single(); var view = adapter.View;
            yield return Idle(view);
            var source = (XmlBattleSessionSource)adapter.SceneHost.SessionSource;
            var original = source.ContentXml;
            const string enemy = "<Enemy id=\"enemy\" ref=\"guardian\"/>";
            var content = new TextAsset(original.text.Replace(enemy, enemy + "<Enemy id=\"enemy-2\" ref=\"guardian\"/>"));
            try
            {
                source.Configure(content, source.EncounterId); adapter.SceneHost.RestartBattle(); yield return Idle(view);
                Assert.AreEqual(2, adapter.Session.State.Duel.Enemies.Count);
                var home = view.EnemyActor.localPosition;
                view.EndTurnButton.onClick.Invoke(); var committed = adapter.AuthorityState;
                yield return Idle(view);
                Assert.AreEqual(1, committed.Duel.CompletedRounds); Assert.AreSame(committed, adapter.AuthorityState);
                Assert.AreEqual(home, view.EnemyActor.localPosition); Match(adapter);
            }
            finally { source.Configure(original, source.EncounterId); Object.Destroy(content); }
        }

        [UnityTest] public IEnumerator AuthoredCompletedDependencyDelaysAttackUntilCardsReachTheEnd()
        {
            yield return Open();
            var adapter = Object.FindObjectsOfType<Battle2DSceneAdapter>().Single(); var view = adapter.View;
            yield return Idle(view);
            var director = view.SceneDirector;
            var group = director.Timeline.Single(g => g.ruleAction == "Play");
            var node = group.nodes.Single(n => n.id == "attack");
            node.after = new[] { new SceneActionDependency { actionId = "cards", kind = DependencyKind.Completed } };
            var action = (TransformTimelineAction)node.action;
            var card = view.Cards[0]; var slot = card.transform.localPosition;
            float offsetAtAttack = -1;
            action.StartPresentationRoutine = routine =>
            {
                offsetAtAttack = card.transform.localPosition.y - slot.y;
                var running = view.StartCoroutine(routine);
                return () => { if (view != null) view.StopCoroutine(running); };
            };
            view.ToggleSelection(card.Id); view.PlayButton.onClick.Invoke();
            var committed = adapter.AuthorityState;
            yield return Idle(view);
            Assert.AreEqual(.7f, offsetAtAttack, .001f, "Attack must start after full preparation, rather than the earlier Prepared marker.");
            Assert.AreSame(committed, adapter.AuthorityState); Match(adapter);
        }
    }
}
