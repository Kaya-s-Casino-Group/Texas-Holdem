#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CardInteraction;
using Poker.BattleHosting;
using Poker.BattlePresentation;
using UnityEditor;
using CardPresentation;
using NUnit.Framework;
using PokerEngine;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using global::BattlePresentation;
using global::BattlePresentation.Unity;

namespace Poker.SingleBattle.Tests
{
    public sealed class SceneTimelinePlaybackTests
    {
        private readonly List<string> errors = new List<string>();
        private bool previousLogPolicy;
        [SetUp] public void CaptureProductErrors()
        {
            errors.Clear(); previousLogPolicy = LogAssert.ignoreFailingMessages;
            // The editor connector reconnects during scene loading. Still fail on every product error.
            LogAssert.ignoreFailingMessages = true; Application.logMessageReceived += CaptureError;
        }
        private void CaptureError(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (message == "[MCP Unity] WebSocket error: An error has occurred in closing the connection.") return;
            errors.Add(message + "\n" + stack);
        }
        [TearDown] public void CheckProductErrors()
        {
            Application.logMessageReceived -= CaptureError;
            LogAssert.ignoreFailingMessages = previousLogPolicy;
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [UnityTest]
        public IEnumerator AuthoredSceneUsesTimelineForOpeningSelectionPlayDiscardAndCancellation()
        {
            var resultPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/BattleRuleAlignment/scene-flow.txt"));
            if (File.Exists(resultPath)) File.Delete(resultPath);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/BattleDemo.unity", new LoadSceneParameters(LoadSceneMode.Single));
            var host = Object.FindObjectsOfType<SingleBattleSceneAdapter>().Single();
            Assert.AreEqual("Packages/com.kaya.battle-engine/Content/StandardSingleBattle-2026-10-21.xml",
                AssetDatabase.GetAssetPath(host.ContentXml));
            Assert.AreSame(host.SceneHost, host.GetComponent<BattleSceneHost>());
            Assert.AreSame(host, host.SceneHost.SceneAdapter);
            Assert.AreSame(host.Session, host.SceneHost.Session);
            Assert.NotNull(host.GetComponent<ScenePresentationDirector>(), "The saved scene must carry the new frontend.");
            Assert.NotNull(host.GetComponent<SceneCandidateFeedback>());
            Assert.IsTrue(host.PresentationDirector.HasConfiguredTimeline);
            Assert.IsNull(typeof(SingleBattleSceneAdapter).GetMethod("BuildPlan", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic),
                "The current scene sequence must come from authored configuration, not a Host plan factory.");
            var uiActions = host.GetComponentsInChildren<UiFeedbackTimelineAction>(true);
            Assert.IsTrue(uiActions.Any(action => action.Kind == UiFeedbackKind.Damage));
            Assert.IsTrue(uiActions.Any(action => action.Kind == UiFeedbackKind.Hand));
            var cameraTarget = host.PresentationDirector.Targets.Single(binding => binding.key == "camera").target as Transform;
            Assert.NotNull(cameraTarget);
            yield return Idle(host);
            Assert.IsNull(host.PresentationDirector.LastError);
            Assert.AreEqual(8, host.Actions.Hand.Cards.Count);
            Assert.AreSame(host.AuthorityState, host.State);
            Assert.IsTrue(host.CanEndTurn);
            Assert.AreEqual(BattlePlayerWindow.Action, host.State.FlowFor(host.Player.ParticipantId).Window,
                "The date standard automatically fills the opening hand and enters the player action window.");
            Assert.AreEqual(3, host.Player.Turn.Energy);
            var authority = host.AuthorityState;
            Select(host, 1); host.RefreshCandidate();
            Assert.AreEqual(CandidateState.Valid, host.Candidate.State);
            Assert.AreEqual(1, host.Candidate.SelectedIds.Count);
            Select(host, 2); host.RefreshCandidate();
            Assert.AreEqual(2, host.Candidate.SelectedIds.Count);
            Select(host, 0); host.RefreshCandidate();
            Assert.AreEqual(CandidateState.Empty, host.Candidate.State);
            Assert.AreSame(authority, host.AuthorityState, "Selection is a local presentation session.");
            Select(host, 1); host.RefreshCandidate();
            var cursors = new List<DisplayCursor>();
            host.PresentationDirector.Display.Changed += update => cursors.Add(update.Current.Cursor);
            long oldEnemy = host.Enemy.Unit.Health;
            var cameraPosition = cameraTarget.localPosition;
            var cameraRotation = cameraTarget.localRotation;
            Assert.AreEqual(1, host.Player.Play(host.Actions.Hand.CaptureSelection(), true));
            var settledPlay = host.AuthorityState;
            Assert.IsTrue(host.IsPresenting);
            Assert.AreEqual(CandidateState.Closed, host.Candidate.State);
            Assert.IsFalse(host.CanEndTurn);
            Assert.AreEqual(oldEnemy, host.Display.Unit(host.Enemy.ParticipantId.Value).Health, "Health waits for the actual impact marker.");
            bool sawDamageUi = false, sawCameraMotion = false, capturedDamage = false;
            float damageVisibleSince = -1;
            float playDeadline = Time.realtimeSinceStartup + 15;
            while (host.IsPresenting && Time.realtimeSinceStartup < playDeadline)
            {
                var damageVisible = uiActions.Any(action => action.Kind == UiFeedbackKind.Damage && action.ActiveViewCount > 0);
                sawDamageUi |= damageVisible;
                if (damageVisible && damageVisibleSince < 0) damageVisibleSince = Time.realtimeSinceStartup;
                if (damageVisible && !capturedDamage && Time.realtimeSinceStartup - damageVisibleSince >= .12f)
                {
                    Directory.CreateDirectory("Library/BattlePresentationValidation");
                    ScreenCapture.CaptureScreenshot("Library/BattlePresentationValidation/configured-damage.png");
                    capturedDamage = true;
                }
                sawCameraMotion |= Vector3.Distance(cameraPosition, cameraTarget.localPosition) > .00001f ||
                    Quaternion.Angle(cameraRotation, cameraTarget.localRotation) > .001f;
                yield return null;
            }
            Assert.IsFalse(host.IsPresenting);
            Assert.IsTrue(sawDamageUi, "The configured damage UI action must actually reveal its owned text.");
            Assert.IsTrue(sawCameraMotion, "The enemy damage filter must start the configured camera action.");
            Assert.Less(Vector3.Distance(cameraPosition, cameraTarget.localPosition), .00001f);
            Assert.Less(Quaternion.Angle(cameraRotation, cameraTarget.localRotation), .001f);
            Assert.AreSame(settledPlay, host.State);
            Assert.Less(host.Enemy.Unit.Health, oldEnemy);
            Assert.AreEqual(7, host.Actions.Hand.Cards.Count, "Playing a card does not refill it.");
            Assert.AreEqual(2, host.Player.Turn.Energy);
            Assert.AreEqual(0, host.State.Duel.CompletedRounds);
            Assert.AreEqual(cursors.Count, cursors.Select(c => c.Sequence).Distinct().Count());
            Assert.IsTrue(cursors.Zip(cursors.Skip(1), (a,b) => a.Sequence < b.Sequence).All(x => x));
            Assert.IsNull(host.PresentationDirector.LastError);
            Select(host, 1);
            Assert.AreEqual(1, host.Player.Discard(host.Actions.Hand.CaptureSelection(), true));
            yield return Idle(host);
            Assert.AreSame(host.AuthorityState, host.State);
            Assert.AreEqual(0, host.State.Duel.CompletedRounds, "Discarding does not end a turn with action points remaining.");
            Assert.AreEqual(1, host.Player.Turn.Energy);
            Assert.AreEqual(BattlePlayerWindow.Action, host.State.FlowFor(host.Player.ParticipantId).Window);
            Assert.AreEqual(host.Player.Unit.Block, host.Player.Shields.TotalBlock);
            Assert.AreEqual(7, host.Actions.Hand.Cards.Count, "Discarding one card immediately replaces one card.");
            Assert.IsNull(host.PresentationDirector.LastError);
            Assert.IsTrue(host.EndTurn(true));
            yield return null;
            var final = host.AuthorityState;
            Assert.IsTrue(host.IsPresenting);
            host.SnapToState();
            Assert.IsTrue(uiActions.All(action => action.ActiveViewCount == 0), "Recovery releases all scoped UI feedback.");
            Assert.AreSame(final, host.State);
            Assert.IsFalse(host.IsPresenting);
            Assert.AreEqual(1, final.Duel.CompletedRounds);
            Assert.AreEqual(8, host.Actions.Hand.Cards.Count, "The next player turn fills the hand to its limit.");
            Assert.AreEqual(3, host.Player.Turn.Energy);
            var cursor = host.Display.Cursor;
            yield return new WaitForSecondsRealtime(1.3f);
            Assert.AreSame(final, host.AuthorityState);
            Assert.AreEqual(cursor, host.Display.Cursor, "Cancelled callbacks cannot advance the recovered timeline.");
            Assert.AreEqual(host.Player.Unit.Block, host.Player.Shields.TotalBlock);
            foreach (var zone in final.Table.Zones) CollectionAssert.AreEquivalent(zone.Cards, host.Match.State.GetZone(zone.Id).Cards);
            host.RestartBattle();
            yield return Idle(host);
            Assert.AreEqual(8, host.Actions.Hand.Cards.Count);
            Assert.AreEqual(3, host.Player.Turn.Energy);
            Assert.IsNull(host.PresentationDirector.LastError);
            var beforeDisable = host.AuthorityState;
            host.Match.gameObject.SetActive(false);
            yield return null;
            Assert.IsFalse(host.Match.IsRunning);
            Assert.AreSame(beforeDisable, host.State);
            host.Match.gameObject.SetActive(true);
            yield return Idle(host);
            Assert.AreSame(beforeDisable, host.AuthorityState, "Reactivation only restores the display; it cannot replay rules.");
            Assert.IsTrue(host.Match.IsRunning);
            Assert.AreEqual(8, host.Actions.Hand.Cards.Count);
            Assert.IsTrue(host.CanEndTurn);
            Assert.IsNull(host.PresentationDirector.LastError);
            Assert.IsEmpty(errors, "The authored scene flow must finish without product errors.");
            Directory.CreateDirectory(Path.GetDirectoryName(resultPath));
            File.WriteAllText(resultPath, "PASS\nAuthoredSceneUsesTimelineForOpeningSelectionPlayDiscardAndCancellation\n" +
                System.DateTime.UtcNow.ToString("O"));
        }
        [UnityTest]
        public IEnumerator MinimalPrefabAutoStartsTwoIndependentBattlesWithoutSceneBindings()
        {
            var previous = SceneManager.GetActiveScene();
            var empty = SceneManager.CreateScene("Battle Host Minimal Runtime Validation");
            SceneManager.SetActiveScene(empty);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameAssets/SingleBattle/Hosting/BattleSceneHostMinimal.prefab");
            Assert.NotNull(prefab);
            var first = Object.Instantiate(prefab).GetComponent<BattleSceneHost>();
            var second = Object.Instantiate(prefab).GetComponent<BattleSceneHost>();
            yield return null;
            Assert.NotNull(first.Session); Assert.NotNull(second.Session);
            Assert.IsTrue(first.AuthorityState.HasStarted); Assert.IsTrue(second.AuthorityState.HasStarted);
            var adapter = (BattleTimelineSceneAdapter)first.SceneAdapter;
            first.Playback.Submit(adapter.ObserverId, CardDuelAction.SkipDraw, animate: false);
            var untouched = second.AuthorityState;
            adapter.SelectCards(first.AuthorityState.Zone(CardZoneKinds.Hand, adapter.ObserverId).Cards.Take(1).Select(id => id.Value));
            Assert.IsTrue(adapter.PlaySelected().Accepted);
            Assert.AreSame(untouched, second.AuthorityState);
            var state = first.AuthorityState;
            first.gameObject.SetActive(false); yield return null;
            first.gameObject.SetActive(true); yield return null;
            Assert.AreSame(state, first.AuthorityState); Assert.IsTrue(adapter.CanAct);
            first.RestartBattle(); yield return null;
            Assert.AreNotSame(state, first.AuthorityState); Assert.AreSame(untouched, second.AuthorityState);
            SceneManager.SetActiveScene(previous);
            yield return SceneManager.UnloadSceneAsync(empty);
        }
        [UnityTest]
        public IEnumerator RevivalStopsAnActiveDeathCoroutineAndItsLatePoseWrite()
        {
            var resultPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/BattleRuleAlignment/revival-coroutine.txt"));
            if (File.Exists(resultPath)) File.Delete(resultPath);
            var root = new GameObject("Revival coroutine regression");
            try
            {
                var actor = root.AddComponent<PlayerBattleActor>();
                var model = new GameObject("Model").transform; model.SetParent(root.transform, false);
                var presenter = model.gameObject.AddComponent<BattleActorPresenter>(); presenter.Configure(model);
                actor.ConfigurePresentation(presenter);
                UnitDisplayData Unit(long hp) => new UnitDisplayData("revival", "Player", "player", hp, 100, 0, 3, 3);
                actor.SynchronizeDisplay(Unit(100), true);
                var lethal = new EffectDisplayData("Damage", "enemy", "revival", 100, 100, 0, Unit(100), Unit(0), true);
                var death = actor.PresentDisplay(BattlePresentationKind.Death, lethal, null);
                yield return null;
                Assert.IsTrue(death.keepWaiting);
                actor.SynchronizeDisplay(Unit(60));
                Assert.AreEqual(BattlePresentationStatus.Completed, death.Status);
                yield return new WaitForSecondsRealtime(.45f);
                Assert.AreEqual(Vector3.one, model.localScale);
                Assert.AreEqual(Quaternion.identity, model.localRotation);
                Assert.AreEqual(60, actor.DisplayUnit.Health);
                Assert.IsFalse(actor.IsDisplayBusy);
            }
            finally { Object.Destroy(root); }
            yield return null;
            Assert.IsEmpty(errors, "The revival regression must finish without product errors.");
            // Preserve a scoped assertion result when the editor's test-runner connection fails during its later cleanup.
            Directory.CreateDirectory(Path.GetDirectoryName(resultPath));
            File.WriteAllText(resultPath, "PASS\nRevivalStopsAnActiveDeathCoroutineAndItsLatePoseWrite\n" + System.DateTime.UtcNow.ToString("O"));
        }

        private static IEnumerator Idle(SingleBattleSceneAdapter host)
        {
            float deadline = Time.realtimeSinceStartup + 15;
            yield return null;
            while (host.IsPresenting && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(host.IsPresenting, "The scene did not reach a decision window before the test deadline.");
        }
        private static void Select(SingleBattleSceneAdapter host, int count)
        {
            var hand = host.Actions.Hand;
            foreach(var card in hand.Cards.ToArray()) hand.SetInteraction(card.Id, default);
            foreach(var card in hand.Cards.Take(count).ToArray()) hand.SetInteraction(card.Id, new CardInteractionState(isSelected:true));
            hand.Publish(); hand.GetComponent<HandZoneView>().CompleteTransitions();
        }
    }
}
#endif
