using System;
using System.Linq;
using CardArt.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Utils;

namespace CardArt.Tests
{
    public sealed class CardRenderingTests
    {
        [Test]
        public void ImportedMeshPreservesSurfaceFlagsAndPhysicalSize()
        {
            var mesh = AssetDatabase.LoadAllAssetsAtPath(CardRenderingValidation.MeshPath).OfType<Mesh>().Single();
            Assert.That(mesh.subMeshCount, Is.EqualTo(1));
            Assert.That(mesh.GetIndexCount(0), Is.EqualTo(480));
            Assert.That(mesh.uv2.Length, Is.EqualTo(mesh.vertexCount));
            Assert.That(mesh.uv2.Any(v => v == Vector2.zero), Is.True);
            Assert.That(mesh.uv2.Any(v => v == Vector2.right), Is.True);
            Assert.That(mesh.uv2.Any(v => v == Vector2.up), Is.True);
            Assert.That(mesh.uv2.All(v => v == Vector2.zero || v == Vector2.right || v == Vector2.up), Is.True);
            var rotation = CardRenderingValidation.SurfaceOrientation(mesh);
            var rotated = mesh.vertices.Select(v => rotation * v).ToArray();
            var size = new Vector3(rotated.Max(v => v.x) - rotated.Min(v => v.x),
                rotated.Max(v => v.y) - rotated.Min(v => v.y), rotated.Max(v => v.z) - rotated.Min(v => v.z));
            Assert.That(size.x, Is.EqualTo(0.064f).Within(0.000001f));
            Assert.That(size.y, Is.EqualTo(0.00035f).Within(0.000001f));
            Assert.That(size.z, Is.EqualTo(0.096f).Within(0.000001f));
            var front = Enumerable.Range(0, mesh.vertexCount).Where(i => mesh.uv2[i] == Vector2.zero).ToArray();
            var left = front.Where(i => mesh.uv[i].x < 0.1f).Select(i => rotated[i]).Aggregate(Vector3.zero, (s, v) => s + v);
            var right = front.Where(i => mesh.uv[i].x > 0.9f).Select(i => rotated[i]).Aggregate(Vector3.zero, (s, v) => s + v);
            Assert.That((right - left).x, Is.GreaterThan(0), "The visible front must not be mirrored.");
            Assert.That(front.All(i => (rotation * mesh.normals[i]).y > 0.99f), Is.True);
            var back = Enumerable.Range(0, mesh.vertexCount).Where(i => mesh.uv2[i] == Vector2.right).ToArray();
            Assert.That(back.All(i => (rotation * mesh.normals[i]).y < -0.99f), Is.True);
            var backLeft = back.Where(i => mesh.uv[i].x < 0.1f).Select(i => rotated[i]).Aggregate(Vector3.zero, (sum, v) => sum + v);
            var backRight = back.Where(i => mesh.uv[i].x > 0.9f).Select(i => rotated[i]).Aggregate(Vector3.zero, (sum, v) => sum + v);
            Assert.That((backRight - backLeft).x, Is.LessThan(0), "Back UVs must account for viewing from the opposite side.");
            var importer = (ModelImporter)AssetImporter.GetAtPath(CardRenderingValidation.MeshPath);
            Assert.That(importer.generateSecondaryUV, Is.False);
        }

        [Test]
        public void CardBindingKeepsResourcesSharedAndFaceParametersIndependent()
        {
            var mesh = AssetDatabase.LoadAllAssetsAtPath(CardRenderingValidation.MeshPath).OfType<Mesh>().Single();
            var atlas = AssetDatabase.LoadAssetAtPath<CardAtlasData>(CardRenderingValidation.AtlasPath);
            var material = new Material(Shader.Find(CardShaderProperties.ShaderName));
            var first = new GameObject("First Test Card");
            var second = new GameObject("Second Test Card");
            try
            {
                CardShaderProperties.ConfigureSkin(material, atlas);
                var skinTint = new Color(0.9f, 0.95f, 1f, 1f);
                material.SetColor(CardShaderProperties.BaseColor, skinTint);
                var a = first.AddComponent<CardView>();
                var b = second.AddComponent<CardView>();
                Assert.That(a.Tint, Is.EqualTo(Color.white));
                var selectedTint = new Color(1f, 0.8f, 0.35f, 1f);
                a.SetTint(selectedTint); // May be set before the view is bound.
                a.Bind(mesh, material, atlas, "spades_k");
                b.Bind(mesh, material, atlas, "hearts_q");
                var originalBack = material.GetVector(CardShaderProperties.BackUVRect);
                Assert.That(first.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(second.GetComponent<MeshFilter>().sharedMesh));
                Assert.That(first.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(second.GetComponent<MeshRenderer>().sharedMaterial));
                var block = new MaterialPropertyBlock();
                first.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                atlas.TryGetCard("spades_k", out var spades);
                Assert.That(block.GetVector(CardShaderProperties.FrontUVRect), Is.EqualTo(spades.uv));
                Assert.That(block.GetColor(CardShaderProperties.ColorTint), Is.EqualTo(selectedTint).Using(new ColorEqualityComparer(0.00001f)));
                second.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                atlas.TryGetCard("hearts_q", out var hearts);
                Assert.That(block.GetVector(CardShaderProperties.FrontUVRect), Is.EqualTo(hearts.uv));
                Assert.That(block.GetColor(CardShaderProperties.ColorTint), Is.EqualTo(Color.white));
                a.SetCard("diamonds_a");
                a.enabled = false;
                a.enabled = true;
                first.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                Assert.That(block.GetColor(CardShaderProperties.ColorTint), Is.EqualTo(selectedTint).Using(new ColorEqualityComparer(0.00001f)));
                Assert.That(a.Tint, Is.EqualTo(selectedTint));
                second.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                Assert.That(block.GetVector(CardShaderProperties.FrontUVRect), Is.EqualTo(hearts.uv));
                Assert.That(block.GetColor(CardShaderProperties.ColorTint), Is.EqualTo(Color.white));
                Assert.That(material.GetVector(CardShaderProperties.BackUVRect), Is.EqualTo(originalBack));
                Assert.That(material.GetColor(CardShaderProperties.BaseColor), Is.EqualTo(skinTint).Using(new ColorEqualityComparer(0.00001f)));
                Assert.That(material.GetColor(CardShaderProperties.ColorTint), Is.EqualTo(Color.white));
                Assert.That(material.enableInstancing, Is.True);
                Assert.Throws<ArgumentException>(() => a.SetCard("missing_card"));
                Assert.That(a.CardId, Is.EqualTo("diamonds_a"));
                foreach (var region in atlas.cards.Where(r => r.id != "card_back"))
                {
                    a.SetCard(region.id);
                    first.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                    Assert.That(block.GetVector(CardShaderProperties.FrontUVRect), Is.EqualTo(region.uv), region.id);
                    Assert.That(block.GetColor(CardShaderProperties.ColorTint), Is.EqualTo(selectedTint).Using(new ColorEqualityComparer(0.00001f)), region.id);
                }
                var currentFace = block.GetVector(CardShaderProperties.FrontUVRect);
                a.SetTint(Color.white);
                first.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                Assert.That(block.GetColor(CardShaderProperties.ColorTint), Is.EqualTo(Color.white));
                Assert.That(block.GetVector(CardShaderProperties.FrontUVRect), Is.EqualTo(currentFace));
                Assert.That(first.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(material));
                Assert.That(second.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(material));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void FoilParametersAreIndependentAndSurviveRebinding()
        {
            var mesh = AssetDatabase.LoadAllAssetsAtPath(CardRenderingValidation.MeshPath).OfType<Mesh>().Single();
            var atlas = AssetDatabase.LoadAssetAtPath<CardAtlasData>(CardRenderingValidation.AtlasPath);
            var material = new Material(Shader.Find(CardShaderProperties.ShaderName));
            var first = new GameObject("Foil Test");
            var second = new GameObject("Paper Test");
            try
            {
                CardShaderProperties.ConfigureSkin(material, atlas);
                var a = first.AddComponent<CardView>();
                var b = second.AddComponent<CardView>();
                var tint = new Color(1.2f, 3.2f, 4); // HDR must survive binding and the instance property block.
                a.SetFoilEnabled(true);
                a.SetFoilColor(tint);
                a.SetFoilStrength(2);
                a.Bind(mesh, material, atlas, "spades_k");
                b.Bind(mesh, material, atlas, "hearts_q");
                a.SetCard("diamonds_a");
                a.SetTint(Color.gray);
                a.enabled = false;
                a.enabled = true;
                Assert.That(a.SupportsFoil, Is.True);
                Assert.That(a.FoilEnabled, Is.True);
                Assert.That(a.FoilColor, Is.EqualTo(tint));
                var block = new MaterialPropertyBlock();
                first.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                Assert.That(block.GetFloat(CardShaderProperties.FoilEnabled), Is.EqualTo(1));
                Assert.That(block.GetFloat(CardShaderProperties.FoilStrength), Is.EqualTo(2));
                Assert.That(block.GetColor(CardShaderProperties.FoilColor), Is.EqualTo(tint).Using(new ColorEqualityComparer(0.00001f)));
                second.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                Assert.That(block.GetFloat(CardShaderProperties.FoilEnabled), Is.Zero);
                Assert.That(block.GetColor(CardShaderProperties.FoilColor), Is.EqualTo(Color.white));
                Assert.That(material.GetFloat(CardShaderProperties.FoilEnabled), Is.Zero);
                a.SetFoilEnabled(false);
                a.SetFoilEnabled(true);
                Assert.That(a.FoilStrength, Is.EqualTo(2));
                a.SetFoilStrength(-1);
                Assert.That(a.FoilStrength, Is.Zero);
                a.SetFoilStrength(99);
                Assert.That(a.FoilStrength, Is.EqualTo(5));
                Assert.Throws<ArgumentOutOfRangeException>(() => a.SetFoilStrength(float.NaN));
                Assert.That(first.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(material));
                Assert.That(second.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(material));
                Assert.That(material.enableInstancing, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void ShaderHasDeferredShadowAndDepthPassesAndPointAtlas()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(CardRenderingValidation.MaterialPath);
            Assert.That(material, Is.Not.Null);
            foreach (var pass in new[] { "GBuffer", "ForwardLit", "ShadowCaster", "DepthOnly", "DepthNormals" })
                Assert.That(material.FindPass(pass), Is.GreaterThanOrEqualTo(0), pass);
            Assert.That(ShaderUtil.GetShaderMessages(material.shader).Where(m => m.severity.ToString() == "Error"), Is.Empty);
            var texture = material.GetTexture(CardShaderProperties.BaseMap);
            Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(texture.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));
            Assert.That(importer.mipmapEnabled, Is.False);
        }
    }
}
