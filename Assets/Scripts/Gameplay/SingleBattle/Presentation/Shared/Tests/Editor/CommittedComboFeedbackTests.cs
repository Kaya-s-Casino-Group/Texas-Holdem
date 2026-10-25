using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using global::BattlePresentation;
using NUnit.Framework;
using PokerEngine;

namespace Poker.BattlePresentation.Tests
{
    public sealed class CommittedComboFeedbackTests
    {
        private static readonly ParticipantId Player = new ParticipantId("combo-player"), Enemy = new ParticipantId("combo-enemy");
        private static readonly StatusDefinitionId Immunity = new StatusDefinitionId("combo-immunity");
        private sealed class Sequence : IScoredHandEffectRule
        {
            private readonly BattleEffectStep[] steps;
            public string Version => "combo-feedback.1";
            internal Sequence(bool immunity)
            {
                var effects = new List<BattleEffectStep>();
                if (immunity) effects.Add(new BattleEffectStep("immunity", BattleEffectSpec.ApplyStatus(new StatusApplicationSpec(Immunity, 2)), EffectTargetSpec.Opponents()));
                for (int i = 0; i < 3; i++) effects.Add(new BattleEffectStep("hit-" + i, BattleEffectSpec.Damage(10), EffectTargetSpec.Opponents()));
                steps = effects.ToArray();
            }
            public BattleEffectSequence Resolve(ScoredHandEffectContext context) => new BattleEffectSequence(Version, "three real hits", steps);
        }
        private sealed class ExtraDamage : ICombatReactionProvider
        {
            public IEnumerable<CombatReaction> React(CombatReactionContext context)
            {
                if (context.Event is CombatEvent fact && fact.Effect.Kind == CombatEffectKind.Damage && fact.Provenance.Origin == CombatEffectOrigin.Primary)
                    return new[] { new CombatReaction(new CombatEffect(CombatEffectKind.Damage, Player, Enemy, 3), "combo-relic", Player,
                        CombatEffectOrigin.Triggered, sourceInstanceId: "relic/extra") };
                return Array.Empty<CombatReaction>();
            }
        }
        private sealed class Frontend : IBattlePresentationFrontend
        {
            internal PresentationBatch Batch;
            public bool IsPresenting => false;
            public void Initialize(BattleDisplaySnapshot snapshot) { }
            public IPresentationOperation Present(PresentationBatch batch, PlaybackMode mode) { Batch = batch; return PresentationOperation.Completed(); }
            public void Synchronize(BattleDisplaySnapshot snapshot) { }
        }
        private static PresentationBatch Play(bool immunity = false, bool reactions = false, long block = 0)
        {
            var catalog = new CombatStatusCatalog("combo-status.1", new[] {
                new CombatStatusDefinition(Immunity, StatusTags.None, new StatusBehaviorSpec[] { new DamageImmunitySpec() }, StatusLifetimeSpec.UntilRemoved())
            });
            var rules = new BattleRules("combo-feedback.1", 20, statusCatalog: catalog, reactionProvider: reactions ? new ExtraDamage() : null);
            var engine = new CardDuelEngine(rules);
            var state = engine.Create("combo-feedback", HmacDeckShuffler.FromSeed(new byte[32]),
                new[] { new BattlePlayerSetup(Player, new CombatantDefinition("hero", 100), rules: new BattlePlayerRules(effectRule: new Sequence(immunity))) },
                new[] { new BattleEnemySetup(Enemy, new CombatantDefinition("enemy", 1000, block), 0) });
            var session = new BattleSession(engine, state); var frontend = new Frontend();
            var bridge = new BattlePresentationBridge(engine, Player, frontend); bridge.Initialize(state);
            var start = session.Execute(session.State.Revision, Player, CardDuelAction.Start);
            Assert.That(start.Accepted, Is.True, start.Error.ToString()); bridge.Present(start, false);
            var result = session.Execute(session.State.Revision, Player, CardDuelAction.Play, session.State.Zone(CardZoneKinds.Hand, Player).Cards.Take(1));
            Assert.That(result.Accepted, Is.True, result.Error.ToString()); bridge.Present(result, true);
            return frontend.Batch;
        }

        [Test]
        public void ComboSummaryAppearsOnLastCommittedPrimarySegmentAndExcludesRelicDamageAndShieldAbsorption()
        {
            var batch = Play(reactions: true, block: 5);
            var hits = batch.Changes.Where(change => change.Effect?.Kind == "Damage").ToArray();
            Assert.That(hits, Has.Length.EqualTo(6), "Individual primary and reaction damage facts remain intact.");
            var summary = hits.Where(change => change.Values.ContainsKey("comboSummary")).ToArray();
            Assert.That(summary, Has.Length.EqualTo(1));
            Assert.That(summary[0].Values["comboSummary"], Is.EqualTo("3段连击·总伤害25"));
            Assert.That(summary[0].Values["effectOrigin"], Is.EqualTo("Primary"));
            Assert.That(summary[0], Is.SameAs(hits[4]));
        }

        [Test]
        public void TwoImmunityConsumptionsRemainTwoZeroDamageSegmentsInTheThreeHitCombo()
        {
            var batch = Play(immunity: true);
            var hits = batch.Changes.Where(change => change.Effect?.Kind == "Damage").ToArray();
            Assert.That(hits.Select(change => change.Effect.HealthLost), Is.EqualTo(new long[] { 0, 0, 10 }));
            Assert.That(hits.Take(2).All(change => change.Values.TryGetValue("damagePrevented", out var reason) && reason == "immune"), Is.True,
                "A committed immunity consumption must be distinguishable from a zero request or shield absorption.");
            Assert.That(hits.Last().Values.TryGetValue("comboSummary", out var summary) ? summary : "", Is.EqualTo("3段连击·总伤害10"));
            var status = batch.Changes.First(change => change.SemanticKey == "StatusChanged");
            Assert.That(status.Values.TryGetValue("floatingFeedback", out var compact) ? compact : "", Is.EqualTo("怪物 1：获得 伤害免疫 ×2"));
            Assert.That(status.Values["feedback"], Does.Contain("每层抵挡"), "The detailed log keeps the full rule explanation.");
        }

        [Test]
        public void CommittedStatusFactsExposeTheirDefinitionForVisualStackUpdateCoalescing()
        {
            var statuses = Play(immunity: true).Changes.Where(change => change.SemanticKey == "StatusChanged").ToArray();
            Assert.That(statuses.Length, Is.GreaterThan(1));
            foreach (var status in statuses)
                Assert.That(status.Values.TryGetValue("statusDefinition", out var definition) ? definition : "", Is.EqualTo(Immunity.Value),
                    "Visuals need stable status identity while preserving every applied and consumed fact.");
        }

        private static PresentationBatch SevenStationPlay(bool straight, bool fullHouse = false)
        {
            var player = new ParticipantId("player");
            var xml = XDocument.Parse(File.ReadAllText("External/BattleToolkit/Engine/Content/SingleChapterBattle.xml"));
            string encounterId = straight ? "single-chapter-wrath" : "single-chapter-battle-1";
            if (!straight)
            {
                var encounter = xml.Root.Element("Encounters").Elements("Encounter").Single(item => (string)item.Attribute("id") == encounterId);
                encounter.Elements("Enemy").Remove();
                encounter.Add(Enumerable.Range(1, 3).Select(index => new XElement("Enemy", new XAttribute("id", "aoe-" + index), new XAttribute("ref", "rotator"))));
            }
            var compiled = BattleContentCompiler.CompileXml(xml.ToString());
            Assert.That(compiled.Success, Is.True, string.Join("\n", compiled.Diagnostics.Select(item => item.Message)));
            var built = BattleContentFactory.Create(compiled.Content, new BattleSetupRequest(encounterId, "combo-retarget", HmacDeckShuffler.FromSeed(new byte[32])));
            Assert.That(built.Success, Is.True);
            var session = built.Session;
            Assert.That(session.Execute(session.State.Revision, player, CardDuelAction.Start).Accepted, Is.True);
            var ranks = straight ? new[] { 2, 3, 4, 5, 6 } : fullHouse ? new[] { 3, 3, 3, 2, 2 } : new[] { 3, 3, 3 };
            var edited = session.ApplyAdmin(session.State.Revision, new BattleAdminPatch(
                zones: new[] { new BattleAdminZone(player, CardZoneKinds.Hand, ranks.Select((rank, index) =>
                    new BattleAdminCard(definitionId: "standard:" + (index % 4 + 1) + ":" + rank))) },
                relics: new[] { new BattleAdminRelics(player, Array.Empty<BattleAdminRelic>()) }));
            Assert.That(edited.Accepted, Is.True, edited.Error.ToString());
            var frontend = new Frontend(); var bridge = new BattlePresentationBridge(session.Engine, player, frontend); bridge.Initialize(session.State);
            var result = session.Execute(session.State.Revision, player, CardDuelAction.Play, session.State.Zone(CardZoneKinds.Hand, player).Cards,
                straight ? new[] { new ParticipantId("hand-1") } : fullHouse ? new[] { new ParticipantId("aoe-1") } : null);
            Assert.That(result.Accepted, Is.True, result.Error.ToString());
            bridge.Present(result, true); return frontend.Batch;
        }

        [Test]
        public void RealStraightRetargetingAfterEachHandDiesProducesOneFiveSegmentActionSummary()
        {
            var hits = SevenStationPlay(straight: true).Changes.Where(change => change.Effect?.Kind == "Damage" &&
                change.Values.TryGetValue("effectOrigin", out var origin) && origin == "Primary").ToArray();
            Assert.That(hits, Has.Length.EqualTo(5));
            Assert.That(hits.Select(change => change.Effect.HealthLost), Is.EqualTo(new long[] { 1, 1, 1, 1, 1 }));
            Assert.That(hits.Select(change => change.Effect.TargetId).Distinct().Count(), Is.EqualTo(5), "The real chain must retarget after each one-health hand dies.");
            var summaries = hits.Where(change => change.Values.ContainsKey("comboSummary")).ToArray();
            Assert.That(summaries, Has.Length.EqualTo(1));
            Assert.That(summaries[0], Is.SameAs(hits.Last()));
            Assert.That(summaries[0].Values["comboSummary"], Is.EqualTo("5段连击·总伤害5"));
        }

        [Test]
        public void OneAreaAttackHittingThreeDifferentEnemiesDoesNotBecomeAThreeSegmentCombo()
        {
            var hits = SevenStationPlay(straight: false).Changes.Where(change => change.Effect?.Kind == "Damage" &&
                change.Values.TryGetValue("effectOrigin", out var origin) && origin == "Primary").ToArray();
            Assert.That(hits, Has.Length.EqualTo(3));
            Assert.That(hits.Select(change => change.Effect.TargetId).Distinct().Count(), Is.EqualTo(3));
            Assert.That(hits.Any(change => change.Values.ContainsKey("comboSummary")), Is.False);
        }

        [Test]
        public void FullHouseAreaDamageWithOneFollowUpReportsHitCountInsteadOfComboSegments()
        {
            var hits = SevenStationPlay(straight: false, fullHouse: true).Changes.Where(change => change.Effect?.Kind == "Damage" &&
                change.Values.TryGetValue("effectOrigin", out var origin) && origin == "Primary").ToArray();
            Assert.That(hits, Has.Length.EqualTo(4), "Three area hits and one targeted follow-up remain separate committed facts.");
            Assert.That(hits.Select(change => change.Effect.HealthLost), Is.EqualTo(new long[] { 36, 36, 36, 16 }));
            Assert.That(hits.Select(change => change.Effect.TargetId), Is.EqualTo(new[] { "aoe-1", "aoe-2", "aoe-3", "aoe-1" }));
            var summaries = hits.Where(change => change.Values.ContainsKey("comboSummary")).ToArray();
            Assert.That(summaries, Has.Length.EqualTo(1));
            Assert.That(summaries[0], Is.SameAs(hits.Last()));
            Assert.That(summaries[0].Values["comboSummary"], Is.EqualTo("4次命中·总伤害124"));
        }
    }
}
