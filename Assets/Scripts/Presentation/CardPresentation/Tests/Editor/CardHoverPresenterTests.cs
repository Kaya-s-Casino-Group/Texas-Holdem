using CardArt;
using System;
using System.Linq;
using CardInteraction;
using CardPresentation.Editor;
using NUnit.Framework;
using PokerEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CardPresentation.Tests
{
    [TestFixture]
    public sealed class CardHoverPresenterTests
    {
        private sealed class Fixture : IDisposable
        {
            public readonly Scene Scene;
            public readonly GameObject Root;
            public readonly HandZoneView Zone;
            public readonly CardZonePlayHost Host;
            public readonly Camera Camera;
            public readonly CardHoverPresenter Presenter;
            public readonly CardTooltipView Prefab;

            public Fixture()
            {
                Scene = EditorSceneManager.NewPreviewScene();
                Root = new GameObject("Hover Tooltip Test") { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(Root, Scene);
                var zoneRoot = new GameObject("Hand"); zoneRoot.transform.SetParent(Root.transform, false);
                Zone = zoneRoot.AddComponent<HandZoneView>();
                Zone.Configure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameAssets/CardArt/Prefabs/Card.prefab"), CardZoneLayoutSettings.Default);
                var motion = Zone.MotionSettings; motion.Enabled = false; Zone.SetMotionSettings(motion);
                var transition = Zone.TransitionSettings; transition.Enabled = false; Zone.SetTransitionSettings(transition);
                var lift = Zone.LiftSettings; lift.Enabled = false; Zone.SetLiftSettings(lift);
                Zone.SetDrawSound(null, enabled: false); Zone.SetSelectionSound(null, enabled: false); Zone.SetDiscardSound(null, enabled: false);
                var model = new CardZonePreviewModel(); model.Add("hearts_q"); model.Add("spades_a");
                Host = zoneRoot.AddComponent<CardZonePlayHost>();
                Host.Capture(model.BuildZone(), model.Cards.Select(card => new CardZoneCardState(
                    card.ToCard(), card.ArtId, card.FaceUp, card.Tint, card.Interaction)));
                Host.BeginSession();
                var cameraRoot = new GameObject("Camera"); cameraRoot.transform.SetParent(Root.transform, false);
                Camera = cameraRoot.AddComponent<Camera>();
                Camera.transform.position = new Vector3(0, 0.4f, -0.2f); Camera.transform.LookAt(Vector3.zero);
                Camera.nearClipPlane = 0.01f; Camera.farClipPlane = 4; Camera.orthographic = true;
                Camera.orthographicSize = 0.2f; Camera.aspect = 1.6f;
                var uiRoot = new GameObject("UI"); uiRoot.transform.SetParent(Root.transform, false);
                Presenter = uiRoot.AddComponent<CardHoverPresenter>();
                Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardTooltipPrefabBuilder.PrefabPath).GetComponent<CardTooltipView>();
                Presenter.Configure(Prefab, Camera, new MonoBehaviour[] { Host });
            }

            public void Hover(int index)
            {
                var view = Zone.Views[index]; var mesh = view.Surface.GetComponent<MeshFilter>().sharedMesh;
                var center = view.Surface.transform.TransformPoint(mesh.bounds.center);
                Host.MouseInput.Controller.ProcessPointer(new Ray(center + Zone.transform.up, -Zone.transform.up));
                Presenter.RefreshPresentation();
            }

            public void Dispose()
            {
                Host.StopSession(); Zone.Clear(); Object.DestroyImmediate(Root); EditorSceneManager.ClosePreviewScene(Scene);
            }
        }

        private static DeckOverviewPresenter AddOverview(Fixture f)
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(DeckOverviewSetup.PrefabPath));
            SceneManager.MoveGameObjectToScene(root,f.Scene);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = f.Camera; canvas.planeDistance = 1; canvas.sortingOrder = 250;
            Canvas.ForceUpdateCanvases();
            var source = root.AddComponent<DeckOverviewPresenter>();
            source.Configure(f.Host.Match,root.GetComponent<DeckOverviewView>(),f.Host.DeckZone.Id,f.Host.Cards.Select(card => card.Card.Id));
            Assert.AreSame(f.Presenter,CardHoverPresenterSetup.Register(source,f.Camera));
            return source;
        }

        [Test] public void SharedTooltipGrowsWithOvershootShrinksAndReversesWithoutRestartingOnRefresh()
        {
            using (var f = new Fixture())
            {
                f.Hover(0); var instance = f.Presenter.Instance; Assert.AreEqual(0,f.Presenter.VisibilityScale);
                f.Presenter.AdvanceAnimation(.08f); Assert.Greater(f.Presenter.VisibilityScale,1);
                float current = f.Presenter.VisibilityScale; f.Presenter.RefreshPresentation();
                Assert.AreEqual(current,f.Presenter.VisibilityScale); f.Presenter.AdvanceAnimation(1);
                Assert.AreEqual(1,f.Presenter.VisibilityScale,.0001f);
                f.Host.MouseInput.Controller.CancelPointer(); f.Presenter.RefreshPresentation();
                Assert.IsFalse(f.Presenter.IsVisible); Assert.IsTrue(instance.gameObject.activeSelf);
                f.Presenter.AdvanceAnimation(.06f); float shrinking = f.Presenter.VisibilityScale;
                f.Hover(1); Assert.AreSame(instance,f.Presenter.Instance); Assert.AreEqual(shrinking,f.Presenter.VisibilityScale);
                f.Presenter.AdvanceAnimation(1); Assert.AreEqual(1,f.Presenter.VisibilityScale,.0001f);
                f.Host.MouseInput.Controller.CancelPointer(); f.Presenter.RefreshPresentation(); f.Presenter.AdvanceAnimation(1);
                Assert.IsFalse(instance.gameObject.activeSelf); Assert.AreEqual(0,f.Presenter.VisibilityScale,.0001f);
            }
        }

        [Test] public void CanvasOverviewReusesHandTooltipOnTheLeftAndOwnsHoverOnlyWhileOpen()
        {
            using (var f = new Fixture())
            {
                f.Hover(0); var shared = f.Presenter.Instance; var state = f.Host.Match.State;
                var source = AddOverview(f); var root = source.View.GetComponent<Canvas>();
                f.Presenter.RefreshPresentation(); Assert.IsFalse(f.Presenter.IsVisible,"An open overview hides background hand hover.");
                var red = source.View.Cards.Single(item => item.Card.ViewId == f.Host.Cards[0].Id);
                red.SetHovered(true,false); f.Presenter.RefreshPresentation();
                Assert.IsTrue(f.Presenter.IsVisible); Assert.AreSame(shared,f.Presenter.Instance);
                Assert.AreEqual("♥ 红桃 Q",shared.SuitRankText.text); Assert.AreEqual(10,shared.ChipPoints);
                Assert.AreSame(root.transform,shared.transform.parent);
                Assert.AreEqual(root.sortingOrder+1,shared.GetComponent<Canvas>().sortingOrder);
                Assert.IsTrue(shared.GetComponentsInChildren<Graphic>().All(graphic => !graphic.raycastTarget));
                var corners = new Vector3[4]; red.Graphic.rectTransform.GetWorldCorners(corners);
                float left = corners.Min(corner => root.transform.InverseTransformPoint(corner).x);
                ((RectTransform)shared.transform).GetWorldCorners(corners);
                Assert.Less(corners.Max(corner => root.transform.InverseTransformPoint(corner).x),left);
                Assert.AreSame(state,f.Host.Match.State); Assert.IsTrue(f.Host.Cards[0].Interaction.IsHovered);
                red.SetHovered(false,false);
                var black = source.View.Cards.Single(item => item != red); black.SetHovered(true,false);
                f.Presenter.RefreshPresentation(); Assert.AreSame(shared,f.Presenter.Instance);
                Assert.AreEqual("♠ 黑桃 A",shared.SuitRankText.text); Assert.AreEqual(11,shared.ChipPoints);
                Assert.Less(shared.SuitRankText.color.r,.3f);
                black.SetHovered(false,false); f.Presenter.RefreshPresentation(); Assert.IsFalse(f.Presenter.IsVisible);
                source.Close(); f.Presenter.RefreshPresentation();
                Assert.IsTrue(f.Presenter.IsVisible); Assert.AreSame(shared,f.Presenter.Instance);
                Assert.AreSame(f.Presenter.transform,shared.transform.parent);
                Assert.AreEqual("♥ 红桃 Q",shared.SuitRankText.text);
                Object.DestroyImmediate(source.gameObject);
            }
        }

        [Test] public void CanvasTooltipFiltersHiddenDisabledGraphicsAndClampsWithoutChangingInspectionData()
        {
            using (var f = new Fixture())
            {
                var source = AddOverview(f); var root = source.View.GetComponent<Canvas>();
                var item = source.View.Cards.First(); item.SetHovered(true,false);
                var visual = item.Card; var data = source.View.Data;
                var fake = f.Root.AddComponent<TestCardHoverSource>(); fake.HasTarget = true;
                fake.Target = new CardHoverTarget(item,Suit.Hearts,Rank.Queen,123);
                f.Presenter.Configure(f.Prefab,f.Camera,new MonoBehaviour[] { fake });
                f.Presenter.RefreshPresentation(); Assert.IsTrue(f.Presenter.IsVisible); Assert.AreEqual(123,f.Presenter.Instance.ChipPoints);
                item.Graphic.enabled = false; f.Presenter.RefreshPresentation(); Assert.IsFalse(f.Presenter.IsVisible);
                item.Graphic.enabled = true;
                var hidden = new CardVisualData(visual.ViewId,null,visual.CardId,visual.Tint,visual.Effects,visual.IsDisabled,visual.Interaction);
                item.Present(hidden,item.IsRemaining,CardZoneMotionSettings.Default);
                f.Presenter.RefreshPresentation(); Assert.IsFalse(f.Presenter.IsVisible);
                item.Present(visual,item.IsRemaining,CardZoneMotionSettings.Default);
                item.SetHovered(true,false); item.transform.localPosition += Vector3.left * 2000;
                f.Presenter.RefreshPresentation(); Assert.IsTrue(f.Presenter.IsVisible);
                var corners = new Vector3[4]; ((RectTransform)f.Presenter.Instance.transform).GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var point = root.transform.InverseTransformPoint(corner); var bounds = ((RectTransform)root.transform).rect;
                    Assert.That(point.x,Is.InRange(bounds.xMin,bounds.xMax)); Assert.That(point.y,Is.InRange(bounds.yMin,bounds.yMax));
                }
                Assert.AreSame(data,source.View.Data);
                item.SetHovered(false,false); f.Presenter.RefreshPresentation(); Assert.IsFalse(f.Presenter.IsVisible);
                Object.DestroyImmediate(source.gameObject);
            }
        }

        [Test] public void MouseHoverShowsRealPointsAndSuitColorsWhileReusingOneSharedInstance()
        {
            using (var f = new Fixture())
            {
                f.Presenter.RefreshPresentation(); Assert.IsNull(f.Presenter.Instance);
                f.Hover(0); var instance = f.Presenter.Instance;
                Assert.IsTrue(f.Presenter.IsVisible); Assert.AreEqual(f.Host.Cards[0].Id, f.Presenter.HoveredViewId);
                Assert.AreEqual("♥ 红桃 Q", instance.SuitRankText.text); Assert.AreEqual(10, instance.ChipPoints);
                Assert.Greater(instance.SuitRankText.color.r, instance.SuitRankText.color.g);
                Assert.AreSame(f.Camera, instance.GetComponent<Canvas>().worldCamera);
                Assert.IsFalse(instance.GetComponent<CanvasGroup>().blocksRaycasts);
                Assert.IsTrue(instance.GetComponentsInChildren<Graphic>().All(graphic => !graphic.raycastTarget));
                f.Hover(1); Assert.AreSame(instance, f.Presenter.Instance);
                Assert.AreEqual("♠ 黑桃 A", instance.SuitRankText.text); Assert.AreEqual(11, instance.ChipPoints);
                Assert.Less(instance.SuitRankText.color.r, 0.3f);
                string id = f.Presenter.HoveredViewId;
                f.Host.ReplaceCards(f.Host.Cards.Reverse(), animate: false); f.Presenter.RefreshPresentation();
                Assert.AreEqual(id, f.Presenter.HoveredViewId); Assert.AreSame(instance, f.Presenter.Instance);
                f.Host.MouseInput.Controller.CancelPointer(); f.Presenter.RefreshPresentation();
                Assert.IsFalse(f.Presenter.IsVisible); Assert.IsNull(f.Presenter.HoveredViewId); Assert.AreSame(instance, f.Presenter.Instance);
                Assert.AreEqual(1, f.Root.GetComponentsInChildren<CardTooltipView>(true).Length);
            }
        }

        [Test] public void SeparateUICameraRendersTooltipWhilePlacementStillUsesSceneProjection()
        {
            using (var f = new Fixture())
            {
                var ui = CardUICameraSetup.Register(f.Scene, f.Camera);
                Assert.IsNotNull(ui); Assert.AreEqual(0, f.Camera.cullingMask & (1 << 5));
                f.Presenter.SetRenderingCamera(ui); f.Hover(0);
                Assert.IsTrue(f.Presenter.IsVisible); var instance = f.Presenter.Instance;
                Assert.AreSame(ui, instance.GetComponent<Canvas>().worldCamera);
                Assert.AreEqual(f.Camera.transform.rotation, instance.transform.rotation);
                Assert.AreEqual(f.Camera.WorldToViewportPoint(instance.transform.position), ui.WorldToViewportPoint(instance.transform.position));
                ui.enabled = false; f.Presenter.RefreshPresentation(); Assert.IsFalse(f.Presenter.IsVisible);
                ui.enabled = true; f.Presenter.RefreshPresentation(); Assert.IsTrue(f.Presenter.IsVisible);
                f.Presenter.SetRenderingCamera(null); f.Presenter.RefreshPresentation(); Assert.IsFalse(f.Presenter.IsVisible);
                f.Camera.cullingMask |= 1 << 5; f.Presenter.RefreshPresentation(); Assert.IsTrue(f.Presenter.IsVisible);
                Assert.AreSame(f.Camera, instance.GetComponent<Canvas>().worldCamera);
            }
        }

        [Test] public void HiddenProjectionAndLeavingCardCannotDiscloseEvenAnAdaptersSuppliedValues()
        {
            using (var f = new Fixture())
            {
                f.Hover(0);
                var first = f.Host.Cards[0];
                f.Host.ReplaceCards(new[] { new CardZoneCardState(first.Card, first.ArtId, false, first.Tint, first.Interaction), f.Host.Cards[1] }, false);
                Assert.IsFalse(f.Host.TryGetHoveredCard(out _));
                var fake = f.Root.AddComponent<TestCardHoverSource>(); fake.HasTarget = true;
                fake.Target = new CardHoverTarget(f.Zone.Views[0], Suit.Hearts, Rank.Queen, 999);
                f.Presenter.Configure(f.Prefab, f.Camera, new MonoBehaviour[] { fake });
                f.Presenter.RefreshPresentation(); Assert.IsFalse(f.Presenter.IsVisible);
                f.Host.ReplaceCards(new[] { new CardZoneCardState(first.Card, first.ArtId, true, first.Tint,
                    new CardInteractionState(isHovered: true, isSelected: true)), f.Host.Cards[1] }, false);
                f.Presenter.RefreshPresentation(); Assert.IsTrue(f.Presenter.IsVisible);
                f.Zone.SetTransitionSettings(CardZoneTransitionSettings.Default);
                f.Host.DiscardSelected(); f.Presenter.RefreshPresentation(); Assert.IsFalse(f.Presenter.IsVisible);
                Assert.AreEqual(1, f.Host.DiscardedCards.Count);
                Assert.IsTrue(fake.Target.View.IsExiting);
            }
        }

        [Test] public void DefinitionDataControlsTooltipAndDisabledIneffectiveCardsKeepTheirBaseValue()
        {
            using (var f = new Fixture())
            {
                var original = f.Host.Cards[0];
                var card = new CardInstance(original.Card.Id, StandardDeck.CardId(Suit.Spades, Rank.Ace),
                    effects: CardEffects.Ineffective, isDisabled: true);
                // Deliberately keep Q artwork: the UI must read the card definition, not parse that artwork name.
                f.Host.ReplaceCards(new[] { new CardZoneCardState(card, original.ArtId, true, original.Tint, default) }, false);
                f.Hover(0); Assert.IsTrue(f.Presenter.IsVisible);
                Assert.AreEqual(Suit.Spades, f.Presenter.Instance.Suit); Assert.AreEqual(Rank.Ace, f.Presenter.Instance.Rank);
                Assert.AreEqual(11, f.Presenter.Instance.ChipPoints); Assert.IsFalse(f.Host.Cards[0].Card.CanScore);
                Assert.IsFalse(f.Host.Cards[0].Card.CanPlay);
                var unknown = new CardInstance(card.Id, new CardDefinitionId("custom:unknown"));
                f.Host.ReplaceCards(new[] { new CardZoneCardState(unknown, original.ArtId, true, original.Tint,
                    new CardInteractionState(isHovered: true)) }, false);
                f.Presenter.RefreshPresentation(); Assert.IsFalse(f.Presenter.IsVisible);
            }
        }

        [Test] public void TooltipFacesTheSharedCameraAndClampsWhileFollowingCardScale()
        {
            using (var f = new Fixture())
            {
                f.Hover(0); f.Presenter.AdvanceAnimation(1);
                var instance = f.Presenter.Instance; var scale = instance.transform.lossyScale;
                var start = instance.transform.position;
                f.Zone.transform.position += new Vector3(0.02f, 0, 0); f.Presenter.RefreshPresentation();
                Assert.Greater(instance.transform.position.x, start.x);
                f.Zone.transform.localScale = Vector3.one * 2; f.Zone.transform.rotation = Quaternion.Euler(0, 21, 0);
                f.Camera.transform.Rotate(0, 0, 11); f.Presenter.RefreshPresentation();
                Assert.Less(Quaternion.Angle(f.Camera.transform.rotation, instance.transform.rotation), 0.001f);
                Assert.Less(Vector3.Distance(scale * 2, instance.transform.lossyScale), 0.000001f);
                f.Zone.transform.position += f.Camera.transform.right * 2; f.Presenter.RefreshPresentation();
                var corners = new Vector3[4]; ((RectTransform)instance.transform).GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var viewport = f.Camera.WorldToViewportPoint(corner);
                    string context = $"corner={viewport} screen={Screen.width}x{Screen.height} safe={Screen.safeArea} pixels={f.Camera.pixelRect} rect={((RectTransform)instance.transform).rect} scale={instance.transform.lossyScale}";
                    Assert.That(viewport.x, Is.InRange(0f, 1f), context); Assert.That(viewport.y, Is.InRange(0f, 1f), context);
                }
                f.Camera.enabled = false; f.Presenter.RefreshPresentation(); Assert.IsFalse(f.Presenter.IsVisible);
            }
        }

        [Test] public void TooltipAutoSizeScalesItsFrameOffsetsAndActiveVisibilityAnimationTogether()
        {
            using (var f = new Fixture())
            {
                var settings = new SerializedObject(f.Presenter);
                settings.FindProperty("clampToViewport").boolValue = false;
                settings.FindProperty("cameraPlaneOffset").vector2Value = new Vector2(.012f, .007f);
                settings.ApplyModifiedPropertiesWithoutUndo();
                var layout = f.Zone.Layout; layout.FitCardsToZone = true;
                layout.Padding = Vector2.zero; layout.ArcHeight = 0; layout.Size = new Vector2(.5f, .1f);
                f.Zone.SetLayout(layout);
                f.Hover(0); f.Presenter.AdvanceAnimation(.08f);
                var instance = f.Presenter.Instance; var card = f.Zone.Views[0];
                float progress = f.Presenter.VisibilityScale;
                var scale = instance.transform.lossyScale; var prefabScale = f.Prefab.transform.localScale;
                var bounds = card.Surface.GetComponent<MeshFilter>().sharedMesh.bounds;
                var offset = instance.transform.position - card.Surface.transform.TransformPoint(bounds.center);
                layout.Size.y = .05f; f.Zone.SetLayout(layout); f.Presenter.RefreshPresentation();
                Assert.AreSame(instance, f.Presenter.Instance); Assert.AreEqual(progress, f.Presenter.VisibilityScale);
                Assert.Less(Vector3.Distance(scale * .5f, instance.transform.lossyScale), .000001f);
                var smallerOffset = instance.transform.position - card.Surface.transform.TransformPoint(bounds.center);
                Assert.Less(Vector3.Distance(offset * .5f, smallerOffset), .000001f);
                f.Presenter.RefreshPresentation();
                Assert.Less(Vector3.Distance(scale * .5f, instance.transform.lossyScale), .000001f);
                Assert.AreEqual(prefabScale, f.Prefab.transform.localScale);
                f.Presenter.AdvanceAnimation(1);
                Assert.AreEqual(1, f.Presenter.VisibilityScale, .0001f);
                Assert.Less(Vector3.Distance(scale * (.5f / progress), instance.transform.lossyScale), .000001f);
            }
        }

        [Test] public void TooltipDoesNotApplySharedParentScaleTwice()
        {
            using (var f = new Fixture())
            {
                f.Hover(0); f.Presenter.AdvanceAnimation(1);
                var scale = f.Presenter.Instance.transform.lossyScale;
                f.Presenter.transform.localScale = Vector3.one * 2;
                f.Presenter.RefreshPresentation();
                Assert.Less(Vector3.Distance(scale, f.Presenter.Instance.transform.lossyScale), .000001f);
                f.Root.transform.localScale = Vector3.one * 3;
                f.Presenter.RefreshPresentation();
                Assert.Less(Vector3.Distance(scale * 3, f.Presenter.Instance.transform.lossyScale), .000001f);
            }
        }

        [Test] public void MultipleSourceAdaptersReuseTheInstanceAndDisableReleasesItsOwnedObject()
        {
            using (var f = new Fixture())
            {
                f.Hover(0); var first = f.Presenter.Instance;
                var adapter = f.Root.AddComponent<TestCardHoverSource>(); adapter.HasTarget = true;
                adapter.Target = new CardHoverTarget(f.Zone.Views[0], Suit.Diamonds, Rank.Ten, 27);
                f.Presenter.RegisterSource(adapter); f.Presenter.RegisterSource(adapter);
                Assert.AreEqual(2, f.Presenter.Sources.Count);
                f.Host.enabled = false; f.Host.Cards[0].SetInteraction(new CardInteractionState(isHovered: true));
                f.Zone.Present(f.Host.Zone, id => f.Host.Cards.Single(card => card.Id == id.Value).Project(), false);
                f.Presenter.RefreshPresentation(); Assert.AreSame(first, f.Presenter.Instance);
                Assert.AreEqual("♦ 方块 10", first.SuitRankText.text); Assert.AreEqual(27, first.ChipPoints);
                Assert.Throws<ArgumentException>(() => f.Presenter.RegisterSource(first));
                adapter.enabled = false; f.Presenter.RefreshPresentation(); Assert.IsFalse(f.Presenter.IsVisible);
                adapter.enabled = true; f.Presenter.RefreshPresentation(); Assert.IsTrue(f.Presenter.IsVisible);
                f.Presenter.enabled = false; Assert.IsNull(f.Presenter.Instance); Assert.IsTrue(first == null);
                f.Presenter.enabled = true; f.Presenter.RefreshPresentation(); Assert.IsTrue(f.Presenter.IsVisible);
                Assert.AreEqual(1, f.Root.GetComponentsInChildren<CardTooltipView>(true).Length);
            }
        }

        [Test] public void EnteringCardsAndRestartedSessionsDoNotRetainStaleHoverPrompts()
        {
            using (var f = new Fixture())
            {
                f.Hover(0); f.Host.MouseInput.Controller.CancelPointer();
                f.Zone.SetTransitionSettings(CardZoneTransitionSettings.Default);
                var card = new CardInstance(new CardInstanceId("new-hover-card"), StandardDeck.CardId(Suit.Clubs, Rank.Jack));
                f.Host.ReplaceCards(f.Host.Cards.Concat(new[] { new CardZoneCardState(card, "clubs_j", true,
                    Color.white, new CardInteractionState(isHovered: true)) }));
                f.Presenter.RefreshPresentation(); Assert.IsFalse(f.Presenter.IsVisible);
                f.Zone.AdvanceMotion(5); f.Presenter.RefreshPresentation(); Assert.IsTrue(f.Presenter.IsVisible);
                Assert.AreEqual("new-hover-card", f.Presenter.HoveredViewId);
                f.Host.BeginSession(); f.Presenter.RefreshPresentation(); Assert.IsFalse(f.Presenter.IsVisible);
                Assert.IsTrue(f.Host.Cards.All(state => !state.Interaction.IsHovered));
            }
        }
    }
}
