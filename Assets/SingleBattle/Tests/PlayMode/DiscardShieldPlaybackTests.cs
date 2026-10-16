#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using CardArt;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace Poker.SingleBattle.Tests
{
    public class DiscardShieldPlaybackTests
    {
        private GameObject root;private DiscardShieldPresenter view;private float timeScale;
        private ShieldSnapshot Snapshot(int remaining=24)=>new ShieldSnapshot(new[]{new ShieldBatch("test",24,remaining,Enumerable.Range(0,3).Select(i=>new ShieldCard("card"+i,"hearts_q")))});
        [SetUp] public void Setup()
        {
            if(!Application.isPlaying)Assert.Ignore("PlayMode required.");
            timeScale=Time.timeScale;root=new GameObject("Shield Playback Test");view=root.AddComponent<DiscardShieldPresenter>();
            view.CardMesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/SingleBattle/ShieldAssets/ShieldCard.asset");
            view.ShieldMaterial=AssetDatabase.FindAssets("t:Material",new[]{"Assets/SingleBattle/ShieldAssets"}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Material>).First();
            view.Atlas=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CardArt/Prefabs/Card.prefab").GetComponentInChildren<CardView>(true).Atlas;
            view.FlightSeconds=.08f;view.CardInterval=.08f;view.DissolveSeconds=.06f;view.DissolveInterval=.04f;
        }
        [TearDown] public void Teardown()
        {
            Time.timeScale=timeScale;if(root!=null)Object.DestroyImmediate(root);
            System.IO.Directory.CreateDirectory("Library/ShieldTests");var c=TestContext.CurrentContext;
            System.IO.File.WriteAllText("Library/ShieldTests/"+c.Test.Name+".txt",DateTime.UtcNow.ToString("O")+"\n"+c.Result.Outcome+"\n"+c.Result.Message);
        }
        [UnityTest] public IEnumerator CardsLandInOrderAndOneWholeCardDissolvesWhilePausedTime()
        {
            Time.timeScale=0;var landed=new System.Collections.Generic.List<string>();var times=new System.Collections.Generic.List<float>();
            view.CardLanded+=id=>{landed.Add(id);times.Add(Time.realtimeSinceStartup);};
            var operation=view.Present(Snapshot(),true);Assert.IsTrue(operation.keepWaiting);
            float limit=Time.realtimeSinceStartup+3;
            while(operation.keepWaiting && Time.realtimeSinceStartup<limit)yield return null;
            Assert.AreEqual(BattlePresentationStatus.Completed,operation.Status);CollectionAssert.AreEqual(new[]{"card0","card1","card2"},landed);
            Assert.GreaterOrEqual(times[2]-times[0],.1f);Assert.AreEqual(3,view.VisibleCardCount);
            operation=view.Present(Snapshot(23),true,true);limit=Time.realtimeSinceStartup+3;
            while(operation.keepWaiting && Time.realtimeSinceStartup<limit)yield return null;
            Assert.AreEqual(BattlePresentationStatus.Completed,operation.Status);Assert.AreEqual(2,view.VisibleCardCount);Assert.AreEqual(3,landed.Count);
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>()){var properties=new MaterialPropertyBlock();renderer.GetPropertyBlock(properties);Assert.AreEqual(0,properties.GetFloat("_Dissolve"));}
            operation=view.Present(Snapshot(23),true);Assert.AreEqual(BattlePresentationStatus.Completed,operation.Status);
        }
        [UnityTest] public IEnumerator LandingParticlesSpawnAtEachRotatedScaledCardBottom()
        {
            root.transform.SetPositionAndRotation(new Vector3(3, 1, 2), Quaternion.Euler(0, 37, 0));
            root.transform.localScale = new Vector3(1.4f, 1.8f, .8f);
            view.CardHeight = .6f;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/VFX/HitFlashes/Prefabs/Sword Slash 14 Hit Flashes.prefab");
            var particle = prefab.GetComponent<ParticleSystem>();
            view.LandingEffect = particle; view.LandingEffectScale = .2f;
            var expected = new System.Collections.Generic.List<Vector3>();
            view.CardLanded += id => expected.Add(DiscardShieldPresenter.CardBottomPosition(root.transform.Find("Shield " + id)));
            var operation = view.Present(Snapshot(), true);
            float deadline = Time.realtimeSinceStartup + 3;
            while (operation.keepWaiting && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(BattlePresentationStatus.Completed, operation.Status);
            var systems = root.GetComponentsInChildren<ParticleSystem>();
            var effects = systems.Where(value => value.transform.parent == root.transform).ToArray();
            Assert.AreEqual(9, systems.Length);
            foreach (var system in systems)
            {
                Assert.IsFalse(system.main.loop);
                Assert.IsTrue(system.main.useUnscaledTime);
                Assert.AreEqual(ParticleSystemScalingMode.Hierarchy, system.main.scalingMode);
            }
            foreach (var effect in effects)
                Assert.That(Vector3.Distance(effect.transform.localScale, Vector3.one * .12f), Is.LessThan(.0001f));
            Assert.AreEqual(3, expected.Count); Assert.AreEqual(3, effects.Length);
            foreach (var position in expected)
                Assert.IsTrue(effects.Any(effect => Vector3.Distance(effect.transform.position, position) < .0001f));
        }
        [UnityTest] public IEnumerator CancelStopsQueuedLandingsAndSnapDoesNotReplayFeedback()
        {
            int landed=0;view.CardLanded+=_=>landed++;var operation=view.Present(Snapshot(),true);
            view.CancelPresentation();view.Snap(Snapshot());Assert.AreEqual(BattlePresentationStatus.Cancelled,operation.Status);
            yield return new WaitForSecondsRealtime(.6f);Assert.AreEqual(0,landed);Assert.AreEqual(3,view.VisibleCardCount);
            view.Snap(ShieldSnapshot.Empty);Assert.AreEqual(0,view.VisibleCardCount);
        }
    }
}
#endif
