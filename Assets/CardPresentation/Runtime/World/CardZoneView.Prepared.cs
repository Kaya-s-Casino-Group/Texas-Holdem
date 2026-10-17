using CardArt;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardPresentation
{
    public partial class CardZoneView
    {
        internal PreparedPresentation PreparePresentation(CardZoneViewData data, bool animate,
            IReadOnlyDictionary<string, CardInstanceView> imports = null, ISet<string> exports = null) =>
            new PreparedPresentation(this, data, () => CreatePresentationUpdate(data, animate), imports, exports);

        // Prepare both endpoints before either changes. Borrowed visuals stay with their source until Commit.
        internal sealed class PreparedPresentation : IDisposable
        {
            private readonly CardZoneView owner;
            private readonly CardZoneViewData data, before;
            private readonly PresentationUpdate update;
            private readonly Dictionary<string, CardInstanceView> next = new Dictionary<string, CardInstanceView>(StringComparer.Ordinal);
            private readonly HashSet<CardInstanceView> created = new HashSet<CardInstanceView>();
            private readonly CardInstanceView[] nextOrdered;
            private readonly ISet<string> exports;
            private readonly CardZoneCardPose[] poses;
            private readonly CardShaderAppearance[] appearances;
            private readonly CardZoneMotionSettings motion;
            private readonly Bounds bounds;
            private readonly GameObject prefab;
            private readonly float cardScale;
            private bool committed, notified, disposed;

            internal PreparedPresentation(CardZoneView owner, CardZoneViewData data, Func<PresentationUpdate> createUpdate,
                IReadOnlyDictionary<string, CardInstanceView> imports, ISet<string> exports)
            {
                this.owner = owner; this.data = data ?? throw new ArgumentNullException(nameof(data));
                before = owner.Data; this.exports = exports; prefab = owner.cardPrefab;
                owner.ValidateCardCount(data.Cards.Count);
                if (before != null && prefab != owner.boundPrefab) throw new InvalidOperationException("Clear before replacing the prefab.");
                bounds = CardInstanceView.ValidatePrefab(prefab, out var template);
                motion = owner.MotionSettings;
                cardScale = owner.CalculateCardScale(owner.Layout, motion, bounds.size);
                poses = owner.CalculateCardPoses(owner.Layout, motion.LayoutSize(bounds.size) * cardScale, data.Cards.Count);
                appearances = new CardShaderAppearance[data.Cards.Count];
                owner.ValidatePresentationSettings();
                update = createUpdate();
                nextOrdered = new CardInstanceView[data.Cards.Count];
                try
                {
                    bool reuse = before != null && before.ZoneId == data.ZoneId;
                    for (int i = 0; i < data.Cards.Count; i++)
                    {
                        var card = data.Cards[i]; CardInstanceView view = null;
                        if (reuse) owner.views.TryGetValue(card.ViewId, out view);
                        if (view == null && imports != null) imports.TryGetValue(card.ViewId, out view);
                        if (view == null)
                        {
                            var root = new GameObject("CardInstance") { hideFlags = HideFlags.DontSave, layer = owner.gameObject.layer };
                            root.SetActive(false); root.transform.SetParent(owner.transform, false);
                            view = root.AddComponent<CardInstanceView>(); created.Add(view);
                            view.Initialize(prefab, bounds);
                        }
                        appearances[i] = CardInstanceView.PrepareArtwork(view.Surface, card);
                        next.Add(card.ViewId, view); nextOrdered[i] = view;
                    }
                }
                catch { Dispose(); throw; }
            }

            internal void ValidateCurrent()
            {
                if (disposed || committed || owner == null || owner.Data != before || owner.cardPrefab != prefab)
                    throw new InvalidOperationException("The prepared zone update is stale.");
                foreach (var view in nextOrdered)
                    if (view == null) throw new InvalidOperationException("A prepared card visual was removed.");
            }

            internal void Commit()
            {
                ValidateCurrent(); update.BeforeApply();
                for (int i = 0; i < nextOrdered.Length; i++)
                {
                    var view = nextOrdered[i];
                    if (view.transform.parent != owner.transform) view.transform.SetParent(owner.transform, false);
                    view.transform.localScale = Vector3.one;
                    view.SetPresentationScale(cardScale);
                    view.ConfigureMotion(motion);
                    update.PresentCard(view, data.Cards[i], appearances[i], poses[i], i, created.Contains(view));
                    view.gameObject.SetActive(true);
                }
                foreach (var previous in owner.ordered)
                    if (previous != null && (exports == null || !exports.Contains(previous.ViewId)) &&
                        (!next.TryGetValue(previous.ViewId, out var retained) || retained != previous)) update.RemoveCard(previous);
                owner.views = next; owner.ordered = nextOrdered; owner.Data = data;
                owner.boundPrefab = prefab; owner.cardBounds = bounds; committed = true;
                owner.layoutRefreshRequested = false;
                owner.OnCardScaleChanged(cardScale);
                foreach (var view in nextOrdered) view.RefreshTransferPose();
            }

            internal void NotifyCommitted()
            {
                if (!committed || notified) return;
                notified = true; update.AfterCommit();
            }
            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                if (!committed) foreach (var view in created) Release(view);
                update.Dispose();
            }
        }
    }
}
