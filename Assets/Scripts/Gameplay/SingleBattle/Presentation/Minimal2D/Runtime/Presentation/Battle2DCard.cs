using System;
using CardArt;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Poker.Battle2DValidation
{
    /// <summary>A clickable flat card. Artwork stays in CardView's shared atlas/material and per-instance property block.</summary>
    public sealed class Battle2DCard : MonoBehaviour, IPointerClickHandler
    {
        private Battle2DView owner;
        private Vector3 slot;
        private Vector3 actionOffset;
        private object actionOwner;
        public string Id { get; private set; }
        public CardView Surface { get; private set; }
        public bool IsSelected { get; private set; }
        public void Bind(Battle2DView presentation, string id, string artwork)
        {
            owner = presentation; Id = id ?? throw new ArgumentNullException(nameof(id)); Surface = GetComponentInChildren<CardView>();
            if (Surface == null) throw new InvalidOperationException("The 2D card prefab needs a CardView surface.");
            Surface.SetCard(artwork);
        }
        public void Place(Vector3 position, float scale)
        {
            slot = position; transform.localRotation = Quaternion.Euler(-90, 0, 0);
            transform.localScale = Vector3.one * scale;
            ApplyPosition();
        }
        public void ShowSelection(bool selected, bool disabled)
        {
            IsSelected = selected;
            Surface.PresentArtwork(disabled ? new[] { "disabled" } : selected ? new[] { "selected" } : Array.Empty<string>(), Color.white);
            ApplyPosition();
        }
        private void ApplyPosition() => transform.localPosition = slot + actionOffset + (IsSelected ? Vector3.up * .16f : Vector3.zero);
        public bool TryOwnAction(object owner)
        {
            if (actionOwner != null && !ReferenceEquals(actionOwner, owner)) return false;
            actionOwner = owner; return true;
        }
        public void SetActionOffset(object owner, Vector3 offset)
        { if (ReferenceEquals(actionOwner, owner)) { actionOffset = offset; ApplyPosition(); } }
        public void ReleaseAction(object owner)
        { if (ReferenceEquals(actionOwner, owner)) { actionOwner = null; actionOffset = Vector3.zero; ApplyPosition(); } }
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) owner.ToggleSelection(Id);
        }
    }
}
