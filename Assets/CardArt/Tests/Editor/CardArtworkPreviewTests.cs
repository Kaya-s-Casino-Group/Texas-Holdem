using CardArt.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CardArt.Tests
{
    public sealed class CardArtworkPreviewTests
    {
        private static float Brightness(Texture texture)
        {
            var previous = RenderTexture.active;
            var pixels = new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);
            try
            {
                RenderTexture.active = (RenderTexture)texture;
                pixels.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0); pixels.Apply();
                return pixels.GetPixel(texture.width / 3, texture.height / 2).grayscale;
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(pixels); }
        }
        [Test] public void PreviewRendersActualInstancesWithoutMutatingSharedAssetsOrScene()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var dirty = scene.isDirty; var rootCount = scene.rootCount;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CardArt/Prefabs/Card.prefab");
            var profile = AssetDatabase.LoadAssetAtPath<CardArtProfile>(CardAtlasWindow.DefaultProfilePath);
            var material = prefab.GetComponentInChildren<CardView>(true).GetComponent<MeshRenderer>().sharedMaterial;
            var before = EditorJsonUtility.ToJson(material);
            using (var preview = new CardArtworkPreviewRenderer(prefab))
            {
                preview.Present(profile, "spades_a", new[]{"wax","foil","cross"});
                Assert.AreSame(material, preview.Current.GetComponent<MeshRenderer>().sharedMaterial);
                Assert.AreSame(material, preview.Reference.GetComponent<MeshRenderer>().sharedMaterial);
                var block = new MaterialPropertyBlock();
                preview.Current.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                Assert.AreEqual(1, block.GetFloat(CardShaderProperties.SealEnabled));
                Assert.AreEqual(1, block.GetFloat(CardShaderProperties.FoilEnabled));
                preview.Reference.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                Assert.AreEqual(0, block.GetFloat(CardShaderProperties.SealEnabled));
                Assert.AreEqual(0, block.GetFloat(CardShaderProperties.FoilEnabled));
                preview.ConfigureLighting(0, 0, 0, new Vector2(45,-35));
                float dark = Brightness(preview.Render(new Rect(0,0,480,300),20));
                preview.ConfigureLighting(1.5f,.5f,.5f,new Vector2(45,-35));
                float lit = Brightness(preview.Render(new Rect(0,0,480,300),20));
                Assert.Greater(lit, dark + .01f, "Preview lights must change actual rendered pixels.");
                var texture = preview.Render(new Rect(0, 0, 480, 300), 20);
                Assert.NotNull(texture); Assert.Greater(texture.width, 0);
                preview.Present(profile, "spades_a", new[]{"wax","foil","dim","cross"},false);
                Assert.IsFalse(preview.Current.SealEnabled); Assert.IsFalse(preview.Current.FoilEnabled);
                Assert.IsFalse(preview.Current.RedCrossEnabled);
            }
            Assert.AreEqual(before, EditorJsonUtility.ToJson(material));
            Assert.AreEqual(dirty, scene.isDirty); Assert.AreEqual(rootCount, scene.rootCount);
        }
    }
}
