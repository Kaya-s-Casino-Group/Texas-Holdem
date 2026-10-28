using System;
using global::BattlePresentation;
using global::BattlePresentation.Unity;

namespace Poker.BattlePresentation
{
    /// <summary>Bridge-owned adapter. Preflights future placements but publishes bindings only with display checkpoints.</summary>
    internal sealed class BattleLayoutFrontend : IBattlePresentationFrontend, IDisposable
    {
        private readonly ScenePresentationDirector director;
        private readonly BattleSceneLayout layout;
        private readonly BattleUnitLayoutOverride[] overrides;
        public BattleLayoutBindings Bindings { get; private set; }
        public bool IsPresenting => director.IsPresenting;
        public BattleLayoutFrontend(ScenePresentationDirector director, BattleSceneLayout layout, BattleUnitLayoutOverride[] overrides)
        {
            this.director = director; this.layout = layout; this.overrides = overrides;
            director.Display.Changed += Changed;
        }
        private BattleLayoutBindings Resolve(BattleDisplaySnapshot snapshot, BattleLayoutBindings previous) =>
            BattleLayoutBindings.Resolve(layout, snapshot, overrides, previous);
        private void Changed(DisplayUpdate update)
        {
            // Independent scene roots have no destruction order. Cleanup synchronization must still
            // cancel the director after the authored Layout has already been destroyed.
            Bindings = layout == null && update.IsSynchronization ? null : Resolve(update.Current, Bindings);
        }
        public void Initialize(BattleDisplaySnapshot snapshot)
        { var initial = Resolve(snapshot, null); Bindings = initial; director.Initialize(snapshot); }
        public IPresentationOperation Present(PresentationBatch batch, PlaybackMode mode)
        {
            var draft = Bindings;
            foreach (var change in batch.Changes) draft = Resolve(change.After, draft);
            Resolve(batch.FinalSnapshot, draft);
            return director.Present(batch, mode);
        }
        public void Synchronize(BattleDisplaySnapshot snapshot)
        {
            if (layout != null) Resolve(snapshot, Bindings);
            director.Synchronize(snapshot);
        }
        public void Dispose() => director.Display.Changed -= Changed;
    }
}
