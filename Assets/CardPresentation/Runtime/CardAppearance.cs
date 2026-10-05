using System;
using CardArt;
using PokerEngine;
using UnityEngine;

namespace CardPresentation
{
    /// <summary>Art tuning only. Rule flags decide which effects exist; these values never enter engine state.</summary>
    [Serializable]
    public struct CardAppearanceSettings
    {
        [ColorUsage(false, true)] public Color FoilColor;
        [Range(0, 5)] public float FoilStrength;
        [ColorUsage(false)] public Color SealColor;
        [Range(0, 1)] public float SealEmissionStrength;
        [Range(0, 1)] public float IneffectiveBrightness;
        [Range(0, 1)] public float IneffectiveFoilMultiplier;

        public static CardAppearanceSettings Default => new CardAppearanceSettings {
            FoilColor = Color.white, FoilStrength = 1.2f,
            SealColor = new Color(0.65f, 0.08f, 0.04f, 1), SealEmissionStrength = 0.2f,
            IneffectiveBrightness = 0.45f, IneffectiveFoilMultiplier = 0.2f };

        public void Validate()
        {
            ValidateColor(FoilColor); ValidateColor(SealColor);
            ValidateRange(FoilStrength, 5); ValidateRange(SealEmissionStrength, 1);
            ValidateRange(IneffectiveBrightness, 1); ValidateRange(IneffectiveFoilMultiplier, 1);
        }

        private static void ValidateRange(float value, float max)
        {
            if (!CardZoneRowLayout.Finite(value) || value < 0 || value > max)
                throw new ArgumentOutOfRangeException(nameof(value), "Card appearance strength is out of range.");
        }
        private static void ValidateColor(Color color)
        {
            // These effect interfaces consume RGB only. HDR values are deliberately not clamped to 1.
            if (!CardZoneRowLayout.Finite(color.r) || !CardZoneRowLayout.Finite(color.g) || !CardZoneRowLayout.Finite(color.b) ||
                color.r < 0 || color.g < 0 || color.b < 0) throw new ArgumentException("Card effect colors require finite nonnegative RGB.");
        }
    }

    /// <summary>Complete, resolved shader input. Never reads last frame's colors or effect strengths.</summary>
    public sealed class CardShaderAppearance
    {
        public Color Tint { get; }
        public bool FoilEnabled { get; }
        public Color FoilColor { get; }
        public float FoilStrength { get; }
        public bool SealEnabled { get; }
        public Color SealColor { get; }
        public float SealEmissionStrength { get; }
        public bool RedCrossEnabled { get; }

        internal CardShaderAppearance(Color tint, bool foil, Color foilColor, float foilStrength,
            bool seal, Color sealColor, float emission, bool redCross)
        {
            Tint = tint; FoilEnabled = foil; FoilColor = foilColor; FoilStrength = foilStrength;
            SealEnabled = seal; SealColor = sealColor; SealEmissionStrength = emission; RedCrossEnabled = redCross;
        }

        internal void ValidateFor(CardView surface)
        {
            var material = surface.GetComponent<MeshRenderer>().sharedMaterial;
            if (FoilEnabled && !surface.SupportsFoil) throw new ArgumentException("塑封需要支持镭射的卡牌材质。");
            if (SealEnabled && (!surface.SupportsSeal || material.GetTexture(CardShaderProperties.SealMap) == null))
                throw new ArgumentException("强化需要支持火漆且配置火漆贴图的卡牌材质。");
            if (SealEmissionStrength > 0 && !surface.SupportsSealEmission)
                throw new ArgumentException("当前强化样式需要支持火漆自发光的卡牌材质。");
            if (RedCrossEnabled && (!surface.SupportsRedCross || material.GetTexture(CardShaderProperties.RedCrossMap) == null))
                throw new ArgumentException("禁用需要支持红叉且配置红叉贴图的卡牌材质。");
        }

        internal void ApplyTo(CardView surface)
        {
            // CardView setters preserve all other parameters. Apply the whole snapshot to clear removed effects too.
            surface.SetTint(Tint);
            surface.SetFoilEnabled(FoilEnabled); surface.SetFoilColor(FoilColor); surface.SetFoilStrength(FoilStrength);
            surface.SetSealEnabled(SealEnabled); surface.SetSealColor(SealColor); surface.SetSealEmissionStrength(SealEmissionStrength);
            surface.SetRedCrossEnabled(RedCrossEnabled);
        }
    }

    public static class CardAppearanceResolver
    {
        public static CardShaderAppearance Resolve(CardVisualData card, CardAppearanceSettings settings)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));
            settings.Validate();
            var foilColor = settings.FoilColor; foilColor.a = 1;
            var sealColor = settings.SealColor; sealColor.a = 1;
            // Enforce the back-face boundary even if a caller directly constructs unfiltered visual data.
            if (!card.FaceUp) return new CardShaderAppearance(Color.white, false, foilColor, 0, false, sealColor, 0, false);

            bool foil = (card.Effects & CardEffects.Laminated) != 0;
            bool seal = (card.Effects & CardEffects.Enhanced) != 0;
            bool ineffective = (card.Effects & CardEffects.Ineffective) != 0;
            var tint = card.Tint;
            if (ineffective)
            {
                tint.r *= settings.IneffectiveBrightness; tint.g *= settings.IneffectiveBrightness; tint.b *= settings.IneffectiveBrightness;
            }
            return new CardShaderAppearance(tint, foil, foilColor,
                foil ? settings.FoilStrength * (ineffective ? settings.IneffectiveFoilMultiplier : 1) : 0,
                seal, sealColor, seal && !ineffective ? settings.SealEmissionStrength : 0, card.IsDisabled);
        }
    }
}
