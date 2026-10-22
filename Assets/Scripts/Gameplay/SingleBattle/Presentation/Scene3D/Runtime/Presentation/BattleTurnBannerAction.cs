using System;
using System.Collections;
using System.Collections.Generic;
using global::BattlePresentation;
using global::BattlePresentation.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.SingleBattle
{
    public sealed class BattleTurnBannerAction : PresentationActionBehaviour
    {
        [SerializeField] private TMP_FontAsset font;
        [SerializeField, Min(.1f)] private float enterSeconds = .2f, holdSeconds = .5f, exitSeconds = .2f;
        private sealed class Run
        {
            internal GameObject Root;
            internal RectTransform Ribbon;
            internal CanvasGroup Group;
            internal PresentationOperation Operation;
            internal Action Stop;
            internal float Direction;
            internal bool Released;
        }
        private readonly List<Run> active = new List<Run>();
        public int ActiveBannerCount => active.Count;
        public string CurrentTitle { get; private set; }
        public override ActionDescriptor Descriptor => new ActionDescriptor("battle.turn-banner", new[] { "Revealed" });
        public void Configure(TMP_FontAsset source) => font = source;
        public static string TitleFor(BattleDisplayChange fact) => fact?.After == null || fact.After.IsFinished ? null :
            fact.After.Phase == "player" ? "你的回合" : fact.After.Phase == "enemy" ? "敌人回合" : null;

        public override IPresentationOperation Play(ActionContext context, IMarkerSink markers)
        {
            string title = TitleFor(context.Data);
            if (title == null) { markers?.Emit("Revealed"); return PresentationOperation.Completed(); }
            Clear();
            bool enemy = context.After.Phase == "enemy";
            var root = new GameObject("Battle Turn Banner", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, gameObject.scene);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = short.MaxValue;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = .5f;
            var group = root.GetComponent<CanvasGroup>(); group.alpha = 0; group.interactable = false; group.blocksRaycasts = false;
            var ribbon = new GameObject("Turn Ribbon", typeof(RectTransform), typeof(BattleTurnBannerGraphic)).GetComponent<RectTransform>();
            ribbon.SetParent(root.transform, false); ribbon.anchorMin = ribbon.anchorMax = new Vector2(.5f, .58f);
            ribbon.sizeDelta = new Vector2(900, 146);
            var accent = enemy ? new Color(.85f, .34f, .24f) : new Color(.81f, .66f, .37f);
            var graphic = ribbon.GetComponent<BattleTurnBannerGraphic>(); graphic.Accent = accent; graphic.raycastTarget = false;
            var titleText = Text(ribbon, "Turn Title", title, 52, new Vector2(0, 12), new Vector2(500, 72));
            titleText.color = enemy ? new Color(1, .81f, .7f) : new Color(1, .94f, .79f);
            titleText.characterSpacing = 12; titleText.fontStyle = FontStyles.Bold;
            var round = Text(ribbon, "Round Label", "第 " + (context.After.CompletedRounds + 1) + " 回合", 18,
                new Vector2(0, -37), new Vector2(420, 28));
            round.color = accent; round.characterSpacing = 8;
            var run = new Run { Root = root, Ribbon = ribbon, Group = group, Direction = enemy ? 1 : -1 };
            run.Operation = new PresentationOperation(() => Release(run)); active.Add(run); CurrentTitle = title;
            context.Scope.Own(() => { if (run.Operation.Status == PresentationStatus.Pending) run.Operation.Cancel(); else Release(run); });
            try
            {
                var stop = StartRoutine(Animate(run, markers));
                if (run.Released) stop(); else run.Stop = stop;
            }
            catch (Exception error) { Release(run); run.Operation.TryFail("TurnBannerFailed", error.Message, error); }
            return run.Operation;
        }
        private TMP_Text Text(Transform parent, string name, string value, float size, Vector2 position, Vector2 dimensions)
        {
            var label = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(parent, false); label.rectTransform.anchoredPosition = position; label.rectTransform.sizeDelta = dimensions;
            label.font = font != null ? font : TMP_Settings.defaultFontAsset;
            label.text = value; label.fontSize = size; label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false; label.raycastTarget = false;
            return label;
        }
        private IEnumerator Animate(Run run, IMarkerSink markers)
        {
            float enter = Mathf.Max(.01f, enterSeconds), hold = Mathf.Max(0, holdSeconds), exit = Mathf.Max(.01f, exitSeconds);
            float duration = enter + hold + exit;
            bool revealed = false;
            for (float elapsed = 0; elapsed < duration; elapsed += Mathf.Max(0, DeltaTime))
            {
                if (run.Released) yield break;
                float inT = Mathf.Clamp01(elapsed / enter), outT = Mathf.Clamp01((elapsed - enter - hold) / exit);
                float ease = 1 - Mathf.Pow(1 - inT, 3);
                run.Group.alpha = Mathf.Min(inT * 2, 1 - outT);
                run.Ribbon.anchoredPosition = new Vector2(run.Direction * 120 * (1 - ease), 16 * outT);
                run.Ribbon.localScale = new Vector3(Mathf.Lerp(.92f, 1, ease), 1, 1);
                if (!revealed && inT >= .5f) { revealed = true; markers?.Emit("Revealed"); }
                yield return null;
            }
            if (!revealed) markers?.Emit("Revealed");
            run.Stop = null; Release(run); run.Operation.TryComplete();
        }
        private void Release(Run run)
        {
            if (run.Released) return;
            run.Released = true; var stop = run.Stop; run.Stop = null; stop?.Invoke(); active.Remove(run);
            if (active.Count == 0) CurrentTitle = null;
            if (run.Root == null) return;
            run.Root.SetActive(false);
            if (Application.isPlaying) Destroy(run.Root); else DestroyImmediate(run.Root);
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
