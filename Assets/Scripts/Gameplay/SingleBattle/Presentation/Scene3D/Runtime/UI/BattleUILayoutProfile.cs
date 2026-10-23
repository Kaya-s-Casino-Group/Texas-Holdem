using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.SingleBattle
{
    // UI geometry only: never stores combat values, active state, button state or liquid/health fill.
    public sealed class BattleUILayoutProfile : ScriptableObject
    {
        public Node[] nodes = Array.Empty<Node>();
        public CanvasSettings[] canvases = Array.Empty<CanvasSettings>();
        public int Version { get; private set; }
        private void OnValidate() => Version++;

        public static string CanonicalPath(string path) => path.Replace("Safe Area/", "");
        public static bool Includes(string root, string path)
        {
            path = CanonicalPath(path);
            if (path.Length == 0 || path == "Safe Area") return false;
            if (root == "Adventure UI") return path == "Run Header" || path.StartsWith("Run Header/旅程菜单");
            if (root != "Battle HUD" && root != "HandActionPanel" && root != "DrawPileIcon" && root != "DeckOverview") return false;
            if (path.StartsWith("Timeline Feedback") || path.StartsWith("Data Bindings") || path.StartsWith("Target Enemy")) return false;
            if (path == "Enemy" || path.StartsWith("Enemy/") || path.StartsWith("Enemy Intent")) return false;
            if (path.Contains("/Relic Icons/")) return false; // Runtime relic instances retain their content and adaptive grid.
            if (path.Contains("/Concept Relic Slot ")) return false; // Slot frames use that same runtime grid.
            if (path.EndsWith("Health Bar/Fill") || path.EndsWith("Health Bar/Health Fill")) return false;
            return true;
        }

        [Serializable]
        public sealed class Node
        {
            public string root;
            public BattleLayoutProfile.RectPose pose;
            public TMP_FontAsset font;
            public bool hasOrbColor;
            public Color orbColor;
            public void Apply(RectTransform target)
            {
                pose.Apply(target);
                if (font != null && target.TryGetComponent<TMP_Text>(out var text)) text.font = font;
                if (hasOrbColor && target.TryGetComponent<Graphic>(out var graphic)) graphic.color = orbColor;
            }
        }

        [Serializable]
        public sealed class CanvasSettings
        {
            public string root;
            public CanvasScaler.ScaleMode mode;
            public CanvasScaler.ScreenMatchMode matchMode;
            public Vector2 referenceResolution;
            public float matchWidthOrHeight, referencePixelsPerUnit, scaleFactor;
            public void Apply(CanvasScaler scaler)
            {
                scaler.uiScaleMode = mode; scaler.screenMatchMode = matchMode;
                scaler.referenceResolution = referenceResolution; scaler.matchWidthOrHeight = matchWidthOrHeight;
                scaler.referencePixelsPerUnit = referencePixelsPerUnit;
                if (mode == CanvasScaler.ScaleMode.ConstantPixelSize) scaler.scaleFactor = scaleFactor;
            }
        }
    }
}
