using UnityEngine;
using UnityEngine.UI;

namespace Poker.SingleBattle
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleActionPointBacking : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); var rect = GetPixelAdjustedRect();
            float radius = Mathf.Min(rect.width, rect.height) * .5f;
            for (int i = 0; i < 96; i++)
            {
                float angle = i * Mathf.PI * 2 / 96;
                var vertex = UIVertex.simpleVert;
                vertex.position = rect.center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                vertex.color = color; mesh.AddVert(vertex);
                if (i > 1) mesh.AddTriangle(0, i - 1, i);
            }
        }
    }
}
