using System;
using UnityEngine;

namespace CardArt
{
    [ExecuteAlways, DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class CardView : MonoBehaviour
    {
        [SerializeField] Mesh sharedMesh;
        [SerializeField] Material sharedMaterial;
        [SerializeField] CardAtlasData atlas;
        [SerializeField, InspectorName("卡牌美术配置")] CardArtProfile artProfile;
        private CardArtProfile subscribedProfile;
        private string[] artworkCombinations;
        private Color artworkTint;
        public CardArtProfile ArtProfile => artProfile;
        public CardShaderAppearance Appearance { get; private set; }
        public void SetArtProfile(CardArtProfile profile)
        {
            if (profile != null && atlas != null) profile.ValidateAtlas(atlas);
            var resolved = profile != null && artworkCombinations != null ? profile.Resolve(artworkCombinations,artworkTint) : null;
            resolved?.ValidateFor(this);
            artProfile = profile; SubscribeProfile();
            if(profile==null) { artworkCombinations=null; Appearance=null; }
            else if(resolved!=null) PresentArtwork(artworkCombinations,artworkTint,resolved);
        }
        private void SubscribeProfile()
        {
            if (subscribedProfile != null) subscribedProfile.Changed -= RefreshArtwork;
            subscribedProfile = isActiveAndEnabled ? artProfile : null;
            if (subscribedProfile != null) subscribedProfile.Changed += RefreshArtwork;
        }
        public CardShaderAppearance ResolveArtwork(System.Collections.Generic.IEnumerable<string> combinations, Color tint)
        {
            if (artProfile == null) throw new ArgumentException("卡牌实例需要绑定参数组合配置。");
            artProfile.ValidateAtlas(atlas); return artProfile.Resolve(combinations,tint);
        }
        public void PresentArtwork(System.Collections.Generic.IEnumerable<string> combinations, Color tint, CardShaderAppearance prepared = null)
        {
            var ids = System.Linq.Enumerable.ToArray(combinations ?? Array.Empty<string>());
            var resolved = prepared ?? ResolveArtwork(ids,tint); resolved.ValidateFor(this);
            artworkCombinations = ids; artworkTint = tint; Appearance = resolved;
            // Existing shader-specific convenience controls remain available for material authoring.
            colorTint = resolved.Tint;
            foilEnabled = resolved.GetFloat("_FoilEnabled") != 0; foilColor = resolved.GetColor("_FoilColor",Color.white); foilStrength = resolved.GetFloat("_FoilStrength");
            sealEnabled = resolved.GetFloat("_SealEnabled") != 0; sealColor = resolved.GetColor("_SealColor",Color.white); sealEmissionStrength = resolved.GetFloat("_SealEmissionStrength");
            redCrossEnabled = resolved.GetFloat("_RedCrossEnabled") != 0; ApplyIfConfigured();
        }
        private void RefreshArtwork() { if (artworkCombinations != null) PresentArtwork(artworkCombinations,artworkTint); }

        [SerializeField] string cardId = "spades_a";
        [SerializeField] Color colorTint = Color.white;
        [Header("镭射（仅正面）")]
        [SerializeField, InspectorName("开启镭射")] bool foilEnabled;
        [SerializeField, InspectorName("镭射颜色"), ColorUsage(false, true)] Color foilColor = Color.white;
        [SerializeField, InspectorName("镭射强度"), Range(0, 5)] float foilStrength = 1;
        [Header("火漆（仅正面）")]
        [SerializeField, InspectorName("开启火漆")] bool sealEnabled;
        [SerializeField, InspectorName("火漆颜色"), ColorUsage(false)] Color sealColor = new Color(0.65f, 0.08f, 0.04f, 1);
        [SerializeField, InspectorName("火漆自发光强度"), Range(0, 1)] float sealEmissionStrength;
        [Header("红叉（仅正面）")]
        [SerializeField, InspectorName("显示红叉")] bool redCrossEnabled;
        MaterialPropertyBlock propertyBlock;
        CardSuitCornerRenderer suitCorners;

        public bool RedCrossEnabled => redCrossEnabled;
        public bool SupportsRedCross => CardShaderProperties.SupportsRedCross(sharedMaterial);
        public void SetRedCrossEnabled(bool value) { Appearance = null; artworkCombinations = null; redCrossEnabled = value; ApplyIfConfigured(); }

        public string CardId => cardId;
        public CardAtlasData Atlas => atlas;
        public Color Tint => colorTint;
        public bool FoilEnabled => foilEnabled;
        public Color FoilColor => foilColor;
        public float FoilStrength => foilStrength;
        public bool SupportsFoil => CardShaderProperties.SupportsFoil(sharedMaterial);

        public bool SealEnabled => sealEnabled;
        public Color SealColor => sealColor;
        public float SealEmissionStrength => sealEmissionStrength;
        public bool SupportsSealEmission => CardShaderProperties.SupportsSealEmission(sharedMaterial);
        /// <summary>Adds tinted seal emission without replacing normal lighting. Range 0–1.</summary>
        public void SetSealEmissionStrength(float value)
        { Appearance = null; artworkCombinations = null;
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
            sealEmissionStrength = Mathf.Clamp01(value);
            ApplyIfConfigured();
        }
        public bool SupportsSeal => CardShaderProperties.SupportsSeal(sharedMaterial);
        public void SetSealEnabled(bool value) { Appearance = null; artworkCombinations = null; sealEnabled = value; ApplyIfConfigured(); }
        public void SetSealColor(Color value) { Appearance = null; artworkCombinations = null; sealColor = value; ApplyIfConfigured(); }

        public void SetFoilEnabled(bool value) { Appearance = null; artworkCombinations = null; foilEnabled = value; ApplyIfConfigured(); }
        /// <summary>HDR RGB tint of the iridescent spectrum; values above 1 are preserved. Alpha is ignored.</summary>
        public void SetFoilColor(Color value) { Appearance = null; artworkCombinations = null; foilColor = value; ApplyIfConfigured(); }
        public void SetFoilStrength(float value)
        { Appearance = null; artworkCombinations = null;
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
            foilStrength = Mathf.Clamp(value, 0, 5);
            ApplyIfConfigured();
        }

        public void Bind(Mesh mesh, Material material, CardAtlasData skin, string id)
        {
            Validate(mesh, material, skin, id);
            if (artProfile != null) artProfile.ValidateAtlas(skin);
            sharedMesh = mesh;
            sharedMaterial = material;
            atlas = skin;
            cardId = id;
            Apply();
        }

        public void SetCard(string id)
        {
            Validate(sharedMesh, sharedMaterial, atlas, id);
            cardId = id;
            Apply();
        }

        /// <summary>Per-card multiplier. White restores the shared skin's colors; binding a face preserves this tint.</summary>
        public void SetTint(Color tint)
        { Appearance = null; artworkCombinations = null;
            colorTint = tint;
            ApplyIfConfigured();
        }

        static void Validate(Mesh mesh, Material material, CardAtlasData skin, string id)
        {
            if (mesh == null || material == null || skin == null || skin.texture == null)
                throw new ArgumentException("A card requires a shared mesh, material and atlas.");
            if (CardShaderProperties.GetContractErrors(material).Count != 0 ||
                material.GetTexture(CardShaderProperties.BaseMap) != skin.texture)
                throw new ArgumentException("Configure this material with the card atlas before binding.");
            if (!skin.TryGetCard(id, out var region) || region.clipped)
                throw new ArgumentException("Missing or cropped card: " + id);
        }

        void OnEnable() { SubscribeProfile(); RefreshArtwork(); ApplyIfConfigured(); }
        void OnDisable() { if (subscribedProfile != null) subscribedProfile.Changed -= RefreshArtwork; subscribedProfile = null;
            if (suitCorners != null) suitCorners.GetComponent<MeshRenderer>().enabled = false; }
        void OnDestroy() { if (subscribedProfile != null) subscribedProfile.Changed -= RefreshArtwork; }
#if UNITY_EDITOR
        bool refreshPending;

        // OnValidate can run while Unity is restoring objects (including Undo).
        // Assigning MeshFilter.sharedMesh there sends messages at an unsafe time.
        void OnValidate() => refreshPending = true;
        void Update()
        {
            if (!refreshPending) return;
            refreshPending = false;
            SubscribeProfile(); RefreshArtwork(); ApplyIfConfigured();
        }
#endif

        void ApplyIfConfigured()
        {
            if (sharedMesh == null || sharedMaterial == null || atlas == null ||
                !atlas.TryGetCard(cardId, out var region) || region.clipped) return;
            Apply();
        }

        void Apply()
        {
            if (!atlas.TryGetCard(cardId, out var region)) return;
            GetComponent<MeshFilter>().sharedMesh = sharedMesh;
            var renderer = GetComponent<MeshRenderer>();
            renderer.sharedMaterial = sharedMaterial;
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            propertyBlock.Clear();
            propertyBlock.SetVector(CardShaderProperties.FrontUVRect, region.uv);
            RefreshSuitCorners();
            if (Appearance != null)
            {
                Appearance.WriteTo(propertyBlock); renderer.SetPropertyBlock(propertyBlock); return;
            }
            propertyBlock.SetColor(CardShaderProperties.ColorTint, colorTint);
            if (SupportsFoil)
            {
                propertyBlock.SetFloat(CardShaderProperties.FoilEnabled, foilEnabled ? 1 : 0);
                propertyBlock.SetColor(CardShaderProperties.FoilColor, foilColor);
                propertyBlock.SetFloat(CardShaderProperties.FoilStrength, foilStrength);
            }
            if (SupportsSeal)
            {
                propertyBlock.SetFloat(CardShaderProperties.SealEnabled, sealEnabled && sharedMaterial.GetTexture(CardShaderProperties.SealMap) != null ? 1 : 0);
                propertyBlock.SetColor(CardShaderProperties.SealColor, sealColor);
                if (SupportsSealEmission)
                    propertyBlock.SetFloat(CardShaderProperties.SealEmissionStrength, sealEmissionStrength);
            }
            if (SupportsRedCross)
                propertyBlock.SetFloat(CardShaderProperties.RedCrossEnabled,
                    redCrossEnabled && sharedMaterial.GetTexture(CardShaderProperties.RedCrossMap) != null ? 1 : 0);
            renderer.SetPropertyBlock(propertyBlock);
        }

        void RefreshSuitCorners()
        {
            if (suitCorners == null) suitCorners = GetComponentInChildren<CardSuitCornerRenderer>(true);
            if (suitCorners == null && CardSuitCorners.TryGetRegion(atlas, cardId, out _))
            {
                var root = new GameObject("Readable suit corners") { hideFlags = HideFlags.DontSave, layer = gameObject.layer };
                root.transform.SetParent(transform, false);
                suitCorners = root.AddComponent<CardSuitCornerRenderer>();
            }
            if (suitCorners != null) suitCorners.Present(sharedMesh, atlas, cardId, colorTint, isActiveAndEnabled);
        }
    }
}
