using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Compilation;

namespace PokerEngine.Tests
{
    [TestFixture]
    public sealed class PackagingTests
    {
        [Test]
        public void Minimal2DRuntimeCannotTransitivelyReachTheScene3DPresentationStack()
        {
            const string root = "Poker.Battle2DValidation.Runtime";
            var declared = CompilationDependencies(root, AssembliesType.Player);
            Assert.That(declared.Keys, Does.Contain("Poker.BattleConfiguration.Runtime"));
            AssertForbiddenDependencies(declared, IsScene3DDependency);
            AssertForbiddenDependencies(CompiledDependencies(root), IsScene3DDependency);
        }

        [Test]
        public void BattleConfigurationContainsAssetsAndCannotDependOnScenePresentations()
        {
            const string root = "Poker.BattleConfiguration.Runtime";
            bool Forbidden(string name) => IsScene3DDependency(name) || name.StartsWith("Poker.Battle2DValidation") ||
                name.StartsWith("Poker.Rendering.") || name.StartsWith("CardArt.") ||
                name == "Unity.TextMeshPro" || name == "Unity.ugui";
            AssertForbiddenDependencies(CompilationDependencies(root, AssembliesType.Player), Forbidden);
            AssertForbiddenDependencies(CompiledDependencies(root), Forbidden);
            var configuration = LoadAssembly(root);
            foreach (string name in new[] { "BattleRulesProfile", "PlayerBattleProfile", "EnemyBattleProfile", "ScoreDamageProfile" })
            {
                var profile = configuration.GetType("Poker.SingleBattle." + name, throwOnError: true);
                Assert.That(typeof(UnityEngine.ScriptableObject).IsAssignableFrom(profile), Is.True, name);
            }
            Assert.That(configuration.GetTypes().Where(typeof(UnityEngine.MonoBehaviour).IsAssignableFrom), Is.Empty);
            foreach (var field in configuration.GetTypes().SelectMany(type => type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)))
            foreach (var type in FieldTypes(field.FieldType))
                Assert.That(typeof(UnityEngine.Component).IsAssignableFrom(type) || type == typeof(UnityEngine.GameObject) ||
                    type == typeof(UnityEngine.SceneManagement.Scene), Is.False,
                    field.DeclaringType.FullName + "." + field.Name + " stores a scene object: " + type.FullName);

            AssertForbiddenDependencies(CompilationDependencies("Poker.BattleConfiguration.Editor", AssembliesType.Editor), Forbidden);
        }

        [Test]
        public void Forward2DRenderingHasNoBattleOrLegacyRenderingDependency()
        {
            const string root = "Poker.Rendering.Forward2D.Runtime";
            bool Forbidden(string name) => IsScene3DDependency(name) || name.StartsWith("PokerEngine.") ||
                name.StartsWith("Poker.Battle") || name.StartsWith("CardArt.");
            AssertForbiddenDependencies(CompilationDependencies(root, AssembliesType.Player), Forbidden);
            AssertForbiddenDependencies(CompiledDependencies(root), Forbidden);
        }

        [Test]
        public void Minimal2DToolsReferenceForward2DWithoutADirectLegacyRendererReference()
        {
            var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor);
            foreach (string name in new[] { "Poker.Battle2DValidation.Editor", "Poker.Battle2DValidation.Tests.Editor" })
            {
                var assembly = assemblies.SingleOrDefault(candidate => candidate.name == name);
                Assert.That(assembly, Is.Not.Null, name);
                var dependencies = assembly.assemblyReferences.Select(reference => reference.name).ToArray();
                Assert.That(dependencies, Does.Contain("Poker.Rendering.Forward2D.Runtime"), name);
                Assert.That(dependencies, Does.Not.Contain("RetroRendering.Runtime"), name);
            }
        }

        [Test]
        public void BothBattlePresentationsUseTheSamePureSessionContract()
        {
            var scene = LoadAssembly("Poker.SingleBattle.Runtime");
            var minimal = LoadAssembly("Poker.Battle2DValidation.Runtime");
            var sceneHost = scene.GetType("Poker.SingleBattle.SingleBattleHost", throwOnError: true);
            var minimalHost = minimal.GetType("Poker.Battle2DValidation.Battle2DHost", throwOnError: true);
            var minimalView = minimal.GetType("Poker.Battle2DValidation.Battle2DView", throwOnError: true);
            Assert.That(typeof(IBattleSessionPresentation).IsAssignableFrom(sceneHost), Is.True);
            Assert.That(typeof(IBattleSessionPresentation).IsAssignableFrom(minimalView), Is.True);
            var hostBase = LoadAssembly("Poker.BattleHosting.Runtime").GetType("Poker.BattleHosting.BaseBattleHost", throwOnError: true);
            foreach (var host in new[] { sceneHost, minimalHost })
            {
                Assert.That(host.BaseType, Is.EqualTo(hostBase), host.FullName);
                Assert.That(host.GetProperty("Lifecycle")?.PropertyType, Is.EqualTo(typeof(BattleHost)), host.FullName);
                foreach (string method in new[] { "StartBattle", "RestartBattle", "AttachSession", "SynchronizeBattle" })
                    Assert.That(host.GetMethod(method)?.DeclaringType, Is.EqualTo(hostBase), host.FullName + "." + method);
                Assert.That(host.GetProperty("Session")?.PropertyType, Is.EqualTo(typeof(BattleSession)), host.FullName);
                Assert.That(host.GetProperty("Playback")?.PropertyType, Is.EqualTo(typeof(BattleSessionPlayback)), host.FullName);
            }
        }

        [Test]
        public void SharedHostingCannotDependOnConfigurationOrAnyPresentationStyle()
        {
            const string root = "Poker.BattleHosting.Runtime";
            bool Forbidden(string name) => IsScene3DDependency(name) || name.StartsWith("Poker.Battle2DValidation") ||
                name.StartsWith("Poker.BattleConfiguration") || name.StartsWith("Poker.Rendering.") || name.StartsWith("CardArt.") ||
                name == "Unity.TextMeshPro" || name == "Unity.ugui";
            AssertForbiddenDependencies(CompilationDependencies(root, AssembliesType.Player), Forbidden);
            AssertForbiddenDependencies(CompiledDependencies(root), Forbidden);
        }

        private static bool IsScene3DDependency(string name) => name.StartsWith("Poker.SingleBattle.") ||
            name.StartsWith("RetroRendering") || name.StartsWith("CardInteraction") || name.StartsWith("CardPresentation") ||
            name.IndexOf("DOTween", StringComparison.OrdinalIgnoreCase) >= 0 || name.StartsWith("DG.Tweening");

        private static void AssertForbiddenDependencies(Dictionary<string, string> dependencies, Func<string, bool> forbidden)
        {
            var violations = dependencies.Where(item => forbidden(item.Key)).Select(item => item.Value).ToArray();
            Assert.That(violations, Is.Empty, "Forbidden dependency paths:\n" + string.Join("\n", violations));
        }

        private static Dictionary<string, string> CompilationDependencies(string root, AssembliesType target)
        {
            var graph = CompilationPipeline.GetAssemblies(target).ToDictionary(assembly => assembly.name);
            Assert.That(graph.ContainsKey(root), Is.True, "Missing assembly: " + root);
            var paths = new Dictionary<string, string> { { root, root } }; var queue = new Queue<string>(); queue.Enqueue(root);
            while (queue.Count > 0)
            {
                string name = queue.Dequeue();
                if (!graph.TryGetValue(name, out var assembly)) continue;
                foreach (var reference in assembly.assemblyReferences)
                {
                    if (paths.ContainsKey(reference.name)) continue;
                    paths.Add(reference.name, paths[name] + " -> " + reference.name); queue.Enqueue(reference.name);
                }
            }
            return paths;
        }

        private static Dictionary<string, string> CompiledDependencies(string root)
        {
            // Unity may supply unused auto-referenced plugin DLLs to the compiler. Follow emitted
            // references here so a real DOTween dependency is caught without treating availability as use.
            var paths = new Dictionary<string, string> { { root, root } }; var queue = new Queue<string>(); queue.Enqueue(root);
            while (queue.Count > 0)
            {
                string name = queue.Dequeue();
                foreach (var reference in LoadAssembly(name).GetReferencedAssemblies())
                {
                    if (paths.ContainsKey(reference.Name)) continue;
                    paths.Add(reference.Name, paths[name] + " -> " + reference.Name);
                    if (!IsFrameworkAssembly(reference.Name)) queue.Enqueue(reference.Name);
                }
            }
            return paths;
        }

        private static System.Reflection.Assembly LoadAssembly(string name) =>
            AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly => assembly.GetName().Name == name) ??
            System.Reflection.Assembly.Load(name);

        private static bool IsFrameworkAssembly(string name) => name == "mscorlib" || name == "netstandard" ||
            name == "System" || name.StartsWith("System.") || name.StartsWith("Microsoft.") ||
            name.StartsWith("Unity") || name.StartsWith("nunit");

        private static IEnumerable<Type> FieldTypes(Type type)
        {
            yield return type;
            if (type.HasElementType)
                foreach (var element in FieldTypes(type.GetElementType())) yield return element;
            if (type.IsGenericType)
                foreach (var argument in type.GetGenericArguments().SelectMany(FieldTypes)) yield return argument;
        }

        [Test]
        public void PlayerCompilationIncludesRuntimeAndExcludesEditorTests()
        {
            var playerAssemblies = CompilationPipeline.GetAssemblies(AssembliesType.Player).Select(a => a.name).ToArray();
            foreach (var module in new[] { "Core", "Abilities", "Scoring", "Holdem", "Draw", "Runs", "Progression", "Encounters", "Combat", "Composition" })
                Assert.That(playerAssemblies, Does.Contain("PokerEngine." + module));
            var projectTests = Directory.GetFiles("Assets", "*.asmdef", SearchOption.AllDirectories)
                .Where(path => path.Replace('\\', '/').Contains("/Tests/"))
                .Select(path => UnityEngine.JsonUtility.FromJson<TestAssemblyDefinition>(File.ReadAllText(path)).name);
            Assert.That(playerAssemblies.Intersect(projectTests), Is.Empty);
        }
        [Test]
        public void EveryTestAssemblyExplicitlyTargetsEditorAndIsNotAutomaticallyReferenced()
        {
            foreach (var path in Directory.GetFiles("Assets", "*.asmdef", SearchOption.AllDirectories)
                .Where(path => path.Replace('\\', '/').Contains("/Tests/")))
            {
                var definition = UnityEngine.JsonUtility.FromJson<TestAssemblyDefinition>(File.ReadAllText(path));
                Assert.That((definition.includePlatforms ?? new string[0]).SequenceEqual(new[] { "Editor" }) ||
                    (definition.defineConstraints ?? new string[0]).Contains("UNITY_EDITOR"), Is.True, path);
                Assert.That(definition.autoReferenced, Is.False, path);
            }
        }
        [System.Serializable] private sealed class TestAssemblyDefinition
        {
            public string name = null;
            public string[] includePlatforms = null;
            public string[] defineConstraints = null;
            public bool autoReferenced = true;
        }

        [Test]
        public void LinkerPreservesTheBasePlayerDataContracts()
        {
            var document = XDocument.Load("Assets/PokerEngine/Runtime/link.xml");
            foreach (var module in new[] { "Core", "Abilities", "Scoring", "Holdem", "Draw", "Runs", "Progression", "Encounters", "Combat", "Composition" })
                Assert.That(document.Root.Elements("assembly").Any(a =>
                    (string)a.Attribute("fullname") == "PokerEngine." + module && (string)a.Attribute("preserve") == "all"), Is.True);
        }

        [Test]
        public void EngineAssetsHaveImportedUniqueMetadata()
        {
            var guids = new HashSet<string>();
            var paths = new[] { "Assets/PokerEngine" }
                .Concat(Directory.GetDirectories("Assets/PokerEngine", "*", SearchOption.AllDirectories))
                .Concat(Directory.GetFiles("Assets/PokerEngine", "*", SearchOption.AllDirectories).Where(p => !p.EndsWith(".meta")));
            foreach (var path in paths)
            {
                Assert.That(File.Exists(path + ".meta"), Is.True, path);
                var guid = AssetDatabase.AssetPathToGUID(path.Replace('\\', '/'));
                Assert.That(guid, Is.Not.Empty, path);
                Assert.That(guids.Add(guid), Is.True, "Duplicate GUID: " + path);
            }
        }
    }
}
