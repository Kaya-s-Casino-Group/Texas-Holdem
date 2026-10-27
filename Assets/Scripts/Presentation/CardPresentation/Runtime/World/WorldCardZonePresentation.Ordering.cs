using System;
using System.Collections.Generic;
using System.Linq;
using BattlePresentation;
using UnityEngine;

namespace CardPresentation
{
    public sealed partial class WorldCardZonePresentation
    {
        [SerializeField, Min(.01f)] private float dragLiftDistance = .08f;
        private readonly List<string> localOrder = new List<string>();
        private string orderZoneId;
        private CardInstanceView dragged;
        private string[] orderBeforeDrag;
        private Plane dragPlane;
        private Vector3 grabOffset;
        public bool IsReordering => dragged != null;
        // Visual safety only: independent of the rule window and whose turn it is.
        public bool CanReorder => isActiveAndEnabled && Hand != null && Hand.isActiveAndEnabled &&
            Current != null && Hand.Views.Count > 0 &&
            !retained.Any(run => run.Operation.Status == PresentationStatus.Pending) &&
            !Hand.Views.Any(view => view == null || view.IsEntering || view.IsExiting);
        public IReadOnlyList<string> VisualOrder => Hand == null ? Array.Empty<string>() : Hand.Views.Select(view => view.ViewId).ToArray();

        private IEnumerable<CardDisplayData> OrderedCards(CardZoneDisplayData snapshot)
        {
            if (orderZoneId != snapshot.Id) { localOrder.Clear(); orderZoneId = snapshot.Id; }
            if (localOrder.Count == 0) return snapshot.Cards;
            var cards = snapshot.Cards.ToDictionary(card => card.ViewId, StringComparer.Ordinal);
            localOrder.RemoveAll(id => !cards.ContainsKey(id));
            foreach (var card in snapshot.Cards) if (!localOrder.Contains(card.ViewId)) localOrder.Add(card.ViewId);
            return localOrder.Select(id => cards[id]).ToArray();
        }
        public bool SortByRank() => Sort(false);
        public bool SortBySuit() => Sort(true);
        private bool Sort(bool suitFirst)
        {
            if (!CanReorder) return false;
            EndReorder(true);
            var cards = Current.Cards.ToDictionary(card => card.ViewId, StringComparer.Ordinal);
            var visible = Hand.Views.Select(view => cards[view.ViewId]);
            var sorted = suitFirst
                ? visible.OrderBy(SuitOrder).ThenByDescending(RankOrder)
                : visible.OrderByDescending(RankOrder).ThenBy(SuitOrder);
            ApplyOrder(sorted.Select(card => card.ViewId)); return true;
        }
        private static int RankOrder(CardDisplayData card)
        {
            if (card.Hidden) return -1;
            var key = card.Properties.TryGetValue("rank", out var rank) ? rank : (card.ArtKey ?? "").Split('_').Last();
            switch (key.ToLowerInvariant()) { case "a": return 14; case "k": return 13; case "q": return 12; case "j": return 11; }
            return int.TryParse(key, out var value) ? (value == 1 ? 14 : value) : -1;
        }
        private static int SuitOrder(CardDisplayData card)
        {
            if (card.Hidden) return 4;
            var key = card.Properties.TryGetValue("suit", out var suit) ? suit : (card.ArtKey ?? "").Split('_')[0];
            switch (key.ToLowerInvariant()) { case "spades": return 0; case "hearts": return 1; case "clubs": return 2; case "diamonds": return 3; default: return 4; }
        }
        private void ApplyOrder(IEnumerable<string> ids, bool animate = true)
        {
            var next = ids.ToArray();
            localOrder.Clear(); localOrder.AddRange(next); orderZoneId = Current.Id;
            Hand.ReorderViews(next, animate);
        }
        public bool MoveToIndex(string viewId, int index)
        {
            if (!CanReorder) return false;
            var ids = Hand.Views.Select(view => view.ViewId).ToList();
            int previous = ids.IndexOf(viewId);
            if (previous < 0 || index < 0 || index >= ids.Count) return false;
            if (previous == index) return true;
            ids.RemoveAt(previous); ids.Insert(index, viewId); ApplyOrder(ids); return true;
        }
        public bool BeginReorder(string viewId, Ray pointer)
        {
            if (!CanReorder || IsReordering || !Hand.TryGetView(viewId, out var card)) return false;
            dragged = card; orderBeforeDrag = VisualOrder.ToArray();
            var center = card.Surface.GetComponent<Renderer>().bounds.center;
            var camera = Hand.FacingCamera != null ? Hand.FacingCamera : Camera.main;
            var normal = Hand.FaceCamera && camera != null ? -camera.transform.forward : Hand.transform.up;
            dragPlane = new Plane(normal, center);
            if (!dragPlane.Raycast(pointer, out var distance)) { dragged = null; orderBeforeDrag = null; return false; }
            grabOffset = center - pointer.GetPoint(distance);
            return UpdateReorder(pointer);
        }
        public bool UpdateReorder(Ray pointer)
        {
            if (!IsReordering) return false;
            if (!CanReorder || !dragPlane.Raycast(pointer, out var distance)) { EndReorder(true); return false; }
            var point = pointer.GetPoint(distance) + grabOffset;
            float x = Hand.transform.InverseTransformPoint(point).x;
            int slot = Enumerable.Range(0, Hand.Views.Count).OrderBy(i => Mathf.Abs(Hand.Views[i].transform.localPosition.x - x)).First();
            MoveToIndex(dragged.ViewId, slot);
            var bounds = dragged.Surface.GetComponent<Renderer>().bounds;
            float lift = Mathf.Min(Mathf.Max(dragLiftDistance, bounds.size.magnitude * .15f), distance * .15f);
            dragged.HoldAt(point - pointer.direction.normalized * lift);
            return true;
        }
        public void EndReorder(bool cancel = false, bool animate = true)
        {
            if (!IsReordering) return;
            var card = dragged; dragged = null;
            if (cancel && orderBeforeDrag != null && Hand != null &&
                orderBeforeDrag.Length == Hand.Views.Count && orderBeforeDrag.All(id => Hand.TryGetView(id, out _)))
                ApplyOrder(orderBeforeDrag, animate);
            orderBeforeDrag = null; card.ReleaseDrag(animate);
        }
    }
}
