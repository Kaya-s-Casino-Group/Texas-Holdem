using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CardArt.Tests
{
    public sealed class CardBreathingOutlineTests
    {
        const string MaterialPath = "Assets/GameAssets/CardArt/Prefabs/CardBreathingOutline.mat";
        [Test] public void CycleUsesOnePhaseAndColorPerGroupAndNeverTouchesCardRenderer()
        {
            var owner = new GameObject("outline test"); var original = owner.AddComponent<MeshRenderer>();
            var originalBlock = new MaterialPropertyBlock(); originalBlock.SetFloat("_Sentinel", 42); original.SetPropertyBlock(originalBlock);
            var shared = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath); Assert.NotNull(shared);
            string before = EditorJsonUtility.ToJson(shared);
            var second = new GameObject("second");
            try
            {
                var a = owner.AddComponent<CardBreathingOutline>(); var b = second.AddComponent<CardBreathingOutline>();
                a.Bind(owner.transform, shared, new Vector2(1,1.5f), Vector3.zero, Quaternion.identity);
                b.Bind(second.transform, shared, new Vector2(1,1.5f), Vector3.zero, Quaternion.identity);
                Assert.AreEqual(0, owner.GetComponentsInChildren<Collider>().Length);
                using (var cycle = new CardOutlineCycle(new[] { new[] { a,b }, new[] { a } }, new[] { Color.red, Color.blue }))
                {
                    cycle.Tick(1); Assert.AreEqual(0, cycle.ActiveIndex); Assert.That(cycle.Pulse, Is.EqualTo(1).Within(.001));
                    var block = new MaterialPropertyBlock(); a.OutlineRenderer.GetPropertyBlock(block);
                    Assert.AreEqual(Color.red, block.GetColor("_Color")); Assert.AreEqual(1, block.GetFloat("_Pulse"));
                    b.OutlineRenderer.GetPropertyBlock(block); Assert.AreEqual(1, block.GetFloat("_Pulse"));
                    cycle.Tick(3); Assert.AreEqual(1, cycle.ActiveIndex); Assert.IsFalse(b.OutlineRenderer.enabled);
                    a.OutlineRenderer.GetPropertyBlock(block); Assert.AreEqual(Color.blue, block.GetColor("_Color"));
                }
                Assert.IsFalse(a.OutlineRenderer.enabled); Assert.IsFalse(b.OutlineRenderer.enabled);
                original.GetPropertyBlock(originalBlock); Assert.AreEqual(42, originalBlock.GetFloat("_Sentinel"));
                Assert.AreEqual(before, EditorJsonUtility.ToJson(shared));
                var visual = a.OutlineRenderer.gameObject; var mesh = visual.GetComponent<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(a); Assert.IsTrue(visual == null); Assert.IsTrue(mesh == null);
            }
            finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(second); }
        }
        [Test] public void ShaderDrawsOutsideBorderWithTransparentCenterAndZeroPulse()
        {
            var material = new Material(AssetDatabase.LoadAssetAtPath<Material>(MaterialPath));
            var rt = new RenderTexture(256,256,0,RenderTextureFormat.ARGB32); rt.Create();
            var pixels = new Texture2D(256,256,TextureFormat.RGBA32,false);
            var quad = new Mesh { vertices = new[] { new Vector3(-1,-1,0), new Vector3(1,-1,0), new Vector3(1,1,0), new Vector3(-1,1,0) },
                uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up }, triangles = new[] { 0,1,2,0,2,3 } };
            var previous = RenderTexture.active;
            try
            {
                Assert.IsFalse(ShaderUtil.ShaderHasError(material.shader));
                material.SetVector("_HalfSize",new Vector4(.5f,.5f,.2f,0)); material.SetFloat("_Width",.04f);
                material.SetFloat("_Softness",.04f); material.SetColor("_Color",Color.white);
                float Draw(float pulse)
                {
                    material.SetFloat("_Pulse",pulse);
                    using (var command = new CommandBuffer())
                    {
                        command.SetRenderTarget(rt); command.ClearRenderTarget(false,true,Color.clear);
                        command.SetViewProjectionMatrices(Matrix4x4.identity,Matrix4x4.identity);
                        command.DrawMesh(quad,Matrix4x4.identity,material,0,0); Graphics.ExecuteCommandBuffer(command);
                    }
                    RenderTexture.active=rt; pixels.ReadPixels(new Rect(0,0,256,256),0,0); pixels.Apply();
                    Assert.That(pixels.GetPixel(128,128).maxColorComponent, Is.LessThan(.01f), "Card center must remain transparent.");
                    return pixels.GetPixel(223,128).r;
                }
                Assert.That(Draw(1), Is.GreaterThan(.2f), "Outline extends beyond the card edge.");
                Assert.That(Draw(0), Is.LessThan(.01f));
            }
            finally { RenderTexture.active=previous; Object.DestroyImmediate(material); Object.DestroyImmediate(pixels); Object.DestroyImmediate(quad); rt.Release(); Object.DestroyImmediate(rt); }
        }
    }
}
