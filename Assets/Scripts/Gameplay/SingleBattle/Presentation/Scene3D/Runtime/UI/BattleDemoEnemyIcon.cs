using UnityEngine;
using UnityEngine.UI;

namespace Poker.SingleBattle
{
    public enum BattleDemoEnemySymbol { Sword, Shield, Poison, Stun, Immunity, Reflect, LifeSteal, Overheal, Armor, Heal, Buff, Debuff, Cards, Cleanse, Unknown, Wait }

    /// <summary>Scene-local vector symbols, drawn directly by uGUI so world-space icons remain sharp.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleDemoEnemyIcon : MaskableGraphic
    {
        public BattleDemoEnemySymbol Symbol { get; private set; }
        public void Set(BattleDemoEnemySymbol symbol, Color tint)
        { if (Symbol != symbol) { Symbol = symbol; SetVerticesDirty(); } color = tint; raycastTarget = false; }
        private VertexHelper mesh;
        private Rect bounds;
        private Color shade;
        private void Polygon(params Vector2[] points)
        {
            int first = mesh.currentVertCount;
            foreach (var point in points)
            {
                var vertex = UIVertex.simpleVert;
                vertex.position = new Vector3(bounds.center.x + point.x * bounds.width, bounds.center.y + point.y * bounds.height);
                vertex.color = shade; mesh.AddVert(vertex);
            }
            for (int index = 1; index < points.Length - 1; index++) mesh.AddTriangle(first, first + index, first + index + 1);
        }
        private void Box(float left, float bottom, float right, float top)
            => Polygon(new Vector2(left, bottom), new Vector2(right, bottom), new Vector2(right, top), new Vector2(left, top));
        private void Disc(float x, float y, float radius, int count = 12)
        {
            var points = new Vector2[count];
            for (int i = 0; i < count; i++) { float a = i * Mathf.PI * 2 / count; points[i] = new Vector2(x + Mathf.Cos(a) * radius, y + Mathf.Sin(a) * radius); }
            Polygon(points);
        }
        private void Star(float x, float y, float size)
        {
            Polygon(new Vector2(x, y + size), new Vector2(x + size * .3f, y), new Vector2(x, y - size), new Vector2(x - size * .3f, y));
            Polygon(new Vector2(x - size, y), new Vector2(x, y + size * .3f), new Vector2(x + size, y), new Vector2(x, y - size * .3f));
        }
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear(); mesh = helper; bounds = GetPixelAdjustedRect();
            var ink = new Color(.09f, .06f, .1f, color.a); var bright = Color.Lerp(color, Color.white, .35f);
            shade = color;
            switch (Symbol)
            {
                case BattleDemoEnemySymbol.Wait:
                    Box(-.27f, .31f, .27f, .39f); Box(-.27f, -.39f, .27f, -.31f);
                    Polygon(new Vector2(-.23f, .29f), new Vector2(.23f, .29f), new Vector2(0, .01f));
                    Polygon(new Vector2(0, -.01f), new Vector2(.23f, -.29f), new Vector2(-.23f, -.29f));
                    break;
                case BattleDemoEnemySymbol.Sword:
                    shade = ink; Polygon(new Vector2(-.19f, -.10f), new Vector2(.28f, .43f), new Vector2(.46f, .47f), new Vector2(.41f, .27f), new Vector2(-.08f, -.20f));
                    shade = color; Polygon(new Vector2(-.12f, -.08f), new Vector2(.30f, .37f), new Vector2(.40f, .41f), new Vector2(.35f, .29f), new Vector2(-.03f, -.12f));
                    shade = bright; Polygon(new Vector2(-.10f, -.08f), new Vector2(.40f, .41f), new Vector2(.30f, .37f));
                    shade = color * .75f; Polygon(new Vector2(-.38f, -.30f), new Vector2(-.29f, -.39f), new Vector2(-.09f, -.19f), new Vector2(-.18f, -.10f));
                    shade = bright; Polygon(new Vector2(-.29f, -.04f), new Vector2(-.20f, .05f), new Vector2(.08f, -.23f), new Vector2(-.01f, -.32f));
                    Disc(-.37f, -.38f, .07f); break;
                case BattleDemoEnemySymbol.Shield:
                case BattleDemoEnemySymbol.Armor:
                case BattleDemoEnemySymbol.Immunity:
                case BattleDemoEnemySymbol.Reflect:
                    shade = ink; Polygon(new Vector2(0, .46f), new Vector2(.40f, .32f), new Vector2(.34f, -.15f), new Vector2(0, -.47f), new Vector2(-.34f, -.15f), new Vector2(-.40f, .32f));
                    shade = bright; Polygon(new Vector2(0, .38f), new Vector2(.32f, .26f), new Vector2(.27f, -.1f), new Vector2(0, -.37f), new Vector2(-.27f, -.1f), new Vector2(-.32f, .26f));
                    shade = color * .70f; Polygon(new Vector2(0, .31f), new Vector2(.25f, .22f), new Vector2(.20f, -.06f), new Vector2(0, -.29f));
                    shade = bright;
                    if (Symbol == BattleDemoEnemySymbol.Immunity) Star(0, .02f, .16f);
                    if (Symbol == BattleDemoEnemySymbol.Armor) { Box(-.04f, -.18f, .04f, .24f); Box(-.20f, .04f, .20f, .10f); }
                    if (Symbol == BattleDemoEnemySymbol.Reflect) Polygon(new Vector2(-.22f, -.03f), new Vector2(.06f, .22f), new Vector2(.04f, .08f), new Vector2(.22f, .08f), new Vector2(.22f, -.02f), new Vector2(.01f, -.02f), new Vector2(.01f, -.15f));
                    break;
                case BattleDemoEnemySymbol.Poison:
                    shade = ink; Disc(0, .08f, .42f);
                    shade = color; Disc(0, .08f, .35f); Box(-.23f, -.34f, .23f, -.05f);
                    shade = ink; Disc(-.14f, .08f, .10f); Disc(.14f, .08f, .10f);
                    Polygon(new Vector2(0, -.04f), new Vector2(.06f, -.15f), new Vector2(-.06f, -.15f));
                    Box(-.11f, -.35f, -.06f, -.24f); Box(.06f, -.35f, .11f, -.24f); break;
                case BattleDemoEnemySymbol.Stun:
                    Star(0, .09f, .26f); shade = bright; Star(-.32f, -.19f, .13f); Star(.32f, .25f, .12f); break;
                case BattleDemoEnemySymbol.LifeSteal:
                    Polygon(new Vector2(0, .45f), new Vector2(.28f, .04f), new Vector2(.30f, -.16f), new Vector2(.14f, -.37f), new Vector2(-.14f, -.37f), new Vector2(-.30f, -.16f), new Vector2(-.28f, .04f));
                    shade = bright; Polygon(new Vector2(-.11f, .12f), new Vector2(-.21f, -.12f), new Vector2(-.11f, -.23f)); break;
                case BattleDemoEnemySymbol.Heal:
                    Disc(-.17f, .14f, .23f); Disc(.17f, .14f, .23f);
                    Polygon(new Vector2(-.38f, .06f), new Vector2(.38f, .06f), new Vector2(0, -.42f));
                    shade = bright; Box(-.04f, -.13f, .04f, .22f); Box(-.16f, 0, .16f, .08f); break;
                case BattleDemoEnemySymbol.Cards:
                    shade = color * .6f; Box(-.40f, -.24f, .16f, .42f);
                    shade = color; Box(-.18f, -.40f, .38f, .26f);
                    shade = bright; Star(.10f, -.08f, .15f); break;
                case BattleDemoEnemySymbol.Buff:
                case BattleDemoEnemySymbol.Debuff:
                    float flip = Symbol == BattleDemoEnemySymbol.Buff ? 1 : -1;
                    Polygon(new Vector2(0, .45f * flip), new Vector2(.40f, .03f * flip), new Vector2(.16f, .03f * flip), new Vector2(.16f, -.40f * flip), new Vector2(-.16f, -.40f * flip), new Vector2(-.16f, .03f * flip), new Vector2(-.40f, .03f * flip)); break;
                case BattleDemoEnemySymbol.Cleanse:
                case BattleDemoEnemySymbol.Overheal:
                    Star(0, 0, .44f); shade = bright; Star(.27f, .30f, .13f);
                    if (Symbol == BattleDemoEnemySymbol.Overheal) { Box(-.05f, -.22f, .05f, .22f); Box(-.22f, -.05f, .22f, .05f); } break;
                default:
                    Polygon(new Vector2(0, .44f), new Vector2(.39f, 0), new Vector2(0, -.44f), new Vector2(-.39f, 0));
                    shade = ink; Box(-.04f, -.03f, .04f, .26f); Disc(0, -.17f, .05f); break;
            }
        }
    }
}
