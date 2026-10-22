using UnityEngine;
using UnityEngine.UI;

namespace Poker.SingleBattle
{
    /// <summary>A soft-edged dark ribbon with fine metallic rules; no stretched artwork.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleTurnBannerGraphic : MaskableGraphic
    {
        public Color Accent = new Color(.81f, .64f, .34f);
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); var r = rectTransform.rect;
            Band(mesh, r, new Color(.035f, .027f, .025f, .94f));
            Band(mesh, new Rect(r.xMin, r.yMax - 2, r.width, 1.5f), Accent);
            Band(mesh, new Rect(r.xMin, r.yMin + .5f, r.width, 1.5f), Accent);
            var muted = Accent; muted.a = .28f;
            Band(mesh, new Rect(r.xMin + 36, r.yMax - 9, r.width - 72, .7f), muted);
            Band(mesh, new Rect(r.xMin + 36, r.yMin + 8, r.width - 72, .7f), muted);
            Diamond(mesh, new Vector2(-r.width * .3f, 9), 6, Accent);
            Diamond(mesh, new Vector2(r.width * .3f, 9), 6, Accent);
        }
        private static void Band(VertexHelper mesh, Rect r, Color color)
        {
            float[] stops = { 0, .15f, .5f, .85f, 1 };
            float[] alpha = { 0, 1, 1, 1, 0 };
            for (int i = 0; i < stops.Length - 1; i++)
            {
                int index = mesh.currentVertCount;
                float left = Mathf.Lerp(r.xMin, r.xMax, stops[i]), right = Mathf.Lerp(r.xMin, r.xMax, stops[i + 1]);
                var a = color; a.a *= alpha[i]; var b = color; b.a *= alpha[i + 1];
                mesh.AddVert(new Vector3(left, r.yMin), a, Vector2.zero); mesh.AddVert(new Vector3(left, r.yMax), a, Vector2.zero);
                mesh.AddVert(new Vector3(right, r.yMax), b, Vector2.zero); mesh.AddVert(new Vector3(right, r.yMin), b, Vector2.zero);
                mesh.AddTriangle(index, index + 1, index + 2); mesh.AddTriangle(index, index + 2, index + 3);
            }
        }
        private static void Diamond(VertexHelper mesh, Vector2 center, float size, Color color)
        {
            int i = mesh.currentVertCount;
            mesh.AddVert(center + Vector2.left * size, color, Vector2.zero); mesh.AddVert(center + Vector2.up * size, color, Vector2.zero);
            mesh.AddVert(center + Vector2.right * size, color, Vector2.zero); mesh.AddVert(center + Vector2.down * size, color, Vector2.zero);
            mesh.AddTriangle(i, i + 1, i + 2); mesh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
