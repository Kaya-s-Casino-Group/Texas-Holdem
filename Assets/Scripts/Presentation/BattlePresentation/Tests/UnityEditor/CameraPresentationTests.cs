using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BattlePresentation.Unity.Tests
{
    public sealed class CameraPresentationTests
    {
        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RealCoroutinesCompleteAndDisableOrSynchronizeCancelsImmediately()
        {
            yield return new EnterPlayMode();
            using (var f = new Fixture())
            using (var scope = new PresentationScope("live-camera"))
            {
                var action = f.Action(CameraActionKind.Shot, new Runner());
                action.StartPresentationRoutine = null; action.DeltaTimeProvider = null;
                var markers = new Markers();
                var op = action.Play(Context(scope), markers);
                float deadline = Time.realtimeSinceStartup + 3;
                while (op.Status == PresentationStatus.Pending && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(op.Status, Is.EqualTo(PresentationStatus.Completed));
                CollectionAssert.AreEqual(new[] { "Arrived", "Settled" }, markers.Values); f.AssertDefault();

                action.Configure(f.Rig, CameraActionKind.Shot, f.Shot, seconds: 1);
                op = action.Play(Context(scope), new Markers());
                yield return null;
                f.Rig.enabled = false;
                Assert.That(op.Status, Is.EqualTo(PresentationStatus.Cancelled)); f.AssertDefault();

                f.Rig.enabled = true;
                op = action.Play(Context(scope), new Markers());
                yield return null;
                f.Director.Synchronize(State());
                Assert.That(op.Status, Is.EqualTo(PresentationStatus.Cancelled)); f.AssertDefault();

                var edited = new Vector3(4, 3, -2);
                f.Camera.transform.localPosition = edited; f.Camera.fieldOfView = 62;
                f.Rig.enabled = false; f.Rig.enabled = true;
                yield return null;
                Assert.That(f.Camera.transform.localPosition, Is.EqualTo(edited));
                Assert.That(f.Camera.fieldOfView, Is.EqualTo(62));
            }
        }

        private sealed class Markers : IMarkerSink
        {
            public readonly List<string> Values = new List<string>();
            public Action<string> OnEmit;
            public bool Emit(string marker) { Values.Add(marker); OnEmit?.Invoke(marker); return true; }
        }
        private sealed class Runner
        {
            private IEnumerator body;
            public int Starts;
            public void Bind(PresentationActionBehaviour action)
            {
                action.DeltaTimeProvider = () => .05f;
                action.StartPresentationRoutine = value => { Starts++; body = value; return () => body = null; };
            }
            public void Step() { var value = body; if (value != null && !value.MoveNext()) body = null; }
            public void Finish() { for (int i = 0; i < 100 && body != null; i++) Step(); Assert.That(body, Is.Null, "Camera action did not terminate."); }
        }
        private sealed class Fixture : IDisposable
        {
            public readonly GameObject Root = new GameObject("Camera Test");
            public readonly CameraPresentation Rig;
            public readonly Camera Camera;
            public readonly CameraShot Default, Shot;
            public readonly ScenePresentationDirector Director;
            public Fixture(bool orthographic = false)
            {
                Director = Root.AddComponent<ScenePresentationDirector>();
                var rigObject = Child("Rig", Root.transform);
                Camera = Child("Camera", rigObject.transform).AddComponent<Camera>();
                Camera.orthographic = orthographic; Camera.fieldOfView = 47; Camera.orthographicSize = 6;
                Camera.transform.SetPositionAndRotation(new Vector3(1, 2, -7), Quaternion.Euler(5, 10, 0));
                Default = Child("Default", rigObject.transform).AddComponent<CameraShot>(); Default.Capture(Camera);
                Shot = Child("Shot", rigObject.transform).AddComponent<CameraShot>();
                Camera.fieldOfView = 29; Camera.orthographicSize = 3; Shot.Capture(Camera);
                Shot.transform.position += Vector3.right * 2;
                Shot.transform.rotation = Quaternion.Euler(10, 25, 0);
                Rig = rigObject.AddComponent<CameraPresentation>(); Rig.Configure(Camera, Default, Director);
            }
            public CameraTimelineAction Action(CameraActionKind kind, Runner runner, float duration = .1f, float hold = .1f, float back = .1f)
            {
                var action = Child(kind.ToString(), Rig.transform).AddComponent<CameraTimelineAction>();
                action.Configure(Rig, kind, Shot, seconds: duration, hold: hold, returnSeconds: back); runner.Bind(action); return action;
            }
            public void AssertDefault()
            {
                Assert.That(Vector3.Distance(Camera.transform.position, Default.transform.position), Is.LessThan(.0001f));
                Assert.That(Quaternion.Angle(Camera.transform.rotation, Default.transform.rotation), Is.LessThan(.001f));
                Assert.That(Camera.fieldOfView, Is.EqualTo(Default.FieldOfView).Within(.0001f));
                Assert.That(Camera.orthographicSize, Is.EqualTo(Default.OrthographicSize).Within(.0001f));
                Assert.That(Rig.ShakeOffset, Is.EqualTo(Vector3.zero));
                Assert.That(Rig.ShakeRotation, Is.EqualTo(Quaternion.identity));
            }
            public void Dispose() => Object.DestroyImmediate(Root);
        }
        private static GameObject Child(string name, Transform parent)
        { var child = new GameObject(name); child.transform.SetParent(parent, false); return child; }
        private static BattleDisplaySnapshot State(long revision = 0) => new BattleDisplaySnapshot("battle", "observer", revision, "player", 1, null, null, null);
        private static ActionContext Context(PresentationScope scope, DictionaryTargetResolver resolver = null) =>
            new ActionContext("batch", 1, scope.Id, new BattleDisplayChange(0, DisplayCursor.Empty, State(), "Hit"), State(), resolver, scope, "focus");

        [Test]
        public void OpeningCheckpointWithholdsCardsUntilNearArrivalAndKeepsCameraTailRunning()
        {
            using (var f = new Fixture())
            {
                var runner = new Runner(); var opening = f.Root.AddComponent<CameraOpeningTimelineAction>();
                opening.Configure(f.Rig, f.Shot, 1, .8f); runner.Bind(opening);
                var empty = new CardZoneDisplayData("hand", "observer", "hand", null);
                var dealt = new CardZoneDisplayData("hand", "observer", "hand", new[] { new CardDisplayData("card", "card", "ace", "ace") });
                var initial = new BattleDisplaySnapshot("battle", "observer", 0, "player", 0, null, null, new[] { empty });
                var after = new BattleDisplaySnapshot("battle", "observer", 1, "player", 0, null, null, new[] { dealt });
                f.Director.ConfigureTimeline(new[] { new SceneTimelineGroup {
                    semanticKey = "BattleStarted", explicitCheckpoint = true,
                    nodes = new[] { new SceneActionNode { id = "opening", action = opening } },
                    checkpoint = new[] { new SceneActionDependency { actionId = "opening", kind = DependencyKind.Marker, marker = "NearlyArrived" } }
                } }, Array.Empty<SceneTargetBinding>());
                f.Director.Initialize(initial);
                var batch = new PresentationBatch("opening", initial, new[] {
                    new BattleDisplayChange(0, DisplayCursor.Empty, initial, "BattleStarted"),
                    new BattleDisplayChange(1, new DisplayCursor("opening", 0), after, "CardsDealt")
                }, after);
                var op = f.Director.Present(batch, PlaybackMode.Animated);
                Assert.That(Vector3.Distance(f.Camera.transform.position, f.Shot.transform.position), Is.LessThan(.0001f));
                Assert.That(f.Camera.fieldOfView, Is.EqualTo(f.Shot.FieldOfView).Within(.0001f));
                for (int i = 0; i < 15; i++) { runner.Step(); f.Director.Tick(); }
                Assert.That(f.Director.Display.Current.Zone("hand").Cards.Count, Is.Zero);
                runner.Step(); f.Director.Tick();
                Assert.That(f.Director.Display.Current.Zone("hand").Cards.Count, Is.EqualTo(1));
                Assert.That(op.Status, Is.EqualTo(PresentationStatus.Pending));
                float remainingFraction = Vector3.Distance(f.Camera.transform.position, f.Default.transform.position) /
                    Vector3.Distance(f.Shot.transform.position, f.Default.transform.position);
                Assert.That(remainingFraction, Is.LessThan(.02f));
                runner.Finish(); f.Director.Tick();
                Assert.That(op.Status, Is.EqualTo(PresentationStatus.Completed)); f.AssertDefault();

                f.Director.Initialize(initial);
                int starts = runner.Starts;
                Assert.That(f.Director.Present(batch, PlaybackMode.Instant).Status, Is.EqualTo(PresentationStatus.Completed));
                Assert.That(runner.Starts, Is.EqualTo(starts)); f.AssertDefault();
            }
        }

        [Test]
        public void OpeningCancellationKillsOnlyItsTweenAndCanReplayFromOpeningPose()
        {
            using (var f = new Fixture())
            using (var first = new PresentationScope("first"))
            using (var second = new PresentationScope("second"))
            {
                var runner = new Runner(); var opening = f.Root.AddComponent<CameraOpeningTimelineAction>();
                opening.Configure(f.Rig, f.Shot, 1, .8f); runner.Bind(opening);
                var markers = new Markers(); var op = opening.Play(Context(first), markers); runner.Step();
                op.Cancel(); runner.Finish(); f.AssertDefault(); Assert.That(markers.Values, Is.Empty);
                var next = opening.Play(Context(second), markers);
                Assert.That(Vector3.Distance(f.Camera.transform.position, f.Shot.transform.position), Is.LessThan(.0001f));
                first.Dispose(); runner.Finish();
                Assert.That(next.Status, Is.EqualTo(PresentationStatus.Completed));
                CollectionAssert.AreEqual(new[] { "NearlyArrived", "Arrived" }, markers.Values); f.AssertDefault();
            }
        }

        [Test]
        public void OpeningClockDoesNotAdvanceOtherManualTweens()
        {
            using (var f = new Fixture())
            using (var other = new Fixture())
            using (var scope = new PresentationScope("isolated"))
            using (var otherScope = new PresentationScope("other"))
            {
                var otherAction = other.Root.AddComponent<CameraOpeningTimelineAction>();
                otherAction.Configure(other.Rig, other.Shot); new Runner().Bind(otherAction);
                var otherOp = otherAction.Play(Context(otherScope), new Markers());
                var runner = new Runner(); var opening = f.Root.AddComponent<CameraOpeningTimelineAction>();
                opening.Configure(f.Rig, f.Shot, 1, .8f); runner.Bind(opening);
                opening.Play(Context(scope), new Markers()); runner.Finish();
                Assert.That(otherOp.Status, Is.EqualTo(PresentationStatus.Pending));
                Assert.That(Vector3.Distance(other.Camera.transform.position, other.Shot.transform.position), Is.LessThan(.0001f));
            }
        }

        [TestCase(0f, .8f)]
        [TestCase(.1f, 1f)]
        public void OpeningZeroDurationAndEndThresholdStillEmitBothMarkers(float duration, float ready)
        {
            using (var f = new Fixture())
            using (var scope = new PresentationScope("boundary"))
            {
                var runner = new Runner(); var opening = f.Root.AddComponent<CameraOpeningTimelineAction>();
                opening.Configure(f.Rig, f.Shot, duration, ready); runner.Bind(opening); var markers = new Markers();
                var op = opening.Play(Context(scope), markers); runner.Finish();
                Assert.That(op.Status, Is.EqualTo(PresentationStatus.Completed));
                CollectionAssert.AreEqual(new[] { "NearlyArrived", "Arrived" }, markers.Values); f.AssertDefault();
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ShotArrivesWithLensThenReturnsBeforeCompletion(bool orthographic)
        {
            using (var f = new Fixture(orthographic))
            using (var scope = new PresentationScope("shot"))
            {
                var runner = new Runner(); var action = f.Action(CameraActionKind.Shot, runner); var markers = new Markers();
                markers.OnEmit = marker => {
                    if (marker == "Arrived") {
                        Assert.That(Vector3.Distance(f.Rig.BasePosition, f.Shot.transform.position), Is.LessThan(.0001f));
                        Assert.That(f.Camera.fieldOfView, Is.EqualTo(29).Within(.001f));
                        Assert.That(f.Camera.orthographicSize, Is.EqualTo(3).Within(.001f));
                    } else f.AssertDefault();
                };
                var op = action.Play(Context(scope), markers); runner.Step();
                Assert.That(markers.Values, Is.Empty);
                Assert.That(op.Status, Is.EqualTo(PresentationStatus.Pending));
                runner.Finish();
                Assert.That(op.Status, Is.EqualTo(PresentationStatus.Completed));
                CollectionAssert.AreEqual(new[] { "Arrived", "Settled" }, markers.Values); f.AssertDefault();
            }
        }

        [Test]
        public void PoseAndShakeOverlapButTwoPoseWritersAreRejected()
        {
            using (var f = new Fixture())
            using (var poseScope = new PresentationScope("pose"))
            using (var shakeScope = new PresentationScope("shake"))
            using (var conflictScope = new PresentationScope("conflict"))
            {
                var poseRunner = new Runner(); var shakeRunner = new Runner(); var conflictRunner = new Runner();
                var pose = f.Action(CameraActionKind.Shot, poseRunner).Play(Context(poseScope), new Markers());
                var shake = f.Action(CameraActionKind.Shake, shakeRunner).Play(Context(shakeScope), new Markers());
                var conflict = f.Action(CameraActionKind.Return, conflictRunner).Play(Context(conflictScope), new Markers());
                Assert.That(conflict.Error.Code, Is.EqualTo("TargetBusy"));
                poseRunner.Step(); shakeRunner.Step();
                Assert.That(f.Rig.ShakeOffset.sqrMagnitude, Is.GreaterThan(0));
                shake.Cancel();
                Assert.That(pose.Status, Is.EqualTo(PresentationStatus.Pending));
                Assert.That(Vector3.Distance(f.Rig.BasePosition, f.Default.transform.position), Is.GreaterThan(.01f));
                poseRunner.Finish(); f.AssertDefault();
            }
        }

        [Test]
        public void GenericTransformWriterCannotStealCameraOutput()
        {
            using (var f = new Fixture())
            using (var scope = new PresentationScope("camera"))
            using (var other = new PresentationScope("generic"))
            {
                var runner = new Runner(); f.Action(CameraActionKind.Shot, runner).Play(Context(scope), new Markers());
                var action = f.Root.AddComponent<TransformTimelineAction>();
                action.Configure(f.Camera.transform, Array.Empty<TransformTimelineStep>());
                Assert.That(action.Play(Context(other), new Markers()).Error.Code, Is.EqualTo("TargetBusy"));
            }
        }

        [Test]
        public void NonzeroCameraLocalPoseIsValidAndShakePreservesItsEditedBase()
        {
            using (var f = new Fixture())
            using (var scope = new PresentationScope("edited-camera"))
            {
                f.Camera.transform.localPosition = new Vector3(.5f, 0, 5.8f);
                f.Camera.transform.localRotation = Quaternion.Euler(12, 30, 4);
                f.Camera.fieldOfView = 44.4f;
                var position = f.Camera.transform.position; var rotation = f.Camera.transform.rotation;
                Assert.That(f.Rig.ValidateConfiguration(out _), Is.True);
                var runner = new Runner(); var action = f.Action(CameraActionKind.Shake, runner);
                var op = action.Play(Context(scope), new Markers()); runner.Step();
                Assert.That(Vector3.Distance(f.Camera.transform.position, position + rotation * f.Rig.ShakeOffset), Is.LessThan(.0001f));
                Assert.That(Quaternion.Angle(f.Camera.transform.rotation, rotation * f.Rig.ShakeRotation), Is.LessThan(.001f));
                op.Cancel();
                Assert.That(Vector3.Distance(f.Camera.transform.position, position), Is.LessThan(.0001f));
                Assert.That(Quaternion.Angle(f.Camera.transform.rotation, rotation), Is.LessThan(.001f));
                Assert.That(f.Camera.fieldOfView, Is.EqualTo(44.4f).Within(.001f));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CompositorCombinesChannelsInEitherOrderAndPoseCancellationKeepsShake(bool shakeFirst)
        {
            using (var f = new Fixture())
            using (var poseScope = new PresentationScope("pose"))
            using (var shakeScope = new PresentationScope("shake"))
            using (var otherScope = new PresentationScope("other"))
            {
                var poseRunner = new Runner(); var shakeRunner = new Runner();
                var pose = f.Action(CameraActionKind.Shot, poseRunner).Play(Context(poseScope), new Markers());
                var shake = f.Action(CameraActionKind.Shake, shakeRunner).Play(Context(shakeScope), new Markers());
                if (shakeFirst) { shakeRunner.Step(); poseRunner.Step(); } else { poseRunner.Step(); shakeRunner.Step(); }
                Assert.That(Vector3.Distance(f.Camera.transform.position, f.Rig.BasePosition + f.Rig.BaseRotation * f.Rig.ShakeOffset), Is.LessThan(.0001f));
                Assert.That(Quaternion.Angle(f.Camera.transform.rotation, f.Rig.BaseRotation * f.Rig.ShakeRotation), Is.LessThan(.001f));
                var offset = f.Rig.ShakeOffset;
                pose.Cancel();
                Assert.That(shake.Status, Is.EqualTo(PresentationStatus.Pending));
                Assert.That(f.Rig.ShakeOffset, Is.EqualTo(offset));
                Assert.That(Vector3.Distance(f.Camera.transform.position, f.Default.transform.position + f.Default.transform.rotation * offset), Is.LessThan(.0001f));
                var generic = f.Root.AddComponent<TransformTimelineAction>();
                generic.Configure(f.Camera.transform, Array.Empty<TransformTimelineStep>());
                Assert.That(generic.Play(Context(otherScope), new Markers()).Error.Code, Is.EqualTo("TargetBusy"));
                shake.Cancel(); f.AssertDefault();
                var nextRunner = new Runner(); var next = f.Action(CameraActionKind.Return, nextRunner).Play(Context(otherScope), new Markers());
                nextRunner.Finish(); Assert.That(next.Status, Is.EqualTo(PresentationStatus.Completed));
            }
        }

        [Test]
        public void TwoRigsCannotDriveTheSameOutputCamera()
        {
            using (var f = new Fixture())
            using (var first = new PresentationScope("first-rig"))
            using (var second = new PresentationScope("second-rig"))
            {
                var otherRig = f.Root.AddComponent<CameraPresentation>(); otherRig.Configure(f.Camera, f.Default);
                var runner = new Runner(); var op = f.Action(CameraActionKind.Shot, runner).Play(Context(first), new Markers()); runner.Step();
                var before = f.Camera.transform.position;
                var otherAction = f.Root.AddComponent<CameraTimelineAction>(); otherAction.Configure(otherRig, CameraActionKind.Shake);
                Assert.That(otherAction.Play(Context(second), new Markers()).Error.Code, Is.EqualTo("TargetBusy"));
                otherRig.ResetPresentation();
                Assert.That(f.Camera.transform.position, Is.EqualTo(before));
                Assert.That(op.Status, Is.EqualTo(PresentationStatus.Pending));
            }
        }

        [Test]
        public void CancellationRestoresLatestBaselineAndOldScopeCannotResetNextCue()
        {
            using (var f = new Fixture())
            using (var old = new PresentationScope("old"))
            using (var next = new PresentationScope("next"))
            {
                var runner = new Runner(); var action = f.Action(CameraActionKind.Shot, runner);
                var op = action.Play(Context(old), new Markers()); runner.Step();
                f.Default.transform.position += Vector3.up;
                op.Cancel(); f.AssertDefault();
                var nextOp = action.Play(Context(next), new Markers()); runner.Step();
                var position = f.Rig.BasePosition;
                old.Dispose();
                Assert.That(f.Rig.BasePosition, Is.EqualTo(position));
                Assert.That(nextOp.Status, Is.EqualTo(PresentationStatus.Pending));
                next.Dispose(); f.AssertDefault();
            }
        }

        [Test]
        public void DisableRigCancelsAllChannelsWithoutLateMarkers()
        {
            using (var f = new Fixture())
            using (var first = new PresentationScope("first"))
            using (var second = new PresentationScope("second"))
            {
                var a = new Runner(); var b = new Runner(); var markers = new Markers();
                var pose = f.Action(CameraActionKind.Shot, a).Play(Context(first), markers);
                var shake = f.Action(CameraActionKind.Shake, b).Play(Context(second), markers);
                a.Step(); b.Step(); f.Rig.enabled = false; a.Finish(); b.Finish();
                Assert.That(pose.Status, Is.EqualTo(PresentationStatus.Cancelled));
                Assert.That(shake.Status, Is.EqualTo(PresentationStatus.Cancelled));
                Assert.That(markers.Values, Is.Empty); f.AssertDefault();
            }
        }

        [Test]
        public void FocusResolvesTimelineTargetAndSamplesItAtStart()
        {
            using (var f = new Fixture())
            using (var scope = new PresentationScope("focus"))
            {
                var target = Child("Target", f.Root.transform); target.transform.position = new Vector3(3, 2, 1);
                var direction = (target.transform.position - f.Rig.BasePosition).normalized;
                var resolver = new DictionaryTargetResolver(); resolver.Bind("focus", target);
                var runner = new Runner(); var action = f.Action(CameraActionKind.Focus, runner); var markers = new Markers();
                markers.OnEmit = key => { if (key == "Arrived") Assert.That(Vector3.Dot((f.Rig.BaseRotation * Vector3.forward), direction), Is.GreaterThan(.9999f)); };
                var op = action.Play(Context(scope, resolver), markers); target.transform.position += Vector3.left * 10;
                runner.Finish(); Assert.That(op.Status, Is.EqualTo(PresentationStatus.Completed)); f.AssertDefault();
            }
        }

        [Test]
        public void CancellingAtArrivalDoesNotEmitSettledOrResumeMovement()
        {
            using (var f = new Fixture())
            using (var scope = new PresentationScope("cancel-at-marker"))
            {
                var runner = new Runner(); var action = f.Action(CameraActionKind.Shot, runner, 0, 0, 0);
                var markers = new Markers { OnEmit = key => { if (key == "Arrived") scope.Dispose(); } };
                var op = action.Play(Context(scope), markers); runner.Finish();
                Assert.That(op.Status, Is.EqualTo(PresentationStatus.Cancelled));
                CollectionAssert.AreEqual(new[] { "Arrived" }, markers.Values); f.AssertDefault();
            }
        }

        [Test]
        public void InvalidClockFailsAndRestoresInsteadOfHangingDirector()
        {
            using (var f = new Fixture())
            using (var scope = new PresentationScope("bad-clock"))
            {
                var runner = new Runner(); var action = f.Action(CameraActionKind.Shot, runner);
                action.DeltaTimeProvider = () => float.NaN;
                var op = action.Play(Context(scope), new Markers()); runner.Finish();
                Assert.That(op.Status, Is.EqualTo(PresentationStatus.Failed)); f.AssertDefault();
            }
        }

        [Test]
        public void DirectorCheckpointAndSynchronizeUseActualCameraOperation()
        {
            using (var f = new Fixture())
            {
                var runner = new Runner(); var action = f.Action(CameraActionKind.Shot, runner);
                f.Director.ConfigureTimeline(new[] { new SceneTimelineGroup {
                    semanticKey = "Hit", replaceDefault = true, explicitCheckpoint = true,
                    nodes = new[] { new SceneActionNode { id = "camera", action = action } },
                    checkpoint = new[] { new SceneActionDependency { actionId = "camera", kind = DependencyKind.Marker, marker = "Arrived" } }
                } }, Array.Empty<SceneTargetBinding>());
                var initial = State(); var after = State(1); f.Director.Initialize(initial);
                var batch = new PresentationBatch("hit", initial, new[] { new BattleDisplayChange(0, DisplayCursor.Empty, after, "Hit") }, after);
                var op = f.Director.Present(batch, PlaybackMode.Animated);
                runner.Step(); f.Director.Tick(); Assert.That(f.Director.Display.Current.RuleRevision, Is.EqualTo(0));
                runner.Step(); runner.Step(); f.Director.Tick();
                Assert.That(f.Director.Display.Current.RuleRevision, Is.EqualTo(1));
                Assert.That(op.Status, Is.EqualTo(PresentationStatus.Pending));
                f.Director.Synchronize(after); runner.Finish();
                Assert.That(op.Status, Is.EqualTo(PresentationStatus.Cancelled)); f.AssertDefault();
                f.Director.Initialize(initial);
                int starts = runner.Starts;
                Assert.That(f.Director.Present(batch, PlaybackMode.Instant).Status, Is.EqualTo(PresentationStatus.Completed));
                Assert.That(runner.Starts, Is.EqualTo(starts)); f.AssertDefault();
            }
        }

        [Test]
        public void ZeroDurationShotCompletesWithOrderedSignals()
        {
            using (var f = new Fixture())
            using (var scope = new PresentationScope("instant-cue"))
            {
                var runner = new Runner(); var action = f.Action(CameraActionKind.Shot, runner, 0, 0, 0); var markers = new Markers();
                var op = action.Play(Context(scope), markers); runner.Finish();
                Assert.That(op.Status, Is.EqualTo(PresentationStatus.Completed));
                CollectionAssert.AreEqual(new[] { "Arrived", "Settled" }, markers.Values); f.AssertDefault();
            }
        }
    }
}
