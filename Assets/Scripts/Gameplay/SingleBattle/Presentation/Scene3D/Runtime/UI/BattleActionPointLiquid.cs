using UnityEngine;
using UnityEngine.UI;

namespace Poker.SingleBattle
{
    /// <summary>A solid reservoir with a horizontal liquid level, measured against three actions.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleActionPointLiquid : MaskableGraphic
    {
        [SerializeField, Range(0, 1)] private float fillAmount = 1;
        [SerializeField] private Texture2D liquidTexture;
        public float FillAmount => fillAmount;
        public override Texture mainTexture => liquidTexture != null ? liquidTexture : Texture2D.whiteTexture;
        public void SetTexture(Texture2D texture)
        {
            if (liquidTexture == texture) return;
            liquidTexture = texture; SetMaterialDirty(); SetVerticesDirty();
        }

        public void SetActions(long current)
        {
            float next = Mathf.Clamp01(current / 3f);
            if (Mathf.Approximately(fillAmount, next)) return;
            fillAmount = next; SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = GetPixelAdjustedRect();
            float radius = Mathf.Min(rect.width, rect.height) * .5f;
            if (fillAmount > 0) Segment(mesh, rect.center, radius, fillAmount);
        }

        void Segment(VertexHelper mesh, Vector2 center, float radius, float fraction)
        {
            // Follow the lower circular arc and close it with the horizontal water surface.
            float cut = Mathf.Asin(Mathf.Clamp(fraction * 2 - 1, -1, 1));
            float start = Mathf.PI - cut, end = 2 * Mathf.PI + cut;
            const int steps = 96;
            int first = mesh.currentVertCount;
            for (int i = 0; i <= steps; i++)
            {
                float angle = Mathf.Lerp(start, end, i / (float)steps);
                var point = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var vertex = UIVertex.simpleVert;
                vertex.position = center + point * radius;
                vertex.uv0 = (point + Vector2.one) * .5f;
                vertex.color = (liquidTexture != null ? Color.white : (Color)new Color32(110, 8, 17, 255)) * color;
                mesh.AddVert(vertex);
                if (i > 1) mesh.AddTriangle(first, first + i - 1, first + i);
            }
        }
    }
}
