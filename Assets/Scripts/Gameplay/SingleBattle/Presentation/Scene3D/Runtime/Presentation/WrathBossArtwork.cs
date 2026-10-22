using System;
using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>Original PSD pixel coordinates, measured from the upper-left of its canvas.</summary>
    [CreateAssetMenu(menuName = "Poker/Wrath Boss Artwork")]
    public sealed class WrathBossArtwork : ScriptableObject
    {
        [Serializable]
        public sealed class Part
        {
            public string Name;
            public Rect Pixels;
            public int Order;
            public Vector2 HudPixel;
            public Material Material;
            public Texture2D Texture => Material != null ? Material.GetTexture("_BaseMap") as Texture2D : null;
        }

        [SerializeField] private Vector2 canvasSize = new Vector2(5000, 5000);
        // Body, four blood hands, binding hand. These are independent combat actors.
        [SerializeField] private Part[] parts;
        [SerializeField] private Part warning;
        public Vector2 CanvasSize => canvasSize;
        public Part[] Parts => parts;
        public Part Warning => warning;

        public void Configure(Vector2 canvas, Part[] layers, Part glow)
        { canvasSize = canvas; parts = layers; warning = glow; }

        public Rect CanvasViewport(Rect area, float screenAspect)
        {
            float aspect = canvasSize.x / canvasSize.y;
            float height = Mathf.Min(area.height, area.width * screenAspect / aspect);
            var size = new Vector2(height * aspect / screenAspect, height);
            return new Rect(area.center - size * .5f, size);
        }

        public Rect PartViewport(Part part, Rect canvas)
        {
            var p = part.Pixels;
            return new Rect(canvas.x + p.x / canvasSize.x * canvas.width,
                canvas.y + (1 - p.yMax / canvasSize.y) * canvas.height,
                p.width / canvasSize.x * canvas.width, p.height / canvasSize.y * canvas.height);
        }
    }
}
