using UnityEngine;
using UnityEngine.UI;

namespace CardPresentation
{
    /// <summary>Native Canvas panel with stepped corners. No generated texture or per-panel material.</summary>
    [RequireComponent(typeof(CanvasRenderer)), ExecuteAlways]
    public sealed class PixelPanelGraphic : MaskableGraphic
    {
        [SerializeField, Min(0)] private float corner = 6;
        [SerializeField, Min(0)] private float border = 2;
        [SerializeField] private Color borderColor = new Color(.7f,.78f,.73f,1);
        public void Configure(Color fill, Color outline, float thickness = 2, float cornerSize = 6)
        { color = fill; borderColor = outline; border = thickness; corner = cornerSize; raycastTarget = false; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); var rect = GetPixelAdjustedRect();
            if (rect.width <= 0 || rect.height <= 0) return;
            var outer = Points(rect,Mathf.Min(corner,Mathf.Min(rect.width,rect.height)*.25f));
            float width = Mathf.Clamp(border,0,Mathf.Min(rect.width,rect.height)*.25f);
            var innerRect = new Rect(rect.xMin+width,rect.yMin+width,rect.width-2*width,rect.height-2*width);
            var inner = Points(innerRect,Mathf.Max(0,Mathf.Min(corner-width,Mathf.Min(innerRect.width,innerRect.height)*.25f)));
            Color outline = borderColor; outline.a *= color.a;
            for (int i = 0; i < outer.Length; i++) mesh.AddVert(outer[i],outline,Vector2.zero);
            for (int i = 0; i < inner.Length; i++) mesh.AddVert(inner[i],color,Vector2.zero);
            mesh.AddVert(rect.center,color,Vector2.zero);
            for (int i = 0; i < outer.Length; i++)
            {
                int next = (i+1)%outer.Length;
                mesh.AddTriangle(i,next,outer.Length+next); mesh.AddTriangle(i,outer.Length+next,outer.Length+i);
                mesh.AddTriangle(outer.Length*2,outer.Length+i,outer.Length+next);
            }
        }
        private static Vector2[] Points(Rect r, float c) => new[] {
            new Vector2(r.xMin+c,r.yMin),new Vector2(r.xMax-c,r.yMin),
            new Vector2(r.xMax-c,r.yMin+c*.5f),new Vector2(r.xMax-c*.5f,r.yMin+c*.5f),
            new Vector2(r.xMax-c*.5f,r.yMin+c),new Vector2(r.xMax,r.yMin+c),
            new Vector2(r.xMax,r.yMax-c),new Vector2(r.xMax-c*.5f,r.yMax-c),
            new Vector2(r.xMax-c*.5f,r.yMax-c*.5f),new Vector2(r.xMax-c,r.yMax-c*.5f),
            new Vector2(r.xMax-c,r.yMax),new Vector2(r.xMin+c,r.yMax),
            new Vector2(r.xMin+c,r.yMax-c*.5f),new Vector2(r.xMin+c*.5f,r.yMax-c*.5f),
            new Vector2(r.xMin+c*.5f,r.yMax-c),new Vector2(r.xMin,r.yMax-c),
            new Vector2(r.xMin,r.yMin+c),new Vector2(r.xMin+c*.5f,r.yMin+c),
            new Vector2(r.xMin+c*.5f,r.yMin+c*.5f),new Vector2(r.xMin+c,r.yMin+c*.5f)
        };
    }
}
