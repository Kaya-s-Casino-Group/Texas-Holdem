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

        public bool RedCrossEnabled => redCrossEnabled;
        public bool SupportsRedCross => CardShaderProperties.SupportsRedCross(sharedMaterial);
        public void SetRedCrossEnabled(bool value) { redCrossEnabled = value; ApplyIfConfigured(); }

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
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
            sealEmissionStrength = Mathf.Clamp01(value);
            ApplyIfConfigured();
        }
        public bool SupportsSeal => CardShaderProperties.SupportsSeal(sharedMaterial);
        public void SetSealEnabled(bool value) { sealEnabled = value; ApplyIfConfigured(); }
        public void SetSealColor(Color value) { sealColor = value; ApplyIfConfigured(); }

        public void SetFoilEnabled(bool value) { foilEnabled = value; ApplyIfConfigured(); }
        /// <summary>HDR RGB tint of the iridescent spectrum; values above 1 are preserved. Alpha is ignored.</summary>
        public void SetFoilColor(Color value) { foilColor = value; ApplyIfConfigured(); }
        public void SetFoilStrength(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
            foilStrength = Mathf.Clamp(value, 0, 5);
            ApplyIfConfigured();
        }

        public void Bind(Mesh mesh, Material material, CardAtlasData skin, string id)
        {
            Validate(mesh, material, skin, id);
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
        {
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

        void OnEnable() => ApplyIfConfigured();
#if UNITY_EDITOR
        bool refreshPending;

        // OnValidate can run while Unity is restoring objects (including Undo).
        // Assigning MeshFilter.sharedMesh there sends messages at an unsafe time.
        void OnValidate() => refreshPending = true;
        void Update()
        {
            if (!refreshPending) return;
            refreshPending = false;
            ApplyIfConfigured();
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
    }
}
