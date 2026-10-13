using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace PokerEngine.Tests
{
    [TestFixture]
    public sealed class ModuleBoundaryTests
    {
        private static readonly Dictionary<string, string[]> Allowed = new Dictionary<string, string[]> {
            { "Core", new string[0] }, { "Abilities", new[] { "Core" } }, { "Scoring", new[] { "Core" } },
            { "Holdem", new[] { "Core", "Abilities" } }, { "Draw", new[] { "Core", "Abilities", "Scoring" } },
            { "Runs", new[] { "Core", "Abilities", "Scoring" } },
            { "Progression", new[] { "Core" } },
            { "Composition", new[] { "Core", "Abilities", "Scoring", "Holdem", "Draw", "Runs", "Progression" } }
        };
        private static readonly Type[] Roots = { typeof(CardInstance), typeof(AbilityExecutor), typeof(ScoreEngine),
            typeof(HoldemMatchEngine), typeof(DrawEngine), typeof(RunEngine<,,>), typeof(HoldemRunAdapter), typeof(PlayerProgressionState) };
        [Test] public void ModulesLiveInSeparateAssemblies()
        {
            var assemblies = Roots.Select(t => t.Assembly.GetName().Name).ToArray();
            Assert.That(assemblies.Distinct().Count(), Is.EqualTo(8));
            Assert.That(assemblies, Is.EquivalentTo(Allowed.Keys.Select(m => "PokerEngine." + m)));
        }
        [Test] public void DependencyDirectionIsEnforcedInCompiledAssemblies()
        {
            foreach (var assembly in Roots.Select(t => t.Assembly))
            {
                var module = assembly.GetName().Name.Substring("PokerEngine.".Length);
                var references = assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
                Assert.That(references.Where(r => r.StartsWith("PokerEngine.")).All(r => Allowed[module].Contains(r.Substring("PokerEngine.".Length))), Is.True, module);
                Assert.That(references.Any(r => r.StartsWith("UnityEngine") || r.StartsWith("UnityEditor")), Is.False, module);
            }
        }
        [Test] public void RunSnapshotsKeepConcreteEncounterTypesWithoutModeSwitching()
        {
            Assert.That(typeof(RunState<DrawState>).GetProperty("Encounter").PropertyType, Is.EqualTo(typeof(DrawState)));
            Assert.That(typeof(RunState<HoldemEncounterState>).GetProperty("Encounter").PropertyType, Is.EqualTo(typeof(HoldemEncounterState)));
            var runAssembly = typeof(RunEngine<,,>).Assembly;
            Assert.That(runAssembly.GetTypes().SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                .Any(f => f.FieldType == typeof(MatchState) || f.FieldType == typeof(DrawState)), Is.False);
        }
    }
}
