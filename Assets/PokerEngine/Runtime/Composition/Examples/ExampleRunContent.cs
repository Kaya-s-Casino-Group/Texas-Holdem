using System;
using System.Collections.Generic;
using System.Linq;

namespace PokerEngine
{
    /// <summary>Executable example content for integration/testing; numbers are not final game balance.</summary>
    public static class ExampleRunContent
    {
        public static readonly AbilityDefinitionId CallEnergy = new AbilityDefinitionId("example.call-energy");
        public static readonly AbilityDefinitionId SwapCard = new AbilityDefinitionId("example.swap-card");
        public static readonly AbilityDefinitionId PeekDeck = new AbilityDefinitionId("example.peek-deck");
        public static RunContent Create(bool drawMode = false)
        {
            var definitions = new[] {
                new AbilityDefinition(CallEnergy, AbilityMechanism.Triggered, AbilityModes.All, new[] { (drawMode ? DrawTimings.AfterScoring : HoldemTiming.AfterBettingAction) },
                    new[] { new AbilityCondition(ConditionKind.OwnerIsEventActor) }, requiredAction: drawMode ? null : HoldemActionKeys.Call),
                new AbilityDefinition(SwapCard, AbilityMechanism.Activated, AbilityModes.Solo, new[] { (drawMode ? DrawTimings.BeforeAction : HoldemTiming.BeforeBettingAction) },
                    new[] { new AbilityCondition(ConditionKind.OwnerIsCurrentActor), new AbilityCondition(ConditionKind.UsesThisRoundBelow, 1) }),
                new AbilityDefinition(PeekDeck, AbilityMechanism.Activated, AbilityModes.Solo, new[] { (drawMode ? DrawTimings.BeforeAction : HoldemTiming.BeforeBettingAction) },
                    new[] { new AbilityCondition(ConditionKind.OwnerIsCurrentActor), new AbilityCondition(ConditionKind.UsesThisRoundBelow, 1) })
            };
            var catalog = new ContentCatalog(drawMode ? "roguelike.draw.example.2" : "roguelike.holdem.example.2", StandardDeck.Cards(), definitions, new StatusDefinitionId[0]);
            var jokers = new[] {
                new JokerDefinition("steady", new[] { new ScoreEffect(ScoreTiming.AfterCards, ScoreOperation.AddMultiplier, 4, perLevel: 2) }),
                new JokerDefinition("double", new[] { new ScoreEffect(ScoreTiming.AfterCards, ScoreOperation.MultiplyMultiplier, 2, perLevel: 0.5m) }),
                new JokerDefinition("growing", new[] { new ScoreEffect(ScoreTiming.AfterCards, ScoreOperation.AddPoints, 10, perGrowth: 5) }, growthPerScoredRound: 1),
                new JokerDefinition("focus", new[] { new ScoreEffect(ScoreTiming.PerCard, ScoreOperation.AddPoints, 3, suit: Suit.Hearts) }, abilityId: CallEnergy),
                new JokerDefinition("exchange", new ScoreEffect[0], abilityId: SwapCard),
                new JokerDefinition("peek", new ScoreEffect[0], abilityId: PeekDeck)
            };
            var offers = jokers.Select(j => new RunOfferDefinition("buy-" + j.Id, RunOfferKind.Joker, 5, jokerId: j.Id)).Concat(new[] {
                new RunOfferDefinition("enhance", RunOfferKind.EnhanceCard, 3, amount: 10, multiplier: 1),
                new RunOfferDefinition("retrigger", RunOfferKind.EnhanceCard, 4, retriggers: 1),
                new RunOfferDefinition("duplicate", RunOfferKind.DuplicateCard, 3),
                new RunOfferDefinition("remove", RunOfferKind.RemoveCard, 2),
                new RunOfferDefinition("pair-level", RunOfferKind.UpgradeHand, 3, handCategory: HandCategory.OnePair),
                new RunOfferDefinition("gold", RunOfferKind.Gold, 4, amount: 3)
            }).ToArray();
            var definition = new RunDefinition(drawMode ? "run.draw.2" : "run.holdem-score.2", ScoreProfile.Default,
                new[] { new RunStageDefinition("opening", 200, 6), new RunStageDefinition("middle", 800, 8), new RunStageDefinition("final", 2000, 8) },
                offers.Select(o => o.Id), offers.Select(o => o.Id), new[] { "steady", "focus" });
            return new RunContent(catalog, definition, jokers, offers, "standard52.1", StandardDeck.Cards().Select(card => card.Id));
        }
        public static AbilityRuntime Runtime(bool drawMode = false)
        {
            var swap = new Swap(drawMode ? CardZoneKinds.Hand : HoldemZoneKinds.HoleCards, drawMode ? (Func<AbilityWorld, int>)(w => 0) : ReservedHoldemCards);
            return new AbilityRuntime(new[] {
                new AbilityBehavior(CallEnergy, triggered: new Energy()),
                new AbilityBehavior(SwapCard, activated: swap, selection: swap, cost: new AbilityCost(Chips.Zero, 1)),
                new AbilityBehavior(PeekDeck, activated: new Peek(), cost: new AbilityCost(Chips.Zero, 1))
            });
        }
        private sealed class Energy : ITriggeredAbility
        {
            public IReadOnlyList<EffectIntent> Plan(AbilityContext c) => new EffectIntent[] {
                new ChangeResourceEffect(c.Ability.Id, c.Ability.OwnerId, ResourceKind.Energy, 1),
                new SetAbilityCounterEffect(c.Ability.Id, "calls", checked(c.Ability.Memory.CounterValue("calls") + 1), MemoryScope.Encounter)
            };
        }
        private static int ReservedHoldemCards(AbilityWorld world)
        {
            if (world.PhaseKey == HoldemAbilityAdapter.Phase(Street.Preflop)) return 8;
            if (world.PhaseKey == HoldemAbilityAdapter.Phase(Street.Flop)) return 4;
            return world.PhaseKey == HoldemAbilityAdapter.Phase(Street.Turn) ? 2 : 0;
        }
        private sealed class Swap : IActivatedAbility, IAbilitySelectionHandler
        {
            private readonly Func<AbilityWorld, int> reserved;
            private readonly string handKind;
            public Swap(string handKind, Func<AbilityWorld, int> reserved) { this.handKind = handKind; this.reserved = reserved; }
            public IReadOnlyList<EffectIntent> Plan(AbilityContext c)
            {
                if (c.State.Zones.Single(z => z.Kind == CardZoneKinds.Deck).Cards.Count <= reserved(c.State))
                    throw new System.ArgumentException("Insufficient cards for this mode's exchange policy.");
                var hole = c.State.Zones.Single(z => z.Kind == handKind && z.ControllerId == c.Ability.OwnerId);
                var spec = new SelectionSpec(SelectionTargetKind.Cards,
                    hole.Cards.Select((id, i) => new SelectionCandidate("choice-" + i, EntityRef.Card(id))), 1, 1);
                return new EffectIntent[] { new RequestSelectionEffect(c.Ability.Id, c.Ability.OwnerId, spec) };
            }
            public IReadOnlyList<EffectIntent> Resume(AbilityContext c, int step, SelectionResult selection)
            {
                if (step != 1 || selection.Skipped) throw new System.ArgumentException("Invalid exchange continuation.");
                var card = new CardInstanceId(selection.Selected.Single().Entity.Id);
                var hole = c.State.Zones.Single(z => z.Kind == handKind && z.ControllerId == c.Ability.OwnerId);
                if (!hole.Cards.Contains(card)) throw new System.ArgumentException("Exchange target is no longer held.");
                return new EffectIntent[] { new MoveCardEffect(c.Ability.Id, card, hole.Id, c.State.Zones.Single(z => z.Kind == CardZoneKinds.Discard).Id),
                    new DrawCardsEffect(c.Ability.Id, c.State.Zones.Single(z => z.Kind == CardZoneKinds.Deck).Id, hole.Id, 1) };
            }
        }
        private sealed class Peek : IActivatedAbility
        {
            public IReadOnlyList<EffectIntent> Plan(AbilityContext c)
            {
                var card = c.State.Zones.Single(z => z.Kind == CardZoneKinds.Deck).Cards.Last();
                var face = new BaseCardFaceResolver().Resolve(c.State, c.Catalog, card);
                return new EffectIntent[] { new GrantKnowledgeEffect(c.Ability.Id, new KnowledgeGrant(
                    new KnowledgeGrantId(c.Ability.Id.Value + "/peek/" + c.State.Revision), EntityRef.Ability(c.Ability.Id), KnowledgeAudience.Participant,
                    c.Ability.OwnerId, KnowledgeMode.Snapshot, c.State.ScopeId, card, c.State.Revision, face)) };
            }
        }
    }
}
