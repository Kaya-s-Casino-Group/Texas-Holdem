using System;
using System.Collections.Generic;
using System.Linq;
using PokerEngine;
using UnityEngine;

namespace Poker.SingleBattle
{
    public enum BattleConfigurationSource { LegacyProfiles, XmlContent }

    public sealed partial class SingleBattleHost
    {
        [Header("规则内容入口（修改后重新开战生效）")]
        [SerializeField] private BattleConfigurationSource configurationSource;
        [SerializeField, InspectorName("XML 内容包")] private TextAsset contentXml;
        [SerializeField, InspectorName("编成 ID")] private string encounterId = "single-battle-new";
        public bool UsesXmlContent => configurationSource == BattleConfigurationSource.XmlContent;
        public TextAsset ContentXml => contentXml;
        public string ContentEncounterId => encounterId;
        public IReadOnlyList<BattleDiagnostic> ContentDiagnostics { get; private set; } = Array.Empty<BattleDiagnostic>();

        /// <summary>Changes only the next battle's source; never mutates an attached session.</summary>
        public void ConfigureContent(TextAsset xml, string encounter)
        {
            if (busy) throw new InvalidOperationException("Wait for presentation before replacing next battle's content.");
            contentXml = xml; encounterId = encounter; configurationSource = BattleConfigurationSource.XmlContent;
            ContentDiagnostics = Array.Empty<BattleDiagnostic>();
        }

        private CompiledBattleContent ReadContent()
        {
            ContentDiagnostics = Array.Empty<BattleDiagnostic>();
            if (contentXml == null) throw ContentFailure("MissingContent", "请绑定 XML 战斗内容包。");
            var result = BattleContentCompiler.CompileXml(contentXml.text, contentXml.name + ".xml");
            ContentDiagnostics = result.Diagnostics;
            if (!result.Success) throw ContentFailure();
            return result.Content;
        }

        private BattleEncounterContentDefinition ReadContentEncounter(CompiledBattleContent content = null)
        {
            content = content ?? ReadContent();
            BattleEncounterContentDefinition encounter;
            if (string.IsNullOrWhiteSpace(encounterId) || !content.Encounters.TryGetValue(encounterId, out encounter))
                throw ContentFailure("UnknownEncounter", "XML 中找不到编成：" + encounterId);
            if (encounter.Players.Count != Players.Count || encounter.Enemies.Count != Enemies.Count)
                throw ContentFailure("SceneRosterMismatch", "XML 编成与场景角色数量不一致。请按编成顺序绑定双方角色。", "/BattleContent/Encounters");
            return encounter;
        }

        private BattleSession BuildContentSession(string battleId, RandomState random)
        {
            var content = ReadContent();
            var encounter = ReadContentEncounter(content);
            var assembly = BattleContentFactory.Create(content, new BattleSetupRequest(encounterId,
                battleId ?? BattleSessionFactory.CreateBattleId(), random ?? HmacDeckShuffler.CreateRandomState()));
            ContentDiagnostics = Array.AsReadOnly(ContentDiagnostics.Concat(assembly.Diagnostics).ToArray());
            if (!assembly.Success) throw ContentFailure();
            ValidateContentRoster(encounter, assembly.Session);
            return assembly.Session;
        }

        private void ValidateContentRoster(BattleEncounterContentDefinition encounter, BattleSession session)
        {
            if (!encounter.Players.Select(slot => slot.Id).SequenceEqual(session.State.Duel.Players.Select(player => player.Id.Value)) ||
                !encounter.Enemies.Select(slot => slot.Id).SequenceEqual(session.State.Duel.Enemies.Select(enemy => enemy.Id.Value)))
                throw ContentFailure("SceneRosterMismatch", "接入会话的参与者 ID 与 XML 编成顺序不一致。", "/BattleContent/Encounters");
        }

        private InvalidOperationException ContentFailure(string code = null, string message = null, string path = "/BattleContent")
        {
            if (code != null)
                ContentDiagnostics = Array.AsReadOnly(ContentDiagnostics.Concat(new[] {
                    new BattleDiagnostic(code, BattleDiagnosticSeverity.Error, message,
                        new BattleSourceLocation(contentXml != null ? contentXml.name + ".xml" : "content.xml", 0, 0, path), encounterId)
                }).ToArray());
            Message = "XML 配置未通过校验：\n" + string.Join("\n", ContentDiagnostics.Select(diagnostic =>
                diagnostic.Code + " · " + diagnostic.Location.Line + ":" + diagnostic.Location.Column + " · " + diagnostic.Location.Path + " · " + diagnostic.Message));
            StateChanged?.Invoke();
            return new InvalidOperationException(Message);
        }
    }
}
