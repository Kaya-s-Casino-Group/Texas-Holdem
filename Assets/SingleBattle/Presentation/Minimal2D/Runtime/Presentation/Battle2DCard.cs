using System;
using CardArt;
using PokerEngine;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Poker.Battle2DValidation
{
    /// <summary>A clickable flat card. Artwork stays in CardView's shared atlas/material and per-instance property block.</summary>
    public sealed class Battle2DCard : MonoBehaviour, IPointerClickHandler
    {
        private Battle2DView owner;
        private Vector3 slot;
        public CardInstanceId Id { get; private set; }
        public CardView Surface { get; private set; }
        public bool IsSelected { get; private set; }
        public void Bind(Battle2DView presentation, CardInstanceId id, string artwork)
        {
            owner = presentation; Id = id; Surface = GetComponentInChildren<CardView>();
            if (Surface == null) throw new InvalidOperationException("The 2D card prefab needs a CardView surface.");
            Surface.SetCard(artwork);
        }
        public void Place(Vector3 position, float scale)
        {
            slot = position; transform.localRotation = Quaternion.Euler(-90, 0, 0);
            transform.localScale = Vector3.one * scale;
            transform.localPosition = slot;
        }
        public void ShowSelection(bool selected, bool disabled)
        {
            IsSelected = selected;
            Surface.PresentArtwork(disabled ? new[] { "disabled" } : selected ? new[] { "selected" } : Array.Empty<string>(), Color.white);
            transform.localPosition = slot + (selected ? Vector3.up * .16f : Vector3.zero);
        }
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) owner.ToggleSelection(Id);
        }
    }
}
