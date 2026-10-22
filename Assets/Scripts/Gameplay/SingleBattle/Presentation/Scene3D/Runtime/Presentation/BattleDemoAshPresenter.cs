using System.Collections;
using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>Scene-only replacement of the enemy body presenter. Death is a GPU dissolve with drifting alpha-masked ash.</summary>
    public sealed class BattleDemoAshPresenter : BattleActorPresenter
    {
        [SerializeField] private MeshRenderer artwork;
        [SerializeField] private Material ashMaterial;
        [SerializeField] private Material artworkMaterial;
        [SerializeField, Min(.1f), InspectorName("灰烬消散时长")] private float ashSeconds = .85f;
        private Material[] original;
        private MaterialPropertyBlock originalProperties, properties;
        private MeshRenderer fragments;
        private Mesh ashMesh;
        public float Progress { get; private set; }
        public MeshRenderer Artwork => artwork;
        public void ConfigureAsh(MeshRenderer surface, Material material)
        {
            artwork = surface; ashMaterial = material;
            if (surface != null && surface.sharedMaterial != material) artworkMaterial = surface.sharedMaterial;
        }
        public void ConfigureArtwork(Material material)
        {
            if (artwork == null || material == null || artworkMaterial == material) return;
            ReleaseSurface();
            artworkMaterial = material;
            artwork.sharedMaterial = material;
            artwork.SetPropertyBlock(null);
            if (isActiveAndEnabled) SetProgress(Progress);
        }
        private void EnsureSurface()
        {
            // An actor's OnDisable may reset us after our own OnDisable already released the surface.
            if (!Application.isPlaying || !isActiveAndEnabled || artwork == null || ashMaterial == null || original != null) return;
            original = artwork.sharedMaterials;
            // Summoned actors can clone an already prepared enemy. Keep a real body material for reuse.
            if (original.Length > 0 && original[0] == ashMaterial && artworkMaterial != null) original[0] = artworkMaterial;
            originalProperties = new MaterialPropertyBlock(); artwork.GetPropertyBlock(originalProperties);
            var texture = originalProperties.GetTexture("_BaseMap");
            if (texture == null && original[0].HasProperty("_BaseMap")) texture = original[0].GetTexture("_BaseMap");
            if (texture == null) texture = original[0].mainTexture;
            properties = new MaterialPropertyBlock();
            properties.SetTexture("_BaseMap", texture);
            properties.SetColor("_BaseColor", original[0].HasProperty("_BaseColor") ? original[0].GetColor("_BaseColor") : Color.white);
            artwork.sharedMaterial = ashMaterial;
            // Runtime fragment children are cloned with the visual template, but their mesh belongs to its presenter.
            for (int i = artwork.transform.childCount - 1; i >= 0; i--)
            {
                var inherited = artwork.transform.GetChild(i);
                if (inherited.name != "Enemy Ash Fragments") continue;
                inherited.gameObject.SetActive(false); Destroy(inherited.gameObject);
            }
            var go = new GameObject("Enemy Ash Fragments", typeof(MeshFilter), typeof(MeshRenderer));
            go.layer = artwork.gameObject.layer; go.transform.SetParent(artwork.transform, false);
            ashMesh = CreateAshMesh(artwork.GetComponent<MeshFilter>().sharedMesh.bounds);
            go.GetComponent<MeshFilter>().sharedMesh = ashMesh;
            fragments = go.GetComponent<MeshRenderer>(); fragments.sharedMaterial = ashMaterial;
            fragments.sortingLayerID = artwork.sortingLayerID; fragments.sortingOrder = artwork.sortingOrder;
            fragments.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fragments.receiveShadows = false;
        }
        private static Mesh CreateAshMesh(Bounds bounds)
        {
            const int count = 96;
            var vertices = new Vector3[count * count * 4]; var uv = new Vector2[vertices.Length];
            var corners = new Vector2[vertices.Length]; var indices = new int[count * count * 6];
            var offsets = new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) };
            for (int y = 0; y < count; y++)
                for (int x = 0; x < count; x++)
                {
                    int cell = y * count + x; var sample = new Vector2((x + .5f) / count, (y + .5f) / count);
                    for (int corner = 0; corner < 4; corner++)
                    {
                        int index = cell * 4 + corner;
                        vertices[index] = bounds.center + new Vector3((sample.x - .5f) * bounds.size.x, (sample.y - .5f) * bounds.size.y, 0);
                        uv[index] = sample; corners[index] = offsets[corner];
                    }
                    int t = cell * 6, v = cell * 4;
                    indices[t] = v; indices[t + 1] = v + 1; indices[t + 2] = v + 2;
                    indices[t + 3] = v; indices[t + 4] = v + 2; indices[t + 5] = v + 3;
                }
            return new Mesh { name = "BattleDemo Ash Grid", vertices = vertices, uv = uv, uv2 = corners, triangles = indices,
                bounds = new Bounds(bounds.center, bounds.size + new Vector3(1, 2, 1)) };
        }
        private void SetProgress(float value)
        {
            Progress = value; EnsureSurface();
            if (properties == null || artwork == null) return;
            if (fragments != null) fragments.sortingOrder = artwork.sortingOrder;
            properties.SetFloat("_AshProgress", value); properties.SetFloat("_AshMode", 0); artwork.SetPropertyBlock(properties);
            if (fragments == null) return;
            properties.SetFloat("_AshMode", 1); fragments.SetPropertyBlock(properties);
            fragments.enabled = value > 0 && value < 1;
        }
        protected override IEnumerator PlayDeath()
        {
            RestorePose();
            for (float elapsed = 0; elapsed < ashSeconds; elapsed += Time.unscaledDeltaTime)
            { SetProgress(Mathf.Clamp01(elapsed / ashSeconds)); yield return null; }
            SnapDeath();
        }
        protected override void SnapDeath() { RestorePose(); SetProgress(1); }
        public override void ResetPresentation() { base.ResetPresentation(); SetProgress(0); }
        private void OnEnable() => SetProgress(Progress);
        protected override void OnDisable()
        {
            base.OnDisable();
            ReleaseSurface();
        }
        private void ReleaseSurface()
        {
            if (artwork != null && original != null) { artwork.sharedMaterials = original; artwork.SetPropertyBlock(originalProperties); }
            original = null; originalProperties = null; properties = null;
            if (fragments != null) { fragments.gameObject.SetActive(false); Destroy(fragments.gameObject); }
            if (ashMesh != null) Destroy(ashMesh);
            fragments = null; ashMesh = null;
        }
    }
}
