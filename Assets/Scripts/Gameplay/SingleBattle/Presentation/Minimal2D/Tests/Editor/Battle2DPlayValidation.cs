using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Poker.Battle2DValidation.Editor;
using PokerEngine;
using RetroRendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Poker.Battle2DValidation.Tests
{
    /// <summary>Exercises the live scene through raycasted pointer events and its actual presentation clock.</summary>
    public static class Battle2DPlayValidation
    {
        private const string Folder = "Library/Battle2DValidation";
        private const string ScreenshotPath = Folder + "/game-screen.png";
        private const string RunMenu = "工具/卡牌战斗/2D验证/验证运行时输入与表现";
        private const string RenderingMenu = "工具/卡牌战斗/2D验证/记录当前渲染配置";
        private static Battle2DHost host;
        private static Battle2DValidationTick driver;
        private static EventSystem events;
        private static Camera camera;
        private static CardDuelState committed;
        private static Battle2DCard movingCard;
        private static CardDuelAction action;
        private static Vector3 playerHome, enemyHome, cardStart;
        private static double deadline;
        private static int stage;
        private static Report report;
        internal static bool IsRunning => host != null;

        [MenuItem(RunMenu)]
        public static void Run()
        {
            if (!Application.isPlaying || EditorApplication.isPaused || IsRunning)
                throw new InvalidOperationException("请在未暂停的 Play 状态下运行，并等待上一次验证结束。");
            var candidate = Object.FindObjectsOfType<Battle2DHost>().Single(item => item.isActiveAndEnabled &&
                item.gameObject.scene.path == Battle2DValidationSetup.ScenePath);
            if (candidate.View.IsPresenting) throw new InvalidOperationException("请等待当前 2D 表现播放完成。");
            host = candidate;
            report = new Report { status = "Running", scene = host.gameObject.scene.path,
                utc = DateTime.UtcNow.ToString("O"), gameScreenCapture = ScreenshotPath, pipeline = ReadPipeline() };
            Directory.CreateDirectory(Folder);
            if (File.Exists(ScreenshotPath)) File.Delete(ScreenshotPath);
            stage = 0; committed = null; movingCard = null; deadline = EditorApplication.timeSinceStartup + 30;
            var value = new GameObject("2D Play Validation (Temporary)") { hideFlags = HideFlags.DontSave };
            SceneManager.MoveGameObjectToScene(value, host.gameObject.scene);
            driver = value.AddComponent<Battle2DValidationTick>();
        }

        [MenuItem(RunMenu, true)] private static bool CanRun() => Application.isPlaying && !EditorApplication.isPaused && !IsRunning;

        [MenuItem(RenderingMenu)]
        public static void RecordRenderingConfiguration()
        {
            Directory.CreateDirectory(Folder);
            string path = Folder + (Application.isPlaying ? "/rendering-play.json" : "/rendering-edit.json");
            File.WriteAllText(path, JsonUtility.ToJson(ReadPipeline(), true));
            Debug.Log("已记录当前渲染配置：" + path);
        }

        internal static void Tick()
        {
            if (!IsRunning) return;
            try
            {
                if (!Application.isPlaying || EditorApplication.timeSinceStartup > deadline)
                    throw new TimeoutException("2D 输入与表现验证超时或 Play 已停止。");
                if (stage > 0) AssertRenderingConfiguration();
                if (committed != null) Assert.AreSame(committed, host.Session.State, "播放不能再次推进权威规则。");
                SampleMotion();
                switch (stage)
                {
                    case 0:
                        events = Object.FindObjectsOfType<EventSystem>().Single(item => item.isActiveAndEnabled && item.gameObject.scene == host.gameObject.scene);
                        camera = Object.FindObjectsOfType<Camera>().Single(item => item.isActiveAndEnabled && item.gameObject.scene == host.gameObject.scene && item.GetComponent<PhysicsRaycaster>() != null);
                        Assert.IsTrue(camera.orthographic);
                        Assert.IsNotNull(host.View.PlayerActor); Assert.IsNotNull(host.View.EnemyActor);
                        host.StartBattle();
                        playerHome = host.View.PlayerActor.localPosition; enemyHome = host.View.EnemyActor.localPosition;
                        report.playerHome = playerHome; report.enemyHome = enemyHome; report.initialRevision = host.Session.State.Revision;
                        AssertRenderingConfiguration(); report.pipeline = ReadPipeline(camera);
                        Assert.AreSame(host.Session.State, host.View.PresentedState); stage = 1; break;
                    case 1: BeginCardAction(CardDuelAction.Play); stage = 2; break;
                    case 2:
                        if (!Settled()) break;
                        Assert.AreEqual(1, host.Session.State.Duel.Scores.Count); report.playCompleted = true; stage = 3; break;
                    case 3: BeginCardAction(CardDuelAction.Discard); stage = 4; break;
                    case 4:
                        if (!Settled()) break;
                        Assert.AreEqual(ScoredDuelActionKind.Discard, host.Session.State.Duel.Actions.Last().Kind);
                        report.discardCompleted = true; stage = 5; break;
                    case 5: BeginEndTurn(); stage = 6; break;
                    case 6:
                        if (!Settled()) break;
                        Assert.AreEqual(1, host.Session.State.Duel.CompletedRounds); report.enemyTurnCompleted = true; stage = 7; break;
                    case 7: BeginEndTurn(); stage = 8; break;
                    case 8:
                        Assert.IsTrue(host.View.IsPresenting, "需要在矩形尚在移动时取消表现。");
                        if (!ActorMoved()) break;
                        // Rule settlement already happened. Disable/enable only catches the display up.
                        host.View.enabled = false;
                        try
                        {
                            Assert.IsFalse(host.View.IsPresenting); Assert.AreSame(committed, host.View.PresentedState);
                            Assert.AreSame(committed, host.Session.State); AssertActorHomes();
                        }
                        finally { host.View.enabled = true; }
                        report.disabledPlaybackSynchronized = true; stage = 9; break;
                    case 9:
                        Assert.IsTrue(Settled()); Assert.AreEqual(2, host.Session.State.Duel.CompletedRounds);
                        Assert.IsTrue(report.playCardMoved); Assert.IsTrue(report.discardCardMoved);
                        Assert.IsTrue(report.playerRectangleMoved); Assert.IsTrue(report.enemyRectangleMoved);
                        Canvas.ForceUpdateCanvases(); ScreenCapture.CaptureScreenshot(ScreenshotPath);
                        stage = 10; break;
                    case 10:
                        if (!File.Exists(ScreenshotPath) || new FileInfo(ScreenshotPath).Length == 0) break;
                        Finish(null); break;
                }
            }
            catch (Exception error) { Finish(error); }
        }

        private static void BeginCardAction(CardDuelAction nextAction)
        {
            Assert.IsFalse(host.View.IsPresenting); Assert.IsEmpty(host.View.Selected);
            var card = host.View.Cards.First();
            var collider = card.GetComponent<Collider>(); Assert.IsNotNull(collider);
            Physics.SyncTransforms();
            var screen = camera.WorldToScreenPoint(collider.bounds.center);
            Assert.Greater(screen.z, 0);
            ClickAt(screen, target => target.GetComponentInParent<Battle2DCard>() == card, "卡牌");
            Assert.AreEqual(card.Id, host.View.Selected.Single()); report.cardRaycastClicks++;
            movingCard = card; cardStart = card.transform.localPosition; action = nextAction;
            BeginButton(nextAction == CardDuelAction.Play ? host.View.PlayButton : host.View.DiscardButton);
        }
        private static void BeginEndTurn()
        {
            movingCard = null; action = CardDuelAction.EndTurn; BeginButton(host.View.EndTurnButton);
        }
        private static void BeginButton(Button button)
        {
            Assert.IsTrue(button.interactable);
            var before = host.Session.State;
            var rect = (RectTransform)button.transform; var canvas = button.GetComponentInParent<Canvas>();
            var screen = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                rect.TransformPoint(rect.rect.center));
            ClickAt(screen, target => target.GetComponentInParent<Button>() == button, button.name);
            report.buttonRaycastClicks++;
            committed = host.Session.State;
            Assert.AreNotSame(before, committed); Assert.Greater(committed.Revision, before.Revision);
            Assert.IsTrue(host.View.IsPresenting); Assert.IsFalse(host.CanEndTurn);
            Assert.IsFalse(host.View.PlayButton.interactable); Assert.IsFalse(host.View.DiscardButton.interactable);
            Assert.IsFalse(host.View.EndTurnButton.interactable);
            SampleMotion();
        }
        private static void ClickAt(Vector2 screen, Func<GameObject, bool> expected, string label)
        {
            Canvas.ForceUpdateCanvases(); Physics.SyncTransforms();
            var pointer = new PointerEventData(events) { position = screen, button = PointerEventData.InputButton.Left, clickCount = 1 };
            var hits = new List<RaycastResult>(); events.RaycastAll(pointer, hits);
            Assert.IsTrue(hits.Count > 0 && expected(hits[0].gameObject), label + "应是指针的首个命中；实际：" +
                string.Join(",", hits.Select(hit => hit.gameObject.name)));
            pointer.pointerCurrentRaycast = hits[0]; pointer.pointerPressRaycast = hits[0];
            var target = hits[0].gameObject;
            pointer.pointerPress = ExecuteEvents.ExecuteHierarchy(target, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(target, pointer, ExecuteEvents.pointerUpHandler);
            Assert.IsNotNull(ExecuteEvents.ExecuteHierarchy(target, pointer, ExecuteEvents.pointerClickHandler));
        }

        private static void SampleMotion()
        {
            if (stage == 0 || host == null) return;
            if (host.View.PlayerActor != null && Vector3.Distance(host.View.PlayerActor.localPosition, playerHome) > .001f) report.playerRectangleMoved = true;
            if (host.View.EnemyActor != null && Vector3.Distance(host.View.EnemyActor.localPosition, enemyHome) > .001f) report.enemyRectangleMoved = true;
            if (movingCard == null || Vector3.Distance(movingCard.transform.localPosition, cardStart) <= .001f) return;
            if (action == CardDuelAction.Play) report.playCardMoved = true;
            if (action == CardDuelAction.Discard) report.discardCardMoved = true;
        }
        private static bool ActorMoved() => Vector3.Distance(host.View.PlayerActor.localPosition, playerHome) > .001f ||
            Vector3.Distance(host.View.EnemyActor.localPosition, enemyHome) > .001f;
        private static void AssertActorHomes()
        {
            Assert.That(Vector3.Distance(host.View.PlayerActor.localPosition, playerHome), Is.LessThan(.0001f));
            Assert.That(Vector3.Distance(host.View.EnemyActor.localPosition, enemyHome), Is.LessThan(.0001f));
        }
        private static bool Settled()
        {
            if (host.View.IsPresenting) return false;
            Assert.AreSame(committed, host.Session.State); Assert.AreSame(committed, host.View.PresentedState);
            AssertActorHomes(); Assert.IsEmpty(host.View.Selected);
            CollectionAssert.AreEqual(committed.Zone(CardZoneKinds.Hand, host.PlayerId).Cards, host.View.Cards.Select(card => card.Id));
            movingCard = null; return true;
        }

        internal static void Interrupted()
        { if (report != null) Finish(new OperationCanceledException("2D 验证完成前已停止 Play 或卸载场景。")); }
        private static void Finish(Exception error)
        {
            if (report == null) return;
            report.status = error == null ? "Passed" : "Failed"; report.error = error?.ToString();
            report.stage = stage; report.finalRevision = host?.Session?.State.Revision ?? -1;
            report.completedRounds = host?.Session?.State.Duel.CompletedRounds ?? -1;
            report.pipeline = ReadPipeline();
            Directory.CreateDirectory(Folder); File.WriteAllText(Folder + "/play.json", JsonUtility.ToJson(report, true));
            if (error != null && Application.isPlaying) ScreenCapture.CaptureScreenshot(ScreenshotPath);
            var value = driver; host = null; driver = null; committed = null; movingCard = null; report = null;
            if (value != null) Object.Destroy(value.gameObject);
            if (error != null) Debug.LogException(error);
            else Debug.Log("2D 运行验证通过：真实卡牌/按钮射线输入、出弃牌移动、双方矩形移动、逐步显示和禁用同步。结果：" + Folder + "/play.json");
        }
        private static void AssertRenderingConfiguration()
        {
            Assert.AreEqual("Assets/Settings/MainURP.asset", AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline));
            Assert.AreEqual("Assets/Settings/MainURP.asset", AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
            Assert.AreEqual("Assets/Settings/MainURP.asset", AssetDatabase.GetAssetPath(GraphicsSettings.currentRenderPipeline));
            Assert.IsInstanceOf<Simple2DRenderer>(camera.GetUniversalAdditionalCameraData().scriptableRenderer);
        }
        private static PipelineRecord ReadPipeline(Camera target = null)
        {
            if (target == null) target = Object.FindObjectsOfType<Camera>().FirstOrDefault(item => item.isActiveAndEnabled &&
                item.gameObject.scene.path == Battle2DValidationSetup.ScenePath && item.GetComponent<PhysicsRaycaster>() != null);
            var record = new PipelineRecord
            {
                utc = DateTime.UtcNow.ToString("O"), isPlaying = Application.isPlaying,
                graphicsPipeline = AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline),
                qualityPipeline = AssetDatabase.GetAssetPath(QualitySettings.renderPipeline),
                activePipeline = AssetDatabase.GetAssetPath(GraphicsSettings.currentRenderPipeline),
                qualityLevel = QualitySettings.GetQualityLevel(), cameraRendererIndex = -1
            };
            if (target != null)
            {
                var data = target.GetUniversalAdditionalCameraData();
                record.camera = target.name; record.cameraRenderer = data.scriptableRenderer?.GetType().FullName;
                record.cameraRendererIndex = new SerializedObject(data).FindProperty("m_RendererIndex").intValue;
                var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if (pipeline != null)
                {
                    var renderers = new SerializedObject(pipeline).FindProperty("m_RendererDataList");
                    int index = record.cameraRendererIndex;
                    if (index < 0) index = new SerializedObject(pipeline).FindProperty("m_DefaultRendererIndex").intValue;
                    if (index >= 0 && index < renderers.arraySize)
                        record.cameraRendererAsset = AssetDatabase.GetAssetPath(renderers.GetArrayElementAtIndex(index).objectReferenceValue);
                }
            }
            return record;
        }
        [Serializable] private sealed class PipelineRecord
        {
            public string utc, graphicsPipeline, qualityPipeline, activePipeline, camera, cameraRenderer, cameraRendererAsset;
            public bool isPlaying;
            public int qualityLevel, cameraRendererIndex;
        }
        [Serializable] private sealed class Report
        {
            public string status, error, scene, utc, gameScreenCapture;
            public PipelineRecord pipeline;
            public int stage, cardRaycastClicks, buttonRaycastClicks, completedRounds;
            public long initialRevision, finalRevision;
            public bool playCompleted, discardCompleted, enemyTurnCompleted, playCardMoved, discardCardMoved,
                playerRectangleMoved, enemyRectangleMoved, disabledPlaybackSynchronized;
            public Vector3 playerHome, enemyHome;
        }
    }

    public sealed class Battle2DValidationTick : MonoBehaviour
    {
        private void Update() => Battle2DPlayValidation.Tick();
        private void OnDestroy() => Battle2DPlayValidation.Interrupted();
    }
}
