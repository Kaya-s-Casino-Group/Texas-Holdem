using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Poker.RunFlow.Editor;
using PokerEngine;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Poker.RunFlow.Tests
{
    [TestFixture]
    public sealed class RunFlowAssetTests
    {
        private static RunFlowAsset Example()
        { var asset = ScriptableObject.CreateInstance<RunFlowAsset>(); asset.CopyFrom(ExampleRunContent.CreateFourFloors().Definition); return asset; }
        private static RunContent Bind(RunFlowAsset asset)
        {
            var source = ExampleRunContent.Create(true); var offers = source.Definition.ShopPool.Select(source.Offer).ToArray();
            var relics = offers.Where(o => o.Kind == RunOfferKind.Relic).Select(o => o.RelicId).Distinct().Select(source.Relic);
            return asset.BuildContent(source.CardsAndAbilities, source.Definition.Scoring, relics, offers, source.StartingDeckVersion, source.StartingDeck);
        }
        private static void Destroy(RunFlowAsset asset) { Undo.ClearUndo(asset); UnityEngine.Object.DestroyImmediate(asset); }

        [Test] public void LegacyAssetFieldNamesPreserveRelicCapacityAndStartingDefinitions()
        {
            var asset = Example();
            string prefix = "Assets/Scripts/Gameplay/RunFlow/Tests/RunFlowLegacy-" + Guid.NewGuid().ToString("N");
            string currentPath = prefix + ".asset", legacyPath = prefix + "-old.asset";
            try
            {
                asset.RelicCapacity = 7;
                asset.StartingRelics.Clear(); asset.StartingRelics.Add("steady"); asset.StartingRelics.Add("echo");
                AssetDatabase.CreateAsset(asset, currentPath); AssetDatabase.SaveAssetIfDirty(asset);
                string yaml = File.ReadAllText(currentPath);
                StringAssert.Contains("RelicCapacity: 7", yaml); StringAssert.Contains("StartingRelics:", yaml);
                File.WriteAllText(legacyPath, yaml.Replace("RelicCapacity:", "JokerCapacity:").Replace("StartingRelics:", "StartingJokers:"));
                AssetDatabase.ImportAsset(legacyPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var restored = AssetDatabase.LoadAssetAtPath<RunFlowAsset>(legacyPath);
                Assert.IsNotNull(restored);
                Assert.AreEqual(7, restored.RelicCapacity);
                CollectionAssert.AreEqual(new[] { "steady", "echo" }, restored.StartingRelics);
                var definition = restored.BuildDefinition(ScoreProfile.Default);
                Assert.AreEqual(7, definition.RelicCapacity);
                CollectionAssert.AreEqual(restored.StartingRelics, definition.StartingRelics);
            }
            finally
            {
                AssetDatabase.DeleteAsset(legacyPath); AssetDatabase.DeleteAsset(currentPath);
                if (asset != null) Destroy(asset);
            }
        }

        [Test] public void SavedUnityAssetReimportsAllRulesConnectionsPositionsAndFourFloors()
        {
            var asset = Example(); string path = "Assets/Scripts/Gameplay/RunFlow/Tests/RunFlowRoundTrip-" + Guid.NewGuid().ToString("N") + ".asset";
            try
            {
                asset.Floors[0].Nodes[0].Position = new Vector2(223, -82);
                asset.Floors[0].Nodes[0].DisplayName = "策划命名";
                var expected = asset.BuildDefinition(ScoreProfile.Default); AssetDatabase.CreateAsset(asset, path); AssetDatabase.SaveAssetIfDirty(asset);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var restored = AssetDatabase.LoadAssetAtPath<RunFlowAsset>(path); var definition = restored.BuildDefinition(ScoreProfile.Default);
                Assert.AreEqual(4, definition.Floors.Count); Assert.AreEqual(RunFloorKind.Boss, definition.Floors[3].Kind);
                CollectionAssert.AreEqual(expected.Floors.Select(f => f.NodeTree.Fingerprint), definition.Floors.Select(f => f.NodeTree.Fingerprint));
                Assert.AreEqual(new Vector2(223, -82), restored.Floors[0].Nodes[0].Position);
                Assert.AreEqual("策划命名", restored.Floors[0].Nodes[0].DisplayName);
                CollectionAssert.AreEqual(expected.ShopPool, definition.ShopPool); CollectionAssert.AreEqual(expected.StartingRelics, definition.StartingRelics);
                Assert.AreEqual(expected.RerollCost, definition.RerollCost);
            }
            finally { AssetDatabase.DeleteAsset(path); if (asset != null) Destroy(asset); }
        }

        [Test] public void CompiledDefinitionFreezesRuleDataAndIgnoresDisplayNamesAndLayout()
        {
            var asset = Example();
            try
            {
                var before = asset.BuildDefinition(ScoreProfile.Default); var fingerprint = before.Floors[0].NodeTree.Fingerprint;
                asset.Floors[0].DisplayName = "改名"; asset.Floors[0].Nodes[0].DisplayName = "另一个名字";
                RunFlowEditing.AutoLayout(asset, asset.Floors[0]);
                Assert.AreEqual(fingerprint, asset.BuildDefinition(ScoreProfile.Default).Floors[0].NodeTree.Fingerprint);
                asset.Floors[0].Nodes[0].TargetScore = 12345; asset.StartingGold = 999; asset.ShopPool.Clear(); asset.ShopPool.Add("gold");
                Assert.AreNotEqual(fingerprint, asset.BuildDefinition(ScoreProfile.Default).Floors[0].NodeTree.Fingerprint);
                Assert.AreEqual(200, before.Stages[0].TargetScore); Assert.AreEqual(10, before.StartingGold); Assert.IsFalse(before.ShopPool.SequenceEqual(asset.ShopPool));
            }
            finally { Destroy(asset); }
        }

        [Test] public void FourFloorAssetProducesContentConsumedByRealDrawRunAndSnapshotRecovery()
        {
            var asset = Example();
            try
            {
                foreach (var floor in asset.Floors) foreach (var node in floor.Nodes) node.TargetScore = 1;
                var content = Bind(asset); var adapter = new DrawRunAdapter(ExampleRunContent.Runtime(true));
                var engine = new RunEngine<DrawState, DrawCommand, DrawEvent>(content, adapter); var player = new ParticipantId("player");
                var state = engine.Create("asset-run", player, HmacDeckShuffler.FromSeed(new byte[32]));
                var started = engine.ExecuteSystem(state, new RunCommand<DrawCommand>("start", state.Revision, RunAction.StartRun));
                Assert.IsTrue(started.Accepted); state = started.State;
                for (int i = 0; state.Phase != RunPhase.Completed; i++)
                {
                    Assert.Less(i, 40); RunCommand<DrawCommand> command;
                    if (state.Phase == RunPhase.Encounter)
                    {
                        var hand = state.Encounter.World.Zones.Single(z => z.Kind == CardZoneKinds.Hand).Cards;
                        command = new RunCommand<DrawCommand>("play-" + i, state.Revision, RunAction.PlayerEncounter,
                            encounterCommand: new DrawCommand("play", state.Encounter.Revision, DrawAction.Play, hand.Take(1)));
                    }
                    else if (state.Phase == RunPhase.Reward) command = new RunCommand<DrawCommand>("skip-" + i, state.Revision, RunAction.SkipReward);
                    else if (state.Phase == RunPhase.Shop) command = new RunCommand<DrawCommand>("leave-" + i, state.Revision, RunAction.CompleteNode);
                    else command = new RunCommand<DrawCommand>("route-" + i, state.Revision, RunAction.ChooseNextNode, nextNodeId: "normal");
                    var result = engine.Execute(state, command, player); Assert.IsTrue(result.Accepted, result.Error.ToString()); state = result.State;
                    state = RunSnapshotCodec<DrawState>.Deserialize(RunSnapshotCodec<DrawState>.Serialize(state, content, adapter), content, adapter);
                }
                Assert.AreEqual(3, state.FloorIndex); Assert.IsTrue(state.Floors.All(f => f.IsCompleted)); Assert.AreEqual(7, state.Scores.Count);
            }
            finally { Destroy(asset); }
        }

        [Test] public void DraftConnectionEditingPreventsCyclesAndDeletionCleansUpIncomingEdgesWithUndo()
        {
            var asset = ScriptableObject.CreateInstance<RunFlowAsset>();
            try
            {
                var floor = RunFlowEditing.AddFloor(asset); var a = RunFlowEditing.AddNode(asset, floor, RunNodeKind.Encounter, Vector2.zero);
                var b = RunFlowEditing.AddNode(asset, floor, RunNodeKind.Shop, Vector2.right); var c = RunFlowEditing.AddNode(asset, floor, RunNodeKind.Reward, Vector2.up);
                Assert.IsTrue(RunFlowEditing.Connect(asset, floor, a.Id, b.Id)); Assert.IsTrue(RunFlowEditing.Connect(asset, floor, b.Id, c.Id));
                Assert.IsFalse(RunFlowEditing.Connect(asset, floor, c.Id, a.Id)); Assert.IsFalse(RunFlowEditing.Connect(asset, floor, a.Id, b.Id));
                Assert.IsFalse(RunFlowEditing.Connect(asset, floor, a.Id, "missing"));
                Undo.FlushUndoRecordObjects(); Undo.IncrementCurrentGroup();
                var removedId = b.Id; RunFlowEditing.RemoveNode(asset, floor, b); Undo.FlushUndoRecordObjects();
                Assert.AreEqual(2, floor.Nodes.Count); Assert.IsEmpty(a.NextNodeIds);
                Undo.PerformUndo(); floor = asset.Floors[0];
                Assert.AreEqual(3, floor.Nodes.Count); Assert.IsTrue(floor.Nodes[0].NextNodeIds.Contains(removedId));
                Undo.PerformRedo(); floor = asset.Floors[0]; Assert.AreEqual(2, floor.Nodes.Count);
                Assert.IsFalse(floor.Nodes.Any(n => n.NextNodeIds.Contains(removedId)));
            }
            finally { Destroy(asset); }
        }

        [Test] public void InvalidDraftsCannotCompileAndContentIdsAreValidatedAtBinding()
        {
            var asset = Example();
            try
            {
                asset.Floors[0].EntryNodeId = "missing"; Assert.Throws<ArgumentException>(() => asset.BuildDefinition(ScoreProfile.Default));
                Assert.IsNotNull(RunFlowEditing.Validate(asset)); asset.CopyFrom(ExampleRunContent.CreateFourFloors().Definition);
                asset.Floors[0].Nodes.Add(new RunFlowNodeData { Id = "unreachable", DisplayName = "孤立节点" });
                Assert.Throws<ArgumentException>(() => asset.BuildDefinition(ScoreProfile.Default)); asset.CopyFrom(ExampleRunContent.CreateFourFloors().Definition);
                asset.ShopPool.Add("unknown-content"); Assert.Throws<ArgumentException>(() => Bind(asset));
                asset.FormatVersion = 999; Assert.Throws<ArgumentException>(() => asset.BuildDefinition(ScoreProfile.Default));
            }
            finally { Destroy(asset); }
        }

        [Test] public void ReorderingFloorsPreservesTreeDataAndDifferentStageIdentitiesRoundTrip()
        {
            var asset = Example();
            try
            {
                var firstId = asset.Floors[0].Id; var tree = asset.Floors[0].Build().NodeTree.Fingerprint;
                RunFlowEditing.MoveFloor(asset, 0, 1); Assert.AreEqual(firstId, asset.Floors[1].Id);
                Assert.AreEqual(tree, asset.BuildDefinition(ScoreProfile.Default).Floors[1].NodeTree.Fingerprint);
                var stage = new RunStageDefinition("separate-stage-id", 123, 4);
                var source = new RunDefinition("source.1", ScoreProfile.Default,
                    new RunNodeTree("v1", "node", new[] { new RunEncounterNodeDefinition("node", stage) }), new[] { "gold" }, new[] { "gold" });
                asset.CopyFrom(source); Assert.AreEqual(source.NodeTree.Fingerprint, asset.BuildDefinition(ScoreProfile.Default).NodeTree.Fingerprint);
            }
            finally { Destroy(asset); }
        }

        [Test] public void PlayerIncludesAssetBridgeAndExcludesAllEditorTools()
        {
            var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Player).Select(a => a.name).ToArray();
            CollectionAssert.Contains(assemblies, "Poker.RunFlow.Runtime");
            CollectionAssert.DoesNotContain(assemblies, "Poker.RunFlow.Editor"); CollectionAssert.DoesNotContain(assemblies, "Poker.RunFlow.Tests.Editor");
            var runtime = typeof(RunFlowAsset).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
            CollectionAssert.DoesNotContain(runtime, "UnityEditor");
            var core = typeof(RunDefinition).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
            Assert.IsFalse(core.Any(name => name.StartsWith("UnityEngine", StringComparison.Ordinal)));
        }

        [Test] public void LayoutKeepsUnrelatedRewardBoxAwayFromDirectShopConnection()
        {
            var asset = Example();
            try
            {
                var floor = asset.Floors[0]; var fingerprint = floor.Build().NodeTree.Fingerprint;
                RunFlowEditing.AutoLayout(asset, floor);
                var normal = floor.Nodes.Single(n => n.Id == "normal");
                var shop = floor.Nodes.Single(n => n.Kind == RunNodeKind.Shop);
                var extraReward = floor.Nodes.Single(n => n.Id == "elite-reward");
                Assert.AreEqual(normal.Position.y, shop.Position.y);
                Assert.IsFalse(new Rect(extraReward.Position, new Vector2(190, 100)).Contains(
                    new Vector2(extraReward.Position.x + 95, normal.Position.y + 43)));
                Assert.AreEqual(fingerprint, floor.Build().NodeTree.Fingerprint);
            }
            finally { Destroy(asset); }
        }
    }
}
