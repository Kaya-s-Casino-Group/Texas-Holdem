using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using DG.Tweening;
using global::BattlePresentation;
using global::BattlePresentation.Unity;
using Poker.BattleHosting;
using PokerEngine;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Poker.BattlePresentation.Tests
{
    public sealed class CameraSceneBindingTests
    {
        private GameObject sceneRoot, battleRoot;
        private CameraSceneConfiguration config;
        private Camera camera;
        private CameraPresentation executor;
        private CameraOpeningTimelineAction opening;
        private CameraTimelineAction focus;
        private BattleTimelineSceneAdapter bridge;
        private ScenePresentationDirector director;
        private CameraShot overview, start;
        [SetUp] public void SetUp()
        {
            sceneRoot = new GameObject("Camera scene data"); battleRoot = new GameObject("Battle");
            camera = Child("Main Camera", sceneRoot.transform).AddComponent<Camera>();
            camera.transform.position = new Vector3(1, 2, -7); camera.fieldOfView = 30;
            overview = Child("Overview", sceneRoot.transform).AddComponent<CameraShot>(); overview.Capture(camera);
            camera.transform.position += Vector3.back * 5; camera.fieldOfView = 44.4f;
            start = Child("Opening", sceneRoot.transform).AddComponent<CameraShot>(); start.Capture(camera);
            var focusPoint = Child("Focus", sceneRoot.transform).transform; focusPoint.position = new Vector3(3, 1, 2);
            config = sceneRoot.AddComponent<CameraSceneConfiguration>();
            config.Configure(camera, overview, focusPoint, new CameraOpeningSettings { shot = start, duration = 2.5f, ease = Ease.OutSine, dealStartProgress = .9f },
                new[] { new CameraCueSettings { key = "focus", kind = CameraActionKind.Focus, duration = .1f } });
            director = battleRoot.AddComponent<ScenePresentationDirector>();
            bridge = battleRoot.AddComponent<BattleTimelineSceneAdapter>(); bridge.Configure(director);
            executor = Child("CameraPresent", battleRoot.transform).AddComponent<CameraPresentation>();
            opening = Child("Opening", executor.transform).AddComponent<CameraOpeningTimelineAction>(); opening.Configure(executor, null);
            focus = Child("Focus", executor.transform).AddComponent<CameraTimelineAction>(); focus.Configure(executor, CameraActionKind.Focus); focus.SetSceneCueKey("focus");
            bridge.ConfigureCamera(config, executor);
        }
        private static GameObject Child(string name, Transform parent)
        { var go = new GameObject(name); go.transform.SetParent(parent, false); return go; }
        [TearDown] public void TearDown() { Object.DestroyImmediate(battleRoot); Object.DestroyImmediate(sceneRoot); }
        [Test] public void BindingIsExplicitAndDoesNotMoveCameraOrReparentSceneObjects()
        {
            Assert.IsNull(executor.OutputCamera);
            var position = camera.transform.position; var fov = camera.fieldOfView;
            bridge.BindCameraPresentation();
            Assert.AreSame(camera, executor.OutputCamera); Assert.AreSame(config, executor.SceneConfiguration);
            Assert.AreEqual(position, camera.transform.position); Assert.AreEqual(fov, camera.fieldOfView);
            Assert.AreSame(sceneRoot.transform, camera.transform.parent);
            Assert.AreEqual(2.5f, opening.Duration); Assert.AreEqual(.9f, opening.DealStartProgress); Assert.AreEqual(Ease.OutSine, opening.Ease);
            Assert.AreSame(start, opening.OpeningShot);
        }
        [Test] public void ExecutorsDoNotSerializeSceneParameters()
        {
            var rigData = new SerializedObject(executor); var openingData = new SerializedObject(opening); var cueData = new SerializedObject(focus);
            Assert.IsNull(rigData.FindProperty("outputCamera")); Assert.IsNull(rigData.FindProperty("defaultShot"));
            Assert.IsNull(openingData.FindProperty("duration")); Assert.IsNull(openingData.FindProperty("openingShot")); Assert.IsNull(openingData.FindProperty("ease"));
            Assert.IsNull(cueData.FindProperty("focusTarget")); Assert.IsNull(cueData.FindProperty("shakePosition"));
            Assert.IsNotNull(cueData.FindProperty("cueKey"));
        }
        [Test] public void MissingCueOrOpeningFailsBeforeBinding()
        {
            focus.SetSceneCueKey("missing"); Assert.Throws<InvalidOperationException>(() => bridge.BindCameraPresentation());
            Assert.IsNull(executor.OutputCamera);
            focus.SetSceneCueKey("focus"); config.Opening.shot = null;
            Assert.Throws<InvalidOperationException>(() => bridge.BindCameraPresentation());
        }
        [Test] public void InvalidTuningAndDuplicateKeysFailValidation()
        {
            config.Opening.duration = float.NaN; Assert.Throws<InvalidOperationException>(() => config.Validate());
            config.Opening.duration = 2.5f;
            config.Configure(camera, overview, null, config.Opening, new[] { config.Cues[0], config.Cues[0] });
            Assert.Throws<InvalidOperationException>(() => config.Validate());
        }
        [Test] public void SceneDataCanBeDestroyedBeforeExecutorCleanup()
        {
            bridge.BindCameraPresentation();
            Object.DestroyImmediate(sceneRoot); sceneRoot = null;
            Assert.DoesNotThrow(() => executor.ResetPresentation());
            Assert.DoesNotThrow(() => director.Synchronize(State()));
        }
        private static BattleDisplaySnapshot State() => new BattleDisplaySnapshot("battle", "player", 0, "player", 0, null, null, null);
        private sealed class Markers : IMarkerSink
        { public bool Ready; public bool Emit(string marker) { if (marker == "NearlyArrived") Ready = true; return true; } }
        [Test] public void SceneOpeningRetainsEaseThresholdAndPrivateTweenClock()
        {
            bridge.BindCameraPresentation();
            IEnumerator routine = null; opening.StartPresentationRoutine = body => { routine = body; return () => routine = null; };
            opening.DeltaTimeProvider = () => .125f;
            using (var scope = new PresentationScope("opening"))
            {
                var state = State(); var markers = new Markers();
                var op = opening.Play(new ActionContext("b", 0, "opening", new BattleDisplayChange(0, state.Cursor, state, "BattleStarted"), state, null, scope), markers);
                for (int i = 0; i < 10; i++) routine.MoveNext();
                var expected = Vector3.Lerp(start.transform.position, overview.transform.position, Mathf.Sin(Mathf.PI * .25f));
                Assert.That(Vector3.Distance(expected, camera.transform.position), Is.LessThan(.001f));
                for (int i = 10; i < 17; i++) routine.MoveNext(); Assert.IsFalse(markers.Ready);
                routine.MoveNext(); Assert.IsTrue(markers.Ready); Assert.AreEqual(PresentationStatus.Pending, op.Status);
                while (routine != null && routine.MoveNext()) { }
                Assert.AreEqual(PresentationStatus.Completed, op.Status); Assert.AreEqual(overview.transform.position, camera.transform.position);
            }
        }
        [Test] public void HostInitializationAndReattachmentReadFreshSceneSettings()
        {
            var host = battleRoot.AddComponent<BattleSceneHost>(); host.Configure(battleRoot.AddComponent<LayoutTestSource>(), bridge, false);
            var engine = new CardDuelEngine(new BattleRules("camera-bind", 5));
            var session = new BattleSession(engine, engine.Create("battle", HmacDeckShuffler.FromSeed(new byte[32]),
                new[] { new BattlePlayerSetup(new ParticipantId("player"), new CombatantDefinition("hero", 100),
                    deck: StandardDeck.Cards().Select((c, i) => new DeckEntry("c" + i, c.Id))) },
                new[] { new BattleEnemySetup(new ParticipantId("enemy"), new CombatantDefinition("enemy", 100), 1) }));
            host.AttachSession(session);
            Assert.AreSame(config, executor.SceneConfiguration); Assert.AreEqual(overview.transform.position, camera.transform.position);
            config.Opening.duration = 3.5f; config.Opening.ease = Ease.Linear;
            host.AttachSession(session);
            Assert.AreEqual(3.5f, opening.Duration); Assert.AreEqual(Ease.Linear, opening.Ease);
        }
    }
}
