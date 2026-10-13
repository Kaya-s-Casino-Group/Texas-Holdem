using System;
using CardArt;
using UnityEngine;
using UnityEngine.UI;

namespace CardPresentation
{
    /// <summary>Native Canvas atlas quad. All per-card appearance travels in vertices, never cloned materials.</summary>
    [AddComponentMenu("UI/Card Atlas Graphic")]
    [RequireComponent(typeof(CanvasRenderer)), ExecuteAlways]
    public sealed class CardUIGraphic : MaskableGraphic
    {
        [SerializeField] private CardAtlasData atlas;
        private CardVisualData data;
        private CardShaderAppearance appearance;
        private float opacity = 1, desaturation;
        public CardAtlasData Atlas => atlas;
        public CardVisualData Data => data;
        public float Opacity => opacity;
        public float Desaturation => desaturation;
        public override Texture mainTexture => atlas != null && atlas.texture != null ? atlas.texture : Texture2D.whiteTexture;
        public static readonly AdditionalCanvasShaderChannels RequiredChannels = AdditionalCanvasShaderChannels.TexCoord1 |
            AdditionalCanvasShaderChannels.TexCoord2 | AdditionalCanvasShaderChannels.TexCoord3 | AdditionalCanvasShaderChannels.Normal;

        public void Bind(CardAtlasData skin, Material sharedMaterial)
        {
            if (skin == null || skin.texture == null || sharedMaterial == null || sharedMaterial.shader == null ||
                sharedMaterial.shader.name != "CardPresentation/Card Canvas") throw new ArgumentException("Bind a card atlas and shared Canvas card material.");
            if (!skin.TryGetCard("card_back", out var back) || back.clipped) throw new ArgumentException("The atlas needs an unclipped card_back region.");
            if (data != null) ValidateCard(skin, data);
            atlas = skin; material = sharedMaterial; EnsureChannels(); SetAllDirty();
        }
        public static void ValidateCard(CardAtlasData skin, CardVisualData card)
        {
            if (skin == null || skin.texture == null || card == null) throw new ArgumentNullException();
            if (!skin.TryGetCard(card.FaceArtId ?? "card_back", out var region) || region.clipped ||
                region.uv.z <= 0 || region.uv.w <= 0) throw new ArgumentException("Missing or cropped UI card artwork.");
        }
        public void Present(CardVisualData card, CardAppearanceSettings settings, float presentationOpacity = 1, float presentationDesaturation = 0)
        {
            ValidateCard(atlas, card); ValidateUnit(presentationOpacity); ValidateUnit(presentationDesaturation);
            var resolved = CardAppearanceResolver.Resolve(card, settings);
            data = card; appearance = resolved; opacity = presentationOpacity; desaturation = presentationDesaturation;
            SetVerticesDirty();
        }
        public void SetPresentation(float presentationOpacity, float presentationDesaturation)
        {
            ValidateUnit(presentationOpacity); ValidateUnit(presentationDesaturation);
            if (opacity == presentationOpacity && desaturation == presentationDesaturation) return;
            opacity = presentationOpacity; desaturation = presentationDesaturation; SetVerticesDirty();
        }
        private static void ValidateUnit(float value)
        { if (!CardZoneRowLayout.Finite(value) || value < 0 || value > 1) throw new ArgumentOutOfRangeException(nameof(value)); }
        protected override void OnEnable() { base.OnEnable(); EnsureChannels(); }
        protected override void OnCanvasHierarchyChanged() { base.OnCanvasHierarchyChanged(); EnsureChannels(); }
        private void EnsureChannels() { if (canvas != null) canvas.additionalShaderChannels |= RequiredChannels; }
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear(); if (data == null || appearance == null || atlas == null) return;
            if (!atlas.TryGetCard(data.FaceArtId ?? "card_back", out var region)) return;
            var rect = GetPixelAdjustedRect(); var uv = region.uv;
            float flags = (appearance.FoilEnabled ? 1 : 0) + (appearance.SealEnabled ? 2 : 0) + (appearance.RedCrossEnabled ? 4 : 0);
            var tint = appearance.Tint * color;
            float exposure = Mathf.Max(1, Mathf.Max(tint.r, Mathf.Max(tint.g, tint.b)));
            var vertexColor = new Color(tint.r / exposure, tint.g / exposure, tint.b / exposure, tint.a * opacity);
            var foil = new Vector4(appearance.FoilColor.r, appearance.FoilColor.g, appearance.FoilColor.b, appearance.FoilStrength);
            var seal = new Vector4(appearance.SealColor.r, appearance.SealColor.g, appearance.SealColor.b, appearance.SealEmissionStrength);
            // Pack the three bits and [0,1] desaturation into separate ranges of one float.
            // Opacity uses native vertex alpha, leaving UV channels for immutable atlas dimensions.
            var state = new Vector4(flags * 2 + desaturation, uv.z, uv.w, exposure);
            Add(0, 0); Add(0, 1); Add(1, 1); Add(1, 0);
            helper.AddTriangle(0, 1, 2); helper.AddTriangle(2, 3, 0);
            void Add(float x, float y)
            {
                var vertex = UIVertex.simpleVert;
                vertex.position = new Vector3(Mathf.Lerp(rect.xMin, rect.xMax, x), Mathf.Lerp(rect.yMin, rect.yMax, y));
                // Carry the entire region; clamp only its boundary samples in the fragment shader, as scene cards do.
                // Canvas transforms normals while batching; atlas coordinates must stay in UV channels.
                vertex.color = vertexColor; vertex.normal = Vector3.back;
                vertex.uv0 = new Vector4(uv.x,uv.y,x,y);
                vertex.uv1 = foil; vertex.uv2 = seal; vertex.uv3 = state; helper.AddVert(vertex);
            }
        }
    }
}
