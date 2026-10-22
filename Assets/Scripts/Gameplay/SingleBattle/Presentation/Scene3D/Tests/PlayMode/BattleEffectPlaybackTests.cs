#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using global::BattlePresentation;
using NUnit.Framework;
using PokerEngine;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Poker.SingleBattle.Tests
{
    public sealed class BattleEffectPlaybackTests
    {
        private sealed class Markers : IMarkerSink
        {
            public int Impacts;
            public float ImpactTime;
            public bool Emit(string marker) { if (marker == "Impact") { Impacts++; ImpactTime = Time.realtimeSinceStartup; } return true; }
        }

        [UnityTest]
        public IEnumerator EnemyRetreatsHitsOnceReturnsInOneAndHalfSecondsAndCancelsCleanly()
        {
            var root = new GameObject("Enemy timing regression");
            float timeScale = Time.timeScale; Time.timeScale = 0;
            try
            {
                root.AddComponent<EnemyBattleActor>();
                var model = new GameObject("Model").transform; model.SetParent(root.transform, false);
                var presenter = model.gameObject.AddComponent<BattleActorPresenter>(); presenter.Configure(model);
                presenter.ConfigureAttackOffset(Vector3.left * .035f);
                float start = Time.realtimeSinceStartup;
                var prepare = presenter.Play(new BattlePresentationCue(BattlePresentationKind.PrepareAttack, "enemy"));
                yield return new WaitForSecondsRealtime(.35f);
                Assert.Greater(model.localPosition.x, .04f, "Windup must visibly retreat away from the player.");
                yield return prepare;
                var markers = new Markers();
                var attack = presenter.Play(new BattlePresentationCue(BattlePresentationKind.Attack, "enemy", markers: markers));
                while (markers.Impacts == 0 && attack.keepWaiting) yield return null;
                Assert.Less(model.localPosition.x, -.18f, "Impact must visibly lunge toward the player.");
                Assert.That(markers.ImpactTime - start, Is.InRange(.75f, 1.05f));
                yield return attack;
                Assert.That(Time.realtimeSinceStartup - start, Is.InRange(1.45f, 1.85f));
                Assert.AreEqual(1, markers.Impacts);
                Assert.AreEqual(Vector3.zero, model.localPosition);
                Assert.AreEqual(Vector3.one, model.localScale);
                presenter.Play(new BattlePresentationCue(BattlePresentationKind.PrepareAttack, "enemy"));
                yield return new WaitForSecondsRealtime(.2f);
                presenter.CancelPresentation();
                Assert.AreEqual(Vector3.zero, model.localPosition);
                Assert.AreEqual(Vector3.one, model.localScale);
                yield return new WaitForSecondsRealtime(.8f);
                Assert.AreEqual(Vector3.zero, model.localPosition, "Cancelled windup cannot write a late pose.");
            }
            finally { Time.timeScale = timeScale; Object.Destroy(root); }
        }

        [UnityTest]
        public IEnumerator SavedBattleShowsHitEffectsDuringEnemyTurnAndReleasesThemOnRecovery()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/BattleDemo.unity", new LoadSceneParameters(LoadSceneMode.Single));
            var host = Object.FindObjectOfType<SingleBattleSceneAdapter>();
            yield return Idle(host);
            var effects = host.GetComponentInChildren<BattleEffectTimelineAction>(true);
            Assert.NotNull(effects);
            // First enemy hits for 40: only 20 is blocked, so the player must still show Hit.
            Assert.IsTrue(host.Playback.ApplyAdmin(host.AuthorityState.Revision, new BattleAdminPatch(vitals:
                new[] { new BattleAdminVitals(host.Player.ParticipantId, block: 20) })).Accepted);
            bool sawEffect = false, captured = false, sawPartialHit = false;
            host.PresentationDirector.Display.Changed += update => {
                if (host.PresentationDirector.TryGetChange(update.Current.Cursor, out var fact) && fact.Effect != null &&
                    fact.Effect.BlockAbsorbed > 0 && fact.Effect.HealthLost > 0)
                    sawPartialHit = BattleEffectTimelineAction.EffectFor(fact) == "Hit";
            };
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../output/battle-effects"));
            Directory.CreateDirectory(output);
            float started = Time.realtimeSinceStartup;
            Assert.IsTrue(host.EndTurn(true));
            float deadline = started + 25;
            while (host.IsPresenting && Time.realtimeSinceStartup < deadline)
            {
                if (effects.ActiveEffectCount > 0)
                {
                    sawEffect = true;
                    if (!captured)
                    {
                        captured = true;
                        yield return new WaitForSecondsRealtime(.08f);
                        ScreenCapture.CaptureScreenshot(Path.Combine(output, "enemy-impact.png"));
                    }
                    Assert.IsFalse(host.CanEndTurn);
                }
                yield return null;
            }
            Assert.IsFalse(host.IsPresenting);
            Assert.IsTrue(sawEffect, "Resolved enemy damage must produce an actual visible effect.");
            Assert.IsTrue(sawPartialHit, "A real partial block must select Hit, not hide it behind Shield.");
            Assert.IsNull(host.PresentationDirector.LastError);
            Assert.AreEqual(0, effects.ActiveEffectCount);
            Assert.Greater(Time.realtimeSinceStartup - started, 1.45f);
            Assert.IsTrue(host.EndTurn(true));
            deadline = Time.realtimeSinceStartup + 20;
            while (effects.ActiveEffectCount == 0 && host.IsPresenting && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.Greater(effects.ActiveEffectCount, 0);
            host.SnapToState();
            Assert.AreEqual(0, effects.ActiveEffectCount, "Recovery must release active canvases and callbacks.");
            Assert.IsFalse(host.IsPresenting);
            yield return new WaitForSecondsRealtime(1.1f);
            Assert.AreEqual(0, effects.ActiveEffectCount);
        }

        [UnityTest]
        public IEnumerator GuardUsesSwayAndIntentRefreshesWithoutUnknownBetweenRounds()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/BattleDemo.unity", new LoadSceneParameters(LoadSceneMode.Single));
            var host = Object.FindObjectOfType<SingleBattleSceneAdapter>();
            yield return Idle(host);
            var enemy = host.Enemies.First();
            var rest = enemy.Visual.localPosition;
            var scale = enemy.Visual.localScale;
            var rotation = enemy.Visual.localRotation;
            bool sawSupportFact = false, sawWait = false;
            host.PresentationDirector.Display.Changed += update => {
                foreach (var unit in update.Current.Units.Where(unit => unit.Team == "enemy" && unit.IsAlive))
                {
                    if (update.Current.Labels.TryGetValue("intent/" + unit.Id, out var intent))
                        StringAssert.DoesNotContain("不可用", intent, "A completed action is not an unknown intent: " + update.Current.Cursor + " phase=" + update.Current.Phase + " actor=" + unit.Id + " pending=" + string.Join(",", host.PresentedState.Duel.PendingEnemies));
                    sawWait |= update.Current.Labels.ContainsKey("intentWait/" + unit.Id);
                }
                if (host.PresentationDirector.TryGetChange(update.Current.Cursor, out var fact) && fact.SemanticKey == "ActionStarted" &&
                    fact.Values.TryGetValue("enemyMotion", out var motion) && motion == "Support") sawSupportFact = true;
            };
            bool swayed = false, enlarged = false, shrunk = false;
            for (int round = 0; round < 3; round++)
            {
                Assert.IsTrue(host.EndTurn(true));
                float deadline = Time.realtimeSinceStartup + 25;
                while (host.IsPresenting && Time.realtimeSinceStartup < deadline)
                {
                    if (round == 2)
                    {
                        Assert.Less(Vector3.Distance(rest, enemy.Visual.localPosition), .001f, "Guard must not lunge at the player.");
                        swayed |= Quaternion.Angle(rotation, enemy.Visual.localRotation) > 1;
                        enlarged |= enemy.Visual.localScale.x > scale.x * 1.025f;
                        shrunk |= enemy.Visual.localScale.x < scale.x * .99f;
                    }
                    foreach (var badge in Object.FindObjectsOfType<BattleDemoEnemyBadgeTooltip>().Where(badge => badge.transform.parent.name == "Intents"))
                        StringAssert.DoesNotContain("意图不可用", badge.Description);
                    yield return null;
                }
                Assert.IsFalse(host.IsPresenting);
                Assert.IsNull(host.PresentationDirector.LastError);
                var preview = enemy.ReadIntent()?.Action;
                Assert.NotNull(preview); Assert.IsTrue(preview.Accepted);
                var badges = BattleDemoEnemyHudModel.Intents(preview);
                Assert.AreEqual(round == 1 ? BattleDemoEnemySymbol.Shield : BattleDemoEnemySymbol.Sword, badges.First().Symbol);
                Assert.AreEqual(round == 0 ? "70" : round == 1 ? "90" : "40", badges.First().Value);
                Assert.IsFalse(host.Display.Labels.ContainsKey("intentWait/" + enemy.DisplayId), "A ready next-round intent must replace the waiting state.");
            }
            Assert.IsTrue(sawWait); Assert.IsTrue(sawSupportFact);
            Assert.IsTrue(swayed); Assert.IsTrue(enlarged); Assert.IsTrue(shrunk);
            Assert.AreEqual(scale, enemy.Visual.localScale); Assert.AreEqual(rotation, enemy.Visual.localRotation);
        }

        private static IEnumerator Idle(SingleBattleSceneAdapter host)
        {
            float deadline = Time.realtimeSinceStartup + 25;
            yield return null;
            while (host.IsPresenting && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(host.IsPresenting);
        }
    }
}
#endif
