using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PokerEngine;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Poker.SingleBattle.Tests
{
    public sealed class BattleAuthoredLayoutTests
    {
        sealed class Fixture : IDisposable
        {
            readonly string path;
            readonly Scene scene;
            public readonly SingleBattleSceneAdapter Host;
            public readonly BattleAuthoredLayout Layout;
            public readonly Camera Camera;
            public readonly BattleDemoEnemyWorldHud World;
            public readonly Transform Hud;
            public Fixture(string encounter)
            {
                path = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/__AuthoredLayoutTest.unity");
                Assert.IsTrue(AssetDatabase.CopyAsset("Assets/Scenes/SingleChapterAdventure.unity", path));
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                Host = All<SingleBattleSceneAdapter>().Single();
                for (var t = Host.transform; t != null; t = t.parent) t.gameObject.SetActive(true);
                Camera = All<Camera>().Single(c => c.CompareTag("MainCamera"));
                Layout = Host.GetComponent<BattleAuthoredLayout>(); Assert.NotNull(Layout); Assert.NotNull(Layout.Profile);
                var content = AssetDatabase.LoadAssetAtPath<TextAsset>("Packages/com.kaya.battle-engine/Content/SingleChapterBattle.xml");
                var compiled = BattleContentCompiler.CompileXml(content.text, content.name);
                var assembled = BattleContentFactory.Create(compiled.Content, new BattleSetupRequest(encounter, "layout-test", HmacDeckShuffler.CreateRandomState()));
                Assert.IsTrue(assembled.Success);
                var session = assembled.Session;
                var opening = session.Execute(session.State.Revision, session.State.Duel.PlayerId, CardDuelAction.Start);
                Assert.IsTrue(opening.Accepted);
                Host.ConfigureContent(content, encounter); Host.AttachCommands(new BattleSessionCommands(session), opening, false);
                World = Host.GetComponent<BattleDemoEnemyWorldHud>();
                Invoke(World, "Start");
                Hud = scene.GetRootGameObjects().Single(r => r.name == "BattleDemo Enemy World UI").transform;
                Refresh();
            }
            T[] All<T>() where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
            public Transform View(string id) => Hud.Find("Enemy World HUD " + Host.Enemies.Single(a => a.DisplayId == id).name);
            public void Refresh()
            {
                Host.SynchronizeBattle(); Host.Actions.Hand.GetComponent<CardPresentation.HandZoneView>().CompleteTransitions();
                Host.GetComponent<WrathBossArtworkLayout>()?.TryApply(Host.Display);
                Layout.ApplyActors(); Canvas.ForceUpdateCanvases(); Invoke(World, "Refresh"); Invoke(World, "LateUpdate"); Canvas.ForceUpdateCanvases();
            }
            public void Edit(params BattleAdminVitals[] values)
            { Assert.IsTrue(Host.Session.ApplyAdmin(Host.AuthorityState.Revision, new BattleAdminPatch(vitals: values)).Accepted); Refresh(); }
            public void Round()
            {
                var player = Host.Player.ParticipantId;
                Assert.IsTrue(Host.Session.Execute(Host.AuthorityState.Revision, player, CardDuelAction.EndTurn).Accepted);
                if (Host.AuthorityState.FlowFor(player).Window == BattlePlayerWindow.Ending)
                    Assert.IsTrue(Host.Session.Execute(Host.AuthorityState.Revision, player, CardDuelAction.FinishTurn).Accepted);
                Refresh();
            }
            public void Dispose()
            {
                if (Host != null) { Host.SnapToState(); Host.Actions.Hand.StopSession(); }
                if (Hud != null) UnityEngine.Object.DestroyImmediate(Hud.gameObject);
                EditorSceneManager.CloseScene(scene, true); AssetDatabase.DeleteAsset(path);
            }
        }
        static void Invoke(object value, string method) => value.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(value, null);
        static void Near(Vector3 a, Vector3 b, string message) => Assert.That(Vector3.Distance(a, b), Is.LessThan(.002f), message);

        [TestCase("single-chapter-battle-1", "01 普通战斗")]
        [TestCase("single-chapter-gatekeeper", "02 精英战斗")]
        [TestCase("single-chapter-wrath", "03 暴怒 Boss")]
        public void RuntimeGeometryMatchesTheEditedWorkshop(string encounter, string groupName)
        {
            var workshop = SceneManager.GetSceneByPath("Assets/Scenes/BattleLayoutWorkshop.unity");
            bool opened = !workshop.isLoaded;
            if (opened) workshop = EditorSceneManager.OpenScene("Assets/Scenes/BattleLayoutWorkshop.unity", OpenSceneMode.Additive);
            try
            {
                var group = workshop.GetRootGameObjects().Single(r => r.name == groupName).transform;
                var expectedActors = group.Cast<Transform>().Single(t => t.name.StartsWith("Enemies -"));
                var expectedHud = group.GetComponentInChildren<Canvas>(true).transform;
                using (var f = new Fixture(encounter))
                foreach (var actor in f.Host.Enemies)
                {
                    var expected = expectedActors.Cast<Transform>().Single(t => t.name.StartsWith(actor.DisplayId + " - "));
                    Near(actor.transform.position, expected.position, actor.DisplayId + " world position");
                    Near(actor.transform.lossyScale, expected.lossyScale, actor.DisplayId + " size");
                    var art = actor.Visual.Find("Artwork");
                    var expectedArt = expected.GetComponentsInChildren<MeshRenderer>(true).Single(r => r.name == "Artwork").transform;
                    Near(art.localPosition, expectedArt.localPosition, "art position"); Near(art.localScale, expectedArt.localScale, "art scale");
                    var authored = expectedHud.Find(actor.DisplayId + " - HUD");
                    var live = f.View(actor.DisplayId);
                    foreach (var rect in authored.GetComponentsInChildren<RectTransform>(true))
                    {
                        var path = AnimationUtility.CalculateTransformPath(rect, authored);
                        if (path.StartsWith("Enemy Badge Tooltip") || path == "Health Bar/Health Fill") continue;
                        var current = (path == "" ? live : live.Find(path)) as RectTransform;
                        if (current == null) continue; // A status absent in the live snapshot is intentionally not created.
                        Near(current.anchoredPosition3D, rect.anchoredPosition3D, actor.DisplayId + "/" + path + " position");
                        Near(current.sizeDelta, rect.sizeDelta, actor.DisplayId + "/" + path + " size");
                        Near(current.localScale, rect.localScale, actor.DisplayId + "/" + path + " scale");
                    }
                }
            }
            finally { if (opened) EditorSceneManager.CloseScene(workshop, true); }
        }

        [Test]
        public void HealthAndBlockRemainLiveWithoutResettingGeometry()
        {
            using (var f = new Fixture("single-chapter-battle-1"))
            {
                var view = f.View("enemy"); var health = (RectTransform)view.Find("Health Bar");
                var size = health.sizeDelta; var position = health.anchoredPosition;
                f.Edit(new BattleAdminVitals(new ParticipantId("enemy"), health: 37, maxHealth: 100, block: 12));
                StringAssert.Contains("37 / 100", health.Find("Health Value").GetComponent<TMP_Text>().text);
                Assert.That(health.Find("Concept Fill").GetComponent<Image>().fillAmount, Is.EqualTo(.37f).Within(.001f));
                Assert.IsTrue(view.Find("Current Block").gameObject.activeSelf);
                StringAssert.Contains("12", view.Find("Current Block/Value").GetComponent<TMP_Text>().text);
                Near(health.sizeDelta, size, "health size after damage"); Near(health.anchoredPosition, position, "health position after damage");
                f.Edit(new BattleAdminVitals(new ParticipantId("enemy"), block: 0));
                Assert.IsFalse(view.Find("Current Block").gameObject.activeSelf);
            }
        }

        [Test]
        public void EliteIntentCountChangesWhileTheEditedRowAndCaptionsPersist()
        {
            using (var f = new Fixture("single-chapter-gatekeeper"))
            {
                f.Edit(new BattleAdminVitals(f.Host.Player.ParticipantId, health: 60000, maxHealth: 60000));
                var row = (RectTransform)f.View("enemy").Find("Intents");
                var position = row.anchoredPosition;
                var caption = (RectTransform)row.Find("Intent 0/Action Caption");
                var captionPosition = caption.anchoredPosition;
                Assert.AreEqual(2, row.GetComponentsInChildren<BattleDemoEnemyBadgeTooltip>().Length);
                f.Round();
                Assert.AreEqual(1, row.GetComponentsInChildren<BattleDemoEnemyBadgeTooltip>().Length);
                Near(row.anchoredPosition, position, "changed intent row"); Near(caption.anchoredPosition, captionPosition, "caption position");
                f.Round();
                Assert.GreaterOrEqual(row.GetComponentsInChildren<BattleDemoEnemyBadgeTooltip>().Length, 1);
                Near(row.anchoredPosition, position, "subsequent intent row");
            }
        }

        [Test]
        public void BossStatusAndRegenerationKeepTheEditedRows()
        {
            using (var f = new Fixture("single-chapter-wrath"))
            {
                f.Edit(new BattleAdminVitals(f.Host.Player.ParticipantId, health: 60000, maxHealth: 60000));
                var body = f.View("enemy"); var statusPosition = ((RectTransform)body.Find("Statuses")).anchoredPosition;
                for (int i = 0; i < 3; i++) f.Round();
                Assert.Greater(body.Find("Statuses").GetComponentsInChildren<BattleDemoEnemyBadgeTooltip>().Length, 0);
                Near(((RectTransform)body.Find("Statuses")).anchoredPosition, statusPosition, "new status uses edited row");
                var hand = f.View("hand-1"); var intentPosition = ((RectTransform)hand.Find("Intents")).anchoredPosition;
                f.Edit(new BattleAdminVitals(new ParticipantId("hand-1"), health: 0));
                Assert.IsTrue(hand.gameObject.activeSelf); Assert.IsFalse(hand.Find("Health Bar").gameObject.activeSelf);
                Assert.IsFalse(hand.Find("Intents").gameObject.activeSelf);
                StringAssert.Contains("复活倒计时", hand.Find("Revive Countdown").GetComponent<TMP_Text>().text);
                Near(hand.Find("Revive Countdown").position, hand.Find("Intents").position, "countdown uses edited intent position");
                Near(((RectTransform)hand.Find("Intents")).anchoredPosition, intentPosition, "regeneration uses edited intent row");
            }
        }

        [Test]
        public void BossCompositionAndHudScaleTogetherAcrossAspectRatios()
        {
            using (var f = new Fixture("single-chapter-wrath"))
            {
                RenderTexture target = null;
                var originalTarget = f.Camera.targetTexture;
                try
                {
                    void Resize(Vector2Int resolution)
                    {
                        var next = new RenderTexture(resolution.x, resolution.y, 0);
                        f.Camera.targetTexture = next;
                        f.Camera.ResetAspect();
                        if (target != null) UnityEngine.Object.DestroyImmediate(target);
                        target = next;
                        f.Refresh();
                    }

                    Dictionary<string, Vector2> Capture()
                    {
                        var points = new Dictionary<string, Vector2>();
                        foreach (var actor in f.Host.Enemies)
                        {
                            // Basis points check both placement and size of every boss part.
                            points[actor.DisplayId + "/origin"] = f.Camera.WorldToScreenPoint(actor.transform.position);
                            points[actor.DisplayId + "/right"] = f.Camera.WorldToScreenPoint(actor.transform.TransformPoint(Vector3.right));
                            points[actor.DisplayId + "/up"] = f.Camera.WorldToScreenPoint(actor.transform.TransformPoint(Vector3.up));
                            foreach (var name in new[] { "Health Bar", "Intents" })
                            {
                                var rect = (RectTransform)f.View(actor.DisplayId).Find(name);
                                Assert.NotNull(rect, actor.DisplayId + "/" + name);
                                var corners = new Vector3[4];
                                rect.GetWorldCorners(corners);
                                for (int i = 0; i < corners.Length; i++)
                                    points[actor.DisplayId + "/" + name + "/" + i] = RectTransformUtility.WorldToScreenPoint(null, corners[i]);
                            }
                        }
                        return points;
                    }

                    var entry = f.Layout.Profile.Find("single-chapter-wrath", "enemy");
                    var referenceSize = new Vector2Int(Mathf.RoundToInt(720 * entry.referenceAspect), 720);
                    Resize(referenceSize);
                    var reference = Capture();
                    foreach (var resolution in new[] { referenceSize, new Vector2Int(1024, 768), new Vector2Int(1600, 900), new Vector2Int(2520, 1080) })
                    {
                        Resize(resolution);
                        float scale = Mathf.Min((float)resolution.x / referenceSize.x, (float)resolution.y / referenceSize.y);
                        var translation = ((Vector2)resolution - (Vector2)referenceSize * scale) * .5f;
                        for (int refresh = 0; refresh < 2; refresh++)
                        {
                            var actual = Capture();
                            Assert.AreEqual(reference.Count, actual.Count);
                            foreach (var point in reference)
                            {
                                // Half a pixel permits rounding the reference aspect to texture dimensions.
                                Assert.That(Vector2.Distance(actual[point.Key], translation + point.Value * scale), Is.LessThan(.5f),
                                    resolution + "/refresh " + refresh + "/" + point.Key);
                            }
                            if (refresh == 0) f.Refresh(); // Reapplying must not accumulate HUD scaling.
                        }
                    }
                }
                finally
                {
                    f.Camera.targetTexture = originalTarget;
                    f.Camera.ResetAspect();
                    if (target != null) UnityEngine.Object.DestroyImmediate(target);
                }
            }
        }

        [Test]
        public void SecondOrdinaryEncounterUsesTheSameAuthoredLayout()
        {
            using (var f = new Fixture("single-chapter-battle-2"))
            {
                Assert.AreSame(f.Layout.Profile.Find("single-chapter-battle-1", "enemy"), f.Layout.Profile.Find("single-chapter-battle-2", "enemy"));
                f.Camera.aspect = 16f / 9f; f.Refresh();
                var point = f.Camera.WorldToViewportPoint(f.Host.Enemy.transform.position);
                Assert.That(point.x, Is.InRange(0, 1)); Assert.That(point.y, Is.InRange(0, 1));
                Assert.IsTrue(f.View("enemy").gameObject.activeSelf);
            }
        }
    }
}
