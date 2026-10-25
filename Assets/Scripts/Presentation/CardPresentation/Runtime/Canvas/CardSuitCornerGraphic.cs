using CardArt;
using UnityEngine;
using UnityEngine.UI;

namespace CardPresentation
{
    [AddComponentMenu(""), RequireComponent(typeof(CanvasRenderer))]
    public sealed class CardSuitCornerGraphic : MaskableGraphic
    {
        private Texture2D leasedTexture;
        private CardAtlasRegion region;
        private float desaturation;
        public override Texture mainTexture => leasedTexture != null ? leasedTexture : Texture2D.whiteTexture;

        public void Present(CardAtlasData atlas, string face, Color tint, float opacity, float fade, bool visible)
        {
            if (!visible || !CardSuitCorners.TryGetRegion(atlas, face, out region)) { enabled = false; return; }
            if (leasedTexture != atlas.texture)
            {
                if (leasedTexture != null) CardSuitCorners.ReleaseMaterial(leasedTexture, true);
                leasedTexture = atlas.texture;
                material = CardSuitCorners.AcquireMaterial(leasedTexture, true);
            }
            tint.a *= opacity; color = tint; desaturation = fade;
            raycastTarget = false; enabled = true; SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (region == null) return;
            var surface = rectTransform.rect;
            for (int corner = 0; corner < 2; corner++)
            {
                var rect = corner == 0 ? CardSuitCorners.Upper : CardSuitCorners.Lower;
                Write(0, 0); Write(0, 1); Write(1, 1); Write(1, 0);
                int start = corner * 4;
                helper.AddTriangle(start, start + 1, start + 2); helper.AddTriangle(start + 2, start + 3, start);
                void Write(float x, float y)
                {
                    var vertex = UIVertex.simpleVert;
                    vertex.position = new Vector3(surface.xMin + (rect.x + rect.width * x) * surface.width,
                        surface.yMin + (rect.y + rect.height * y) * surface.height);
                    vertex.uv0 = CardSuitCorners.SampleUV(region, x, y, corner == 1);
                    vertex.uv1 = new Vector4(desaturation, 0, 0, 0); vertex.color = color;
                    helper.AddVert(vertex);
                }
            }
        }

        protected override void OnDestroy()
        {
            if (leasedTexture != null) CardSuitCorners.ReleaseMaterial(leasedTexture, true);
            leasedTexture = null;
            base.OnDestroy();
        }
    }
}
