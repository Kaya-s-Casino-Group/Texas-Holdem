using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PokerEngine;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Poker.SingleBattle.Tests
{
    public sealed class BattleEnemyBadgeFollowupTests
    {
        private sealed class Fixture : IDisposable
        {
            private readonly string path;
            private readonly UnityEngine.SceneManagement.Scene scene;
            public readonly SingleBattleSceneAdapter Host;
            public readonly BattleDemoEnemyWorldHud World;
            public readonly BattleDemoTargetSelection Selector;
            public readonly Transform Root;

            public Fixture()
            {
                path = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/__EnemyBadgeFollowupTest.unity");
                Assert.IsTrue(AssetDatabase.CopyAsset("Assets/Scenes/SingleChapterAdventure.unity", path));
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                Host = All<SingleBattleSceneAdapter>().Single();
                for (var parent = Host.transform; parent != null; parent = parent.parent) parent.gameObject.SetActive(true);
                var content = AssetDatabase.LoadAssetAtPath<TextAsset>("Packages/com.kaya.battle-engine/Content/SingleChapterBattle.xml");
                var compiled = BattleContentCompiler.CompileXml(content.text, "enemy-badge-followup");
                var assembled = BattleContentFactory.Create(compiled.Content,
                    new BattleSetupRequest("single-chapter-battle-1", "enemy-badge-followup", HmacDeckShuffler.FromSeed(new byte[32])));
                Assert.IsTrue(assembled.Success);
                var session = assembled.Session;
                var opening = session.Execute(session.State.Revision, session.State.Duel.PlayerId, CardDuelAction.Start);
                Assert.IsTrue(opening.Accepted);
                Host.ConfigureContent(content, "single-chapter-battle-1");
                Host.AttachCommands(new BattleSessionCommands(session), opening, false);
                World = Host.GetComponent<BattleDemoEnemyWorldHud>(); Invoke(World, "Start");
                Root = scene.GetRootGameObjects().Single(root => root.name == "BattleDemo Enemy World UI").transform;
                Selector = Host.GetComponent<BattleDemoTargetSelection>(); Invoke(Selector, "Start");
                Refresh();
            }

            private T[] All<T>() where T : Component => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
            public Transform EnemyHud => Root.Find("Enemy World HUD " + Host.Enemies.Single().name);
            public BattleDemoEnemyBadgeTooltip Poison => EnemyHud.Find("Statuses").GetComponentsInChildren<BattleDemoEnemyBadgeTooltip>()
                .Single(badge => badge.Description.Contains("中毒"));
            public void Cards(params string[] definitions)
            {
                Assert.IsTrue(Host.Session.ApplyAdmin(Host.AuthorityState.Revision, new BattleAdminPatch(zones: new[] {
                    new BattleAdminZone(Host.Player.ParticipantId, CardZoneKinds.Hand, definitions.Select(id => new BattleAdminCard(definitionId: id))) })).Accepted);
                Refresh();
                foreach (var card in Host.Actions.Hand.Cards.ToArray())
                    Host.Actions.Hand.SetInteraction(card.Id, new CardPresentation.CardInteractionState(isSelected: true));
                Host.Actions.Hand.Publish(); Host.Actions.Hand.GetComponent<CardPresentation.HandZoneView>().CompleteTransitions();
                Host.RefreshCandidate(); Render();
            }
            public void PoisonWithRealStraight()
            {
                Cards("standard:1:2", "standard:2:3", "standard:3:4", "standard:4:5", "standard:1:6");
                var result = Host.Session.Execute(Host.AuthorityState.Revision, Host.Player.ParticipantId, CardDuelAction.Play,
                    Host.Session.State.Zone(CardZoneKinds.Hand, Host.Player.ParticipantId).Cards, new[] { Host.Enemies.Single().ParticipantId });
                Assert.IsTrue(result.Accepted, result.Error.ToString()); Refresh();
                Assert.AreEqual(5, Host.Display.Unit(Host.Enemies.Single().DisplayId).Statuses.Single(status => status.Id == "bleed").Stacks);
            }
            public void Refresh()
            {
                Host.SynchronizeBattle(); Host.Actions.Hand.GetComponent<CardPresentation.HandZoneView>().CompleteTransitions();
                Render();
            }
            public void Render()
            {
                var hud = All<SingleBattleHud>().Single();
                hud.GetComponent<DeskBattleLayout>().Apply(); hud.GetComponent<BattleAuthoredUILayout>().Apply(); hud.Refresh();
                Host.GetComponent<BattleAuthoredLayout>().ApplyActors();
                Invoke(Selector, "LateUpdate"); Invoke(World, "Refresh"); Invoke(World, "LateUpdate"); Canvas.ForceUpdateCanvases();
            }
            public EventSystem Events => All<EventSystem>().Single();
            public void Dispose()
            {
                Host.SnapToState(); Host.Actions.Hand.StopSession();
                foreach (var target in All<RectTransform>().Where(rect => rect.name.StartsWith("Target ", StringComparison.Ordinal)).ToArray())
                    UnityEngine.Object.DestroyImmediate(target.gameObject);
                foreach (var contour in Host.Enemies.SelectMany(actor => actor.Visual.GetComponentsInChildren<MeshRenderer>(true))
                    .Where(renderer => renderer.name == "Selected enemy contour").ToArray()) UnityEngine.Object.DestroyImmediate(contour.gameObject);
                if (Root != null) UnityEngine.Object.DestroyImmediate(Root.gameObject);
                EditorSceneManager.CloseScene(scene, true); AssetDatabase.DeleteAsset(path);
            }
        }

        // Catches the former small status frame/font without changing the authored row or poison artwork.
        [Test]
        public void EnemyPoisonBadgeEnlargesItsFrameArtworkAndTextWithinTheExistingRow()
        {
            using (var f = new Fixture())
            {
                f.PoisonWithRealStraight();
                var badge = f.Poison; var rect = badge.GetComponent<RectTransform>();
                Assert.GreaterOrEqual(rect.rect.width, 74 * 1.25f); Assert.GreaterOrEqual(rect.rect.height, 24 * 1.25f);
                Assert.That(rect.anchoredPosition, Is.EqualTo(Vector2.zero));
                var row = rect.parent.GetComponent<RectTransform>();
                Assert.That(row.anchorMin, Is.EqualTo(new Vector2(.5f, .5f)));
                Assert.AreEqual(-60, row.anchoredPosition.x, .01f); Assert.AreEqual(-62.8f, row.anchoredPosition.y, .01f);
                var art = badge.GetComponentsInChildren<Image>().Single(image => image.name == "Concept Icon");
                Assert.AreSame(ConceptBattleSkin.Get("Icons/icon_poison"), art.sprite); Assert.IsTrue(art.preserveAspect);
                var text = badge.GetComponentInChildren<TMP_Text>(); text.ForceMeshUpdate();
                Assert.GreaterOrEqual(text.fontSize, 12 * 1.25f); StringAssert.Contains("5", text.text);
                Assert.LessOrEqual(text.textBounds.size.x, text.rectTransform.rect.width + .01f);
            }
        }

        // EditMode has no rendered Graphic.depth; PlayMode covers the global EventSystem hit ordering.
        [Test]
        public void PoisonBadgeHitSurfaceAcceptsPointerAndOpensItsOwnExplanation()
        {
            using (var f = new Fixture())
            {
                f.PoisonWithRealStraight(); var badge = f.Poison;
                var canvas = badge.GetComponentInParent<Canvas>(); var raycaster = canvas.GetComponent<GraphicRaycaster>();
                Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode); Assert.IsNull(raycaster.eventCamera);
                var rect = badge.transform.Find("Concept Icon") as RectTransform ?? badge.GetComponent<RectTransform>();
                var point = RectTransformUtility.WorldToScreenPoint(raycaster.eventCamera, rect.TransformPoint(rect.rect.center));
                var hitSurface = badge.GetComponent<Image>();
                Assert.IsTrue(hitSurface.isActiveAndEnabled); Assert.IsTrue(hitSurface.raycastTarget);
                Assert.IsTrue(RectTransformUtility.RectangleContainsScreenPoint(hitSurface.rectTransform, point, raycaster.eventCamera));
                Assert.IsTrue(hitSurface.Raycast(point, raycaster.eventCamera), "The real badge and its CanvasGroups must accept a pointer at the visible poison art.");
                var pointer = new PointerEventData(f.Events) { position = point };
                var entered = ExecuteEvents.GetEventHandler<IPointerEnterHandler>(hitSurface.gameObject);
                Assert.AreEqual(badge.gameObject, entered);
                ExecuteEvents.Execute(entered, pointer, ExecuteEvents.pointerEnterHandler);
                var tooltip = f.EnemyHud.Find("Enemy Badge Tooltip"); Assert.IsTrue(tooltip.gameObject.activeInHierarchy);
                StringAssert.Contains("中毒", tooltip.GetComponentInChildren<TMP_Text>().text);
                ExecuteEvents.Execute(entered, pointer, ExecuteEvents.pointerExitHandler); Assert.IsFalse(tooltip.gameObject.activeSelf);
            }
        }

        // Catches reintroducing the inline target caption while retaining the actual rule choice and gold contour.
        [Test]
        public void SelectedEnemyRetainsGoldContourAndRuleTargetWithoutAppendingToItsHealth()
        {
            using (var f = new Fixture())
            {
                f.Cards("standard:1:2"); var actor = f.Host.Enemies.Single();
                Assert.IsTrue(f.Selector.ChooseTarget(actor)); f.Render();
                Assert.AreEqual(actor.DisplayId, f.Host.PlayTargetChoices.Single().SelectedId);
                var contour = actor.Visual.GetComponentsInChildren<MeshRenderer>(true).Single(renderer => renderer.name == "Selected enemy contour");
                Assert.IsTrue(contour.enabled); var properties = new MaterialPropertyBlock(); contour.GetPropertyBlock(properties);
                var gold = properties.GetColor("_OutlineColor"); Assert.Greater(gold.r, .95f); Assert.Greater(gold.g, .8f); Assert.Less(gold.b, .4f);
                Assert.AreEqual("350 / 350", f.EnemyHud.Find("Health Bar/Health Value").GetComponent<TMP_Text>().text);
            }
        }

        private static void Invoke(object target, string method) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
