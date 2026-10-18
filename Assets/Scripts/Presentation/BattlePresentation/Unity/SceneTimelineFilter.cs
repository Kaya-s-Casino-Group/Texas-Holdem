using System;
using System.Collections.Generic;
using System.Linq;

namespace BattlePresentation.Unity
{
    public enum SceneMatchBoolean { Any, True, False }
    public enum SceneAmountMatch { Any, Zero, Positive }
    public enum SceneValueChange { Any, Increased, Decreased, Unchanged }
    public enum SceneTargetRelation { Any, Observer, OtherThanObserver, Actor, OtherThanActor }

    /// <summary>
    /// All configured predicates must match the same immutable fact. Empty strings mean any value.
    /// Target predicates must match one and the same target, preferring the effect's actual target.
    /// Effect-specific predicates never match a fact without an effect (including false/zero predicates).
    /// </summary>
    [Serializable]
    public sealed class SceneTimelineFilter
    {
        public SceneMatchBoolean effectPresence;
        public string effectKind;
        public string targetId;
        public string targetTeam;
        public SceneTargetRelation targetRelation;
        public string actorId;
        public string actorTeam;
        public SceneTargetRelation actorRelation;
        public SceneAmountMatch healthLost;
        public SceneAmountMatch blockAbsorbed;
        public SceneValueChange healthChange;
        public SceneValueChange blockChange;
        public SceneMatchBoolean isDeath;
        public SceneMatchBoolean skipped;

        public bool Matches(ActionContext context)
        {
            if (context?.Data == null) return false;
            var data = context.Data;
            var effect = data.Effect;
            if (!BooleanMatches(effectPresence, effect != null)) return false;
            if (RequiresEffect && effect == null) return false;
            if (!string.IsNullOrEmpty(effectKind) && !string.Equals(effectKind, effect.Kind, StringComparison.Ordinal)) return false;
            if (effect != null && (!AmountMatches(healthLost, effect.HealthLost) || !AmountMatches(blockAbsorbed, effect.BlockAbsorbed) ||
                !BooleanMatches(isDeath, effect.IsDeath) || !ChangesMatch(effect))) return false;
            var wasSkipped = data.Values.TryGetValue("skipped", out var value) && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
            if (!BooleanMatches(skipped, wasSkipped)) return false;
            if (!SubjectMatches(data.ActorId, actorId, actorTeam, actorRelation, context, false)) return false;
            if (string.IsNullOrEmpty(targetId) && string.IsNullOrEmpty(targetTeam) && targetRelation == SceneTargetRelation.Any) return true;
            IEnumerable<string> targetIds = effect != null ? new[] { effect.TargetId } : data.TargetIds;
            return targetIds.Any(id => SubjectMatches(id, targetId, targetTeam, targetRelation, context, true));
        }

        private bool RequiresEffect => !string.IsNullOrEmpty(effectKind) || healthLost != SceneAmountMatch.Any ||
            blockAbsorbed != SceneAmountMatch.Any || healthChange != SceneValueChange.Any ||
            blockChange != SceneValueChange.Any || isDeath != SceneMatchBoolean.Any;

        private bool ChangesMatch(EffectDisplayData effect)
        {
            if (healthChange == SceneValueChange.Any && blockChange == SceneValueChange.Any) return true;
            if (effect.Before == null || effect.After == null) return false;
            return ChangeMatches(healthChange, effect.Before.Health, effect.After.Health) &&
                ChangeMatches(blockChange, effect.Before.Block, effect.After.Block);
        }

        private static bool ChangeMatches(SceneValueChange change, long before, long after)
        {
            switch (change)
            {
                case SceneValueChange.Any: return true;
                case SceneValueChange.Increased: return after > before;
                case SceneValueChange.Decreased: return after < before;
                case SceneValueChange.Unchanged: return after == before;
                default: return false;
            }
        }

        private static bool SubjectMatches(string id, string wantedId, string team, SceneTargetRelation relation,
            ActionContext context, bool effectTarget)
        {
            if (string.IsNullOrEmpty(wantedId) && string.IsNullOrEmpty(team) && relation == SceneTargetRelation.Any) return true;
            if (string.IsNullOrEmpty(id)) return false;
            if (!string.IsNullOrEmpty(wantedId) && !string.Equals(wantedId, id, StringComparison.Ordinal)) return false;
            var unit = effectTarget && context.Data.Effect?.TargetId == id ? context.Data.Effect.After ?? context.Data.Effect.Before : null;
            unit = unit ?? context.After.Unit(id) ?? context.Before?.Unit(id);
            if (!string.IsNullOrEmpty(team) && (unit == null || !string.Equals(team, unit.Team, StringComparison.Ordinal))) return false;
            switch (relation)
            {
                case SceneTargetRelation.Any: return true;
                case SceneTargetRelation.Observer: return !string.IsNullOrEmpty(context.After.ObserverId) && id == context.After.ObserverId;
                case SceneTargetRelation.OtherThanObserver: return !string.IsNullOrEmpty(context.After.ObserverId) && id != context.After.ObserverId;
                case SceneTargetRelation.Actor: return !string.IsNullOrEmpty(context.Data.ActorId) && id == context.Data.ActorId;
                case SceneTargetRelation.OtherThanActor: return !string.IsNullOrEmpty(context.Data.ActorId) && id != context.Data.ActorId;
                default: return false;
            }
        }

        private static bool BooleanMatches(SceneMatchBoolean match, bool value) => match == SceneMatchBoolean.Any ||
            (match == SceneMatchBoolean.True && value) || (match == SceneMatchBoolean.False && !value);
        private static bool AmountMatches(SceneAmountMatch match, long value) => match == SceneAmountMatch.Any ||
            (match == SceneAmountMatch.Zero && value == 0) || (match == SceneAmountMatch.Positive && value > 0);
    }
}
