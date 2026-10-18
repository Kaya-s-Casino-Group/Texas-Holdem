using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BattlePresentation.Unity
{
    [Serializable]
    public sealed class SceneCandidateRoute
    {
        public bool filterState;
        public CandidateState state = CandidateState.Valid;
        [Tooltip("Empty matches every pattern; otherwise use the semantic pattern key from the preview model.")]
        public string patternKey;
        public MonoBehaviour[] feedback = Array.Empty<MonoBehaviour>();
        public bool Matches(CandidatePresentationModel model) => (!filterState || model.State == state) &&
            (string.IsNullOrEmpty(patternKey) || model.PatternKey == patternKey);
    }

    /// <summary>Candidate states route to arbitrary feedback adapters. Empty configuration is intentionally inert.</summary>
    [DisallowMultipleComponent]
    public sealed class SceneCandidateFeedback : MonoBehaviour, IInteractionFeedback
    {
        [SerializeField] private SceneCandidateRoute[] routes = Array.Empty<SceneCandidateRoute>();
        private sealed class Binding
        {
            public MonoBehaviour Component;
            public IInteractionFeedback Feedback;
            public PresentationScope Scope;
        }
        private readonly List<Binding> active = new List<Binding>();
        private PresentationScope parent;
        public void Configure(SceneCandidateRoute[] value) { ReleaseAll(ReleaseMode.Replaced); routes = value ?? Array.Empty<SceneCandidateRoute>(); }
        public void Reconcile(CandidatePresentationModel next, PresentationScope scope)
        {
            if (!isActiveAndEnabled || !scope.IsActive) return;
            if (!ReferenceEquals(parent, scope)) { ReleaseAll(ReleaseMode.Replaced); parent = scope; }
            var desired = routes.Where(route => route != null && route.Matches(next))
                .SelectMany(route => route.feedback ?? Array.Empty<MonoBehaviour>()).Where(component => component != null && component != this)
                .Distinct().ToArray();
            for (int i = active.Count - 1; i >= 0; --i)
                if (!desired.Contains(active[i].Component)) { ReleaseBinding(active[i], ReleaseMode.Replaced); active.RemoveAt(i); }
            foreach (var component in desired)
            {
                if (!(component is IInteractionFeedback feedback))
                    throw new InvalidOperationException(component.name + " must implement IInteractionFeedback.");
                var binding = active.FirstOrDefault(value => value.Component == component);
                if (binding == null)
                {
                    binding = new Binding { Component = component, Feedback = feedback,
                        Scope = new PresentationScope(scope.Id + "/" + component.GetInstanceID()) };
                    active.Add(binding); scope.Own(binding.Scope);
                }
                feedback.Reconcile(next, binding.Scope);
            }
        }
        public void Release(PresentationScope scope, ReleaseMode mode)
        { if (ReferenceEquals(parent, scope)) ReleaseAll(mode); }
        private void ReleaseAll(ReleaseMode mode)
        {
            foreach (var binding in active) ReleaseBinding(binding, mode);
            active.Clear(); parent = null;
        }
        private static void ReleaseBinding(Binding binding, ReleaseMode mode)
        {
            try { binding.Scope.Dispose(); }
            finally { if (binding.Component != null) binding.Feedback.Release(binding.Scope, mode); }
        }
        private void OnDisable() => ReleaseAll(ReleaseMode.Synchronized);
    }
}
