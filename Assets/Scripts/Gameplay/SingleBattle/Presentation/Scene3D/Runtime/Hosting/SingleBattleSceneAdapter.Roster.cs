using System;
using System.Collections.Generic;
using System.Linq;
using PokerEngine;
using UnityEngine;

namespace Poker.SingleBattle
{
    public sealed partial class SingleBattleSceneAdapter
    {
        private readonly Dictionary<BattleActor, ParticipantId> actorParticipants = new Dictionary<BattleActor, ParticipantId>();
        private readonly List<EnemyBattleActor> enemyPool = new List<EnemyBattleActor>();
        /// <summary>Optional encounter-specific visual factory. Returned actors must be configured for this adapter.</summary>
        public Func<ParticipantId, EnemyBattleActor> EnemyActorFactory { get; set; }

        // Resolve identities from the displayed snapshot: a summoned unit appears only when its fact is revealed.
        private void ReconcileActors(bool reset = false)
        {
            if (State == null) return;
            if (State.Duel.Players.Count != Players.Count) throw new InvalidOperationException("The player roster changed.");
            foreach (var actor in enemyActors) if (actor != null && !enemyPool.Contains(actor)) enemyPool.Add(actor);
            enemyPool.RemoveAll(actor => actor == null);
            var next = new List<EnemyBattleActor>();
            foreach (var enemy in State.Duel.Enemies)
            {
                var actor = enemyPool.FirstOrDefault(candidate => !next.Contains(candidate) &&
                    actorParticipants.TryGetValue(candidate, out var id) && id == enemy.Id);
                if (actor == null)
                    actor = enemyPool.FirstOrDefault(candidate => !next.Contains(candidate) &&
                        (!actorParticipants.TryGetValue(candidate, out var id) || !State.Duel.Enemies.Any(unit => unit.Id == id)));
                if (actor == null)
                {
                    actor = EnemyActorFactory?.Invoke(enemy.Id) ?? CloneEnemy(enemy.Id, next.Count);
                    if (actor == null || actor.Battle != this || actor.gameObject.scene != gameObject.scene || enemyPool.Contains(actor))
                        throw new InvalidOperationException("The enemy factory must return a new actor owned by this scene adapter.");
                    enemyPool.Add(actor);
                }
                next.Add(actor);
            }
            enemyActors = next.ToArray();
            foreach (var actor in enemyPool.Where(actor => !next.Contains(actor)))
            {
                if (actorParticipants.TryGetValue(actor, out var removed)) presentationDirector?.Bind(removed.Value, null);
                actorParticipants.Remove(actor); actor.gameObject.SetActive(false);
            }
            for (int index = 0; index < playerActors.Length; index++) BindActor(playerActors[index], State.Duel.Players[index].Id, reset);
            for (int index = 0; index < enemyActors.Length; index++) BindActor(enemyActors[index], State.Duel.Enemies[index].Id, reset);
        }

        private void BindActor(BattleActor actor, ParticipantId id, bool reset)
        {
            bool changed = !actorParticipants.TryGetValue(actor, out var previous) || previous != id;
            if (changed && previous.IsValid) presentationDirector?.Bind(previous.Value, null);
            actorParticipants[actor] = id;
            if (changed || reset || !actor.IsBound) actor.Bind(id);
            if (!actor.gameObject.activeSelf) actor.gameObject.SetActive(true);
            presentationDirector?.Bind(id.Value, actor);
        }

        private EnemyBattleActor CloneEnemy(ParticipantId id, int index)
        {
            var template = enemyPool.FirstOrDefault();
            if (template == null) throw new InvalidOperationException("Bind an enemy template or an enemy visual factory.");
            var clone = Instantiate(template.gameObject, template.transform.parent).GetComponent<EnemyBattleActor>();
            if (clone.gameObject.scene != gameObject.scene)
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(clone.gameObject, gameObject.scene);
            clone.name = "Enemy " + id.Value;
            clone.transform.localPosition = template.transform.localPosition + Vector3.right * (2.4f * index);
            clone.Configure(this, clone.Visual);
            return clone;
        }
    }
}
