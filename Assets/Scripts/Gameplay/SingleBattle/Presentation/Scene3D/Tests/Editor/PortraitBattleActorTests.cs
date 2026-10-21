using System;
using System.Collections.Generic;
using System.Linq;
using global::BattlePresentation;
using NUnit.Framework;
using Poker.BattlePresentation;
using PokerEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Poker.SingleBattle.Tests
{
    public sealed class PortraitBattleActorTests
    {
        sealed class Fixture : IDisposable
        {
            readonly Scene scene = EditorSceneManager.NewPreviewScene();
            public readonly PlayerBattleActor Actor;
            public readonly PortraitBattleActorPresenter View;
            public readonly MeshRenderer Surface;
            public readonly Texture2D[] Textures = Enumerable.Range(0, 4).Select(_ => new Texture2D(2, 2)).ToArray();
            readonly Material material;
            public Fixture()
            {
                var root = new GameObject("Player"); SceneManager.MoveGameObjectToScene(root, scene);
                Actor = root.AddComponent<PlayerBattleActor>();
                var body = new GameObject("Body"); body.transform.SetParent(root.transform, false);
                var art = new GameObject("Artwork"); art.transform.SetParent(body.transform, false);
                Surface = art.AddComponent<MeshRenderer>();
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                material.SetTexture("_BaseMap", Textures[0]); Surface.sharedMaterial = material;
                View = body.AddComponent<PortraitBattleActorPresenter>(); View.Configure(body.transform);
                View.ConfigurePortrait(Surface, Textures[0], Textures[1], Textures[2], Textures[3]);
                Actor.ConfigurePresentation(View);
            }
            public void Dispose()
            {
                EditorSceneManager.ClosePreviewScene(scene);
                UnityEngine.Object.DestroyImmediate(material);
                foreach (var texture in Textures) UnityEngine.Object.DestroyImmediate(texture);
            }
        }
        static UnitDisplayData Unit(string pose = null) => new UnitDisplayData("player", "Player", "player", 100, 100, 0, 3, 3,
            labels: pose == null ? null : new Dictionary<string, string> { ["portraitPose"] = pose });

        [Test] public void ActorConsumesNeutralSnapshotWithoutRuleSessionAndDamageFactsKeepPose()
        {
            using (var f = new Fixture())
            {
                f.Actor.SynchronizeDisplay(Unit("discard"), true);
                Assert.AreSame(f.Textures[1], f.View.DisplayedTexture);
                f.Actor.SynchronizeDisplay(Unit("action"));
                Assert.AreSame(f.Textures[2], f.View.DisplayedTexture);
                f.Actor.SynchronizeDisplay(Unit());
                Assert.AreEqual("action", f.View.Pose);
                Assert.IsNull(f.Actor.Battle);
                Assert.AreSame(f.Textures[0], f.Surface.sharedMaterial.GetTexture("_BaseMap"),
                    "A portrait must not mutate the shared material.");
            }
        }
        [Test] public void FadeMovesRightThenRestoresArtworkWithoutMovingCombatBody()
        {
            using (var f = new Fixture())
            {
                f.View.PresentPose("discard", false);
                var body = f.View.Model.localPosition;
                f.View.PresentPose("action");
                f.View.Advance(.06f);
                Assert.That(f.View.Opacity, Is.InRange(.1f, .9f));
                Assert.Greater(f.Surface.transform.localPosition.x, 0);
                Assert.AreSame(f.Textures[1], f.View.DisplayedTexture);
                f.View.Advance(.06f);
                Assert.AreEqual(0, f.View.Opacity, .001f);
                Assert.AreSame(f.Textures[2], f.View.DisplayedTexture);
                f.View.Advance(.3f);
                Assert.IsFalse(f.View.IsChangingPose);
                Assert.AreEqual(1, f.View.Opacity);
                Assert.AreEqual(Vector3.zero, f.Surface.transform.localPosition);
                Assert.AreEqual(body, f.View.Model.localPosition);
            }
        }
        [Test] public void RetargetCancelAndRestartCannotLeaveInvisibleOrDisplacedArtwork()
        {
            using (var f = new Fixture())
            {
                f.View.PresentPose("discard", false); f.View.PresentPose("action"); f.View.Advance(.05f);
                float alpha = f.View.Opacity;
                f.View.PresentPose("guard");
                Assert.AreEqual(alpha, f.View.Opacity);
                f.View.Advance(1);
                Assert.AreSame(f.Textures[3], f.View.DisplayedTexture);
                f.View.PresentPose("discard"); f.View.Advance(.04f); f.View.CancelPresentation();
                Assert.IsFalse(f.View.IsChangingPose); Assert.AreEqual(1, f.View.Opacity);
                Assert.AreEqual(Vector3.zero, f.Surface.transform.localPosition);
                f.Actor.SynchronizeDisplay(Unit("action"), true);
                Assert.AreSame(f.Textures[2], f.View.DisplayedTexture);
                Assert.IsFalse(f.View.IsChangingPose);
                f.View.enabled = false;
                f.View.ResetPresentation();
                Assert.AreEqual(1, f.View.Opacity);
                Assert.AreSame(f.Textures[0], f.View.DisplayedTexture);
            }
        }
        [Test] public void DuplicateSnapshotDoesNotRestartTransitionAndMissingPoseFallsBack()
        {
            using (var f = new Fixture())
            {
                f.View.ConfigurePortrait(f.Surface, f.Textures[0], f.Textures[1], f.Textures[2], null);
                f.View.PresentPose("discard", false); f.View.PresentPose("action"); f.View.Advance(.05f);
                f.View.PresentPose("action"); f.View.Advance(.3f);
                Assert.IsFalse(f.View.IsChangingPose);
                f.View.PresentPose("guard", false);
                Assert.AreSame(f.Textures[0], f.View.DisplayedTexture);
            }
        }
        [Test] public void RuleProjectionCapturesPhaseAtEachSnapshotAndOmitsPhaseFromDamageFacts()
        {
            var xml = AssetDatabase.LoadAssetAtPath<TextAsset>("Packages/com.kaya.battle-engine/Content/StandardSingleBattle.xml");
            var compiled = BattleContentCompiler.CompileXml(xml.text);
            Assert.IsTrue(compiled.Success);
            var built = BattleContentFactory.Create(compiled.Content, new BattleSetupRequest(
                "standard-single-battle", "portrait-test", HmacDeckShuffler.FromSeed(new byte[32])));
            Assert.IsTrue(built.Success);
            var session = built.Session; var player = session.State.Duel.PlayerId;
            Assert.IsTrue(session.Execute(session.State.Revision, player, CardDuelAction.Start).Accepted);
            var opening = BattleDisplayProjector.Build(session.State, session.Engine, player);
            Assert.AreEqual("discard", opening.Unit(player.Value).Labels["portraitPose"]);
            Assert.IsTrue(session.Execute(session.State.Revision, player, CardDuelAction.SkipDraw).Accepted);
            var playing = BattleDisplayProjector.Build(session.State, session.Engine, player);
            Assert.AreEqual("action", playing.Unit(player.Value).Labels["portraitPose"]);
            Assert.AreEqual("discard", opening.Unit(player.Value).Labels["portraitPose"], "Old cursor snapshots remain immutable.");
            var result = session.Execute(session.State.Revision, player, CardDuelAction.EndTurn);
            Assert.IsTrue(result.Accepted);
            var guardState = result.Steps.Select(step => step.Transition.State).First(state =>
                state.FlowFor(player).Window == BattlePlayerWindow.Ending ||
                state.Duel.Phase == ScoredDuelPhase.AwaitingEnemyAction);
            var ending = BattleDisplayProjector.Build(guardState, session.Engine, player);
            Assert.AreEqual("guard", ending.Unit(player.Value).Labels["portraitPose"]);
            Assert.AreEqual("discard", BattleDisplayProjector.Build(session.State, session.Engine, player)
                .Unit(player.Value).Labels["portraitPose"], "Automatic enemy turns must not make the displayed guard pose read ahead.");
            Assert.IsFalse(BattleDisplayProjector.Unit(session.State.Duel.Combat.Get(player), session.State.Duel)
                .Labels.ContainsKey("portraitPose"));
        }
    }
}
