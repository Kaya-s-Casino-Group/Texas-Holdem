using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using global::BattlePresentation;

namespace Poker.BattlePresentation
{
    public enum WrathIntentIcon { Unknown, Attack, Defense, Charge }
    public static class WrathPromptText
    {
        public const string Opening = "警告：每存活一只手，暴怒攻击力+5！！";
        public const string Ultimate = "警告：Boss有五只手臂，将于下一回合释放大招！！";
        public static WrathIntentIcon IconFor(string actionId) => actionId == "strike" || actionId == "strike-2"
            ? WrathIntentIcon.Attack : actionId == "guard" ? WrathIntentIcon.Defense :
                actionId == "charge" ? WrathIntentIcon.Charge : WrathIntentIcon.Unknown;
    }
    public sealed class WrathRegenerationPrompt
    {
        public string ActorId { get; }
        public string Text { get; }
        internal WrathRegenerationPrompt(string id, string text) { ActorId = id; Text = text; }
    }
    public sealed class WrathPromptBatch
    {
        public string OpeningText { get; }
        public IReadOnlyList<WrathRegenerationPrompt> Regenerations { get; }
        internal WrathPromptBatch(string opening = null, IEnumerable<WrathRegenerationPrompt> regenerations = null)
        { OpeningText = opening; Regenerations = new List<WrathRegenerationPrompt>(regenerations ?? Array.Empty<WrathRegenerationPrompt>()).AsReadOnly(); }
    }
    public sealed class WrathPromptTracker
    {
        private readonly HashSet<string> opened = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, long> enemyRounds = new Dictionary<string, long>(StringComparer.Ordinal);

        /// <summary>Consume presented authority only. Repeated/older frames never replay one-shot prompts.</summary>
        public WrathPromptBatch Observe(BattleDisplaySnapshot snapshot, bool committed = true)
        {
            if (!committed || snapshot == null || !snapshot.HasStarted || snapshot.IsFinished ||
                !snapshot.Labels.TryGetValue("wrath.body", out var body)) return new WrathPromptBatch();
            string opening = opened.Add(snapshot.BattleId) ? WrathPromptText.Opening : null;
            long round = snapshot.CompletedRounds + 1;
            if (snapshot.Phase != "enemy" || snapshot.Resource(body, "wrath.enemyPhaseActive")?.Value != 1 ||
                enemyRounds.TryGetValue(snapshot.BattleId, out long previous) && round <= previous)
                return new WrathPromptBatch(opening);
            enemyRounds[snapshot.BattleId] = round;
            return new WrathPromptBatch(opening, snapshot.Units.Where(unit => !unit.IsAlive &&
                snapshot.Labels.ContainsKey("wrath.hand/" + unit.Id)).Select(unit => new WrathRegenerationPrompt(unit.Id,
                    (snapshot.Resource(unit.Id, "wrath.reviveRemaining")?.Value ?? 0).ToString(CultureInfo.InvariantCulture) + "个回合后再生")));
        }
    }
}
