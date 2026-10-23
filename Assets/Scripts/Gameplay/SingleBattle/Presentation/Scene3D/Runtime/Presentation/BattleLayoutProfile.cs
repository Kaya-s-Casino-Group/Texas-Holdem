using System;
using System.Linq;
using TMPro;
using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>Authored geometry only. Combat values, visibility and badge symbols remain live.</summary>
    public sealed class BattleLayoutProfile : ScriptableObject
    {
        public Entry[] entries = Array.Empty<Entry>();

        public Entry Find(string encounter, string actorId)
        {
            if (encounter == "single-chapter-battle-2") encounter = "single-chapter-battle-1";
            return entries.FirstOrDefault(e => e.encounter == encounter && e.actorId == actorId);
        }

        [Serializable]
        public sealed class Entry
        {
            public string encounter, actorId;
            public Vector2 actorViewport;
            public float depth, referenceFrustumHeight, referenceAspect;
            public float referenceHudHeight = 720;
            public Vector3 actorScale;
            public Quaternion cameraRelativeRotation;
            public Vector3 artworkPosition, artworkScale = Vector3.one;
            public Quaternion artworkRotation = Quaternion.identity;
            public Vector2 hudOffset, hudViewport;
            public bool fixedHud;
            public RectPose hudRoot;
            public RectPose[] nodes = Array.Empty<RectPose>();
        }

        [Serializable]
        public sealed class RectPose
        {
            public string path;
            public Vector2 anchorMin, anchorMax, pivot, sizeDelta;
            public Vector3 position, scale = Vector3.one;
            public Quaternion rotation = Quaternion.identity;
            public bool hasText, autoSize;
            public float fontSize, fontMin, fontMax, characterSpacing, lineSpacing;
            public TextAlignmentOptions alignment;
            public FontStyles fontStyle;
            public void Apply(RectTransform target, bool positionToo = true)
            {
                target.anchorMin = anchorMin; target.anchorMax = anchorMax; target.pivot = pivot;
                target.sizeDelta = sizeDelta; target.localScale = scale; target.localRotation = rotation;
                if (positionToo) target.anchoredPosition3D = position;
                if (!hasText || !target.TryGetComponent<TMP_Text>(out var text)) return;
                text.enableAutoSizing = autoSize; text.fontSize = fontSize;
                text.fontSizeMin = fontMin; text.fontSizeMax = fontMax;
                text.characterSpacing = characterSpacing; text.lineSpacing = lineSpacing;
                text.alignment = alignment; text.fontStyle = fontStyle;
            }
        }
    }
}
