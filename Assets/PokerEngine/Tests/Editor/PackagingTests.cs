using System.Collections.Generic;
using System.IO;
using System.Linq;
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
