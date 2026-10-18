using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using global::BattlePresentation;

namespace BattlePresentation.Unity
{
    [Serializable]
    public sealed class SceneActionDependency
    {
        public string actionId;
        public DependencyKind kind;
        public string marker;
        public ActionDependency Build() => kind == DependencyKind.Started ? ActionDependency.Started(actionId) :
            kind == DependencyKind.Marker ? ActionDependency.Marker(actionId, marker) : ActionDependency.Completed(actionId);
    }
    [Serializable]
    public sealed class SceneActionNode
    {
        public string id;
        public MonoBehaviour action;
        public string targetKey;
        public bool blocking = true;
        public SceneTimelineFilter filter = new SceneTimelineFilter();
        public SceneActionDependency[] after = Array.Empty<SceneActionDependency>();
        public bool Matches(ActionContext context) => filter == null || filter.Matches(context);
        public ActionNode Build()
        {
            if (!(action is IPresentationAction implementation)) throw new InvalidOperationException(id + " must implement IPresentationAction.");
            return new ActionNode(id, implementation, (after ?? Array.Empty<SceneActionDependency>()).Select(x =>
                x?.Build() ?? throw new InvalidOperationException(id + " has a missing dependency.")), blocking, targetKey);
        }
    }
    [Serializable]
    public sealed class SceneTimelineGroup
    {
        public string semanticKey;
        [Tooltip("Empty matches every action at this fact.")]
        public string ruleAction;
        public SceneTimelineFilter filter = new SceneTimelineFilter();
        public bool replaceDefault;
        public SceneActionNode[] nodes = Array.Empty<SceneActionNode>();
        public bool explicitCheckpoint;
        public SceneActionDependency[] checkpoint = Array.Empty<SceneActionDependency>();
        public bool Matches(ActionContext context) => semanticKey == context.Data.SemanticKey &&
            (string.IsNullOrEmpty(ruleAction) || context.Data.Values.TryGetValue("action", out var action) && action == ruleAction) &&
            (filter == null || filter.Matches(context));
    }
    [Serializable]
    public sealed class SceneTargetBinding { public string key; public UnityEngine.Object target; }

    /// <summary>Reusable Unity frontend: no game rules, card library or actor implementation dependencies.</summary>
    [DisallowMultipleComponent]
    public sealed class ScenePresentationDirector : MonoBehaviour, IBattlePresentationFrontend
    {
        [SerializeField] private SceneTargetBinding[] targets = Array.Empty<SceneTargetBinding>();
        [SerializeField] private SceneTimelineGroup[] timeline = Array.Empty<SceneTimelineGroup>();
        private readonly DisplayStore store = new DisplayStore();
        private readonly DictionaryTargetResolver resolver = new DictionaryTargetResolver();
        private PresentationDirector director;
        private IPresentationOperation active;
        private Func<ActionContext, ActionPlan> defaults;
        private readonly HashSet<string> configuredTargetKeys = new HashSet<string>();
        public IDisplayReader Display => store;
        public IReadOnlyList<SceneTimelineGroup> Timeline => Array.AsReadOnly(timeline ?? Array.Empty<SceneTimelineGroup>());
        public IReadOnlyList<SceneTargetBinding> Targets => Array.AsReadOnly(targets ?? Array.Empty<SceneTargetBinding>());
        public PresentationBatch LastBatch { get; private set; }
        public bool HasConfiguredTimeline => timeline != null && timeline.Any(group => group != null &&
            group.nodes != null && group.nodes.Any(node => node != null && node.action != null));
        public bool IsPresenting => director != null && director.IsPresenting;
        public PresentationError LastError => director?.LastError;
        public event Action PlaybackFinished;
        public void Configure(Func<ActionContext, ActionPlan> defaultPlan) { defaults = defaultPlan; EnsureDirector(); }
        public void Bind(string key, object target) => resolver.Bind(key, target);
        public void ConfigureTimeline(SceneTimelineGroup[] groups, SceneTargetBinding[] bindings)
        {
            if (IsPresenting) throw new InvalidOperationException("Synchronize the active presentation before replacing its timeline.");
            // Completed batches may still own nonblocking tails and deferred dependencies. Invalidate their
            // scopes before changing the shared resolver, so old nodes cannot acquire new scene bindings.
            // Call the core directly: configuration changes must not emit another PlaybackFinished event.
            if (store.Current != null) director?.Synchronize(store.Current);
            foreach (var key in configuredTargetKeys) resolver.Bind(key, null);
            configuredTargetKeys.Clear();
            timeline = groups ?? Array.Empty<SceneTimelineGroup>(); targets = bindings ?? Array.Empty<SceneTargetBinding>();
        }
        private void EnsureDirector()
        {
            if (director == null) director = new PresentationDirector(store, resolver, Plan);
            foreach (var binding in targets)
            {
                if (binding == null || string.IsNullOrEmpty(binding.key)) throw new InvalidOperationException("Scene target bindings require a nonempty key.");
                resolver.Bind(binding.key, binding.target); configuredTargetKeys.Add(binding.key);
            }
        }
        private ActionPlan Plan(ActionContext context)
        {
            var plan = defaults?.Invoke(context) ?? ActionPlan.Empty;
            foreach (var group in timeline.Where(x => x != null && x.Matches(context)))
                plan = new ActionPlan((group.replaceDefault ? Enumerable.Empty<ActionNode>() : plan.Nodes).Concat(
                    (group.nodes ?? Array.Empty<SceneActionNode>()).Where(x => x != null && x.Matches(context)).Select(x => x.Build())),
                    group.explicitCheckpoint ? (group.checkpoint ?? Array.Empty<SceneActionDependency>()).Select(x =>
                        x?.Build() ?? throw new InvalidOperationException(group.semanticKey + " has a missing checkpoint dependency.")) :
                        group.replaceDefault ? null : plan.Checkpoint);
            return plan;
        }
        public bool TryGetChange(DisplayCursor cursor, out BattleDisplayChange change)
        {
            change = cursor != null && LastBatch != null && cursor.BatchId == LastBatch.Id
                ? LastBatch.Changes.FirstOrDefault(item => item.Sequence == cursor.Sequence) : null;
            return change != null;
        }
        public void Initialize(BattleDisplaySnapshot snapshot) { EnsureDirector(); LastBatch = null; director.Initialize(snapshot); }
        public IPresentationOperation Present(PresentationBatch batch, PlaybackMode mode)
        {
            EnsureDirector();
            if (director.IsPresenting) return PresentationOperation.Failed("Busy", "The previous presentation has not finished.");
            var previousBatch = LastBatch;
            // The first fact may publish synchronously inside Present. Its DTO must already be discoverable.
            LastBatch = batch;
            var operation = director.Present(batch, isActiveAndEnabled ? mode : PlaybackMode.Instant); active = operation;
            if (operation.Error?.Code == "InvalidBatch") LastBatch = previousBatch;
            operation.Finished += _ => PlaybackFinished?.Invoke();
            if (operation.Status != PresentationStatus.Pending) PlaybackFinished?.Invoke();
            return operation;
        }
        public void Synchronize(BattleDisplaySnapshot snapshot) { EnsureDirector(); director.Synchronize(snapshot); PlaybackFinished?.Invoke(); }
        public void Tick() => director?.Tick();
        private void Update() => Tick();
        private void OnDisable()
        {
            active?.Cancel();
            // Completed operations can still own nonblocking tails. Disable releases those scopes as well.
            if (store.Current != null) director?.Synchronize(store.Current);
        }
    }
}
