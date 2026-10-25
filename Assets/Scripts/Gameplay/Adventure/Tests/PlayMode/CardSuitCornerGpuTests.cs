using System.Collections;
using System.IO;
using CardArt;
using CardPresentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Poker.Adventure.Tests
{
    public sealed class CardSuitCornerGpuTests
    {
        [UnityTest]
        public IEnumerator StandaloneSuitLayerRendersOutlinedSilhouettesOnAnOrdinaryCanvas()
        {
            var atlas = AssetDatabase.LoadAssetAtPath<CardAtlasData>("Assets/Art/CardAtlases/FourBeastsAtlas.asset");
            var root = new GameObject("Suit GPU verification", typeof(RectTransform), typeof(Canvas));
            try
            {
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32760;
                var background = new GameObject("Neutral background", typeof(RectTransform), typeof(Image));
                var fill = (RectTransform)background.transform; fill.SetParent(root.transform, false);
                fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.sizeDelta = Vector2.zero;
                background.GetComponent<Image>().color = new Color(.45f, .45f, .45f, 1);

                var viewport = new GameObject("Shop scroll viewport", typeof(RectTransform), typeof(RectMask2D));
                var viewportRect = (RectTransform)viewport.transform; viewportRect.SetParent(root.transform, false);
                viewportRect.anchorMin = Vector2.zero; viewportRect.anchorMax = Vector2.one; viewportRect.sizeDelta = Vector2.zero;

                var card = new GameObject("Standalone shop suit", typeof(RectTransform), typeof(CardSuitCornerGraphic));
                var rect = (RectTransform)card.transform; rect.SetParent(viewportRect, false);
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(240, 360);
                var corner = card.GetComponent<CardSuitCornerGraphic>();
                bool[] spades = null;
                foreach (var face in new[] { "spades_k", "clubs_k" })
                {
                    // The adventure shop creates an ordinary Canvas. This is
                    // intentionally not a CardUIGraphic-owned hierarchy.
                    canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.None;
                    corner.Present(atlas, face, Color.white, 1, 0, true);
                    Canvas.ForceUpdateCanvases();
                    yield return null;
                    yield return new WaitForEndOfFrame();
                    // Rendering the variant starts Editor async compilation.
                    // Its cyan loading shader is not the finished GPU output.
                    float shaderDeadline = Time.realtimeSinceStartup + 15;
                    while (ShaderUtil.anythingCompiling && Time.realtimeSinceStartup < shaderDeadline) yield return null;
                    Assert.That(ShaderUtil.anythingCompiling, Is.False, "Suit shader compilation did not finish before pixel verification.");
                    yield return null;
                    yield return new WaitForEndOfFrame();
                    var frame = ScreenCapture.CaptureScreenshotAsTexture();
                    try
                    {
                        Directory.CreateDirectory("Library/AdventureValidation");
                        File.WriteAllBytes("Library/AdventureValidation/suit-corner-gpu-" + face + ".png", frame.EncodeToPNG());
                        var position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(new Vector3(-120, -180)));
                        var area = new RectInt(Mathf.RoundToInt(position.x + 240 * .025f), Mathf.RoundToInt(position.y + 360 * .75f),
                            Mathf.RoundToInt(240 * .17f), Mathf.RoundToInt(360 * .13f));
                        var pixels = frame.GetPixels(area.x, area.y, area.width, area.height);
                        var baseline = frame.GetPixel(10, 10);
                        float neutral = Luminance(baseline);
                        int cyan = 0, ink = 0, outline = 0, changed = 0;
                        var mask = new bool[pixels.Length];
                        for (int index = 0; index < pixels.Length; index++)
                        {
                            var pixel = pixels[index];
                            if (pixel.r < .15f && pixel.g > .85f && pixel.b > .85f) cyan++;
                            float light = Luminance(pixel);
                            if (light < neutral - .15f) ink++;
                            if (light > neutral + .15f) outline++;
                            mask[index] = Mathf.Abs(light - neutral) > .1f;
                            if (mask[index]) changed++;
                        }
                        Assert.That(cyan, Is.Zero, "The GPU must not turn a missing vertex channel into cyan corner rectangles.");
                        Assert.That(ink, Is.GreaterThan(20), "The existing dark suit must visibly render.");
                        Assert.That(outline, Is.GreaterThan(4), "The pale outline must visibly separate the silhouette from the card.");
                        Assert.That(changed, Is.InRange(pixels.Length * .08f, pixels.Length * .85f),
                            "The layer must retain transparent space around the suit instead of drawing a solid rectangle.");
                        if (spades == null) spades = mask;
                        else
                        {
                            int differences = 0;
                            for (int index = 0; index < mask.Length; index++) if (mask[index] != spades[index]) differences++;
                            Assert.That(differences, Is.GreaterThan(10), "Spades and clubs must differ in rendered shape, even at the same dark color.");
                        }
                    }
                    finally { Object.Destroy(frame); }
                }
            }
            finally { Object.Destroy(root); }
        }

        private static float Luminance(Color color) => color.r * .299f + color.g * .587f + color.b * .114f;
    }
}
