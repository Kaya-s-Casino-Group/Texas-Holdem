using System;
using System.Collections.Generic;
using global::BattlePresentation;
using NUnit.Framework;
using TMPro;
using UIFoundation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Poker.SingleBattle.Tests
{
    [TestFixture]
    public sealed class HandCandidateDisplayTests
    {
        private sealed class Fixture : IDisposable
        {
            private readonly Scene scene = EditorSceneManager.NewPreviewScene();
            public readonly HandCandidatePresenter View;
            public Fixture()
            {
                var root = New("Candidate display");
                View = root.AddComponent<HandCandidatePresenter>();
                var title = Label("Pattern", root.transform);
                var details = Label("Details", root.transform);
                var points = Label("Points", root.transform).gameObject.AddComponent<RollingNumberText>();
                var multiplier = Label("Multiplier", root.transform).gameObject.AddComponent<RollingNumberText>();
                points.Configure("N0"); multiplier.Configure("0.##");
                View.ConfigureViews(title, details, points, multiplier);
            }
            private GameObject New(string name)
            {
                var value = new GameObject(name); SceneManager.MoveGameObjectToScene(value, scene); return value;
            }
            private TMP_Text Label(string name, Transform parent)
            {
                var value = New(name); value.transform.SetParent(parent, false);
                var text = value.AddComponent<TextMeshProUGUI>();
                text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansCJK/TMP/NotoSansCJKSCRegular.asset");
                return text;
            }
            public void Advance() { View.Chips.Advance(1); View.Multiplier.Advance(1); }
            public void Dispose() => EditorSceneManager.ClosePreviewScene(scene);
        }
        private static CandidatePresentationModel Model(long version, string pattern, long points, decimal multiplier,
            bool canPlay = true, CandidateState state = CandidateState.Valid) => new CandidatePresentationModel(
                "display-test", "player", 4, version, state, state == CandidateState.Valid ? new[] { "card-1", "card-2" } : Array.Empty<string>(),
                pattern, pattern == "OnePair" ? "一对" : "三条", points, multiplier, (long)(points * multiplier), canPlay,
                canDiscard: true, reason: canPlay ? null : "能量不足", labels: new Dictionary<string, string> { ["scoredCount"] = "2" });

        [Test] public void LatestCandidateRetargetsCurrentNumbersAndClearReturnsToEmptyWithoutRuleAccess()
        {
            using (var f = new Fixture())
            using (var interaction = new InteractionSession())
            {
                interaction.Changed += f.View.PresentCandidate;
                Assert.IsTrue(interaction.Reconcile(Model(1, "OnePair", 20, 2)));
                f.View.Chips.Advance(.06f);
                decimal inFlight = f.View.Chips.CurrentValue;
                Assert.That(inFlight, Is.GreaterThan(0).And.LessThan(20));
                Assert.IsTrue(interaction.Reconcile(Model(2, "ThreeOfAKind", 90, 3)));
                Assert.AreEqual("三条", f.View.HandText);
                Assert.AreEqual(inFlight, f.View.Chips.CurrentValue, "Retargeting starts from the currently shown number.");
                f.Advance(); Assert.AreEqual(90, f.View.Chips.CurrentValue); Assert.AreEqual(3, f.View.Multiplier.CurrentValue);
                interaction.Clear(); f.Advance();
                Assert.AreEqual("未选牌", f.View.HandText); Assert.AreEqual(CandidateState.Empty, f.View.Candidate.State);
                Assert.AreEqual(0, f.View.Chips.CurrentValue); Assert.AreEqual(0, f.View.Multiplier.CurrentValue);
                Assert.IsFalse(f.View.Chips.IsAnimating);
            }
        }

        [Test] public void StalePreviewCannotRestoreFeedbackAfterCommitOrClose()
        {
            using (var f = new Fixture())
            using (var interaction = new InteractionSession())
            {
                interaction.Changed += f.View.PresentCandidate;
                long version = interaction.BeginRequest("display-test", "player", 4, new[] { "card-1", "card-2" });
                var preview = Model(version, "OnePair", 20, 2);
                Assert.IsTrue(interaction.Apply(preview));
                var frozen = interaction.BeginCommit();
                Assert.AreSame(preview, frozen); Assert.AreEqual(CandidateState.Committing, f.View.Candidate.State);
                Assert.IsFalse(interaction.Apply(preview));
                interaction.Close();
                Assert.IsFalse(interaction.Apply(preview)); f.Advance();
                Assert.AreEqual(CandidateState.Closed, f.View.Candidate.State);
                Assert.AreEqual("未选牌", f.View.HandText); Assert.AreEqual(0, f.View.Chips.CurrentValue);
            }
        }

        [Test] public void CompatibleMechanismsDoNotReplaceTheHighestScoringHand()
        {
            using (var f = new Fixture())
            {
                var candidate = new CandidatePresentationModel("display-test", "player", 4, 1,
                    CandidateState.Valid, new[] { "a", "b", "c", "d", "e" }, "StraightFlush", "同花顺",
                    20, 8, 160, true, true, labels: new Dictionary<string, string>
                    { ["scoredCount"] = "5", ["additionalMechanisms"] = "同花、顺子" });
                f.View.PresentCandidate(candidate); f.Advance();
                Assert.AreEqual("同花顺", f.View.HandText);
                Assert.AreEqual("兼容 同花、顺子", f.View.DetailText);
                Assert.AreEqual(20, f.View.Chips.CurrentValue);
                Assert.AreEqual(8, f.View.Multiplier.CurrentValue);
                f.View.PresentCandidate(Model(2, "OnePair", 30, 2));
                StringAssert.DoesNotContain("兼容", f.View.DetailText);
            }
        }

        [Test] public void RecognizedPatternIsIndependentFromSubmissionLegalityAndDisableKillsInterpolation()
        {
            using (var f = new Fixture())
            {
                var candidate = Model(1, "OnePair", 30, 2, canPlay: false);
                f.View.PresentCandidate(candidate);
                Assert.AreEqual("一对", f.View.HandText); Assert.IsFalse(f.View.Candidate.CanPlay);
                StringAssert.Contains("计分 2", f.View.DetailText);
                f.View.enabled = false;
                // PreviewScene EditMode does not send lifecycle callbacks to this runtime component.
                typeof(HandCandidatePresenter).GetMethod("OnDisable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(f.View, null);
                Assert.IsFalse(f.View.Chips.IsAnimating); Assert.AreEqual(0, f.View.Chips.CurrentValue);
                f.View.enabled = true;
                typeof(HandCandidatePresenter).GetMethod("OnEnable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(f.View, null);
                f.View.PresentCandidate(candidate); f.Advance();
                Assert.AreEqual(30, f.View.Chips.CurrentValue); Assert.AreEqual("一对", f.View.HandText);
            }
        }
    }
}
