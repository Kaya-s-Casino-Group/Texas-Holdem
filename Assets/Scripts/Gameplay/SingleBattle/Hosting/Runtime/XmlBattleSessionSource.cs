using System;
using System.Collections.Generic;
using PokerEngine;
using UnityEngine;

namespace Poker.BattleHosting
{
    [Serializable]
    public sealed class BattleXmlModule
    {
        public string Path;
        public TextAsset Asset;
    }

    [DisallowMultipleComponent]
    public sealed class XmlBattleSessionSource : MonoBehaviour, IBattleSessionSource
    {
        [SerializeField] private TextAsset contentXml;
        [SerializeField] private string encounterId = "single-battle-new";
        [SerializeField] private BattleXmlModule[] modules = Array.Empty<BattleXmlModule>();
        public TextAsset ContentXml => contentXml;
        public string EncounterId => encounterId;
        public IReadOnlyList<BattleXmlModule> Modules => modules ?? Array.Empty<BattleXmlModule>();
        public IReadOnlyList<BattleDiagnostic> Diagnostics { get; private set; } = Array.Empty<BattleDiagnostic>();
        public void Configure(TextAsset content, string encounter)
            => Configure(content, encounter, Array.Empty<BattleXmlModule>());
        public void Configure(TextAsset content, string encounter, BattleXmlModule[] dependencies)
        { contentXml = content; encounterId = encounter; modules = dependencies ?? Array.Empty<BattleXmlModule>(); }
        public BattleSession CreateSession() => Build();
        public BattleSession Build(string battleId = null, RandomState random = null)
        {
            Diagnostics = Array.Empty<BattleDiagnostic>();
            if (contentXml == null) throw new InvalidOperationException("Bind an XML battle content asset.");
            string entry = contentXml.name + ".xml";
            var files = new Dictionary<string, TextAsset>(StringComparer.Ordinal) { { entry, contentXml } };
            foreach (var module in Modules)
            {
                if (module == null || string.IsNullOrWhiteSpace(module.Path) || module.Asset == null)
                    throw new InvalidOperationException("Every XML module needs a bundle-relative path and TextAsset.");
                string path = module.Path.Replace('\\', '/');
                if (files.ContainsKey(path)) throw new InvalidOperationException("Duplicate XML module path: " + path);
                files.Add(path, module.Asset);
            }
            var compiled = BattleContentCompiler.CompileFiles(entry,
                path => files.TryGetValue(path, out var asset) ? asset.text : null);
            Diagnostics = compiled.Diagnostics;
            if (!compiled.Success) throw new InvalidOperationException("Battle XML compilation failed. Inspect the source diagnostics.");
            var assembly = BattleContentFactory.Create(compiled.Content, new BattleSetupRequest(encounterId,
                battleId ?? Guid.NewGuid().ToString("N"), random ?? HmacDeckShuffler.CreateRandomState()));
            Diagnostics = assembly.Diagnostics;
            if (!assembly.Success) throw new InvalidOperationException("Cannot create battle encounter: " + encounterId);
            return assembly.Session;
        }
    }
}
