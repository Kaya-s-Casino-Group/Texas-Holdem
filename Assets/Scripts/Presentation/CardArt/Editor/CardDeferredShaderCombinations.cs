using CardArt;
using UnityEngine;
namespace CardArt.Editor
{
    /// <summary>Example presets for the existing shader. The generic profile and renderer do not require these names.</summary>
    public static class CardDeferredShaderCombinations
    {
        public static CardShaderCombination[] Create(Color? foil = null, float foilStrength = 1.2f, Color? seal = null,
            float emission = .2f, float brightness = .45f, float foilMultiplier = .2f) => new[] {
            new CardShaderCombination("base", CardShaderParameter.Color("_ColorTint",Color.white),
                CardShaderParameter.Float("_FoilEnabled",0),CardShaderParameter.Color("_FoilColor",foil ?? Color.white),CardShaderParameter.Float("_FoilStrength",0),
                CardShaderParameter.Float("_SealEnabled",0),CardShaderParameter.Color("_SealColor",seal ?? new Color(.65f,.08f,.04f,1)),CardShaderParameter.Float("_SealEmissionStrength",0),CardShaderParameter.Float("_RedCrossEnabled",0)),
            new CardShaderCombination("wax",CardShaderParameter.Float("_SealEnabled",1),CardShaderParameter.Float("_SealEmissionStrength",emission)),
            new CardShaderCombination("foil",CardShaderParameter.Float("_FoilEnabled",1),CardShaderParameter.Float("_FoilStrength",foilStrength)),
            new CardShaderCombination("dim",CardShaderParameter.Color("_ColorTint",new Color(brightness,brightness,brightness,1),CardShaderParameterOperation.Multiply),CardShaderParameter.Float("_FoilStrength",foilMultiplier,CardShaderParameterOperation.Multiply),CardShaderParameter.Float("_SealEmissionStrength",0)),
            new CardShaderCombination("cross",CardShaderParameter.Float("_RedCrossEnabled",1))
        };
    }
}
