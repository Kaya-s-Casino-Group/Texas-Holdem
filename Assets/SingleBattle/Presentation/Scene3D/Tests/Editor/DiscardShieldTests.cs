using System;
using System.Linq;
using System.IO;
using CardArt;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Poker.SingleBattle.Tests
{
    public sealed class DiscardShieldTests
    {
        private static ShieldCard[] Cards(int count) => Enumerable.Range(0,count).Select(i => new ShieldCard("card"+i,"hearts_q")).ToArray();
        [Test] public void WholeCardConsumptionUsesCumulativeCeilingAndNeverRegrows()
        {
            var ledger=new ShieldLedger();ledger.Reset(0);ledger.Apply("gain",0,24,Cards(3));
            Assert.AreEqual(1,ledger.Apply("hit1",24,23).Batches[0].DissolvedCount);
            Assert.AreEqual(1,ledger.Apply("hit2",23,16).Batches[0].DissolvedCount);
            Assert.AreEqual(2,ledger.Apply("hit3",16,15).Batches[0].DissolvedCount);
            Assert.AreEqual(3,ledger.Apply("hit4",15,1).Batches[0].DissolvedCount);
            Assert.IsEmpty(ledger.Apply("hit5",1,0).Batches);
        }
        [Test] public void FifoCrossesBatchesAndInitialBlockDoesNotEraseDiscardCardsEarly()
        {
            var ledger=new ShieldLedger();ledger.Reset(5);ledger.Apply("first",5,29,Cards(3));ledger.Apply("second",29,45,Cards(2));
            var first=ledger.Apply("hit",45,40);Assert.AreEqual(2,first.Batches.Count);Assert.AreEqual(0,first.Batches[0].DissolvedCount);
            var next=ledger.Apply("cross",40,10);Assert.AreEqual(1,next.Batches.Count);Assert.AreEqual("second",next.Batches[0].Id);Assert.AreEqual(1,next.Batches[0].DissolvedCount);
            Assert.IsEmpty(ledger.Apply("clear",10,0).Batches);
        }
        [Test] public void DuplicateEventsSnapshotsAndResetPreserveAccounting()
        {
            var ledger=new ShieldLedger();ledger.Reset(0);var old=ledger.Apply("gain",0,24,Cards(3));
            ledger.Apply("gain",0,24,Cards(3));Assert.AreEqual(1,ledger.Snapshot.Batches.Count);
            ledger.Apply("hit",24,12);Assert.AreEqual(24,old.Batches[0].RemainingBlock);
            ledger.Reset(3);Assert.AreEqual(3,ledger.Snapshot.TotalBlock);Assert.IsEmpty(ledger.Snapshot.Batches[0].Cards);
            ledger.Apply("gain",3,27,Cards(3));Assert.AreEqual(2,ledger.Snapshot.Batches.Count);
            Assert.AreEqual(1,new ShieldBatch("large",int.MaxValue,int.MaxValue-1,Cards(52)).DissolvedCount);
        }
        private static void Resources(out Mesh mesh,out Material material,out CardAtlasData atlas)
        {
            mesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/SingleBattle/Presentation/Scene3D/ShieldAssets/ShieldCard.asset");
            material=AssetDatabase.FindAssets("t:Material",new[]{"Assets/SingleBattle/Presentation/Scene3D/ShieldAssets"}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Material>).First();
            atlas=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CardArt/Prefabs/Card.prefab").GetComponentInChildren<CardView>(true).Atlas;
        }
        [Test] public void SnapReusesSharedResourcesAndSeededLayoutWithoutReplayingLanding()
        {
            Resources(out var mesh,out var material,out var atlas);var root=new GameObject("Shield Test");
            try
            {
                var view=root.AddComponent<DiscardShieldPresenter>();view.CardMesh=mesh;view.ShieldMaterial=material;view.Atlas=atlas;
                int landed=0;view.CardLanded+=_=>landed++;
                var snapshot=new ShieldSnapshot(new[]{new ShieldBatch("gain",24,24,Cards(3))});view.Snap(snapshot);
                var poses=root.GetComponentsInChildren<MeshRenderer>().Select(r=>r.transform.localPosition).ToArray();
                Assert.AreEqual(3,view.VisibleCardCount);view.Snap(snapshot);
                CollectionAssert.AreEqual(poses,root.GetComponentsInChildren<MeshRenderer>().Select(r=>r.transform.localPosition));
                Assert.IsTrue(root.GetComponentsInChildren<MeshRenderer>().All(r=>r.sharedMaterial==material));
                Assert.IsTrue(root.GetComponentsInChildren<MeshFilter>().All(r=>r.sharedMesh==mesh));
                view.Snap(new ShieldSnapshot(new[]{new ShieldBatch("gain",24,23,Cards(3))}));
                Assert.AreEqual(2,view.VisibleCardCount);Assert.AreEqual(0,landed);
                view.Snap(ShieldSnapshot.Empty);Assert.AreEqual(0,view.VisibleCardCount);
            }
            finally{Object.DestroyImmediate(root);}
        }
        [TestCase(0f)]
        [TestCase(1f)]
        public void UniformSizeKeepsAtlasAspectAndExplicitAnglesRespectFacingChance(float outwardChance)
        {
            Resources(out var mesh, out var material, out var atlas);
            var root = new GameObject("Size And Angle Test");
            try
            {
                var view = root.AddComponent<DiscardShieldPresenter>();
                view.CardMesh = mesh; view.ShieldMaterial = material; view.Atlas = atlas;
                view.CardHeight = .6f; view.OutwardChance = outwardChance;
                view.PitchRange = new Vector2(15, 15);
                view.YawRange = new Vector2(-25, -25);
                view.RollRange = new Vector2(35, 35);
                var snapshot = new ShieldSnapshot(new[] { new ShieldBatch("angles", 3, 3, Cards(3)) });
                view.ValidateConfiguration(); view.Snap(snapshot);
                var renderers = root.GetComponentsInChildren<MeshRenderer>();
                var bottoms = renderers.Select(r => DiscardShieldPresenter.CardBottomPosition(r.transform)).ToArray();
                for (int i = 0; i < renderers.Length; i++)
                {
                    var card = renderers[i].transform;
                    Assert.That(card.localScale.x / card.localScale.y, Is.EqualTo((float)atlas.cardSize.x / atlas.cardSize.y).Within(.0001f));
                    Assert.That(card.localScale.y, Is.EqualTo(.6f).Within(.0001f));
                    var radial = bottoms[i] - view.RingCenter; radial.y = 0;
                    var expected = Quaternion.LookRotation(outwardChance == 1 ? radial : -radial) * Quaternion.Euler(15, -25, 35);
                    Assert.That(Quaternion.Angle(card.localRotation, expected), Is.LessThan(.05f));
                }
                view.CardHeight = .9f; view.Snap(snapshot);
                for (int i = 0; i < renderers.Length; i++)
                {
                    Assert.That(renderers[i].transform.localScale.y, Is.EqualTo(.9f).Within(.0001f));
                    Assert.That(Vector3.Distance(bottoms[i], DiscardShieldPresenter.CardBottomPosition(renderers[i].transform)), Is.LessThan(.0001f));
                    Assert.That(renderers[i].transform.localScale.x / .9f, Is.EqualTo((float)atlas.cardSize.x / atlas.cardSize.y).Within(.0001f));
                }
                view.PitchRange = new Vector2(20, 10);
                Assert.Throws<InvalidOperationException>(() => view.ValidateConfiguration());
            }
            finally { Object.DestroyImmediate(root); }
        }
        [TestCase(0f, 60f)]
        [TestCase(170f, 40f)]
        public void ArcLayoutAnchorsBottomsInsideSectorAndRandomizesInwardOutwardFacing(float centerAngle, float arcAngle)
        {
            Resources(out var mesh, out var material, out var atlas);
            var root = new GameObject("Ring Test");
            try
            {
                root.transform.SetPositionAndRotation(new Vector3(3, 2, 1), Quaternion.Euler(0, 57, 0));
                root.transform.localScale = new Vector3(2, 1.3f, .7f);
                var view = root.AddComponent<DiscardShieldPresenter>();
                view.CardMesh = mesh; view.ShieldMaterial = material; view.Atlas = atlas;
                view.RingCenter = new Vector3(.3f, -.1f, .2f);
                view.ArcCenterAngle = centerAngle; view.ArcAngle = arcAngle;
                view.RingInnerRadius = .6f; view.RingOuterRadius = .9f;
                view.LandingHeightVariation = .04f; view.PitchRange = view.YawRange = view.RollRange = Vector2.zero;
                var snapshot = new ShieldSnapshot(new[] { new ShieldBatch("ring", 64, 64, Cards(64)) });
                view.Snap(snapshot);
                int inward = 0, outward = 0;
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                {
                    var card = renderer.transform;
                    var bottom = root.transform.InverseTransformPoint(DiscardShieldPresenter.CardBottomPosition(card));
                    var offset = bottom - view.RingCenter;
                    Assert.That(Mathf.Abs(offset.y), Is.LessThanOrEqualTo(.02001f));
                    offset.y = 0;
                    Assert.That(offset.magnitude, Is.InRange(.59999f, .90001f));
                    float angle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(centerAngle, angle)), Is.LessThanOrEqualTo(arcAngle * .5f + .001f));
                    float facing = Vector3.Dot(card.localRotation * Vector3.forward, offset.normalized);
                    Assert.That(Mathf.Abs(facing), Is.GreaterThan(.999f));
                    if (facing > 0) outward++; else inward++;
                }
                Assert.Greater(inward, 0); Assert.Greater(outward, 0);
                view.PitchRange = new Vector2(-8, 8); view.YawRange = new Vector2(-12, 12); view.RollRange = new Vector2(-20, 20);
                view.Snap(snapshot);
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                {
                    var bottom = root.transform.InverseTransformPoint(DiscardShieldPresenter.CardBottomPosition(renderer.transform));
                    var offset = bottom - view.RingCenter; offset.y = 0;
                    Assert.That(offset.magnitude, Is.InRange(.59999f, .90001f));
                }
            }
            finally { Object.DestroyImmediate(root); }
        }
        [Test] public void ShaderRendersFullPartialAndEmptyInstancesAndHasDeferredDepthShadowPasses()
        {
            Resources(out var mesh,out var material,out var atlas);atlas.TryGetCard("hearts_q",out var face);
            var preview=new PreviewRenderUtility();Texture2D pixels=null;
            try
            {
                for(int i=0;i<3;i++)
                {
                    var root=new GameObject("Dissolve "+i);preview.AddSingleGO(root);root.transform.position=new Vector3((1-i)*.9f,0,0);root.transform.localScale=new Vector3(.65f,1,1);
                    root.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=root.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
                    var props=new MaterialPropertyBlock();props.SetVector("_FrontUVRect",face.uv);props.SetFloat("_Dissolve",i*.5f);renderer.SetPropertyBlock(props);
                }
                preview.camera.orthographic=true;preview.camera.orthographicSize=.7f;preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=10;
                preview.camera.transform.position=new Vector3(0,0,3);preview.camera.transform.LookAt(Vector3.zero,Vector3.up);
                preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=Color.black;
                preview.ambientColor=Color.gray;preview.lights[0].intensity=1.5f;preview.lights[0].transform.rotation=Quaternion.Euler(30,160,0);
                preview.BeginPreview(new Rect(0,0,1024,512),GUIStyle.none);preview.Render(allowScriptableRenderPipeline:true);var texture=(RenderTexture)preview.EndPreview();
                var previous=RenderTexture.active;try{RenderTexture.active=texture;pixels=new Texture2D(texture.width,texture.height,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);pixels.Apply();}finally{RenderTexture.active=previous;}
                Directory.CreateDirectory("Library/ShieldTests");File.WriteAllBytes("Library/ShieldTests/dissolve.png",pixels.EncodeToPNG());
                var samples=pixels.GetPixels();var counts=new int[3];
                for(int y=0;y<pixels.height;y++)for(int x=0;x<pixels.width;x++)if(samples[y*pixels.width+x].maxColorComponent>.05f)counts[Math.Min(2,x*3/pixels.width)]++;
                Assert.Greater(counts[0],1000);Assert.Greater(counts[1],1000);Assert.Less(counts[1],counts[0]*.85f);Assert.Less(counts[2],100);
                foreach(var pass in new[]{"GBuffer","ForwardLit","ShadowCaster","DepthOnly","DepthNormals"})Assert.GreaterOrEqual(material.FindPass(pass),0);
                Assert.IsEmpty(ShaderUtil.GetShaderMessages(material.shader).Where(m=>m.severity.ToString()=="Error"));
            }
            finally{preview.Cleanup();if(pixels!=null)Object.DestroyImmediate(pixels);}
        }
    }
}
