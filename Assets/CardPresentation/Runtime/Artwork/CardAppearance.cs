using System;
using CardArt;
using UnityEngine;
namespace CardPresentation
{
    /// <summary>The gameplay adapter chooses art combination IDs, never interprets their shader parameters.</summary>
    public static class CardAppearanceResolver
    {
        public static string[] Combinations(CardVisualData card)
        {
            if(card==null) throw new ArgumentNullException(nameof(card));
            return (card.ArtBindings != null ? card.ArtBindings : CardStateArtBindings.Standard).Select(card);
        }
        public static Color Tint(CardVisualData card) => card.FaceUp ? card.Tint : Color.white;
        public static CardShaderAppearance Resolve(CardVisualData card, CardArtProfile profile)
        {
            if(profile==null) throw new ArgumentException("请绑定卡牌参数组合美术配置。");
            return profile.Resolve(Combinations(card),Tint(card));
        }
    }
}
