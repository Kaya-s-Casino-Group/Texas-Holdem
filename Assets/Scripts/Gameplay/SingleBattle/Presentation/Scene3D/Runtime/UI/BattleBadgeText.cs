using TMPro;
using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>Shared black-face, white-outline preset for icon counters.</summary>
    public static class BattleBadgeText
    {
        private static Material material;

        public static void Apply(TMP_Text label)
        {
            if (label == null || label.font == null) return;
            if (material == null) material = Resources.Load<Material>("NotoSansCJKSCRegular Badge");
            // A preset is only valid for the font atlas it was authored against.
            if (material == null || material.mainTexture != label.font.atlasTexture) return;
            if (label.fontSharedMaterial != material)
            {
                label.fontSharedMaterial = material;
                label.UpdateMeshPadding();
            }
            // TMP multiplies the vertex tint into both the face and outline.
            label.color = Color.white;
        }
    }
}
