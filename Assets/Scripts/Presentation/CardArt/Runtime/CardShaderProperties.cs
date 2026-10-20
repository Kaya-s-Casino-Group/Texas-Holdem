using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CardArt
{
    public static class CardShaderProperties
    {
        public const string ShaderName = "CardArt/Card Unlit Forward";
        public const string UVRectContract = "OffsetXYScaleZW";
        public const string SurfaceContract = "UV1BackXEdgeY";
        public static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        public static readonly int FrontUVRect = Shader.PropertyToID("_FrontUVRect");
        public static readonly int BackUVRect = Shader.PropertyToID("_BackUVRect");
        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public static readonly int ColorTint = Shader.PropertyToID("_ColorTint");
        public static readonly int EdgeColor = Shader.PropertyToID("_EdgeColor");
        public static readonly int FoilEnabled = Shader.PropertyToID("_FoilEnabled");
        public static readonly int FoilColor = Shader.PropertyToID("_FoilColor");
        public static readonly int FoilStrength = Shader.PropertyToID("_FoilStrength");

        public static readonly int RedCrossEnabled = Shader.PropertyToID("_RedCrossEnabled");
        public static readonly int RedCrossMap = Shader.PropertyToID("_RedCrossMap");
        public static bool SupportsRedCross(Material material) => material != null &&
            material.GetTag("CardRedCrossContract", false) == "OverlayV1" &&
            material.HasProperty(RedCrossEnabled) && material.HasProperty(RedCrossMap);

        public static readonly int SealEnabled = Shader.PropertyToID("_SealEnabled");
        public static readonly int SealEmissionStrength = Shader.PropertyToID("_SealEmissionStrength");
        public static bool SupportsSealEmission(Material material) => SupportsSeal(material) && material.HasProperty(SealEmissionStrength);
        public static readonly int SealColor = Shader.PropertyToID("_SealColor");
        public static readonly int SealMap = Shader.PropertyToID("_SealMap");
        public static readonly int SealNormalMap = Shader.PropertyToID("_SealNormalMap");
        public static readonly int SealNormalScale = Shader.PropertyToID("_SealNormalScale");
        public static readonly int SealRotation = Shader.PropertyToID("_SealRotation");
        public static readonly int SealRect = Shader.PropertyToID("_SealRect");
        public static bool SupportsSeal(Material material) => material != null &&
            (material.GetTag("CardSealContract", false) == "ColorOverlayV1" || material.GetTag("CardSealContract", false) == "ColorNormalV1") &&
            material.HasProperty(SealEnabled) && material.HasProperty(SealColor) &&
            material.HasProperty(SealMap) && material.HasProperty(SealRect);

        // Optional extension: older compatible card shaders can still be bound.
        public static bool SupportsFoil(Material material) => material != null &&
            material.GetTag("CardFoilContract", false) == "ColorStrengthV1" &&
            material.HasProperty(FoilEnabled) && material.HasProperty(FoilColor) && material.HasProperty(FoilStrength);

        // Shader names are not a compatibility contract: compatible custom shaders are allowed.
        public static List<string> GetContractErrors(Material material)
        {
            var errors = new List<string>();
            if (material == null || material.shader == null)
            {
                errors.Add("请选择卡牌 Shader。");
                return errors;
            }
            var shader = material.shader;
            CheckProperty(shader, "_BaseMap", ShaderPropertyType.Texture, errors);
            CheckProperty(shader, "_FrontUVRect", ShaderPropertyType.Vector, errors);
            CheckProperty(shader, "_BackUVRect", ShaderPropertyType.Vector, errors);
            CheckProperty(shader, "_BaseColor", ShaderPropertyType.Color, errors);
            CheckProperty(shader, "_ColorTint", ShaderPropertyType.Color, errors);
            CheckProperty(shader, "_EdgeColor", ShaderPropertyType.Color, errors);
            var textureIndex = shader.FindPropertyIndex("_BaseMap");
            if (textureIndex >= 0 && shader.GetPropertyType(textureIndex) == ShaderPropertyType.Texture &&
                shader.GetPropertyTextureDimension(textureIndex) != TextureDimension.Tex2D)
                errors.Add("_BaseMap 必须是 2D 纹理。");
            if (material.GetTag("RenderPipeline", false) != "UniversalPipeline")
                errors.Add("RenderPipeline 必须为 UniversalPipeline。");
            if (material.GetTag("CardUVRectContract", false) != UVRectContract)
                errors.Add("缺少 Tag：CardUVRectContract = " + UVRectContract + "（xy 偏移 / zw 缩放）。");
            if (material.GetTag("CardSurfaceContract", false) != SurfaceContract)
                errors.Add("缺少 Tag：CardSurfaceContract = " + SurfaceContract + "（UV1.x 背面 / UV1.y 侧边）。");
            return errors;
        }

        static void CheckProperty(Shader shader, string name, ShaderPropertyType type, List<string> errors)
        {
            var index = shader.FindPropertyIndex(name);
            if (index < 0) errors.Add("缺少属性 " + name + "（" + type + "）。");
            else if (shader.GetPropertyType(index) != type) errors.Add(name + " 必须为 " + type + "，当前为 " + shader.GetPropertyType(index) + "。");
        }

        // Configure once per skin, never once per card instance.
        public static void ConfigureSkin(Material material, CardAtlasData atlas)
        {
            if (material == null || atlas == null || atlas.texture == null)
                throw new ArgumentException("A material and an atlas texture are required.");
            var errors = GetContractErrors(material);
            if (errors.Count != 0) throw new ArgumentException(string.Join("\n", errors));
            if (!atlas.TryGetCard("card_back", out var back) || back.clipped)
                throw new ArgumentException("The atlas must contain an uncropped card_back.");
            material.SetTexture(BaseMap, atlas.texture);
            material.SetVector(BackUVRect, back.uv);
            material.enableInstancing = true;
        }
    }
}
