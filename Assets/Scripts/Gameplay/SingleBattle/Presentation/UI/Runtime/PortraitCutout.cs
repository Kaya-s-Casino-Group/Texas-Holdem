using UnityEngine;
using UnityEngine.UI;

namespace Poker.BattlePresentation
{
    // Convex opening underneath the illustrated frame; Mask uses this geometry for its stencil.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PortraitCutout : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var r = rectTransform.rect;
            var points = new[] { new Vector2(.13f,1), new Vector2(.79f,1), new Vector2(1,.78f),
                new Vector2(1,.03f), new Vector2(.26f,0), new Vector2(0,.22f), new Vector2(0,.87f) };
            foreach (var p in points) mesh.AddVert(new Vector3(r.x + p.x*r.width, r.y + p.y*r.height), color, p);
            for (int i=1; i<points.Length-1; i++) mesh.AddTriangle(0,i,i+1);
        }
    }
}
