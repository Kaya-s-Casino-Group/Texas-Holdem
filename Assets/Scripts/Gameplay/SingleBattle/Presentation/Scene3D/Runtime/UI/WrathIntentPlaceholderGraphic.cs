using Poker.BattlePresentation;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.SingleBattle
{
    /// <summary>Temporary vector artwork. Assign HUD sprites to replace it without changing intent logic.</summary>
    public sealed class WrathIntentPlaceholderGraphic : MaskableGraphic
    {
        private WrathIntentIcon kind;
        public void Show(WrathIntentIcon value) { if (kind == value) return; kind = value; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (kind == WrathIntentIcon.Attack)
            {
                Polygon(mesh, new Vector2(.22f, .15f), new Vector2(.34f, .08f), new Vector2(.9f, .8f), new Vector2(.88f, .98f), new Vector2(.71f, .9f));
                Polygon(mesh, new Vector2(.12f, .28f), new Vector2(.2f, .4f), new Vector2(.51f, .17f), new Vector2(.42f, .06f));
            }
            else if (kind == WrathIntentIcon.Defense)
                Polygon(mesh, new Vector2(.5f, .04f), new Vector2(.12f, .35f), new Vector2(.1f, .88f),
                    new Vector2(.5f, .98f), new Vector2(.9f, .88f), new Vector2(.88f, .35f));
            else if (kind == WrathIntentIcon.Charge)
            {
                Polygon(mesh, new Vector2(.33f, .3f), new Vector2(.67f, .3f), new Vector2(.67f, .62f), new Vector2(.33f, .62f));
                Polygon(mesh, new Vector2(.07f, .62f), new Vector2(.93f, .62f), new Vector2(.5f, .98f));
                Polygon(mesh, new Vector2(.25f, .08f), new Vector2(.75f, .08f), new Vector2(.75f, .2f), new Vector2(.25f, .2f));
            }
            else Polygon(mesh, new Vector2(.4f, .1f), new Vector2(.6f, .1f), new Vector2(.6f, .9f), new Vector2(.4f, .9f));
        }
        private void Polygon(VertexHelper mesh, params Vector2[] points)
        {
            int start = mesh.currentVertCount; var rect = GetPixelAdjustedRect();
            foreach (var point in points) mesh.AddVert(new Vector3(rect.xMin + point.x * rect.width,
                rect.yMin + point.y * rect.height), color, Vector2.zero);
            for (int i = 1; i < points.Length - 1; i++) mesh.AddTriangle(start, start + i, start + i + 1);
        }
    }
}
