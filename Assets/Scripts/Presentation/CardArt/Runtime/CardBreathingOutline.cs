using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace CardArt
{
    /// <summary>Independent exterior quad. Never writes the card material, selection, colliders or art property block.
    /// Bind to the actual moving card surface; supply its local face rectangle (XY with +Z normal).</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class CardBreathingOutline : MonoBehaviour
    {
        Mesh mesh;
        GameObject visual;
        MeshRenderer outlineRenderer;
        MaterialPropertyBlock block;
        static readonly int ColorId = Shader.PropertyToID("_Color"), SizeId = Shader.PropertyToID("_HalfSize"),
            WidthId = Shader.PropertyToID("_Width"), RadiusId = Shader.PropertyToID("_Radius"),
            SoftnessId = Shader.PropertyToID("_Softness"), PulseId = Shader.PropertyToID("_Pulse");
        public Renderer OutlineRenderer => outlineRenderer;

        public void Bind(Transform surface, Material sharedMaterial, Vector2 size, Vector3 localCenter, Quaternion localRotation,
            float width = 0.025f, float softness = 0.04f, float radius = 0.04f, float normalOffset = 0.002f)
        {
            if (surface == null || sharedMaterial == null || sharedMaterial.shader.name != "CardArt/BreathingOutline" ||
                !Finite(size.x) || !Finite(size.y) || size.x <= 0 || size.y <= 0 || !Finite(width) || width <= 0 ||
                !Finite(softness) || softness <= 0 || !Finite(radius) || radius < 0 || !Finite(normalOffset))
                throw new ArgumentException("Outline requires a face rectangle and a CardArt/BreathingOutline material.");
            Release();
            float padding = width + softness * 2;
            Vector2 extent = size * .5f + Vector2.one * padding;
            mesh = new Mesh { name = "Card guide outline", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = new[] { new Vector3(-extent.x,-extent.y), new Vector3(extent.x,-extent.y), new Vector3(extent.x,extent.y), new Vector3(-extent.x,extent.y) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 }; mesh.RecalculateBounds();
            visual = new GameObject("Guide outline", typeof(MeshFilter), typeof(MeshRenderer)) { hideFlags = HideFlags.HideAndDontSave, layer = surface.gameObject.layer };
            visual.transform.SetParent(surface, false); visual.transform.localPosition = localCenter + localRotation * Vector3.forward * normalOffset;
            visual.transform.localRotation = localRotation;
            visual.GetComponent<MeshFilter>().sharedMesh = mesh;
            outlineRenderer = visual.GetComponent<MeshRenderer>(); outlineRenderer.sharedMaterial = sharedMaterial;
            outlineRenderer.shadowCastingMode = ShadowCastingMode.Off; outlineRenderer.receiveShadows = false;
            outlineRenderer.lightProbeUsage = LightProbeUsage.Off; outlineRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            if (block == null) block = new MaterialPropertyBlock();
            block.Clear(); block.SetVector(SizeId, new Vector4(size.x * .5f, size.y * .5f, padding, 0));
            block.SetFloat(WidthId, width); block.SetFloat(SoftnessId, softness); block.SetFloat(RadiusId, radius);
            SetPulse(Color.white, 0);
        }
        public void SetPulse(Color color, float pulse)
        {
            if (!Finite(pulse)) throw new ArgumentOutOfRangeException(nameof(pulse));
            if (outlineRenderer == null) return;
            block.SetColor(ColorId, color); block.SetFloat(PulseId, Mathf.Clamp01(pulse)); outlineRenderer.SetPropertyBlock(block);
            outlineRenderer.enabled = isActiveAndEnabled && pulse > 0;
        }
        public void Clear() => SetPulse(Color.white, 0);
        // Editor domain reload and pooled-card deactivation must not orphan the owned mesh/child.
        // Bind again when re-enabling; closing a guide without deactivation can simply Clear().
        void OnDisable() => Release();
        void OnDestroy() => Release();
        void Release()
        {
            if (visual != null) { visual.SetActive(false); DestroyOwned(visual); }
            if (mesh != null) DestroyOwned(mesh);
            visual = null; outlineRenderer = null; mesh = null;
        }
        static void DestroyOwned(UnityEngine.Object value) { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>A shared, caller-driven unscaled clock. Use ActiveIndex to select the matching formula/card sample.</summary>
    public sealed class CardOutlineCycle : IDisposable
    {
        readonly CardBreathingOutline[][] groups;
        readonly Color[] colors;
        public int ActiveIndex { get; private set; } = -1;
        public float Pulse { get; private set; }
        public CardOutlineCycle(CardBreathingOutline[][] groups, Color[] colors)
        {
            if (groups == null || colors == null || groups.Length != colors.Length) throw new ArgumentException("One color per combination is required.");
            this.groups = new CardBreathingOutline[groups.Length][]; this.colors = (Color[])colors.Clone();
            for (int i = 0; i < groups.Length; i++) this.groups[i] = (CardBreathingOutline[])(groups[i] ?? throw new ArgumentException("Null group.")).Clone();
        }
        public void Tick(double elapsedSeconds, double secondsPerGroup = 2)
        {
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0 ||
                double.IsNaN(secondsPerGroup) || double.IsInfinity(secondsPerGroup) || secondsPerGroup <= 0) throw new ArgumentOutOfRangeException();
            Clear(); if (groups.Length == 0) return;
            double turn = elapsedSeconds / secondsPerGroup;
            ActiveIndex = (int)(Math.Floor(turn) % groups.Length);
            Pulse = (float)Math.Pow(Math.Sin(Math.PI * (turn - Math.Floor(turn))), 2);
            foreach (var outline in groups[ActiveIndex]) if (outline != null) outline.SetPulse(colors[ActiveIndex], Pulse);
        }
        public void Clear()
        {
            foreach (var group in groups) foreach (var outline in group) if (outline != null) outline.Clear();
            ActiveIndex = -1; Pulse = 0;
        }
        public void Dispose() => Clear();
    }
}
