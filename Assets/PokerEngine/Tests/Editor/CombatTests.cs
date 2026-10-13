using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace PokerEngine.Tests
{
    [TestFixture]
    public sealed class CombatTests
    {
        private static readonly ParticipantId Player = new ParticipantId("player");
        private static readonly ParticipantId Enemy = new ParticipantId("enemy");
        private static CombatState Create(int block = 3) => new CombatState("combat", 0, new[] {
            new CombatantDefinition("hero", 20).Create(Player, "allies"),
            new CombatantState(Enemy, "slime", "enemies", 10, 10, block) });
        private static CombatEffect Damage(long amount) => new CombatEffect(CombatEffectKind.Damage, Player, Enemy, amount);

        [Test] public void PlayerAndMonsterShareVitalsWithoutControllersOrDecks()
        {
            var state = Create();
            Assert.That(state.Get(Player).GetType(), Is.EqualTo(state.Get(Enemy).GetType()));
            Assert.Throws<ArgumentException>(() => new CombatantDefinition("bad", 0));
            Assert.Throws<ArgumentException>(() => new CombatantState(Player, "hero", "ally", 10, -1));
            Assert.Throws<ArgumentException>(() => new CombatantDefinition("hero", 10).Create(Player, "ally", 11));
            Assert.Throws<ArgumentException>(() => new CombatState("bad", 0, new[] { state.Get(Player), state.Get(Player) }));
        }
        [Test] public void DamageConsumesBlockBeforeHealthAndPreservesUnitIdentity()
        {
            var before = Create(); var result = CombatEngine.Resolve(before, 0, new[] { Damage(7) });
            Assert.That(result.Accepted, Is.True); Assert.That(result.State.Revision, Is.EqualTo(1));
            Assert.That(result.State.Get(Enemy).Health, Is.EqualTo(6)); Assert.That(result.State.Get(Enemy).Block, Is.Zero);
            Assert.That(result.State.Get(Enemy).DefinitionId, Is.EqualTo("slime"));
            Assert.That(result.State.Get(Enemy).TeamId, Is.EqualTo("enemies"));
            Assert.That(result.State.Get(Enemy).MaxHealth, Is.EqualTo(10));
            Assert.That(result.Events[0].BlockAbsorbed, Is.EqualTo(3)); Assert.That(result.Events[0].HealthLost, Is.EqualTo(4));
            Assert.That(result.Events[0].Died, Is.False); Assert.That(before.Get(Enemy).Health, Is.EqualTo(10));
            Assert.That(result.Events[0].Before, Is.SameAs(before.Get(Enemy)));
        }
        [Test] public void OverkillClampsHealthAndReportsDeathOnce()
        {
            var result = CombatEngine.Resolve(Create(), 0, new[] { Damage(long.MaxValue) });
            Assert.That(result.Accepted, Is.True); Assert.That(result.State.Get(Enemy).Health, Is.Zero);
            Assert.That(result.Events[0].Died, Is.True); Assert.That(result.Events[0].Overkill, Is.EqualTo(long.MaxValue - 13));
            var repeat = CombatEngine.Resolve(result.State, 1, new[] { Damage(1) });
            Assert.That(repeat.Error, Is.EqualTo(CombatError.InvalidEffect)); Assert.That(repeat.Events, Is.Empty);
            var revive = CombatEngine.Resolve(result.State, 1, new[] { new CombatEffect(CombatEffectKind.Heal, Player, Enemy, 1) });
            Assert.That(revive.Accepted, Is.False);
        }
        [Test] public void HealCapsAtMaximumAndBlockLifecycleIsExplicit()
        {
            var hurt = CombatEngine.Resolve(Create(), 0, new[] { Damage(8) }).State;
            var healed = CombatEngine.Resolve(hurt, 1, new[] { new CombatEffect(CombatEffectKind.Heal, Enemy, Enemy, long.MaxValue),
                new CombatEffect(CombatEffectKind.GainBlock, Enemy, Enemy, 9) });
            Assert.That(healed.State.Get(Enemy).Health, Is.EqualTo(10)); Assert.That(healed.State.Get(Enemy).Block, Is.EqualTo(9));
            Assert.That(healed.Events[0].HealthRestored, Is.EqualTo(5)); Assert.That(healed.Events[1].Index, Is.EqualTo(1));
            var cleared = CombatEngine.Resolve(healed.State, 2, new[] { new CombatEffect(CombatEffectKind.ClearBlock, Enemy, Enemy) });
            Assert.That(cleared.State.Get(Enemy).Block, Is.Zero);
        }
        [Test] public void InvalidLaterEffectRollsBackWholeBatch()
        {
            var before = Create();
            var result = CombatEngine.Resolve(before, 0, new[] { Damage(100), new CombatEffect(CombatEffectKind.Damage, Enemy, Player, 1) });
            Assert.That(result.Error, Is.EqualTo(CombatError.InvalidEffect)); Assert.That(result.State, Is.SameAs(before));
            Assert.That(result.Events, Is.Empty); Assert.That(before.Get(Enemy).Health, Is.EqualTo(10));
        }
        [Test] public void OverflowAndStaleCommandsProduceNoPartialEffects()
        {
            var before = Create();
            var result = CombatEngine.Resolve(before, 0, new[] { Damage(1), new CombatEffect(CombatEffectKind.GainBlock, Player, Enemy, int.MaxValue) });
            Assert.That(result.Error, Is.EqualTo(CombatError.Overflow)); Assert.That(result.State, Is.SameAs(before)); Assert.That(result.Events, Is.Empty);
            Assert.That(CombatEngine.Resolve(before, 1, new[] { Damage(1) }).Error, Is.EqualTo(CombatError.StaleRevision));
            var max = new CombatState(before.Id, long.MaxValue, before.Combatants);
            Assert.That(CombatEngine.Resolve(max, long.MaxValue, new[] { Damage(1) }).Error, Is.EqualTo(CombatError.Overflow));
        }
        [Test] public void CombatCollectionsAreIsolatedAndStatusesKeepOneAuthoritativeLocation()
        {
            var units = new[] { Create().Get(Player), Create().Get(Enemy) };
            var status = new StatusInstance(new StatusInstanceId("status"), new StatusDefinitionId("mark"),
                EntityRef.Participant(Player), EntityRef.Participant(Enemy), 1, StatusDuration.UntilRemoved());
            var statuses = new[] { status }; var state = new CombatState("combat", 0, units, statuses);
            units[0] = null; statuses[0] = null;
            Assert.That(state.Combatants[0], Is.Not.Null); Assert.That(state.Statuses[0], Is.SameAs(status));
            Assert.Throws<NotSupportedException>(() => ((IList<CombatantState>)state.Combatants)[0] = null);
            var after = CombatEngine.Resolve(state, 0, new[] { Damage(1) }).State;
            Assert.That(after.Statuses[0], Is.SameAs(status));
            Assert.Throws<ArgumentException>(() => new CombatState("combat", 0, state.Combatants, new[] { status, status }));
            var foreign = new StatusInstance(new StatusInstanceId("foreign"), status.DefinitionId, status.Source,
                EntityRef.Participant(new ParticipantId("absent")), 1, status.Duration);
            Assert.Throws<ArgumentException>(() => new CombatState("combat", 0, state.Combatants, new[] { foreign }));
        }
        private sealed class ReadCurrentHealth : IIncomingDamageModifier
        {
            private readonly bool rejectAfterDamage;
            public ReadCurrentHealth(bool rejectAfterDamage = false) { this.rejectAfterDamage = rejectAfterDamage; }
            public DamageModification Modify(DamageModifierContext c)
            {
                long health = c.Combat.Get(c.Target).Health;
                if (rejectAfterDamage && health < 10) return null;
                return new DamageModification("current-health.1", "按当前生命承伤", c.Amount, health / 2);
            }
        }
        private sealed class ZeroIncoming : IIncomingDamageModifier
        {
            public DamageModification Modify(DamageModifierContext c) => new DamageModification("immune.1", "免疫", c.Amount, 0);
        }
        [Test] public void IncomingModifierReadsStateAfterEarlierEffectsInTheSameBatch()
        {
            var state = Create(0);
            var result = CombatEngine.Resolve(state, 0, new[] { Damage(100), Damage(100) }, new ReadCurrentHealth());
            Assert.IsTrue(result.Accepted); Assert.AreEqual(5, result.Events[0].SettledDamage);
            Assert.AreEqual(2, result.Events[1].SettledDamage); Assert.AreEqual(3, result.State.Get(Enemy).Health);
            Assert.AreEqual(10, state.Get(Enemy).Health);
        }
        [Test] public void InvalidLaterIncomingModifierRollsBackEarlierDamage()
        {
            var state = Create(0);
            var result = CombatEngine.Resolve(state, 0, new[] { Damage(100), Damage(100) }, new ReadCurrentHealth(true));
            Assert.AreEqual(CombatError.InvalidDamageModifier, result.Error);
            Assert.AreSame(state, result.State); Assert.IsEmpty(result.Events);
        }
        [Test] public void ZeroIncomingDoesNotConsumeBlockAndDoesNotModifyNonDamageEffects()
        {
            var result = CombatEngine.Resolve(Create(), 0, new[] { Damage(long.MaxValue),
                new CombatEffect(CombatEffectKind.GainBlock, Enemy, Enemy, 2) }, new ZeroIncoming());
            Assert.IsTrue(result.Accepted); Assert.AreEqual(0, result.Events[0].HealthLost);
            Assert.AreEqual(0, result.Events[0].BlockAbsorbed); Assert.AreEqual(0, result.Events[0].Overkill);
            Assert.AreEqual(5, result.State.Get(Enemy).Block); Assert.AreEqual(10, result.State.Get(Enemy).Health);
            Assert.IsNull(result.Events[1].IncomingModification);
        }
        [Test] public void ResolverSupportsAlliesAndMultipleOpponentsWithoutOwningTargetPolicy()
        {
            var ally = new ParticipantId("ally");
            var state = new CombatState("combat", 0, new[] { Create().Get(Player), Create().Get(Enemy), new CombatantDefinition("ally", 5).Create(ally, "allies") });
            var result = CombatEngine.Resolve(state, 0, new[] { new CombatEffect(CombatEffectKind.Damage, Player, ally, 2), Damage(1) });
            Assert.That(result.Accepted, Is.True); Assert.That(result.State.Get(ally).Health, Is.EqualTo(3));
            // Friendly-fire legality is a mode concern; the common resolver only validates references and vitals.
            Assert.That(result.State.Combatants.Count, Is.EqualTo(3));
        }
    }
}
