#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using CardInteraction;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace CardPresentation.Tests
{
    public static class DeckOverviewPlayValidation
    {
        [MenuItem("Poker/Tests/Validate Deck Icon And UI Motion In Play")]
        public static void ValidateMotion()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play before validating deck UI motion.");
            var pile = Object.FindObjectsOfType<DrawPilePresenter>().First(item => item.Overview != null);
            var source = pile.Overview; var view = source.View; var hover = Object.FindObjectsOfType<CardHoverPresenter>().First(item => item.Sources.Contains(source));
            bool wasPinned = pile.IsPinned, wasOpen = view.IsOpen, wasHovered = pile.View.IsHovered;
            var before = source.Match.State; string path = "Library/DeckOverviewValidation/motion.json";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            try
            {
                pile.SetPinned(false); pile.View.SetHovered(false,false); source.Close(false);
                if (pile.Summary != null) pile.Summary.Hide(false);
                var rest = view.Frame.anchoredPosition;
                pile.Refresh(); Assert.AreEqual(pile.Source.DeckCards.Count.ToString(),pile.View.RemainingCount.text);
                ExecuteEvents.Execute(pile.View.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);
                Assert.IsFalse(view.IsOpen); Assert.IsTrue(pile.Summary.IsOpen);
                var summaryRect = (RectTransform)pile.Summary.transform; float startY = summaryRect.anchoredPosition.y;
                pile.Summary.AdvanceAnimation(.09f); Assert.Greater(summaryRect.anchoredPosition.y,startY+28);
                pile.Summary.AdvanceAnimation(1);
                ExecuteEvents.Execute(pile.View.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerExitHandler);
                Assert.IsFalse(pile.Summary.IsOpen); Assert.IsTrue(pile.Summary.gameObject.activeSelf);
                pile.Summary.AdvanceAnimation(.04f); var summaryPosition = summaryRect.anchoredPosition; var summaryScale = summaryRect.localScale;
                pile.View.SetHovered(true,false); Assert.AreEqual(summaryPosition,summaryRect.anchoredPosition);
                Assert.AreEqual(summaryScale,summaryRect.localScale,"Re-entry must preserve the actual summary pose.");
                pile.Summary.AdvanceAnimation(1); pile.View.SetHovered(false,false);
                pile.Summary.AdvanceAnimation(1); Assert.IsFalse(pile.Summary.gameObject.activeSelf);
                pile.SetPinned(true); Assert.IsTrue(view.IsOpen); Assert.Less(view.Frame.anchoredPosition.y,rest.y);
                view.AdvanceAnimation(.11f); Assert.Greater(view.Frame.anchoredPosition.y,rest.y,"The upward arrival must overshoot gently.");
                view.AdvanceAnimation(1); Assert.AreEqual(rest,view.Frame.anchoredPosition);
                var card = view.Cards.First(); card.SetHovered(true,false); hover.RefreshPresentation();
                hover.AdvanceAnimation(.08f); Assert.Greater(hover.VisibilityScale,1); hover.AdvanceAnimation(1);
                card.SetHovered(false,false); hover.RefreshPresentation(); Assert.IsFalse(hover.IsVisible);
                Assert.IsTrue(hover.Instance.gameObject.activeSelf); hover.AdvanceAnimation(1); Assert.IsFalse(hover.Instance.gameObject.activeSelf);
                ExecuteEvents.Execute(pile.View.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerExitHandler);
                Assert.IsTrue(view.IsOpen,"Pinned full page survives icon exit."); pile.SetPinned(false); Assert.IsFalse(view.IsOpen); Assert.IsTrue(view.gameObject.activeSelf);
                view.AdvanceAnimation(1); Assert.IsFalse(view.gameObject.activeSelf);
                ExecuteEvents.Execute(pile.View.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerClickHandler);
                Assert.IsTrue(pile.IsPinned); pile.View.SetHovered(true,false); pile.View.SetHovered(false,false);
                view.AdvanceAnimation(1); Assert.IsTrue(view.IsOpen,"Click-pinned information survives icon exit.");
                view.CloseButton.onClick.Invoke(); Assert.IsFalse(pile.IsPinned); Assert.IsFalse(view.IsOpen);
                view.AdvanceAnimation(.06f); var reversing = view.Frame.anchoredPosition;
                pile.SetPinned(true); Assert.AreEqual(reversing,view.Frame.anchoredPosition,"Reverse the panel from its actual position.");
                view.AdvanceAnimation(1); Assert.AreEqual(rest,view.Frame.anchoredPosition); Assert.AreSame(before,source.Match.State);
                File.WriteAllText(path,"{\"status\":\"Passed\",\"nativeMouse\":false}");
            }
            catch (Exception error) { File.WriteAllText(path,JsonUtility.ToJson(new Failure { status = "Failed",error = error.ToString() },true)); throw; }
            finally
            {
                pile.View.SetHovered(wasHovered,false); pile.SetPinned(wasPinned);
                if (pile.Summary != null) pile.Summary.Hide(false);
                if (wasOpen) { source.Open(false); view.AdvanceAnimation(1); } else source.Close(false);
                hover.Hide(false);
            }
        }

        [MenuItem("Poker/Tests/Capture Deck Hover Summary In Play")]
        public static void CaptureSummary()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play first.");
            var pile = Object.FindObjectsOfType<DrawPilePresenter>().First(item => item.SummaryPrefab != null);
            bool wasPinned = pile.IsPinned, wasHovered = pile.View.IsHovered;
            pile.SetPinned(false); pile.View.SetHovered(false,false); pile.View.SetHovered(true,false);
            pile.Summary.AdvanceAnimation(1); Assert.IsTrue(pile.Summary.IsOpen);
            string path = "Library/DeckOverviewValidation/hover-summary.png";
            Directory.CreateDirectory(Path.GetDirectoryName(path)); if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path); double until = EditorApplication.timeSinceStartup+10;
            void Restore()
            {
                if (Application.isPlaying && !File.Exists(path) && EditorApplication.timeSinceStartup < until) return;
                EditorApplication.update -= Restore;
                if (pile == null) return;
                pile.View.SetHovered(wasHovered,false); pile.SetPinned(wasPinned);
                if (!wasHovered && pile.Summary != null) pile.Summary.Hide(false);
            }
            EditorApplication.update += Restore;
        }

        [MenuItem("Poker/Tests/Capture Canvas Deck Hover In Play")]
        public static void CaptureHover()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play before capturing deck hover.");
            var source = Object.FindObjectsOfType<DeckOverviewPresenter>(true).First(item => item.Match != null && item.Match.IsRunning);
            var pile = Object.FindObjectsOfType<DrawPilePresenter>().FirstOrDefault(item => item.Overview == source);
            bool wasPinned = pile != null && pile.IsPinned;
            bool wasOpen = source.View.IsOpen; if (pile != null) pile.SetPinned(true); else source.Open();
            source.View.AdvanceAnimation(1);
            var card = source.View.Cards.First(item => source.View.Data.Cards.Single(entry => entry.Visual.ViewId == item.Card.ViewId).Suit == PokerEngine.Suit.Hearts);
            bool wasHovered = card.Interaction.IsHovered; card.SetHovered(true,false);
            var hover = Object.FindObjectsOfType<CardHoverPresenter>().First(item => item.Sources.Contains(source));
            hover.RefreshPresentation(); hover.AdvanceAnimation(1); Assert.IsTrue(hover.IsVisible);
            string path = "Library/DeckOverviewValidation/hover.png";
            Directory.CreateDirectory(Path.GetDirectoryName(path)); if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            double until = EditorApplication.timeSinceStartup + 10;
            void Restore()
            {
                if (Application.isPlaying && !File.Exists(path) && EditorApplication.timeSinceStartup < until) return;
                EditorApplication.update -= Restore;
                if (card != null) card.SetHovered(wasHovered,false);
                if (pile != null) pile.SetPinned(wasPinned);
                if (!wasOpen && source != null) source.Close(false);
                if (hover != null) hover.RefreshPresentation();
            }
            EditorApplication.update += Restore;
        }

        [MenuItem("Poker/Tests/Validate Canvas Deck Overview In Play")]
        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play before validating Canvas deck interaction.");
            var presenter = Object.FindObjectsOfType<DeckOverviewPresenter>(true).First(item => item.Match != null && item.Match.IsRunning);
            var host = Object.FindObjectsOfType<CardZonePlayHost>().First(item => item.Match == presenter.Match && item.GetComponent<HandZoneView>() != null);
            var match = presenter.Match; var before = match.State;
            var observer = before.Zones.SelectMany(zone => match.GetCards(zone.Id)).Select(card => card.Copy()).ToArray();
            var pile = Object.FindObjectsOfType<DrawPilePresenter>().FirstOrDefault(item => item.Overview == presenter);
            bool wasPinned = pile != null && pile.IsPinned;
            bool wasOpen = presenter.View.IsOpen; string path = "Library/DeckOverviewValidation/play.json";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            try
            {
                if (pile != null) pile.SetPinned(true); else presenter.Open(); var view = presenter.View; view.AdvanceAnimation(1); var card = view.Cards.First();
                var identities = view.Cards.ToDictionary(item => item.Card.ViewId,item => item.GetInstanceID());
                ExecuteEvents.Execute(card.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);
                Assert.IsTrue(card.Interaction.IsHovered); Assert.IsFalse(card.Interaction.IsSelected); Assert.AreSame(before,match.State);
                var hover = Object.FindObjectsOfType<CardHoverPresenter>().First(item => item.Sources.Contains(presenter));
                hover.RefreshPresentation(); Assert.IsTrue(hover.IsVisible); Assert.AreEqual(card.Card.ViewId,hover.HoveredViewId);
                var entry = view.Data.Cards.Single(item => item.Visual.ViewId == card.Card.ViewId);
                Assert.AreEqual(entry.Suit.Value,hover.Instance.Suit); Assert.AreEqual(entry.Rank.Value,hover.Instance.Rank);
                Assert.AreEqual(PokerEngine.CardScoreRules.GetBasePoints(entry.Rank.Value),hover.Instance.ChipPoints);
                Assert.AreSame(view.GetComponent<Canvas>().rootCanvas.transform,hover.Instance.transform.parent);
                ExecuteEvents.Execute(card.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerExitHandler);
                hover.RefreshPresentation(); Assert.IsFalse(card.Interaction.IsHovered); Assert.IsFalse(hover.IsVisible);
                Assert.AreEqual(host.DeckCards.Count,view.Data.RemainingCount);
                int remaining = view.Data.RemainingCount; Assert.IsTrue(host.CanDraw()); host.DrawCards(1,false); presenter.Refresh(true);
                Assert.AreEqual(remaining - 1,view.Data.RemainingCount);
                foreach (var item in view.Cards) Assert.AreEqual(identities[item.Card.ViewId],item.GetInstanceID());
                Assert.IsTrue(view.Cards.All(item => item.Graphic.material == view.CardMaterial));
                view.CloseButton.onClick.Invoke(); Assert.IsFalse(view.IsOpen); view.AdvanceAnimation(1); Assert.IsFalse(view.gameObject.activeSelf); Assert.IsTrue(match.IsRunning);
                presenter.Open(); Assert.AreEqual(remaining - 1,view.Data.RemainingCount);
                File.WriteAllText(path,"{\"status\":\"Passed\",\"nativeMouse\":false,\"cards\":" + view.Cards.Count + "}");
            }
            catch (Exception error) { File.WriteAllText(path,JsonUtility.ToJson(new Failure { status = "Failed", error = error.ToString() },true)); throw; }
            finally
            {
                match.StartMatch(before,observer); presenter.Refresh(true); if (pile != null) pile.SetPinned(wasPinned); if (!wasOpen) presenter.Close(false);
            }
        }
        [Serializable] private sealed class Failure { public string status,error; }
    }
}
#endif
