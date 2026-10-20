using System;
using System.Collections.Generic;
using PokerEngine;
using UnityEngine;

namespace Poker.BattleHosting
{
    [DisallowMultipleComponent]
    public sealed class XmlBattleSessionSource : MonoBehaviour, IBattleSessionSource
    {
        [SerializeField] private TextAsset contentXml;
        [SerializeField] private string encounterId = "standard-single-battle";
        public TextAsset ContentXml => contentXml;
        public string EncounterId => encounterId;
        public IReadOnlyList<BattleDiagnostic> Diagnostics { get; private set; } = Array.Empty<BattleDiagnostic>();
        public void Configure(TextAsset content, string encounter)
        { contentXml = content; encounterId = encounter; }
        public BattleSession CreateSession() => Build();
        public BattleSession Build(string battleId = null, RandomState random = null)
        {
            Diagnostics = Array.Empty<BattleDiagnostic>();
            if (contentXml == null) throw new InvalidOperationException("Bind an XML battle content asset.");
            var compiled = BattleContentCompiler.CompileXml(contentXml.text, contentXml.name + ".xml");
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
