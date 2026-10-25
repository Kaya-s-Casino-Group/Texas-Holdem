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
        [SerializeField] private CardArtProfile artProfile;
        private CardArtProfile subscribedProfile;
        public CardArtProfile ArtProfile => artProfile;
        public void SetArtProfile(CardArtProfile profile)
        {
            if (profile != null && atlas != null) profile.ValidateAtlas(atlas);
            artProfile = profile; SubscribeProfile(); RefreshArtwork();
        }
        private void SubscribeProfile()
        {
            if (subscribedProfile != null) subscribedProfile.Changed -= RefreshArtwork;
            subscribedProfile = isActiveAndEnabled ? artProfile : null;
            if (subscribedProfile != null) subscribedProfile.Changed += RefreshArtwork;
        }
        private void RefreshArtwork()
        {
            if (data == null) return;
            if (artProfile != null) artProfile.ValidateAtlas(atlas);
            var resolved = CardAppearanceResolver.Resolve(data, artProfile);
            ValidateCanvasParameters(resolved); appearance = resolved; RefreshSuitCorners(); SetVerticesDirty();
        }
        private CardVisualData data;
        private CardShaderAppearance appearance;
        private CardSuitCornerGraphic suitCorners;
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
            if (artProfile != null) artProfile.ValidateAtlas(skin);
            atlas = skin; material = sharedMaterial; EnsureChannels(); RefreshSuitCorners(); SetAllDirty();
        }
        public static void ValidateCard(CardAtlasData skin, CardVisualData card)
        {
            if (skin == null || skin.texture == null || card == null) throw new ArgumentNullException();
            if (!skin.TryGetCard(card.FaceArtId ?? "card_back", out var region) || region.clipped ||
                region.uv.z <= 0 || region.uv.w <= 0) throw new ArgumentException("Missing or cropped UI card artwork.");
        }
        public void Present(CardVisualData card, float presentationOpacity = 1, float presentationDesaturation = 0)
        {
            ValidateCard(atlas, card); ValidateUnit(presentationOpacity); ValidateUnit(presentationDesaturation);
            if (artProfile != null) artProfile.ValidateAtlas(atlas);
            var resolved = CardAppearanceResolver.Resolve(card, artProfile);
            ValidateCanvasParameters(resolved);
            data = card; appearance = resolved; opacity = presentationOpacity; desaturation = presentationDesaturation;
            RefreshSuitCorners();
            SetVerticesDirty();
        }
        private static void ValidateCanvasParameters(CardShaderAppearance resolved)
        {
            var supported = new System.Collections.Generic.HashSet<string> { "_ColorTint", "_FoilEnabled", "_FoilColor", "_FoilStrength", "_SealEnabled", "_SealColor", "_SealEmissionStrength", "_RedCrossEnabled" };
            foreach (var parameter in resolved.Parameters)
                {
                    if (!supported.Contains(parameter.Name)) throw new ArgumentException("当前 Canvas 卡牌适配器未定义参数通道：" + parameter.Name);
                    bool color = parameter.Name == "_ColorTint" || parameter.Name == "_FoilColor" || parameter.Name == "_SealColor";
                    if(parameter.Kind != (color ? CardShaderValueKind.Color : CardShaderValueKind.Float)) throw new ArgumentException("Canvas 参数通道类型不匹配：" + parameter.Name);
                }
        }
        public void SetPresentation(float presentationOpacity, float presentationDesaturation)
        {
            ValidateUnit(presentationOpacity); ValidateUnit(presentationDesaturation);
            if (opacity == presentationOpacity && desaturation == presentationDesaturation) return;
            opacity = presentationOpacity; desaturation = presentationDesaturation; RefreshSuitCorners(); SetVerticesDirty();
        }
        private static void ValidateUnit(float value)
        { if (!CardZoneRowLayout.Finite(value) || value < 0 || value > 1) throw new ArgumentOutOfRangeException(nameof(value)); }
        protected override void OnEnable() { base.OnEnable(); SubscribeProfile(); RefreshArtwork(); EnsureChannels(); }
        protected override void OnDisable()
        { if (subscribedProfile != null) subscribedProfile.Changed -= RefreshArtwork; subscribedProfile = null;
            if (suitCorners != null) suitCorners.enabled = false;
            base.OnDisable(); }
#if UNITY_EDITOR
        protected override void OnValidate() { base.OnValidate(); SubscribeProfile(); RefreshArtwork(); }
#endif
        protected override void OnCanvasHierarchyChanged() { base.OnCanvasHierarchyChanged(); EnsureChannels(); }
        private void EnsureChannels() { if (canvas != null) canvas.additionalShaderChannels |= RequiredChannels; }
        private void RefreshSuitCorners()
        {
            if (data == null || appearance == null) return;
            if (suitCorners == null) suitCorners = GetComponentInChildren<CardSuitCornerGraphic>(true);
            if (suitCorners == null && CardSuitCorners.TryGetRegion(atlas, data.FaceArtId, out _))
            {
                var root = new GameObject("Readable suit corners", typeof(RectTransform)) { hideFlags = HideFlags.DontSave, layer = gameObject.layer };
                var rect = (RectTransform)root.transform; rect.SetParent(transform, false);
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
                suitCorners = root.AddComponent<CardSuitCornerGraphic>();
            }
            if (suitCorners != null) suitCorners.Present(atlas, data.FaceArtId, appearance.Tint * color, opacity, desaturation, isActiveAndEnabled);
        }
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear(); if (data == null || appearance == null || atlas == null) return;
            if (!atlas.TryGetCard(data.FaceArtId ?? "card_back", out var region)) return;
            var rect = GetPixelAdjustedRect(); var uv = region.uv;
            float flags = (appearance.GetFloat("_FoilEnabled") != 0 ? 1 : 0) + (appearance.GetFloat("_SealEnabled") != 0 ? 2 : 0) + (appearance.GetFloat("_RedCrossEnabled") != 0 ? 4 : 0);
            var tint = appearance.Tint * color;
            float exposure = Mathf.Max(1, Mathf.Max(tint.r, Mathf.Max(tint.g, tint.b)));
            var vertexColor = new Color(tint.r / exposure, tint.g / exposure, tint.b / exposure, tint.a * opacity);
            var foilColor = appearance.GetColor("_FoilColor", Color.white);
            var foil = new Vector4(foilColor.r, foilColor.g, foilColor.b, appearance.GetFloat("_FoilStrength"));
            var sealColor = appearance.GetColor("_SealColor", Color.white);
            var seal = new Vector4(sealColor.r, sealColor.g, sealColor.b, appearance.GetFloat("_SealEmissionStrength"));
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
