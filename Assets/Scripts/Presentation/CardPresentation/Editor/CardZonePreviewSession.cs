#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using BattlePresentation;
using UnityEditor;
using UnityEngine;

namespace CardPresentation.Editor
{
    /// <summary>Temporary display-only lease. Never knows a match, command port or concrete renderer.</summary>
    public sealed class CardZonePreviewSession : IDisposable, IMarkerSink
    {
        private static readonly Dictionary<int, CardZonePreviewSession> owners = new Dictionary<int, CardZonePreviewSession>();
        private readonly MonoBehaviour component;
        private readonly ICardZonePresentation view;
        private readonly ICardZonePresentationPreview clock;
        private readonly IDisposable restoration;
        private readonly int ownerId;
        private PresentationScope scope;
        private bool disposed;
        private int sequence;
        private readonly List<string> events = new List<string>();
        public IReadOnlyList<string> Events => events;
        public IPresentationOperation Operation { get; private set; }
        public CardZoneDisplayData Current => view.Current;
        public double Time { get; private set; }
        public bool Paused { get; set; }
        public bool IsDisposed => disposed;
        public CardZonePreviewSession(MonoBehaviour source)
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("表现预览仅在编辑模式运行；Play 中只观察实战。");
            if (source == null || !source.isActiveAndEnabled || !source.gameObject.scene.IsValid() || EditorUtility.IsPersistent(source))
                throw new ArgumentException("请选择场景中已启用的牌区表现组件。");
            view = source as ICardZonePresentation ?? throw new ArgumentException("需要 ICardZonePresentation。");
            clock = source as ICardZonePresentationPreview ?? throw new ArgumentException("该实现未提供 ICardZonePresentationPreview 编辑器时钟。");
            ownerId = source.GetInstanceID();
            if (owners.ContainsKey(ownerId) || view.IsPresenting) throw new InvalidOperationException("该牌区已被其他表现或预览占用。");
            component = source; restoration = clock.PreservePreviewState() ?? throw new InvalidOperationException("提供者必须返回预览恢复句柄。"); owners.Add(ownerId, this);
        }
        public void Load(CardZoneDisplayData snapshot)
        {
            RequireActive(); scope?.Dispose(); scope = null;
            view.Synchronize(snapshot); Operation = null;
        }
        public IPresentationOperation Play(string key, CardZoneDisplayData after, IEnumerable<string> cards = null,
            IDictionary<string, string> parameters = null)
        {
            RequireActive();
            var before = view.Current ?? throw new InvalidOperationException("先载入预览展示数据。");
            var previous = scope;
            // Selection owns its transition policy; let the provider replace it before retiring the old lease.
            bool switchSelection = key == CardZoneActions.Selection && lastAction == key;
            if (!switchSelection) { previous?.Dispose(); previous = null; }
            scope = new PresentationScope("editor-preview/" + ++sequence);
            try
            {
                clock.AdvancePreview(0);
                Record("开始 " + key);
                Operation = view.Play(new CardZonePresentationRequest(key, scope.Id, scope, before, after, cards,
                    parameters: parameters), this);
                var operation = Operation;
                void Finished(PresentationStatus result) => Record(result + (operation.Error == null ? "" : " · " + operation.Error));
                if (operation.Status == PresentationStatus.Pending)
                { operation.Finished += Finished; scope.Own(() => operation.Finished -= Finished); }
                else Finished(operation.Status);
                lastAction = key;
                return operation;
            }
            catch { scope.Dispose(); scope = null; throw; }
            finally { previous?.Dispose(); }
        }
        private string lastAction;
        public void Advance(float deltaTime, bool step = false)
        {
            RequireActive();
            if (Paused && !step) return;
            if (deltaTime < 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            Time += deltaTime; clock.AdvancePreview(deltaTime);
        }
        private void RequireActive()
        {
            if (disposed || component == null || !component.isActiveAndEnabled) throw new InvalidOperationException("预览目标已失效。");
        }
        public bool Emit(string marker) { if (disposed) return false; Record("信号 " + marker); return true; }
        private void Record(string value)
        { if (events.Count >= 200) events.RemoveAt(0); events.Add(Time.ToString("F3") + "s · " + value); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { scope?.Dispose(); }
            finally
            {
                scope = null; owners.Remove(ownerId);
                restoration.Dispose();
            }
        }
    }
}
#endif
