using UnityEngine;
using UnityEngine.UI;

namespace Poker.SingleBattle
{
    /// <summary>Native UI image with uniform contain/cover. Source pixels are never stretched independently.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RitualPicture : MaskableGraphic
    {
        [SerializeField] private Texture source;
        [SerializeField] private Rect sourceUV = new Rect(0, 0, 1, 1);
        [SerializeField] private bool cover;
        public override Texture mainTexture => source != null ? source : Texture2D.whiteTexture;
        public void Configure(Texture texture, bool fill = false, Rect? uv = null)
        { source = texture; cover = fill; sourceUV = uv ?? new Rect(0, 0, 1, 1); raycastTarget = false; SetAllDirty(); }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); if (source == null) return;
            var rect = GetPixelAdjustedRect(); var uv = sourceUV;
            if (rect.width <= 0 || rect.height <= 0) return;
            float aspect = source.width * uv.width / (source.height * uv.height);
            float target = rect.width / rect.height;
            if (cover)
            {
                if (target > aspect) { float h = uv.height * aspect / target; uv.y += (uv.height - h) / 2; uv.height = h; }
                else { float w = uv.width * target / aspect; uv.x += (uv.width - w) / 2; uv.width = w; }
            }
            else if (target > aspect) { float w = rect.height * aspect; rect.x += (rect.width - w) / 2; rect.width = w; }
            else { float h = rect.width / aspect; rect.y += (rect.height - h) / 2; rect.height = h; }
            mesh.AddVert(new Vector3(rect.xMin, rect.yMin), color, new Vector2(uv.xMin, uv.yMin));
            mesh.AddVert(new Vector3(rect.xMin, rect.yMax), color, new Vector2(uv.xMin, uv.yMax));
            mesh.AddVert(new Vector3(rect.xMax, rect.yMax), color, new Vector2(uv.xMax, uv.yMax));
            mesh.AddVert(new Vector3(rect.xMax, rect.yMin), color, new Vector2(uv.xMax, uv.yMin));
            mesh.AddTriangle(0, 1, 2); mesh.AddTriangle(2, 3, 0);
        }
    }
}
