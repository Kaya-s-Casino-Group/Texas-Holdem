using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using global::BattlePresentation;
using global::BattlePresentation.Unity;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Poker.SingleBattle.Tests
{
    public sealed class WrathWarningBoundaryTests
    {
        [Test]
        public void PhaseEndedKeepsTheRealPendingCheckpointUntilTheExistingHudDeadlineExpires()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var hostRoot = New(scene, "Boundary Host"); hostRoot.SetActive(false);
                var host = hostRoot.AddComponent<SingleBattleSceneAdapter>();
                Field(host, "externalCommands", true);
                var hud = New(scene, "Boundary HUD", typeof(RectTransform), typeof(Canvas)).AddComponent<SingleBattleHud>();
                Field(hud, "host", host); Field(hud, "wrathPromptsVisible", true); Field(hud, "wrathUltimateActive", true);
                float deadline = Time.unscaledTime + 2;
                Field(hud, "wrathUltimateUntil", deadline);
                var director = New(scene, "Boundary Director").AddComponent<ScenePresentationDirector>();
                var recovery = New(scene, "Authored Recovery").AddComponent<BattleAttackRecoveryAction>();
                var oldCheckpoint = new SceneActionDependency { actionId = "old-recovery", kind = DependencyKind.Marker, marker = "Recovered" };
                director.ConfigureTimeline(new[] { new SceneTimelineGroup {
                    semanticKey = "PhaseEnded", ruleAction = "EnemyAction", explicitCheckpoint = true,
                    nodes = new[] { new SceneActionNode { id = "old-recovery", action = recovery } },
                    checkpoint = new[] { oldCheckpoint }
                } }, new SceneTargetBinding[0]);
                BattleTurnBannerTimeline.Upgrade(host, director);
                BattleTurnBannerTimeline.Upgrade(host, director);
                var boundary = director.Timeline.Single(group => group.semanticKey == "PhaseEnded");
                var node = boundary.nodes.Single(item => item.action is WrathWarningBoundaryAction);
                Assert.AreEqual(2, boundary.nodes.Length, "Repeated upgrades must retain the authored node and add only one gate.");
                Assert.AreSame(oldCheckpoint, node.after.Single(), "The gate must follow the existing checkpoint dependencies.");
                Assert.AreSame(oldCheckpoint, boundary.checkpoint[0]);
                Assert.AreEqual(node.id, boundary.checkpoint[1].actionId);
                Assert.AreEqual(DependencyKind.Completed, boundary.checkpoint[1].kind);
                var gate = (WrathWarningBoundaryAction)node.action;
                gate.Configure(host, hud);
                IEnumerator waiting = null;
                gate.StartPresentationRoutine = routine => {
                    waiting = routine;
                    Assert.IsTrue(waiting.MoveNext());
                    return () => { };
                };
                var before = Snapshot(DisplayCursor.Empty, 6);
                var ended = Snapshot(new DisplayCursor("boundary", 0), 6);
                var refreshed = Snapshot(new DisplayCursor("boundary", 1), 7);
                var values = new Dictionary<string, string> { ["action"] = "EnemyAction" };
                director.Initialize(before);
                var operation = director.Present(new PresentationBatch("boundary", before, new[] {
                    new BattleDisplayChange(0, before.Cursor, ended, "PhaseEnded", "body", values: values, before: before),
                    new BattleDisplayChange(1, ended.Cursor, refreshed, "ResourcesChanged", "body", values: values, before: ended)
                }, refreshed), PlaybackMode.Animated);
                Assert.IsNull(director.LastError);
                Assert.AreEqual(PresentationStatus.Pending, operation.Status);
                Assert.AreEqual(6, director.Display.Current.CompletedRounds,
                    "The pending warning must receive a rendered window before the release-round refresh.");
                Assert.AreEqual(1, director.Display.Current.Resource("body", "wrath.warning").Value);
                Assert.AreEqual(deadline, GetField(hud, "wrathUltimateUntil"), "Waiting must not start a new four-second clock.");
                Field(hud, "wrathUltimateUntil", Time.unscaledTime - 1);
                Assert.IsFalse(waiting.MoveNext());
                director.Tick();
                Assert.AreEqual(PresentationStatus.Completed, operation.Status);
                Assert.AreEqual(7, director.Display.Current.CompletedRounds);
                Assert.AreEqual(1, director.Display.Current.Resource("body", "wrath.warning").Value,
                    "Presentation expiry must leave the authoritative pending ultimate intact.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static BattleDisplaySnapshot Snapshot(DisplayCursor cursor, int completed) => new BattleDisplaySnapshot(
            "wrath-battle", "player", 1, "enemy", completed, null,
            new[] { new UnitDisplayData("body", "暴怒", "enemy", 2000, 2000, 0, 0, 0) }, zones: null,
            resources: new[] { new ResourceDisplayData("body", "wrath.warning", 1),
                new ResourceDisplayData("body", "wrath.releaseRound", 8) }, cursor: cursor, hasStarted: true,
            completedRounds: completed, labels: new Dictionary<string, string> { ["wrath.body"] = "body" });
        private static GameObject New(Scene scene, string name, params System.Type[] components)
        { var result = new GameObject(name, components); SceneManager.MoveGameObjectToScene(result, scene); return result; }
        private static void Field(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static object GetField(object target, string name) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }
}
