#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardPresentation.Editor
{
    /// <summary>Editable display order. Decks show their next draw first while Core keeps its tail-as-top convention.</summary>
    public sealed class CardZonePreviewList
    {
        public CardZonePreviewModel Model { get; }
        private readonly bool topFirst;
        public IReadOnlyList<PreviewCard> Cards => topFirst ? Model.Cards.Reverse().ToArray() : Model.Cards;

        public CardZonePreviewList(CardZonePreviewModel model, bool topFirst = false)
        { Model = model ?? throw new ArgumentNullException(nameof(model)); this.topFirst = topFirst; }

        private int DataIndex(int row)
        {
            if (row < 0 || row >= Model.Cards.Count) throw new ArgumentOutOfRangeException(nameof(row));
            return topFirst ? Model.Cards.Count - 1 - row : row;
        }
        private int InsertionIndex(int row)
        {
            if (row < 0 || row > Model.Cards.Count) throw new ArgumentOutOfRangeException(nameof(row));
            return topFirst ? Model.Cards.Count - row : row;
        }
        public void Move(int fromRow, int toRow) => Model.Move(DataIndex(fromRow), DataIndex(toRow));
        public void RemoveAt(int row) => Model.RemoveAt(DataIndex(row));
        public void Insert(int row, string artId, int count = 1) => Model.Insert(InsertionIndex(row), artId, count);
        public void DuplicateAfter(int row) => Model.Duplicate(DataIndex(row), InsertionIndex(row + 1));
        public void SortBySuit() => Sort(Model.SortBySuit);
        public void SortByRank() => Sort(Model.SortByRank);
        private void Sort(Action sort)
        {
            // Sort in display order so equal cards and unranked artwork remain stable on repeated clicks.
            if (topFirst) Model.Reverse();
            try { sort(); }
            finally { if (topFirst) Model.Reverse(); }
        }
    }
}
#endif
