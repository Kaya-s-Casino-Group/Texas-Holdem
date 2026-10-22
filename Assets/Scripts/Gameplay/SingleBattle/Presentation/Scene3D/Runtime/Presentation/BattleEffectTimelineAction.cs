using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using global::BattlePresentation;
using global::BattlePresentation.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.SingleBattle
{
    /// <summary>Scoped flipbook effects driven only by resolved display facts.</summary>
    public sealed class BattleEffectTimelineAction : PresentationActionBehaviour
    {
        private sealed class Run
        {
            internal Canvas Canvas;
            internal RawImage Image;
            internal BattleActor Actor;
            internal Camera Camera;
            internal string Kind;
            internal Texture2D[] Frames;
            internal PresentationOperation Operation;
            internal Action Stop;
            internal bool Released;
        }
        private readonly List<Run> active = new List<Run>();
        private readonly Dictionary<string, Texture2D[]> clips = new Dictionary<string, Texture2D[]>();
        public int ActiveEffectCount => active.Count;
        public override ActionDescriptor Descriptor => new ActionDescriptor("battle.flipbook", new[] { "Revealed" });

        public static string EffectFor(BattleDisplayChange fact)
        {
            if (fact == null) return null;
            var effect = fact.Effect;
            if (effect != null)
            {
                // Partial block still hurts: health loss takes precedence over shield absorption.
                if (effect.HealthLost > 0) return "Hit";
                if (effect.BlockAbsorbed > 0) return "Shield";
                if (effect.Kind == "GainBlock" && effect.After.Block > effect.Before.Block) return "Shield";
                if (effect.Kind == "Heal" && effect.After.Health > effect.Before.Health &&
                    fact.Values.TryGetValue("effectOrigin", out var origin) && origin == "Lifesteal") return "Lifesteal";
            }
            if (!fact.Values.TryGetValue("statusChange", out var change)) return null;
            if (change == "Cleansed") return "Cleanse";
            if (change != "Applied" && change != "Refreshed") return null;
            if (!fact.Values.TryGetValue("statusTags", out var tags)) return null;
            if (tags.Split(',').Any(tag => tag.Trim() == "Control")) return "Control";
            return tags.Split(',').Any(tag => tag.Trim() == "Negative") ? "Debuff" : null;
        }

        public override IPresentationOperation Play(ActionContext context, IMarkerSink markers)
        {
            string kind = EffectFor(context.Data);
            if (kind == null) { markers?.Emit("Revealed"); return PresentationOperation.Completed(); }
            string id = context.Data.Effect?.TargetId;
            if (id == null) context.Data.Values.TryGetValue("statusTarget", out id);
            var target = context.Targets?.Resolve(id, context);
            var actor = target as BattleActor;
            if (actor == null && target is Component component) actor = component.GetComponentInParent<BattleActor>();
            if (actor == null && target is GameObject go) actor = go.GetComponentInParent<BattleActor>();
            var camera = GetComponentInParent<SingleBattleSceneAdapter>()?.Actions?.Hand?.GetComponent<CardPresentation.HandZoneView>()?.FacingCamera ?? Camera.main;
            if (actor == null || camera == null) { markers?.Emit("Revealed"); return PresentationOperation.Completed(); }
            if (!clips.TryGetValue(kind, out var frames))
            {
                frames = Resources.LoadAll<Texture2D>("BattleEffects/" + kind).OrderBy(frame => frame.name, StringComparer.Ordinal).ToArray();
                clips.Add(kind, frames);
            }
            if (frames.Length == 0) return PresentationOperation.Failed("MissingBattleEffect", kind);
            var root = new GameObject("Battle Effect " + kind, typeof(RectTransform), typeof(Canvas));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, gameObject.scene);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            // Created after the portrait overlay so the hit remains visible on top of the player.
            var image = new GameObject(kind, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            image.transform.SetParent(root.transform, false); image.raycastTarget = false;
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = Vector2.zero;
            var run = new Run { Canvas = canvas, Image = image, Actor = actor, Camera = camera, Kind = kind, Frames = frames };
            run.Operation = new PresentationOperation(() => Release(run)); active.Add(run);
            context.Scope.Own(() => { if (run.Operation.Status == PresentationStatus.Pending) run.Operation.Cancel(); else Release(run); });
            try
            {
                var stop = StartRoutine(Animate(run, markers));
                if (run.Released) stop(); else run.Stop = stop;
            }
            catch (Exception error) { Release(run); run.Operation.TryFail("BattleEffectFailed", error.Message, error); }
            return run.Operation;
        }

        private IEnumerator Animate(Run run, IMarkerSink markers)
        {
            float duration = run.Kind == "Hit" ? .38f : run.Kind == "Lifesteal" ? .65f : run.Kind == "Cleanse" ? .8f : 1f;
            bool revealed = false;
            for (float elapsed = 0; elapsed < duration; elapsed += Mathf.Max(0, DeltaTime))
            {
                if (run.Released) yield break;
                if (run.Actor == null || !run.Actor.isActiveAndEnabled || run.Camera == null) break;
                float progress = elapsed / duration;
                run.Image.texture = run.Frames[Mathf.Min(run.Frames.Length - 1, (int)(progress * run.Frames.Length))];
                run.Image.color = new Color(1, 1, 1, Mathf.Clamp01((1 - progress) / .18f));
                Position(run);
                if (!revealed) { revealed = true; markers?.Emit("Revealed"); }
                yield return null;
            }
            if (!revealed) markers?.Emit("Revealed");
            run.Stop = null; Release(run); run.Operation.TryComplete();
        }

        private static void Position(Run run)
        {
            var visual = run.Actor.Visual;
            var renderer = visual != null ? (visual.Find("Artwork")?.GetComponent<Renderer>() ?? visual.GetComponentInChildren<Renderer>()) : null;
            var bounds = renderer != null ? renderer.bounds : new Bounds(run.Actor.transform.position, Vector3.one * .2f);
            Vector3 center = run.Camera.WorldToScreenPoint(bounds.center);
            float low = float.PositiveInfinity, high = float.NegativeInfinity;
            for (int corner = 0; corner < 8; corner++)
            {
                var point = bounds.center + Vector3.Scale(bounds.extents, new Vector3((corner & 1) == 0 ? -1 : 1,
                    (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                float y = run.Camera.WorldToScreenPoint(point).y; low = Mathf.Min(low, y); high = Mathf.Max(high, y);
            }
            float height = Mathf.Clamp(high - low, Screen.height * .12f, Screen.height * .6f);
            if (run.Kind == "Control") center.y += height * .36f;
            float size = height * (run.Kind == "Control" ? .4f : run.Kind == "Hit" ? .72f : 1.05f);
            var texture = run.Frames[0];
            run.Image.rectTransform.anchoredPosition = new Vector2(center.x, center.y);
            run.Image.rectTransform.sizeDelta = new Vector2(size * texture.width / texture.height, size);
            run.Canvas.enabled = center.z > 0;
        }

        private void Release(Run run)
        {
            if (run.Released) return;
            run.Released = true;
            var stop = run.Stop; run.Stop = null; stop?.Invoke(); active.Remove(run);
            if (run.Canvas == null) return;
            run.Canvas.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(run.Canvas.gameObject); else DestroyImmediate(run.Canvas.gameObject);
        }
        private void Clear()
        {
            foreach (var run in active.ToArray())
                if (run.Operation.Status == PresentationStatus.Pending) run.Operation.Cancel(); else Release(run);
        }
        public override void Synchronize(ActionSyncContext context) => Clear();
        private void OnDisable() => Clear();
    }
}
