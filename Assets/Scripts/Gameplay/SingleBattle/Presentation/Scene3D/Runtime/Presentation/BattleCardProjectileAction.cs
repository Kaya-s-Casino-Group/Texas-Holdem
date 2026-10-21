using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CardPresentation;
using global::BattlePresentation;
using global::BattlePresentation.Unity;
using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>Scene target binding plus a custom card-zone action. Consumes resolved display data only.</summary>
    public sealed class BattleCardProjectileAction : PresentationActionBehaviour, ICardZonePresentationAction
    {
        public const string ActionKey = "cards.projectile";
        [SerializeField] private WorldCardZonePresentation source;
        [SerializeField, Min(.01f)] private float liftSeconds = .12f;
        [SerializeField, Min(.01f)] private float flightSeconds = .42f;
        [SerializeField, Min(0)] private float volleyInterval = .045f;
        [SerializeField, Min(.01f)] private float straightInterval = .1f;
        [SerializeField, Range(0, 1)] private float arcHeight = .18f;
        [SerializeField, Range(.05f, 1), Tooltip("Fraction of flight used to turn the card face onto the horizontal world plane.")] private float horizontalAt = .45f;
        [SerializeField] private Material tailMaterial;
        [SerializeField, Range(.05f, 1), InspectorName("计分牌拖尾宽度比例")] private float tailWidth = .55f;
        [SerializeField, Min(.01f)] private float tailSeconds = .18f;
        [SerializeField] private Color tailColor = new Color(1, .68f, .22f, .85f);
        private readonly List<Run> runs = new List<Run>();
        private readonly Dictionary<Renderer, HiddenSource> hidden = new Dictionary<Renderer, HiddenSource>();
        private readonly Dictionary<string, Binding> bindings = new Dictionary<string, Binding>();
        private readonly Dictionary<string, Run> waves = new Dictionary<string, Run>();
        private sealed class Shot
        { internal string ViewId, TargetId, CueId; internal Vector3 Destination; internal int Lane, Count, Order; internal bool Unscored; }
        private sealed class Binding
        { internal string Id, Category; internal bool Royal, Sequential; internal Shot[] Shots; }
        private sealed class HiddenSource { internal bool Previous; internal int Owners; }
        private sealed class Ghost
        {
            internal GameObject Object;
            internal MeshRenderer Mesh;
            internal TrailRenderer Tail;
            internal Renderer Original;
            internal Vector3 Start;
            internal Quaternion Rotation;
            internal float Size, Delay;
            internal Shot Shot;
            internal float ArrivedAt;
            internal bool Arrived, SourceHidden;
        }
        private sealed class Run
        {
            internal PresentationOperation Operation;
            internal readonly List<Ghost> Cards = new List<Ghost>();
            internal Action CancelRoutine;
            internal bool Released;
            internal Binding Binding;
            internal float Clock;
            internal readonly List<Waiter> Waiters = new List<Waiter>();
            internal readonly List<Pulse> Pulses = new List<Pulse>();
            internal readonly HashSet<string> Pulsed = new HashSet<string>();
        }
        private sealed class Waiter
        { internal string CueId; internal PresentationOperation Operation; internal IMarkerSink Markers; internal bool Hit; internal CardZonePresentationRequest Request; }
        private sealed class Pulse
        { internal GameObject Object; internal LineRenderer Line; internal float Started, Radius; }
        public override ActionDescriptor Descriptor => new ActionDescriptor(ActionKey, new[] { "Released", "Impact", "AllArrived" }, new[] { "cards.projectile" });
        public void Configure(WorldCardZonePresentation frontend, Material tail)
        { source = frontend; tailMaterial = tail; frontend.RegisterCustomAction(this); }

        private static IPresentationOperation Pass(IMarkerSink markers)
        { markers.Emit("Released"); markers.Emit("Impact"); markers.Emit("AllArrived"); return PresentationOperation.Completed(); }

        public override IPresentationOperation Play(ActionContext context, IMarkerSink markers)
        {
            var effect = context.Data.Effect;
            if (effect == null || (effect.Kind != "Damage" && effect.Kind != "Eliminate") ||
                effect.SourceId != context.Before.ObserverId || context.Data.ActorId != context.Before.ObserverId ||
                !context.Data.Values.TryGetValue("action", out var action) || action != "Play" ||
                context.After.Unit(effect.TargetId)?.Team != "enemy") return Pass(markers);
            if (source == null) return PresentationOperation.Failed("MissingCardZone", name);
            try
            {
                var before = context.Before.Zones.FirstOrDefault(zone => zone.Id == source.Current?.Id);
                var after = context.After.Zones.FirstOrDefault(zone => zone.Id == before?.Id);
                if (before == null || after == null) return PresentationOperation.Failed("MissingCardZoneSnapshot", name);
                var values = context.Data.Values.ToDictionary(pair => pair.Key, pair => pair.Value);
                var bridge = GetComponentInParent<BattleCardEffectBridge>();
                string[] ids;
                if (bridge != null)
                {
                    // A missing cue is a derived effect or a non-card action, never another full-hand volley.
                    if (!bridge.TryGet(context.Data.Sequence, out var cue)) return Pass(markers);
                    var key = context.BatchId + "/" + cue.Flight.Id;
                    if (!bindings.TryGetValue(key, out var binding))
                    {
                        var shots = cue.Flight.Shots.Select(shot => new Shot {
                            ViewId = before.Cards.Single(card => card.CardId == shot.CardId || card.ViewId == shot.CardId).ViewId,
                            TargetId = shot.TargetId, CueId = shot.CueId, Order = shot.Order, Destination = Destination(context, shot.TargetId)
                        }).ToArray();
                        shots = shots.Concat(cue.Flight.UnscoredCards.Select(id => new Shot {
                            ViewId = before.Cards.Single(card => card.CardId == id || card.ViewId == id).ViewId,
                            CueId = "unscored", Unscored = true, Destination = source.GetComponent<HandZoneView>().DiscardWorldPosition
                        })).ToArray();
                        foreach (var group in shots.GroupBy(shot => shot.CueId))
                        { int lane = 0; foreach (var shot in group) { shot.Lane = lane++; shot.Count = group.Count(); } }
                        bindings.Add(key, binding = new Binding { Id = key, Category = cue.Flight.Category, Royal = cue.Flight.Royal, Sequential = cue.Flight.Sequential, Shots = shots });
                        context.Scope.Own(() => bindings.Remove(key));
                    }
                    values["scene.flight"] = key; values["scene.cue"] = cue.Id;
                    ids = binding.Shots.Select(shot => shot.ViewId).Distinct().ToArray();
                }
                else
                {
                    // Unconfigured fixtures and other scenes retain their existing single-target card action.
                    var destination = Destination(context, effect.TargetId);
                    values["destination.x"] = destination.x.ToString(CultureInfo.InvariantCulture);
                    values["destination.y"] = destination.y.ToString(CultureInfo.InvariantCulture);
                    values["destination.z"] = destination.z.ToString(CultureInfo.InvariantCulture);
                    ids = context.Data.CardIds.Where(id => before.Cards.Any(card => card.CardId == id || card.ViewId == id)).ToArray();
                }
                return source.Play(new CardZonePresentationRequest(ActionKey, context.ActionInstanceId, context.Scope,
                    before, after, ids, parameters: values), markers);
            }
            catch (Exception error) { return PresentationOperation.Failed("CardProjectileBindingFailed", error.Message); }
        }

        private static Vector3 Destination(ActionContext context, string targetId)
        {
            var target = context.Targets?.Resolve(targetId, context) as Component;
            if (target == null) throw new InvalidOperationException("Missing projectile target: " + targetId);
            var actor = target as BattleActor;
            var visual = actor != null && actor.Visual != null ? actor.Visual : target.transform;
            var renderers = visual.GetComponentsInChildren<Renderer>().Where(renderer => renderer.enabled && !(renderer is ParticleSystemRenderer)).ToArray();
            if (renderers.Length == 0) return visual.position;
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds.center;
        }

        public IPresentationOperation Play(CardZonePresentationRequest request, IMarkerSink markers)
        {
            if (!isActiveAndEnabled) return PresentationOperation.Failed("ActionDisabled", name);
            if (request.Mode == PlaybackMode.Instant || request.ViewIds.Count == 0) return Pass(markers);
            var hand = source != null ? source.GetComponent<HandZoneView>() : null;
            if (hand == null || hand.FacingCamera == null || tailMaterial == null)
                return PresentationOperation.Failed("MissingProjectileVisualBinding", name);
            Run run = null;
            try
            {
                Binding binding;
                string cueId = "local";
                if (request.Parameters.TryGetValue("scene.flight", out var key))
                { binding = bindings[key]; cueId = request.Parameters["scene.cue"]; waves.TryGetValue(key, out run); }
                else
                {
                    float Number(string field) => float.Parse(request.Parameters[field], CultureInfo.InvariantCulture);
                    var destination = new Vector3(Number("destination.x"), Number("destination.y"), Number("destination.z"));
                    if (!float.IsFinite(destination.x) || !float.IsFinite(destination.y) || !float.IsFinite(destination.z))
                        throw new ArgumentException("Projectile destination must be finite.");
                    request.Parameters.TryGetValue("handCategory", out var category);
                    binding = new Binding { Category = category, Shots = request.ViewIds.Select((id, index) =>
                        new Shot { ViewId = id, Destination = destination, CueId = cueId, Lane = index, Count = request.ViewIds.Count }).ToArray() };
                }
                bool created = run == null;
                if (created)
                {
                    run = new Run { Binding = binding };
                    var owned = run;
                    run.Operation = new PresentationOperation(() => Release(owned)); runs.Add(run);
                    if (binding.Id != null) waves.Add(binding.Id, run);
                    request.Scope.Own(() => { if (owned.Operation.Status == PresentationStatus.Pending) owned.Operation.Cancel(); else Release(owned); });
                    foreach (var shot in binding.Shots) Spawn(run, hand, shot);
                }
                var waiter = new Waiter { CueId = cueId, Markers = markers, Request = request };
                var current = run;
                waiter.Operation = new PresentationOperation(() => current.Waiters.Remove(waiter));
                run.Waiters.Add(waiter);
                request.Scope.Own(() => { if (waiter.Operation.Status == PresentationStatus.Pending) waiter.Operation.Cancel(); current.Waiters.Remove(waiter); });
                markers.Emit("Released"); Poll(run, waiter);
                if (created) run.CancelRoutine = StartRoutine(Fly(run, hand.FacingCamera));
                return waiter.Operation;
            }
            catch (Exception error)
            {
                if (run != null) { run.Operation.TryFail("CardProjectileFailed", error.Message, error); Release(run); }
                return PresentationOperation.Failed("CardProjectileFailed", error.Message);
            }
        }

        private void Spawn(Run run, HandZoneView hand, Shot shot)
        {
            if (!hand.TryGetView(shot.ViewId, out var card) || card.Surface == null)
                throw new InvalidOperationException("Missing played card visual: " + shot.ViewId);
            var original = card.Surface.GetComponent<MeshRenderer>();
            var go = new GameObject(shot.Unscored ? "Unscored Card " + card.ViewId : "Card Projectile " + card.ViewId + " -> " + shot.TargetId, typeof(MeshFilter), typeof(MeshRenderer));
            go.layer = card.gameObject.layer; go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(card.Surface.transform.position, card.Surface.transform.rotation);
            go.transform.localScale = Vector3.Scale(card.Surface.transform.lossyScale, new Vector3(
                1 / transform.lossyScale.x, 1 / transform.lossyScale.y, 1 / transform.lossyScale.z));
            var ghost = new Ghost { Object = go, Original = original, Start = go.transform.position, Shot = shot,
                Rotation = go.transform.rotation, Size = Mathf.Max(original.bounds.size.x, original.bounds.size.y, original.bounds.size.z),
                Delay = shot.Unscored ? 0 : run.Binding.Sequential ? shot.Order * straightInterval : LaunchDelay(run.Binding.Category, shot.Lane) * volleyInterval };
            run.Cards.Add(ghost);
            go.GetComponent<MeshFilter>().sharedMesh = card.Surface.GetComponent<MeshFilter>().sharedMesh;
            ghost.Mesh = go.GetComponent<MeshRenderer>(); ghost.Mesh.sharedMaterials = original.sharedMaterials;
            var block = new MaterialPropertyBlock(); original.GetPropertyBlock(block); ghost.Mesh.SetPropertyBlock(block);
            if (!shot.Unscored)
            {
                var tail = new GameObject("Tail", typeof(TrailRenderer)); tail.layer = go.layer; tail.transform.SetParent(go.transform, false);
                var meshBounds = go.GetComponent<MeshFilter>().sharedMesh.bounds;
                tail.transform.localPosition = meshBounds.center + Vector3.back * meshBounds.extents.z;
                tail.transform.localRotation = Quaternion.Euler(90, 0, 0);
                ghost.Tail = tail.GetComponent<TrailRenderer>(); ghost.Tail.sharedMaterial = tailMaterial;
                ghost.Tail.time = tailSeconds; ghost.Tail.minVertexDistance = Mathf.Max(.001f, ghost.Size * .04f);
                ghost.Tail.widthMultiplier = meshBounds.size.x * Mathf.Abs(go.transform.lossyScale.x) * tailWidth;
                ghost.Tail.widthCurve = AnimationCurve.Linear(0, 1, 1, 0); ghost.Tail.alignment = LineAlignment.TransformZ;
                var color = run.Binding.Royal ? new Color(1, .9f, .35f, 1) : tailColor;
                ghost.Tail.startColor = color; ghost.Tail.endColor = color;
                ghost.Tail.emitting = false; ghost.Tail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (!hidden.TryGetValue(original, out var state)) hidden.Add(original, state = new HiddenSource { Previous = original.forceRenderingOff });
            state.Owners++; original.forceRenderingOff = true; ghost.SourceHidden = true;
        }

        private void Poll(Run run, Waiter waiter)
        {
            if (waiter.Operation.Status != PresentationStatus.Pending) return;
            if (!waiter.Request.Scope.IsActive) { waiter.Operation.Cancel(); return; }
            var cards = run.Cards.Where(card => card.Shot.CueId == waiter.CueId).ToArray();
            if (cards.Length == 0) throw new InvalidOperationException("The projectile cue has no visual cards.");
            if (cards.Any(card => !card.Arrived)) return;
            if (!waiter.Hit) { waiter.Hit = true; waiter.Markers.Emit("Impact"); waiter.Markers.Emit("AllArrived"); }
            if (run.Clock - cards.Max(card => card.ArrivedAt) >= tailSeconds) waiter.Operation.TryComplete();
        }

        public static int LaunchDelay(string category, int index) => category == "Straight" || category == "StraightFlush" ? index :
            category == "TwoPair" ? index / 2 : category == "FullHouse" ? (index < 3 ? 0 : 2) : 0;

        public static Quaternion FlightRotation(Quaternion start, Vector3 direction, float progress)
        {
            var heading = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (heading.sqrMagnitude < .000001f) heading = Vector3.forward;
            return Quaternion.Slerp(start, Quaternion.LookRotation(heading, Vector3.up), Mathf.SmoothStep(0, 1, progress));
        }

        // Linear travel plus a quadratic height offset gives a true parabola with constant acceleration.
        // Categories vary the arc's height/plane and launch cadence, while retaining exact endpoints.
        public static Vector3 Trajectory(string category, float t, int index, int count, Vector3 start, Vector3 destination,
            Vector3 up, Vector3 right, float arc)
        {
            float envelope = 4 * t * (1 - t);
            float lane = index - (count - 1) * .5f;
            var position = Vector3.Lerp(start, destination, t);
            float lateral = 0, height = 1;
            switch (category)
            {
                case "OnePair": lateral = lane * .3f; height = .9f; break;
                case "TwoPair": lateral = (index % 2 == 0 ? -1 : 1) * .45f; height = 1.05f; break;
                case "ThreeOfAKind": lateral = lane * .35f; height = 1.1f + index * .15f; break;
                case "Straight": lateral = lane * .08f; height = .65f; break;
                case "Flush": lateral = lane * .55f; height = 1.1f; break;
                case "FullHouse": lateral = index < 3 ? -.5f : .5f; height = index < 3 ? .7f : 1.3f; break;
                case "FourOfAKind": lateral = lane * .12f; height = 1.8f; break;
                case "StraightFlush": lateral = lane * .3f; height = 1.35f; break;
                case "FiveOfAKind": lateral = Mathf.Cos(index * Mathf.PI * 2 / Mathf.Max(1, count)) * 1.3f;
                    height = 1.5f + Mathf.Sin(index * Mathf.PI * 2 / Mathf.Max(1, count)) * .3f; break;
            }
            return position + (up * height + right * lateral) * (envelope * arc);
        }

        private IEnumerator Fly(Run run, Camera camera)
        {
            while (run.Operation.Status == PresentationStatus.Pending)
            {
                try
                {
                    if (camera == null) throw new InvalidOperationException("Projectile camera was removed during flight.");
                    foreach (var card in run.Cards)
                    {
                        if (card.Arrived) continue;
                        float time = run.Clock - card.Delay;
                        card.Mesh.enabled = time >= 0;
                        if (card.Shot.Unscored)
                        {
                            float fall = Mathf.Clamp01(time / (liftSeconds + flightSeconds));
                            card.Object.transform.position = Vector3.Lerp(card.Start, card.Shot.Destination, fall * fall);
                            card.Object.transform.rotation = card.Rotation * Quaternion.Euler(0, 0, fall * (card.Shot.Lane % 2 == 0 ? 16 : -16));
                            if (fall >= 1) { card.Arrived = true; card.ArrivedAt = run.Clock; card.Mesh.enabled = false; }
                            continue;
                        }
                        var raised = card.Start + camera.transform.up * card.Size * .65f;
                        float t = Mathf.Clamp01((time - liftSeconds) / flightSeconds);
                        card.Object.transform.position = time <= liftSeconds
                            ? Vector3.Lerp(card.Start, raised, Mathf.SmoothStep(0, 1, Mathf.Clamp01(time / liftSeconds)))
                            : Trajectory(run.Binding.Category, t, card.Shot.Lane, card.Shot.Count, raised, card.Shot.Destination,
                                camera.transform.up, camera.transform.right, Vector3.Distance(raised, card.Shot.Destination) * arcHeight);
                        card.Tail.emitting = time > liftSeconds;
                        card.Object.transform.rotation = FlightRotation(card.Rotation, card.Shot.Destination - raised, Mathf.Clamp01(t / horizontalAt));
                        if (t >= 1)
                        { card.Arrived = true; card.ArrivedAt = run.Clock; card.Mesh.enabled = false; card.Tail.emitting = false; }
                    }
                    if (run.Binding.Royal)
                        foreach (var group in run.Cards.Where(card => !card.Shot.Unscored).GroupBy(card => card.Shot.CueId))
                            if (group.All(card => card.Arrived) && run.Pulsed.Add(group.Key)) CreateRoyalPulse(run, group.First(), camera);
                    foreach (var pulse in run.Pulses)
                    {
                        float t = Mathf.Clamp01((run.Clock - pulse.Started) / tailSeconds);
                        pulse.Object.transform.localScale = Vector3.one * pulse.Radius * (1 + t * 1.8f);
                        var color = new Color(1, .9f, .35f, 1 - t);
                        pulse.Line.startColor = pulse.Line.endColor = color;
                    }
                    foreach (var waiter in run.Waiters.ToArray()) Poll(run, waiter);
                    if (run.Cards.All(card => card.Arrived) && run.Clock - run.Cards.Max(card => card.ArrivedAt) >= tailSeconds)
                    {
                        foreach (var card in run.Cards) DestroyVisual(card.Object);
                        foreach (var pulse in run.Pulses) DestroyVisual(pulse.Object);
                        run.Operation.TryComplete();
                    }
                }
                catch (Exception error)
                {
                    run.Operation.TryFail("CardProjectileFailed", error.Message, error);
                    foreach (var waiter in run.Waiters.ToArray()) waiter.Operation.TryFail("CardProjectileFailed", error.Message, error);
                    Release(run);
                }
                if (run.Operation.Status != PresentationStatus.Pending) yield break;
                yield return null;
                run.Clock += Mathf.Max(.0001f, DeltaTime);
            }
        }

        private void CreateRoyalPulse(Run run, Ghost card, Camera camera)
        {
            var go = new GameObject("Royal Flush Impact " + card.Shot.TargetId, typeof(LineRenderer));
            go.layer = card.Object.layer; go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(card.Shot.Destination, camera.transform.rotation);
            var line = go.GetComponent<LineRenderer>(); line.sharedMaterial = tailMaterial;
            line.useWorldSpace = false; line.loop = true; line.positionCount = 48;
            line.widthMultiplier = card.Size * .08f; line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int index = 0; index < line.positionCount; index++)
            {
                float angle = index * Mathf.PI * 2 / line.positionCount;
                line.SetPosition(index, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0));
            }
            run.Pulses.Add(new Pulse { Object = go, Line = line, Started = run.Clock, Radius = card.Size * .65f });
        }

        private void Release(Run run)
        {
            if (run.Released) return;
            run.Released = true; run.CancelRoutine?.Invoke();
            if (run.Binding.Id != null) waves.Remove(run.Binding.Id);
            foreach (var waiter in run.Waiters.ToArray()) waiter.Operation.Cancel();
            run.Waiters.Clear();
            foreach (var pulse in run.Pulses) DestroyVisual(pulse.Object);
            foreach (var card in run.Cards)
            {
                DestroyVisual(card.Object);
                if (card.SourceHidden && !ReferenceEquals(card.Original, null) && hidden.TryGetValue(card.Original, out var state) && --state.Owners == 0)
                { if (card.Original != null) card.Original.forceRenderingOff = state.Previous; hidden.Remove(card.Original); }
            }
            runs.Remove(run);
        }
        private static void DestroyVisual(GameObject go)
        { if (go == null) return; go.SetActive(false); if (Application.isPlaying) Destroy(go); else DestroyImmediate(go); }
        private void CancelAll() { foreach (var run in runs.ToArray()) { if (run.Operation.Status == PresentationStatus.Pending) run.Operation.Cancel(); else Release(run); } }
        public override void Synchronize(ActionSyncContext context) => CancelAll();
        private void OnDisable() => CancelAll();
    }
}
