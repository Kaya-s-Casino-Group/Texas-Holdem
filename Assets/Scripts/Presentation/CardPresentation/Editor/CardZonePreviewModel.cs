#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using PokerEngine;
using CardInteraction;
using UnityEngine;

namespace CardPresentation.Editor
{
    [Serializable]
    public sealed class PreviewCard
    {
        public string Id;
        public string ArtId;
        public string OriginId;
        public bool FaceUp = true;
        public Color Tint = Color.white;
        public CardEffects Effects;
        public bool IsDisabled;
        public bool IsHovered;
        public bool IsSelected;
        public CardInteractionState Interaction => new CardInteractionState(IsHovered, IsSelected);
        public void SetInteraction(CardInteractionState state) { IsHovered = state.IsHovered; IsSelected = state.IsSelected; }

        // Serialized editor backing data. Rule edits are validated as Core CardInstance values before being stored.
        public CardInstance ToCard() => new CardInstance(new CardInstanceId(Id), CardZonePreviewCatalog.DefinitionFor(ArtId),
            originId: string.IsNullOrEmpty(OriginId) ? null : OriginId, effects: Effects, isDisabled: IsDisabled);
    }

    /// <summary>Editor sample input, separate from the visual instances and any live match.</summary>
    [Serializable]
    public sealed class CardZonePreviewModel : ICardInteractionSource
    {
        [SerializeField] private List<PreviewCard> cards = new List<PreviewCard>();
        public IReadOnlyList<PreviewCard> Cards => cards.AsReadOnly();
        public const int MaximumCards = 64;

        public void Add(string artId) => Insert(cards.Count, artId);

        public void Insert(int index, string artId, int count = 1, bool faceUp = true)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (count > MaximumCards - cards.Count) throw new InvalidOperationException("预览最多支持 64 张牌。");
            Guard.Text(artId, nameof(artId));
            var added = Enumerable.Range(0, count).Select(_ => new PreviewCard {
                Id = "preview-" + Guid.NewGuid().ToString("N"), ArtId = artId, FaceUp = faceUp }).ToArray();
            foreach (var card in added) card.ToCard();
            var zone = BuildZone().InsertRange(index, added.Select(card => new CardInstanceId(card.Id)));
            var all = cards.Concat(added).ToDictionary(card => card.Id, StringComparer.Ordinal);
            cards = zone.Cards.Select(id => all[id.Value]).ToList();
        }

        public void Duplicate(int index, int targetIndex)
        {
            if (index < 0 || index >= cards.Count) throw new ArgumentOutOfRangeException(nameof(index));
            var original = cards[index];
            original.ToCard();
            Insert(targetIndex, original.ArtId);
            var copy = cards[targetIndex];
            copy.Effects = original.Effects; copy.IsDisabled = original.IsDisabled;
            copy.FaceUp = original.FaceUp; copy.Tint = original.Tint;
            copy.OriginId = original.OriginId;
            // A new identity must not inherit a pending click/selection.
        }
        public void Clear() => cards.Clear();
        public void Load(IEnumerable<CardZoneCardState> source, bool clearHover = true)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var next = source.Select(card => new PreviewCard {
                Id = card.Card.Id.Value, ArtId = card.ArtId, OriginId = card.Card.OriginId, FaceUp = card.FaceUp, Tint = card.Tint,
                Effects = card.Card.Effects, IsDisabled = card.Card.IsDisabled,
                IsSelected = card.Interaction.IsSelected, IsHovered = !clearHover && card.Interaction.IsHovered
            }).ToList();
            if (next.Count > MaximumCards) throw new InvalidOperationException("预览最多支持 64 张牌。");
            new CardZone(new ZoneId("editor/card-zone"), "editor.preview", null, next.Select(card => card.ToCard().Id));
            cards = next;
        }
        public CardInstance GetCard(CardInstanceId id) => Find(id).ToCard();

        public bool TryGetInteraction(string viewId, out CardInteractionState state)
        {
            var card = cards.FirstOrDefault(c => c.Id == viewId);
            state = card == null ? default(CardInteractionState) : card.Interaction;
            return card != null;
        }
        public bool CanSelect(string viewId)
        {
            var card = cards.FirstOrDefault(c => c.Id == viewId);
            return card != null && card.ToCard().CanPlay;
        }
        public void SetInteraction(string viewId, CardInteractionState state) => Find(new CardInstanceId(viewId)).SetInteraction(state);

        public void SetCardProperties(CardInstanceId id, CardDefinitionId definition, CardEffects effects, bool disabled)
        {
            var sample = Find(id); var current = sample.ToCard();
            var next = new CardInstance(current.Id, definition, current.OriginId, current.Effects, current.IsDisabled)
                .WithEffects(effects).WithDisabled(disabled);
            var art = CardZonePreviewCatalog.ArtFor(next.DefinitionId);
            // Complete Core/catalog validation before changing the serialized editor data.
            sample.ArtId = art; sample.Effects = next.Effects; sample.IsDisabled = next.IsDisabled;
        }

        private PreviewCard Find(CardInstanceId id) => cards.Single(c => c.Id == id.Value);
        public void LoadEffectSamples(string artId)
        {
            Guard.Text(artId, nameof(artId)); Clear();
            var effects = new[] { CardEffects.None, CardEffects.Enhanced, CardEffects.Laminated,
                CardEffects.Enhanced | CardEffects.Laminated, CardEffects.Ineffective,
                CardEffects.Enhanced | CardEffects.Laminated | CardEffects.Ineffective, CardEffects.None,
                CardEffects.Enhanced | CardEffects.Laminated | CardEffects.Ineffective };
            for (int i = 0; i < effects.Length; i++)
            {
                Add(artId); var card = cards[i].ToCard();
                SetCardProperties(card.Id, card.DefinitionId, effects[i], i >= 6);
            }
        }
        public CardZone BuildZone() => BuildZone(new ZoneId("editor/card-zone"));
        public CardZone BuildZone(ZoneId id, string kind = "editor.preview", ParticipantId? controllerId = null) => new CardZone(id, kind, controllerId,
            cards.Select(c => new CardInstanceId(c.Id)));

        /// <summary>Editor simulation: transfer existing top-card identities through Core, then commit both sample lists.</summary>
        public CardZoneTransfer DrawFromDeck(CardZonePreviewModel deck, int count, int targetIndex = -1)
        {
            if (deck == null) throw new ArgumentNullException(nameof(deck));
            if (ReferenceEquals(deck, this)) throw new ArgumentException("牌堆与目标牌区必须不同。", nameof(deck));
            if (count <= 0 || count > deck.cards.Count) throw new ArgumentOutOfRangeException(nameof(count), "抽牌数须大于零且不超过牌堆剩余数量。");
            return TakeFromDeck(deck, deck.cards.AsEnumerable().Reverse().Take(count).Select(card => new CardInstanceId(card.Id)), targetIndex);
        }

        public CardZoneTransfer TakeFromDeck(CardZonePreviewModel deck, IEnumerable<CardInstanceId> ids, int targetIndex = -1)
        {
            if (deck == null) throw new ArgumentNullException(nameof(deck));
            if (ReferenceEquals(deck, this)) throw new ArgumentException("牌堆与目标牌区必须不同。", nameof(deck));
            if (ids == null) throw new ArgumentNullException(nameof(ids));
            var selected = ids.ToArray();
            if (selected.Length == 0) throw new ArgumentException("请选择至少一张牌。", nameof(ids));
            if (selected.Length > MaximumCards - cards.Count) throw new InvalidOperationException("预览最多支持 64 张牌。");
            var transfer = deck.BuildZone(new ZoneId("editor/draw-deck")).TransferRangeTo(BuildZone(), selected,
                targetIndex == -1 ? cards.Count : targetIndex);
            foreach (var id in selected) deck.GetCard(id);
            var all = cards.Concat(deck.cards).ToDictionary(c => c.Id, StringComparer.Ordinal);
            var nextHand = transfer.Target.Cards.Select(id => all[id.Value]).ToList();
            var nextDeck = transfer.Source.Cards.Select(id => all[id.Value]).ToList();
            cards = nextHand; deck.cards = nextDeck;
            return transfer;
        }
        public CardVisualData Project(CardInstanceId id)
        {
            var card = Find(id);
            return CardVisualData.FromCard(GetCard(id), card.Id, card.FaceUp, CardZonePreviewCatalog.ArtFor,
                card.Interaction, card.Tint);
        }
        public void RemoveAt(int index) => SetOrder(BuildZone().RemoveAt(index).Cards);
        public void Move(int fromIndex, int toIndex) => SetOrder(BuildZone().Move(fromIndex, toIndex).Cards);
        public void Reverse() => SetOrder(BuildZone().Reorder(cards.Select(c => new CardInstanceId(c.Id)).Reverse()).Cards);
        public void SortBySuit() => SetOrder(BuildZone().SortBySuit(ResolveDefinition).Cards);
        public void SortByRank() => SetOrder(BuildZone().SortByRank(ResolveDefinition).Cards);
        private CardDefinition ResolveDefinition(CardInstanceId id) =>
            CardZonePreviewCatalog.StandardDefinitionFor(GetCard(id).DefinitionId);
        public void Shuffle()
        {
            var order = cards.Select(c => new CardInstanceId(c.Id)).ToArray(); var random = new System.Random();
            for (int i = order.Length - 1; i > 0; i--)
            { int j = random.Next(i + 1); var item = order[i]; order[i] = order[j]; order[j] = item; }
            SetOrder(BuildZone().Reorder(order).Cards);
        }
        private void SetOrder(IEnumerable<CardInstanceId> order)
        {
            var existing = cards.ToDictionary(c => c.Id, StringComparer.Ordinal);
            cards = order.Select(id => existing[id.Value]).ToList();
        }
    }
}
#endif
