using System;
using System.Collections.Generic;
using System.Linq;
using PokerEngine;
using UnityEngine;

namespace CardPresentation
{
    public enum CardDisabledCondition { Any, Disabled, Enabled }
    [Serializable]
    public sealed class CardStateArtRule
    {
        [InspectorName("必须具备的效果")] public CardEffects Required;
        [InspectorName("不能具备的效果")] public CardEffects Excluded;
        [InspectorName("禁用条件")] public CardDisabledCondition Disabled;
        [InspectorName("叠加的美术组合 ID")] public List<string> Combinations = new List<string>();
        public bool Matches(CardVisualData card) => (card.Effects & Required) == Required && (card.Effects & Excluded) == 0 &&
            (Disabled == CardDisabledCondition.Any || card.IsDisabled == (Disabled == CardDisabledCondition.Disabled));
    }
    /// <summary>Consumer-owned state-to-art mapping; list order is the explicit overlay priority.</summary>
    [CreateAssetMenu(fileName="卡牌状态美术映射",menuName="玩法/卡牌状态美术映射")]
    public sealed class CardStateArtBindings : ScriptableObject
    {
        [SerializeField, InspectorName("按顺序应用匹配规则")] private List<CardStateArtRule> rules = new List<CardStateArtRule>();
        private static CardStateArtBindings standard;
        public static CardStateArtBindings Standard => standard != null ? standard :
            (standard = Resources.Load<CardStateArtBindings>("StandardCardStateArt")) ?? throw new InvalidOperationException("缺少默认卡牌状态美术映射。");
        public void Configure(IEnumerable<CardStateArtRule> entries)
        {
            if(entries==null) throw new ArgumentNullException(nameof(entries));
            var copy=new List<CardStateArtRule>();
            foreach(var entry in entries)
            {
                if(entry==null || entry.Combinations==null || entry.Combinations.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("无效状态映射规则。");
                CardEffectRules.Validate(entry.Required); CardEffectRules.Validate(entry.Excluded);
                if((entry.Required & entry.Excluded)!=0 || !Enum.IsDefined(typeof(CardDisabledCondition),entry.Disabled)) throw new ArgumentException("状态匹配条件冲突或无效。");
                copy.Add(new CardStateArtRule { Required=entry.Required,Excluded=entry.Excluded,Disabled=entry.Disabled,Combinations=new List<string>(entry.Combinations) });
            }
            rules=copy;
        }
        public string[] Select(CardVisualData card)
        {
            if(card==null) throw new ArgumentNullException(nameof(card));
            if(!card.FaceUp) return Array.Empty<string>();
            return rules.Where(rule=>rule.Matches(card)).SelectMany(rule=>rule.Combinations).ToArray();
        }
    }
}
