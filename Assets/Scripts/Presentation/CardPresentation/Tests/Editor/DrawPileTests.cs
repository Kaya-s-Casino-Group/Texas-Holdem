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
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace CardPresentation.Tests
{
    [TestFixture]
    public sealed class DrawPileTests
    {
        [Test] public void IconUsesCurrentAtlasAndLayerThicknessWithOneStablePointerTarget()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DrawPileSetup.PrefabPath);
            Assert.IsNotNull(prefab); Assert.IsTrue(prefab.GetComponent<DrawPileView>().IsConfigured);
            Assert.AreEqual((int)RenderMode.ScreenSpaceCamera, new SerializedObject(prefab.GetComponent<Canvas>()).FindProperty("m_RenderMode").intValue);
            Assert.IsNull(prefab.GetComponent<Canvas>().worldCamera); Assert.IsNotNull(prefab.GetComponent<GraphicRaycaster>());
            Assert.IsEmpty(prefab.GetComponentsInChildren<Button>(true)); Assert.AreEqual(1,prefab.GetComponentsInChildren<TMPro.TMP_Text>(true).Length);
            var count = prefab.GetComponent<DrawPileView>().RemainingCount; Assert.IsNotNull(count); Assert.IsFalse(count.raycastTarget);
            Assert.AreEqual(new Vector2(1,0),((RectTransform)count.transform.parent).anchorMin);
            var icon = (RectTransform)prefab.transform.Find("Icon");
            Assert.AreEqual(new Vector2(1, 0), icon.anchorMin); Assert.AreEqual(icon.anchorMin, icon.anchorMax);
            Assert.AreEqual(new Vector2(-20, 20), icon.anchoredPosition); Assert.AreEqual(new Vector2(64, 96), icon.sizeDelta);
            Assert.IsEmpty(prefab.GetComponentsInChildren<RawImage>(true));
            var graphics = prefab.GetComponentsInChildren<CardUIGraphic>(true); Assert.AreEqual(8,graphics.Length);
            foreach (var image in graphics)
            { Assert.IsFalse(image.raycastTarget); Assert.AreSame(prefab.GetComponent<DrawPileView>().Atlas,image.Atlas); }
            Assert.AreEqual(1,prefab.GetComponentsInChildren<Graphic>(true).Count(image => image.raycastTarget));
            Assert.AreEqual("card_back",prefab.GetComponent<DrawPileView>().CoverCardId);
        }

        [Test] public void IconFollowsRuntimeDeckConsumptionAndRestartWithoutOwningOrMutatingCards()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("Deck host"); SceneManager.MoveGameObjectToScene(root, scene);
                var hand = root.AddComponent<HandZoneView>();
                hand.Configure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameAssets/CardArt/Prefabs/Card.prefab"), CardZoneLayoutSettings.Default);
                var host = root.AddComponent<CardZonePlayHost>();
                var deck = new[] { Card("deck-a"), Card("deck-b") };
                host.Capture(new CardZone(new ZoneId("deck-icon/hand"), CardZoneKinds.Hand, null, new CardInstanceId[0]),
                    Array.Empty<CardZoneCardState>(), deck: deck); host.BeginSession();
                var presenter = DrawPileSetup.Register(host, null); var icon = presenter.View;
                Assert.AreEqual(2, icon.RemainingCards); Assert.AreEqual("2",icon.RemainingCount.text); Assert.IsEmpty(host.Cards);
                host.DrawCards(animate: false); presenter.Refresh(); Assert.AreEqual(1, icon.RemainingCards); Assert.AreEqual("1",icon.RemainingCount.text);
                Assert.AreEqual("deck-b", host.Cards.Single().Id); Assert.AreEqual(2, host.InitialDeckCards.Count);
                host.DrawCards(animate: false); presenter.Refresh(); Assert.AreEqual(0, icon.RemainingCards); Assert.AreEqual("0",icon.RemainingCount.text);
                Assert.IsEmpty(host.DeckZone.Cards); Assert.AreEqual(2, host.Cards.Count);
                Assert.AreEqual(1, icon.GetComponentsInChildren<CardUIGraphic>().Length);
                Assert.AreEqual(0.4f, icon.GetComponentsInChildren<CardUIGraphic>().Single().color.r);
                host.Match.RestartMatch(); host.BeginSession(); presenter.Refresh(); Assert.AreEqual(2, icon.RemainingCards); Assert.AreEqual("2",icon.RemainingCount.text); Assert.IsEmpty(host.Cards);
                Assert.AreSame(presenter, DrawPileSetup.Register(host, null));
                Assert.AreEqual(1, scene.GetRootGameObjects().SelectMany(item => item.GetComponentsInChildren<DrawPilePresenter>()).Count());
                host.enabled = false; presenter.Refresh(); Assert.AreEqual(0, icon.RemainingCards); Assert.AreEqual("0",icon.RemainingCount.text);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test] public void EmptyPileKeepsOneDimmedBackAndRejectsInvalidCountsWithoutChangingTheView()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(DrawPileSetup.PrefabPath));
            try
            {
                var view = root.GetComponent<DrawPileView>(); view.SetRemainingCards(0);
                var images = view.GetComponentsInChildren<CardUIGraphic>(true); Assert.AreEqual(8, images.Length);
                Assert.AreEqual(1, images.Count(image => image.gameObject.activeSelf));
                Assert.Throws<ArgumentOutOfRangeException>(() => view.SetRemainingCards(-1)); Assert.AreEqual(0, view.RemainingCards);
                view.SetRemainingCards(1); Assert.AreEqual(1, images.Count(image => image.gameObject.activeSelf));
                view.SetRemainingCards(2); Assert.AreEqual(2, images.Count(image => image.gameObject.activeSelf));
                view.SetRemainingCards(50); Assert.IsTrue(images.All(image => image.gameObject.activeSelf));
                Assert.AreEqual("50",view.RemainingCount.text);
                Assert.Throws<ArgumentOutOfRangeException>(() => view.SetRemainingCards(-1)); Assert.AreEqual("50",view.RemainingCount.text);
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static CardZoneCardState Card(string id) => new CardZoneCardState(new CardInstance(new CardInstanceId(id),
            StandardDeck.CardId(Suit.Clubs, Rank.Two)), "clubs_02", true, Color.white, default);

        [Test] public void CoverUsesAtlasAndHoverInertiaReversesFromCurrentPoseWithoutMovingTheHitArea()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(DrawPileSetup.PrefabPath));
            try
            {
                var view = root.GetComponent<DrawPileView>(); var hit = (RectTransform)root.transform.Find("Icon");
                var position = hit.anchoredPosition; view.SetRemainingCards(52); view.SetCoverCard("hearts_q");
                Assert.AreEqual("hearts_q",view.MotionRoot.Find("TopCard").GetComponent<CardUIGraphic>().Data.FaceArtId);
                Assert.Throws<ArgumentException>(() => view.SetCoverCard("missing")); Assert.AreEqual("hearts_q",view.CoverCardId);
                view.SetHovered(true); view.AdvanceAnimation(.105f);
                Assert.Greater(view.MotionRoot.localScale.x,1.095445f); Assert.AreEqual(Vector3.one,hit.localScale);
                view.AdvanceAnimation(1); Assert.AreEqual(1.095445f,view.MotionRoot.localScale.x,.0001f);
                view.SetHovered(false); view.AdvanceAnimation(.04f); float current = view.MotionRoot.localScale.x;
                view.SetHovered(true); Assert.AreEqual(current,view.MotionRoot.localScale.x,.0001f);
                view.AdvanceAnimation(1); view.SetHovered(false); view.AdvanceAnimation(1);
                Assert.AreEqual(Vector3.one,view.MotionRoot.localScale); Assert.AreEqual(position,hit.anchoredPosition);
                view.SetHovered(true); root.SetActive(false); Assert.AreEqual(Vector3.one,view.MotionRoot.localScale);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test] public void PreviewClosesOnIconExitClickPinsReturnDismissesAndBindingsDoNotDuplicate()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("Deck intent host"); SceneManager.MoveGameObjectToScene(root,scene);
                var hand = root.AddComponent<HandZoneView>();
                hand.Configure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameAssets/CardArt/Prefabs/Card.prefab"),CardZoneLayoutSettings.Default);
                var host = root.AddComponent<CardZonePlayHost>();
                host.Capture(new CardZone(new ZoneId("intent/hand"),CardZoneKinds.Hand,null,Array.Empty<CardInstanceId>()),
                    Array.Empty<CardZoneCardState>(),deck:new[] { Card("a"),Card("b") }); host.BeginSession();
                var pile = DrawPileSetup.Register(host,null); var overview = DeckOverviewSetup.Register(host,null);
                pile.BindOverview(overview); pile.BindOverview(overview); overview.Close(false);
                pile.View.SetHovered(true,false); Assert.IsTrue(pile.Summary.IsOpen); Assert.IsFalse(overview.View.IsOpen);
                var canvas = pile.View.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
                var canvasRect = (RectTransform)canvas.transform;
                var summaryRect = (RectTransform)pile.Summary.transform;
                var iconRect = (RectTransform)pile.View.MotionRoot.parent;
                foreach (var size in new[] { new Vector2(1377,757),new Vector2(1920,1080),new Vector2(2778,1284) })
                {
                    canvasRect.sizeDelta = size;
                    iconRect.anchoredPosition = new Vector2(-size.x*.3f,20);
                    Canvas.ForceUpdateCanvases();
                    Assert.Less(Vector3.Distance(canvasRect.TransformPoint(canvasRect.rect.center),
                        summaryRect.TransformPoint(summaryRect.rect.center)),.01f,
                        "Hover summary stays in the canvas center when the screen or deck button moves.");
                }
                pile.View.SetHovered(false,false); Assert.IsFalse(pile.Summary.IsOpen,"Leaving the icon closes preview immediately.");
                pile.Summary.AdvanceAnimation(1); Assert.IsFalse(pile.Summary.gameObject.activeSelf);
                pile.View.SetHovered(true,false);
                host.DrawCards(animate:false); pile.Refresh(); Assert.AreEqual("1",pile.Summary.Cell(2,12).text);
                pile.View.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
                Assert.IsTrue(pile.IsPinned); pile.View.SetHovered(false,false);
                Assert.IsTrue(overview.View.IsOpen); Assert.IsFalse(pile.Summary.IsOpen);
                Assert.AreEqual(pile.Summary.Data.Remaining(Suit.Clubs,Rank.Two),overview.View.Data.Remaining(Suit.Clubs,Rank.Two),
                    "Opening the full page uses the updated shared counts.");
                Assert.AreEqual(1,overview.View.Data.RemainingCount);
                pile.View.SetHovered(true,false); overview.View.CloseButton.onClick.Invoke();
                Assert.IsFalse(pile.IsPinned); Assert.IsFalse(overview.View.IsOpen);
                Assert.IsFalse(overview.View.IsOpen);
                pile.View.SetHovered(false,false); pile.View.SetHovered(true,false); Assert.IsTrue(pile.Summary.IsOpen); Assert.IsFalse(overview.View.IsOpen);
                pile.enabled = false; pile.View.OnPointerClick(new PointerEventData(null)); Assert.IsFalse(pile.IsPinned);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
