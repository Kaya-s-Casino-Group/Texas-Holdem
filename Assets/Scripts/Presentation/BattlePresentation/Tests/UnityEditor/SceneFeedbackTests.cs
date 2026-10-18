using System;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BattlePresentation.Unity.Tests
{
    public sealed class SceneFeedbackTests
    {
        private sealed class Fixture : IDisposable
        {
            public readonly GameObject Root = new GameObject("Feedback Test");
            public readonly GameObject Layer = new GameObject("Dedicated Offset");
            public Fixture() { Layer.transform.SetParent(Root.transform, false); }
            public void Dispose() { Object.DestroyImmediate(Root); }
        }
        private static CandidatePresentationModel Candidate(long version, string pattern) =>
            new CandidatePresentationModel("battle", "observer", 1, version, CandidateState.Valid, new[] { "card" }, pattern);
        private sealed class Markers : IMarkerSink { public bool Emit(string marker) => true; }
        private static ActionContext Context(PresentationScope scope)
        {
            var state = new BattleDisplaySnapshot("battle", "observer", 1, "player", 1, null, null, null);
            var data = new BattleDisplayChange(0, DisplayCursor.Empty, state, "ActionStarted");
            return new ActionContext("batch", 1, scope.Id, data, state, null, scope);
        }

        [Test]
        public void CandidateReleasePreservesLatestParentLayout()
        {
            using (var fixture = new Fixture())
            {
                fixture.Root.transform.position = new Vector3(10, 0, 0);
                var feedback = fixture.Root.AddComponent<CandidateTransformFeedback>();
                feedback.Configure(fixture.Layer.transform, Vector3.right, Vector3.zero, Vector3.one, 0);
                var scope = new PresentationScope("first");
                feedback.Reconcile(Candidate(1, "Pair"), scope);
                Assert.That(fixture.Layer.transform.position.x, Is.EqualTo(11));
                fixture.Root.transform.position = new Vector3(25, 0, 0);
                feedback.Release(scope, ReleaseMode.Committed);
                Assert.That(fixture.Layer.transform.position.x, Is.EqualTo(25));
                Assert.That(fixture.Layer.transform.localPosition, Is.EqualTo(Vector3.zero));
                scope.Dispose();
            }
        }

        [Test]
        public void CandidateRouterTransfersExitingContributionWithoutOldScopeReset()
        {
            using (var fixture = new Fixture())
            {
                var pair = fixture.Root.AddComponent<CandidateTransformFeedback>();
                pair.Configure(fixture.Layer.transform, Vector3.right, Vector3.zero, Vector3.one, .12f);
                var triple = fixture.Root.AddComponent<CandidateTransformFeedback>();
                triple.Configure(fixture.Layer.transform, Vector3.right * 2, Vector3.zero, Vector3.one, .12f);
                var router = fixture.Root.AddComponent<SceneCandidateFeedback>();
                router.Configure(new[] {
                    new SceneCandidateRoute { patternKey = "Pair", feedback = new MonoBehaviour[] { pair } },
                    new SceneCandidateRoute { patternKey = "Three", feedback = new MonoBehaviour[] { triple } }
                });
                var session = new InteractionSession(router);
                session.Reconcile(Candidate(1, "Pair")); pair.AdvanceFeedback(.2f);
                var precedingPosition = fixture.Layer.transform.localPosition.x;
                session.Reconcile(Candidate(2, "Three"));
                Assert.That(fixture.Layer.transform.localPosition.x, Is.EqualTo(precedingPosition).Within(.00001f));
                pair.AdvanceFeedback(1); // The exited feedback no longer owns a channel and cannot reset the new one.
                Assert.That(fixture.Layer.transform.localPosition.x, Is.EqualTo(precedingPosition).Within(.00001f));
                triple.AdvanceFeedback(.2f);
                Assert.That(fixture.Layer.transform.localPosition.x, Is.GreaterThan(1.9f));
                session.BeginCommit();
                Assert.That(fixture.Layer.transform.localPosition, Is.EqualTo(Vector3.zero));
                session.Dispose();
            }
        }

        [Test]
        public void ActiveCandidateAndTimelineCannotBothWriteSameTransform()
        {
            using (var fixture = new Fixture())
            {
                var feedback = fixture.Root.AddComponent<CandidateTransformFeedback>();
                feedback.Configure(fixture.Layer.transform, Vector3.right, Vector3.zero, Vector3.one, 0);
                var interactionScope = new PresentationScope("candidate");
                feedback.Reconcile(Candidate(1, "Pair"), interactionScope);
                var action = fixture.Root.AddComponent<TransformTimelineAction>();
                action.Configure(fixture.Layer.transform, Array.Empty<TransformTimelineStep>());
                var actionScope = new PresentationScope("action");
                var operation = action.Play(Context(actionScope), new Markers());
                Assert.That(operation.Status, Is.EqualTo(PresentationStatus.Failed));
                Assert.That(operation.Error.Code, Is.EqualTo("TargetBusy"));
                Assert.That(fixture.Layer.transform.localPosition, Is.EqualTo(Vector3.right));
                feedback.Release(interactionScope, ReleaseMode.Committed);
                actionScope.Dispose(); interactionScope.Dispose();
            }
        }
    }
}
