using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace CardArt
{
    /// <summary>Atlas and named shader combinations only. Game-state selection belongs to the consumer.</summary>
    [CreateAssetMenu(fileName = "卡牌美术配置", menuName = "卡牌美术/美术配置")]
    public sealed class CardArtProfile : ScriptableObject
    {
        [SerializeField, InspectorName("卡牌图集")] private CardAtlasData atlas;
        [SerializeField, InspectorName("基础组合 ID")] private string baseCombination = "base";
        [SerializeField, InspectorName("Shader 参数组合")] private List<CardShaderCombination> combinations = new List<CardShaderCombination> { new CardShaderCombination("base",CardShaderParameter.Color("_ColorTint",Color.white)) };
        public CardAtlasData Atlas => atlas;
        public string BaseCombination => baseCombination;
        public IReadOnlyList<CardShaderCombination> Combinations => combinations.Select(c=>c.Copy()).ToArray();
        public event Action Changed;
        public void Configure(CardAtlasData skin, IEnumerable<CardShaderCombination> presets, string baseId = "base")
        {
            if(skin==null) throw new ArgumentNullException(nameof(skin));
            var copy=ValidateCombinations(presets,baseId); atlas=skin; combinations=copy; baseCombination=baseId; Changed?.Invoke();
        }
        private static List<CardShaderCombination> ValidateCombinations(IEnumerable<CardShaderCombination> presets,string baseId)
        {
            if(presets==null) throw new ArgumentNullException(nameof(presets));
            var copy=presets.Select(c=>c?.Copy() ?? throw new ArgumentException("缺少参数组合。")).ToList();
            if(copy.Select(c=>c.Id).Distinct(StringComparer.Ordinal).Count()!=copy.Count || !copy.Any(c=>c.Id==baseId)) throw new ArgumentException("组合 ID 必须唯一，且存在基础组合。");
            // Validate every overlay independently against the base; ordered combinations validate again when resolved.
            var basis=copy.Single(c=>c.Id==baseId);
            foreach(var preset in copy) CardShaderAppearance.Compose(new[]{basis,preset},Color.white);
            return copy;
        }
        public void SetCombinations(IEnumerable<CardShaderCombination> presets)
        { combinations=ValidateCombinations(presets,baseCombination); Changed?.Invoke(); }
        public void ValidateAtlas(CardAtlasData skin)
        { if(atlas!=null && skin!=atlas) throw new ArgumentException("Card renderer and art profile must use the same atlas."); }
        public CardShaderAppearance Resolve(IEnumerable<string> ids, Color tint)
        {
            var chosen=new List<CardShaderCombination>{combinations.SingleOrDefault(c=>c.Id==baseCombination) ?? throw new ArgumentException("缺少基础组合。")};
            foreach(var id in ids ?? Array.Empty<string>())
                chosen.Add(combinations.SingleOrDefault(c=>c.Id==id) ?? throw new ArgumentException("美术配置缺少组合："+id));
            return CardShaderAppearance.Compose(chosen,tint);
        }
#if UNITY_EDITOR
        private void OnValidate()
        { UnityEditor.EditorApplication.delayCall -= NotifyChanged; UnityEditor.EditorApplication.delayCall += NotifyChanged; }
        private void NotifyChanged() { if(this!=null) Changed?.Invoke(); }
#endif
    }
}
