using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

namespace BattlePresentation.Unity
{
    public enum UiFeedbackKind { Custom, Action, Hand, Cost, Damage, Block, Heal, Feedback, Phase, Result }

    /// <summary>Transient, scoped UI from immutable display data. Every invocation owns a separate text view.</summary>
    public sealed class UiFeedbackTimelineAction : PresentationActionBehaviour
    {
        [Tooltip("An inactive template. Each invocation clones it; the original and the HUD are never animated.")]
        [SerializeField] private TMP_Text template;
        [SerializeField] private RectTransform outputRoot;
        [SerializeField] private UiFeedbackKind kind;
        [Tooltip("Optional override. Tokens include {pattern}, {damage}, {value:key}, {after:key}, {before:target.health}.")]
        [SerializeField, TextArea] private string textTemplate;
        [SerializeField] private Color tint = Color.white;
        [Min(.01f), SerializeField] private float seconds = .65f;
        [Range(0, .45f), SerializeField] private float fadeInFraction = .12f;
        [Range(0, .45f), SerializeField] private float fadeOutFraction = .3f;
        [SerializeField] private Vector2 rise = new Vector2(0, 40);
        [Min(0), SerializeField] private float concurrentSpacing = 34;
        [Range(.1f, 1), SerializeField] private float initialScale = .92f;
        private float minimumVisualSeconds;

        private readonly struct LaneAnchor : IEquatable<LaneAnchor>
        {
            private readonly int root;
            private readonly Vector2 anchorMin, anchorMax, position;
            internal LaneAnchor(RectTransform templateRect, RectTransform output)
            { root = output.GetInstanceID(); anchorMin = templateRect.anchorMin; anchorMax = templateRect.anchorMax; position = templateRect.anchoredPosition; }
            public bool Equals(LaneAnchor other) => root == other.root && anchorMin.Equals(other.anchorMin) && anchorMax.Equals(other.anchorMax) && position.Equals(other.position);
            public override bool Equals(object obj) => obj is LaneAnchor other && Equals(other);
            public override int GetHashCode() => (((root * 397) ^ anchorMin.GetHashCode()) * 397 ^ anchorMax.GetHashCode()) * 397 ^ position.GetHashCode();
        }

        private sealed class RunState
        {
            internal TMP_Text View;
            internal CanvasGroup Group;
            internal Vector2 Origin;
            internal Vector3 Scale;
            internal LaneAnchor LaneKey;
            internal float LaneOffset, LaneHeight;
            internal float VisualSeconds;
            internal string BatchId, StatusAction, StatusTarget, StatusDefinition;
            internal long PlaybackEpoch;
            internal bool Revealed, Released, VisualRetired;
            internal Action Stop;
            internal PresentationOperation Operation;
        }
        private readonly List<RunState> active = new List<RunState>();
        private static readonly Dictionary<LaneAnchor, List<RunState>> occupiedLanes = new Dictionary<LaneAnchor, List<RunState>>();
        private static readonly Regex Token = new Regex(@"\{([^{}]+)\}", RegexOptions.CultureInvariant);
        public UiFeedbackKind Kind => kind;
        public int ActiveViewCount => active.Count(run => !run.VisualRetired);
        public override ActionDescriptor Descriptor => new ActionDescriptor(name, new[] { "Revealed" });

        public void Configure(TMP_Text source, UiFeedbackKind feedbackKind, string content = null,
            Color? color = null, float duration = .65f, Vector2? movement = null, RectTransform root = null)
        {
            ReleaseAll(); minimumVisualSeconds = 0; template = source; kind = feedbackKind; textTemplate = content;
            tint = color ?? Color.white; seconds = duration; rise = movement ?? new Vector2(0, 40); outputRoot = root;
        }

        /// <summary>Extends only the visual tail; the authored operation and checkpoint deadlines stay unchanged.</summary>
        public void KeepVisualFor(float duration) => minimumVisualSeconds = Finite(duration) ? Mathf.Max(0, duration) : 0;

        public override IPresentationOperation Play(ActionContext context, IMarkerSink markers)
        {
            if (!isActiveAndEnabled) return PresentationOperation.Failed("ActionDisabled", name);
            if (context == null || context.Data == null || context.Scope == null || !context.Scope.IsActive)
                return PresentationOperation.Failed("InvalidUiContext", name);
            var content = Format(context.Data, kind, textTemplate);
            // An absent optional message has no visual work; it still satisfies the same dependency contract.
            if (string.IsNullOrWhiteSpace(content))
            {
                try { markers?.Emit("Revealed"); }
                catch (Exception error) { return PresentationOperation.Failed("UiAnimationFailed", error.Message); }
                return PresentationOperation.Completed();
            }
            if (template == null) return PresentationOperation.Failed("MissingUiTemplate", name);
            if (!Finite(seconds) || seconds <= 0 || !Finite(rise.x) || !Finite(rise.y))
                return PresentationOperation.Failed("InvalidUiAnimation", name);

            var root = outputRoot != null ? outputRoot : template.rectTransform.parent as RectTransform;
            if (root == null) return PresentationOperation.Failed("MissingUiRoot", name);
            var statusAction = kind == UiFeedbackKind.Feedback && context.Data.SemanticKey == "StatusChanged" ? Value(context.Data, "actionId") : null;
            var statusTarget = Value(context.Data, "statusTarget");
            var statusDefinition = Value(context.Data, "statusDefinition");
            if (!string.IsNullOrEmpty(statusAction) && !string.IsNullOrEmpty(statusTarget) && !string.IsNullOrEmpty(statusDefinition))
                foreach (var previous in active.Where(candidate => candidate.Revealed && !candidate.VisualRetired &&
                    candidate.BatchId == context.BatchId && candidate.PlaybackEpoch == context.PlaybackEpoch && candidate.StatusAction == statusAction &&
                    candidate.StatusTarget == statusTarget && candidate.StatusDefinition == statusDefinition).ToArray())
                    RetireVisual(previous);
            var view = Instantiate(template, root, false);
            view.name = name + " [" + context.ActionInstanceId + "]";
            view.text = content;
            view.color = tint;
            view.raycastTarget = false;
            var group = view.GetComponent<CanvasGroup>();
            if (group == null) group = view.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0; group.blocksRaycasts = false; group.interactable = false;
            // Templates for damage, shields and statuses can occupy the same authored spot.
            // Reserve by that spot, so independent actions share rows without moving any HUD anchor.
            var laneKey = new LaneAnchor(template.rectTransform, root);
            if (!occupiedLanes.TryGetValue(laneKey, out var lanes)) occupiedLanes.Add(laneKey, lanes = new List<RunState>());
            var height = Mathf.Max(concurrentSpacing, view.fontSize * 1.3f);
            var offset = ReserveLane(lanes, height, view.rectTransform, root);
            var run = new RunState { View = view, Group = group, LaneKey = laneKey, LaneOffset = offset, LaneHeight = height,
                Origin = view.rectTransform.anchoredPosition + Vector2.up * offset,
                Scale = view.rectTransform.localScale,
                BatchId = context.BatchId, PlaybackEpoch = context.PlaybackEpoch, StatusAction = statusAction,
                StatusTarget = statusTarget, StatusDefinition = statusDefinition,
                VisualSeconds = Mathf.Max(seconds, minimumVisualSeconds, kind == UiFeedbackKind.Feedback ? 1.4f : 0) };
            lanes.Add(run);
            run.Operation = new PresentationOperation(() => Release(run));
            active.Add(run);
            view.rectTransform.anchoredPosition = run.Origin;
            view.gameObject.SetActive(true);
            context.Scope.Own(() => { if (run.Operation.Status == PresentationStatus.Pending) run.Operation.Cancel(); else Release(run); });
            try
            {
                var stop = StartRoutine(Animate(run, markers));
                if (run.Released) stop?.Invoke(); else run.Stop = stop;
            }
            catch (Exception error)
            { Release(run); run.Operation.TryFail("UiAnimationFailed", error.Message, error); }
            return run.Operation;
        }

        private float ReserveLane(List<RunState> lanes, float height, RectTransform view, RectTransform root)
        {
            float Up(float start)
            {
                var offset = start;
                foreach (var occupied in lanes.OrderBy(item => item.LaneOffset))
                {
                    var separation = (height + occupied.LaneHeight) * .5f;
                    if (offset + separation <= occupied.LaneOffset) break;
                    if (Mathf.Abs(offset - occupied.LaneOffset) < separation) offset = occupied.LaneOffset + separation;
                }
                return offset;
            }
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(root, view);
            // Reserve the entire authored rectangle and its travel, without moving the template.
            var minimum = root.rect.yMin - bounds.min.y - Mathf.Min(0, rise.y);
            var maximum = root.rect.yMax - bounds.max.y - Mathf.Max(0, rise.y);
            if (minimum > maximum) return Up(0); // A root smaller than its template has no bounded lane.
            var upper = Up(Mathf.Max(0, minimum));
            if (upper <= maximum) return upper;
            var lower = Mathf.Min(0, maximum);
            foreach (var occupied in lanes.OrderByDescending(item => item.LaneOffset))
            {
                var separation = (height + occupied.LaneHeight) * .5f;
                if (lower - separation >= occupied.LaneOffset) break;
                if (Mathf.Abs(lower - occupied.LaneOffset) < separation) lower = occupied.LaneOffset - separation;
            }
            // Even a fully occupied root must keep the newest text within its visible bounds.
            return Mathf.Clamp(lower, minimum, maximum);
        }

        private void RetireVisual(RunState run)
        {
            if (run.Operation.Status != PresentationStatus.Pending) { Release(run); return; }
            run.VisualRetired = true;
            run.View.gameObject.SetActive(false);
            ReturnLane(run);
            // The preceding fact still owns its operation, marker and authored deadline.
            // Only its redundant visual tail has been replaced by the new fact's clone.
        }

        private IEnumerator Animate(RunState run, IMarkerSink markers)
        {
            var elapsed = 0f;
            while (!run.Released && elapsed < run.VisualSeconds)
            {
                if (run.VisualRetired)
                {
                    elapsed += Mathf.Max(0, DeltaTime);
                    if (elapsed >= seconds)
                    {
                        run.Stop = null; Release(run); run.Operation.TryComplete(); yield break;
                    }
                    yield return null; continue;
                }
                if (run.View == null || run.Group == null)
                { Fail(run, "The owned UI view was destroyed before completion."); yield break; }
                elapsed += Mathf.Max(0, DeltaTime);
                var progress = Mathf.Clamp01(elapsed / run.VisualSeconds);
                var fadeIn = fadeInFraction <= 0 ? 1 : Mathf.Clamp01(progress / fadeInFraction);
                var fadeOut = fadeOutFraction <= 0 ? 1 : Mathf.Clamp01((1 - progress) / fadeOutFraction);
                run.Group.alpha = Mathf.Min(fadeIn, fadeOut);
                // A long frame can finish an action immediately, but its reveal signal must still represent a visible value.
                if (!run.Revealed && run.Group.alpha <= 0 && progress >= 1) run.Group.alpha = 1;
                run.View.rectTransform.anchoredPosition = run.Origin + rise * progress;
                run.View.rectTransform.localScale = run.Scale * Mathf.Lerp(initialScale, 1, Mathf.SmoothStep(0, 1, Mathf.Clamp01(progress * 4)));
                if (!run.Revealed && run.Group.alpha > 0)
                {
                    run.Revealed = true;
                    Exception failure = null;
                    try { markers?.Emit("Revealed"); } catch (Exception error) { failure = error; }
                    if (failure != null) { Fail(run, failure.Message); yield break; }
                }
                // A readable tail is still owned by this scope after the authored work completes.
                // It cannot hold combat input or a last-round result behind the longer text lifetime.
                if (elapsed >= seconds) run.Operation.TryComplete();
                yield return null;
            }
            if (run.Released) yield break;
            run.Stop = null;
            Release(run);
            run.Operation.TryComplete();
        }

        private void Fail(RunState run, string message)
        { run.Stop = null; Release(run); run.Operation.TryFail("UiAnimationFailed", message); }
        private void Release(RunState run)
        {
            if (run.Released) return;
            run.Released = true;
            var stop = run.Stop; run.Stop = null; stop?.Invoke();
            active.Remove(run);
            ReturnLane(run);
            if (run.View == null) return;
            run.View.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(run.View.gameObject); else DestroyImmediate(run.View.gameObject);
        }
        private static void ReturnLane(RunState run)
        {
            if (occupiedLanes.TryGetValue(run.LaneKey, out var lanes))
            { lanes.Remove(run); if (lanes.Count == 0) occupiedLanes.Remove(run.LaneKey); }
        }
        private void ReleaseAll()
        {
            foreach (var run in active.ToArray())
                if (run.Operation.Status == PresentationStatus.Pending) run.Operation.Cancel(); else Release(run);
        }
        public override void Synchronize(ActionSyncContext context) => ReleaseAll();
        private void OnDisable() => ReleaseAll();
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public static string Format(BattleDisplayChange data, UiFeedbackKind feedbackKind, string content = null)
        {
            if (data == null) return string.Empty;
            if (feedbackKind == UiFeedbackKind.Damage && Value(data, "damagePrevented") == "immune") return "免疫";
            var format = string.IsNullOrEmpty(content) ? DefaultTemplate(feedbackKind) : content;
            return Token.Replace(format, match => Resolve(data, match.Groups[1].Value) ?? match.Value);
        }
        private static string DefaultTemplate(UiFeedbackKind feedbackKind)
        {
            switch (feedbackKind)
            {
                case UiFeedbackKind.Action: return "{action}";
                case UiFeedbackKind.Hand: return "{pattern}  ·  {score}";
                case UiFeedbackKind.Cost: return "能量 -{cost}";
                case UiFeedbackKind.Damage: return "-{damage}";
                case UiFeedbackKind.Block: return "格挡 {block}";
                case UiFeedbackKind.Heal: return "+{heal}";
                case UiFeedbackKind.Feedback: return "{feedback}";
                case UiFeedbackKind.Phase: return "第 {displayRound} 回合 · {phase}";
                case UiFeedbackKind.Result: return "{result}";
                default: return "{feedback}";
            }
        }
        private static string Resolve(BattleDisplayChange data, string token)
        {
            var effect = data.Effect;
            if (token.StartsWith("value:", StringComparison.Ordinal)) return Value(data, token.Substring(6));
            if (token.StartsWith("after:", StringComparison.Ordinal)) return SnapshotValue(data, data.After, token.Substring(6));
            if (token.StartsWith("before:", StringComparison.Ordinal)) return SnapshotValue(data, data.Before, token.Substring(7));
            switch (token)
            {
                case "actor": return UnitName(data.After, data.ActorId);
                case "target": return UnitName(data.After, effect?.TargetId ?? FirstTarget(data));
                case "amount": return Number(effect?.Amount ?? 0);
                case "damage": case "healthLost": return Number(effect?.HealthLost ?? 0);
                case "block": return Number(effect == null ? 0 : effect.BlockAbsorbed > 0 ? effect.BlockAbsorbed : (effect.After?.Block ?? 0) - (effect.Before?.Block ?? 0));
                case "heal": return Number(effect == null ? 0 : Math.Max(0, (effect.After?.Health ?? 0) - (effect.Before?.Health ?? 0)));
                case "round": return Number(data.After.Round);
                case "displayRound": return Number(DisplayRound(data.After));
                case "phase": return Phase(data.After.Phase);
                case "result": return data.After.Result == "Won" ? "战斗胜利" : data.After.Result == "Lost" ? "战斗失败" : data.After.Result ?? string.Empty;
                case "action":
                    var action = Value(data, "action");
                    return action == "Play" ? "出牌" : action == "Discard" ? "弃牌" : action == "EnemyAction" ? "敌方行动" : action;
                case "feedback": return data.Values.TryGetValue("floatingFeedback", out var compact) ? compact : Value(data, token);
                case "pattern": case "score": case "points": case "multiplier": case "cost": return Value(data, token);
                default: return null;
            }
        }
        private static string SnapshotValue(BattleDisplayChange data, BattleDisplaySnapshot state, string key)
        {
            if (state == null) return string.Empty;
            if (state.Labels.TryGetValue(key, out var label)) return label;
            if (key == "phase") return Phase(state.Phase);
            if (key == "round") return Number(state.Round);
            if (key == "displayRound") return Number(DisplayRound(state));
            if (key == "result") return state.Result ?? string.Empty;
            var separator = key.IndexOf('.');
            if (separator < 0) return string.Empty;
            var role = key.Substring(0, separator);
            var id = role == "actor" ? data.ActorId : role == "target" ? data.Effect?.TargetId ?? FirstTarget(data) : role == "observer" ? state.ObserverId : role;
            var property = key.Substring(separator + 1);
            var unit = state.Unit(id);
            if (unit != null)
            {
                switch (property)
                {
                    case "name": return unit.Name;
                    case "health": return Number(unit.Health);
                    case "maxHealth": return Number(unit.MaxHealth);
                    case "block": return Number(unit.Block);
                    case "energy": return Number(unit.Energy);
                    case "maxEnergy": return Number(unit.MaxEnergy);
                }
                if (unit.Labels.TryGetValue(property, out var value)) return value;
            }
            return Number(state.Resource(id, property)?.Value ?? 0);
        }
        private static string FirstTarget(BattleDisplayChange data) => data.TargetIds.Count > 0 ? data.TargetIds[0] : null;
        private static string UnitName(BattleDisplaySnapshot state, string id) => state?.Unit(id)?.Name ?? id ?? string.Empty;
        private static string Value(BattleDisplayChange data, string key) => data.Values.TryGetValue(key, out var value) ? value : string.Empty;
        private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
        private static long DisplayRound(BattleDisplaySnapshot state) => state.IsFinished ? state.CompletedRounds : state.CompletedRounds + 1;
        private static string Phase(string value) => value == "player" ? "玩家回合" : value == "enemy" ? "敌人回合" : value ?? string.Empty;
    }
}
