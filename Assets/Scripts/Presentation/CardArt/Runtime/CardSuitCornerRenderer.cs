using UnityEngine;
using UnityEngine.Rendering;

namespace CardArt
{
    [ExecuteAlways, AddComponentMenu(""), RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class CardSuitCornerRenderer : MonoBehaviour
    {
        private Mesh mesh;
        private Texture2D leasedTexture;
        private Mesh source;
        private CardView owner;
        private MeshRenderer surfaceRenderer, cornerRenderer;
        private bool contentVisible;
        private Vector3 origin, right, up, normal;
        private readonly Vector3[] vertices = new Vector3[8];
        private readonly Vector2[] uv = new Vector2[8];
        private readonly Color[] colors = new Color[8];

        public void Present(Mesh surface, CardAtlasData atlas, string face, Color tint, bool visible)
        {
            var renderer = cornerRenderer = GetComponent<MeshRenderer>();
            if (owner == null) owner = GetComponentInParent<CardView>();
            if (surfaceRenderer == null && owner != null) surfaceRenderer = owner.GetComponent<MeshRenderer>();
            if (!visible || !CardSuitCorners.TryGetRegion(atlas, face, out var region) || !SetFrame(surface))
            { contentVisible = false; renderer.enabled = false; return; }
            contentVisible = true;
            if (leasedTexture != atlas.texture)
            {
                if (leasedTexture != null) CardSuitCorners.ReleaseMaterial(leasedTexture, false);
                leasedTexture = atlas.texture;
                renderer.sharedMaterial = CardSuitCorners.AcquireMaterial(leasedTexture, false);
            }
            if (mesh == null)
            {
                mesh = new Mesh { name = "Readable suit corners", hideFlags = HideFlags.HideAndDontSave };
                mesh.MarkDynamic();
                GetComponent<MeshFilter>().sharedMesh = mesh;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            for (int corner = 0; corner < 2; corner++)
            {
                var rect = corner == 0 ? CardSuitCorners.Upper : CardSuitCorners.Lower;
                Write(0, 0, 0); Write(1, 0, 1); Write(2, 1, 1); Write(3, 1, 0);
                void Write(int index, float x, float y)
                {
                    index += corner * 4;
                    vertices[index] = origin + right * (rect.x + rect.width * x) + up * (rect.y + rect.height * y) + normal * .00002f;
                    uv[index] = CardSuitCorners.SampleUV(region, x, y, corner == 1);
                    colors[index] = tint;
                }
            }
            mesh.vertices = vertices; mesh.uv = uv; mesh.colors = colors;
            mesh.triangles = new[] { 0, 1, 2, 2, 3, 0, 4, 5, 6, 6, 7, 4 };
            mesh.RecalculateBounds();
            SynchronizeVisibility();
        }

        // Deal delays and projectile presentation temporarily hide only the
        // original renderer. Keep the corner layer under the same visibility.
        private void LateUpdate() => SynchronizeVisibility();
        private void SynchronizeVisibility()
        {
            if (cornerRenderer == null) return;
            bool visible = contentVisible && owner != null && owner.isActiveAndEnabled && surfaceRenderer != null && surfaceRenderer.enabled;
            if (cornerRenderer.enabled != visible) cornerRenderer.enabled = visible;
            bool forcedOff = surfaceRenderer != null && surfaceRenderer.forceRenderingOff;
            if (cornerRenderer.forceRenderingOff != forcedOff) cornerRenderer.forceRenderingOff = forcedOff;
        }

        private bool SetFrame(Mesh surface)
        {
            if (source == surface && source != null) return true;
            if (surface == null || !surface.isReadable) return false;
            var points = surface.vertices; var coordinates = surface.uv; var flags = surface.uv2;
            if (coordinates.Length != points.Length || flags.Length != points.Length) return false;
            int first = -1, second = -1, third = -1;
            for (int index = 0; index < points.Length; index++)
            {
                if (flags[index] != Vector2.zero) continue;
                if (first < 0) { first = index; continue; }
                if (second < 0)
                { if (Mathf.Abs(coordinates[index].x - coordinates[first].x) > .25f) second = index; continue; }
                var a = coordinates[second] - coordinates[first]; var b = coordinates[index] - coordinates[first];
                if (Mathf.Abs(a.x * b.y - a.y * b.x) > .1f) { third = index; break; }
            }
            if (third < 0) return false;
            var axisA = coordinates[second] - coordinates[first]; var axisB = coordinates[third] - coordinates[first];
            var deltaA = points[second] - points[first]; var deltaB = points[third] - points[first];
            float determinant = axisA.x * axisB.y - axisA.y * axisB.x;
            right = (deltaA * axisB.y - deltaB * axisA.y) / determinant;
            up = (deltaB * axisA.x - deltaA * axisB.x) / determinant;
            origin = points[first] - right * coordinates[first].x - up * coordinates[first].y;
            normal = surface.normals[first]; source = surface;
            return true;
        }

        private void OnDestroy()
        {
            if (leasedTexture != null) CardSuitCorners.ReleaseMaterial(leasedTexture, false);
            CardSuitCorners.DestroyOwned(mesh);
        }
    }
}
