using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace CardArt
{
    public enum CardShaderValueKind { Float, Color, Vector }
    public enum CardShaderParameterOperation { Set, Multiply }

    [Serializable]
    public sealed class CardShaderParameter
    {
        public string Name;
        public CardShaderValueKind Kind;
        public CardShaderParameterOperation Operation;
        public Vector4 Value;
        public CardShaderParameter(string name, CardShaderValueKind kind, Vector4 value, CardShaderParameterOperation operation = CardShaderParameterOperation.Set)
        { Name = name; Kind = kind; Value = value; Operation = operation; Validate(); }
        public CardShaderParameter Copy() => new CardShaderParameter(Name, Kind, Value, Operation);
        public static CardShaderParameter Float(string name, float value, CardShaderParameterOperation operation = CardShaderParameterOperation.Set) =>
            new CardShaderParameter(name, CardShaderValueKind.Float, new Vector4(value,0,0,0), operation);
        public static CardShaderParameter Color(string name, UnityEngine.Color value, CardShaderParameterOperation operation = CardShaderParameterOperation.Set) =>
            new CardShaderParameter(name, CardShaderValueKind.Color, value, operation);
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Name)) throw new ArgumentException("Shader 参数名不能为空。");
            if (Name == "_FrontUVRect" || Name == "_BackUVRect" || Name == "_BaseMap") throw new ArgumentException("Atlas 采样参数由渲染器维护，组合不能覆盖：" + Name);
            if (!Enum.IsDefined(typeof(CardShaderValueKind),Kind) || !Enum.IsDefined(typeof(CardShaderParameterOperation),Operation)) throw new ArgumentException("未知参数类型或运算。");
            for (int i=0;i<4;i++) if (float.IsNaN(Value[i]) || float.IsInfinity(Value[i])) throw new ArgumentException("Shader 参数值必须有限：" + Name);
        }
        public void ValidateFor(Material material)
        {
            Validate(); if (material == null || material.shader == null) throw new ArgumentException("缺少卡牌材质。");
            var shader = material.shader; int index = shader.FindPropertyIndex(Name);
            if (index < 0) throw new ArgumentException("Shader 缺少组合参数：" + Name);
            var type = shader.GetPropertyType(index);
            bool matches = Kind == CardShaderValueKind.Float ? type == ShaderPropertyType.Float || type == ShaderPropertyType.Range :
                Kind == CardShaderValueKind.Color ? type == ShaderPropertyType.Color : type == ShaderPropertyType.Vector;
            if (!matches) throw new ArgumentException("Shader 参数类型不匹配：" + Name);
        }
    }
    [Serializable]
    public sealed class CardShaderCombination
    {
        public string Id;
        public List<CardShaderParameter> Parameters = new List<CardShaderParameter>();
        public CardShaderCombination(string id, params CardShaderParameter[] parameters) { Id = id; Parameters = parameters.Select(p=>p.Copy()).ToList(); Validate(); }
        public CardShaderCombination Copy() => new CardShaderCombination(Id, Parameters.ToArray());
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Id) || Parameters == null || Parameters.Any(p=>p==null)) throw new ArgumentException("组合 ID 和参数列表必须有效。");
            foreach(var parameter in Parameters) parameter.Validate();
            if (Parameters.Select(p=>p.Name).Distinct(StringComparer.Ordinal).Count()!=Parameters.Count) throw new ArgumentException("同一组合不能重复写入参数。");
        }
    }
    /// <summary>Resolved, generic per-instance shader values. No game-state names or effect contracts.</summary>
    public sealed class CardShaderAppearance
    {
        private readonly Dictionary<string,CardShaderParameter> values;
        public IReadOnlyList<CardShaderParameter> Parameters => Array.AsReadOnly(values.Values.Select(p=>p.Copy()).ToArray());
        public Color Tint => GetColor("_ColorTint", Color.white);
        internal CardShaderAppearance(Dictionary<string,CardShaderParameter> parameters) { values = parameters; }
        public float GetFloat(string name, float fallback=0) => values.TryGetValue(name,out var value) ? value.Value.x : fallback;
        public Vector4 GetVector(string name, Vector4 fallback) => values.TryGetValue(name,out var value) ? value.Value : fallback;
        public Color GetColor(string name, Color fallback) => GetVector(name,fallback);
        public void ValidateFor(CardView surface) => ValidateFor(surface.GetComponent<MeshRenderer>().sharedMaterial);
        public void ValidateFor(Material material) { foreach(var parameter in values.Values) parameter.ValidateFor(material); }
        public void WriteTo(MaterialPropertyBlock block)
        {
            foreach(var parameter in values.Values)
            {
                int id=Shader.PropertyToID(parameter.Name);
                if(parameter.Kind==CardShaderValueKind.Float) block.SetFloat(id,parameter.Value.x);
                else if(parameter.Kind==CardShaderValueKind.Color) block.SetColor(id,parameter.Value);
                else block.SetVector(id,parameter.Value);
            }
        }
        internal static CardShaderAppearance Compose(IEnumerable<CardShaderCombination> combinations, Color tint)
        {
            var values = new Dictionary<string,CardShaderParameter>(StringComparer.Ordinal);
            foreach(var combination in combinations)
            {
                combination.Validate();
                foreach(var source in combination.Parameters)
                {
                    var parameter=source.Copy();
                    if(parameter.Operation==CardShaderParameterOperation.Multiply)
                    {
                        if(!values.TryGetValue(parameter.Name,out var previous) || previous.Kind!=parameter.Kind)
                            throw new ArgumentException("乘算参数需要已有同类型基础值："+parameter.Name);
                        parameter.Value=Vector4.Scale(previous.Value,parameter.Value);
                    }
                    parameter.Operation=CardShaderParameterOperation.Set; parameter.Validate(); values[parameter.Name]=parameter;
                }
            }
            var multiplier=CardShaderParameter.Color("_ColorTint",tint); multiplier.Validate();
            if(values.TryGetValue("_ColorTint",out var original)) multiplier.Value=Vector4.Scale(original.Value,multiplier.Value);
            values["_ColorTint"]=multiplier;
            return new CardShaderAppearance(values);
        }
    }
}
