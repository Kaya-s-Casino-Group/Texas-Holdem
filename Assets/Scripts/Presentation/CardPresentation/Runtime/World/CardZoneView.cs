using CardArt;
using System;
using System.Collections.Generic;
using System.Linq;
using PokerEngine;
using UnityEngine;

namespace CardPresentation
{
    public enum CardZoneFacingMode
    {
        [InspectorName("保持配置朝向")] KeepAuthored = 0,
        [InspectorName("朝向相机位置")] LookAtCameraPosition = 1,
        [InspectorName("平行相机画面")] ParallelCameraPlane = 2
    }

    /// <summary>Common ordered-zone visuals. Consumes snapshots without mutating card or zone data.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public partial class CardZoneView : MonoBehaviour, ISerializationCallbackReceiver
    {
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private CardZoneLayoutSettings layout = CardZoneLayoutSettings.Default;
        [SerializeField] private CardZoneMotionSettings motionSettings = CardZoneMotionSettings.Default;
        [SerializeField, HideInInspector] private bool faceCamera; // Legacy scene/prefab migration only.
        [SerializeField] private CardZoneFacingMode facingMode;
        [SerializeField] private Camera facingCamera;
        [SerializeField, HideInInspector] private bool hasUnfacedRotation;
        [SerializeField, HideInInspector] private Quaternion unfacedLocalRotation = Quaternion.identity;
        private Dictionary<string, CardInstanceView> views = new Dictionary<string, CardInstanceView>(StringComparer.Ordinal);
        protected CardInstanceView[] ordered = new CardInstanceView[0];
        private GameObject boundPrefab;
        private Bounds cardBounds;
        private bool layoutRefreshRequested;

        protected double AnimationTime { get; private set; }

        public CardZoneViewData Data { get; private set; }
        public IReadOnlyList<CardInstanceView> Views => Array.AsReadOnly(ordered);
        public virtual string DefaultZoneKind => "zone";
        public virtual int? CardCapacity => null;
        public virtual bool SupportsArc => true;
        public virtual bool SupportsSway => true;
        public CardZoneLayoutSettings Layout => ResolveLayoutSettings(layout);
        public GameObject CardPrefab => cardPrefab;
        /// <summary>Uniform size relative to the artwork prefab, also used by distance-based animation.</summary>
        public float CardScale => !Layout.FitCardsToZone || cardPrefab == null ? 1 :
            CalculateCardScale(Layout, MotionSettings, (Data == null ? CardInstanceView.ValidatePrefab(cardPrefab, out _) : cardBounds).size);
        // Transfers use world distances. Keep legacy world tuning when automatic sizing is disabled.
        internal float AnimationWorldScale => Layout.FitCardsToZone ? CardScale * transform.TransformVector(Vector3.up).magnitude : 1;
        public CardZoneMotionSettings MotionSettings => ResolveMotionSettings(motionSettings);
        public virtual bool IsTransitioning => ordered.Any(view => view != null && view.IsTransitioning);
        public bool FaceCamera => facingMode != CardZoneFacingMode.KeepAuthored;
        public CardZoneFacingMode FacingMode => facingMode;
        public Camera FacingCamera => facingCamera;
        protected bool ContainsViewIdentity(string id) => views.ContainsKey(id);

        /// <summary>Configure before Present, or Clear first to replace the prefab.</summary>
        public void Configure(GameObject prefab, CardZoneLayoutSettings settings)
        {
            if (Data != null) throw new InvalidOperationException("Clear the zone before replacing its prefab; use SetLayout to move existing views.");
            var bounds = CardInstanceView.ValidatePrefab(prefab, out _);
            settings = ResolveLayoutSettings(settings);
            var motion = MotionSettings;
            CalculateCardPoses(settings, motion.LayoutSize(bounds.size) * CalculateCardScale(settings, motion, bounds.size), 0);
            ValidatePresentationSettings();
            cardPrefab = prefab; layout = settings; motionSettings = motion;
            layoutRefreshRequested = false;
        }

        protected virtual void ValidatePresentationSettings() { }

        public void ValidateCardCount(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (CardCapacity.HasValue && count > CardCapacity.Value)
                throw new ArgumentOutOfRangeException(nameof(count), GetType().Name + " 最多容纳 " + CardCapacity + " 张牌。");
        }

        private CardZoneLayoutSettings ResolveLayoutSettings(CardZoneLayoutSettings settings)
        {
            if (!SupportsArc) settings.ArcHeight = 0;
            return settings;
        }
        private CardZoneMotionSettings ResolveMotionSettings(CardZoneMotionSettings settings)
        {
            if (!SupportsSway)
            {
                settings = CardZoneMotionSettings.Default;
                settings.Enabled = false; settings.MaxSwayAngle = 0;
            }
            return settings;
        }
        protected virtual CardZoneCardPose[] CalculateCardPoses(CardZoneLayoutSettings settings, Vector3 cardSize, int count) =>
            CardZoneRowLayout.CalculatePoses(settings, cardSize, count);

        // Keep scale independent of the current card count so drawing/removing cards never changes their size.
        protected float CalculateCardScale(CardZoneLayoutSettings settings, CardZoneMotionSettings motion, Vector3 size)
        {
            if (!settings.FitCardsToZone) return 1;
            var footprint = motion.LayoutSize(size);
            float width = settings.Size.x - 2 * settings.Padding.x;
            float height = settings.Size.y - 2 * settings.Padding.y;
            // Leave room for the requested fan, even when the card fills the available height.
            height -= Mathf.Min(Mathf.Abs(settings.ArcHeight), height * .5f);
            float scale = Mathf.Min(width / (footprint.x * (CardCapacity ?? 1)), height / footprint.z);
            if (!CardZoneRowLayout.Finite(scale) || scale <= 0)
                throw new ArgumentException("牌区尺寸必须大于两侧留白，才能自动缩放卡牌。");
            // Avoid rounding a fitted footprint just beyond the layout's strict boundary.
            return scale * .999999f;
        }

        // Inspector/Prefab serialization and runtime setters obey the same subtype constraints.
        protected virtual void OnValidate()
        {
            layout = ResolveLayoutSettings(layout); motionSettings = ResolveMotionSettings(motionSettings);
            layoutRefreshRequested = true;
        }

        public void Present(CardZone zone, Func<CardInstanceId, CardVisualData> project, bool animate = true) =>
            Present(CardZoneViewData.FromZone(zone, project), animate);

        public virtual void Present(CardZoneViewData data, bool animate = true) =>
            ApplyPresentation(data, () => CreatePresentationUpdate(data, animate));

        /// <summary>Validate visuals first, then commit the prepared match command before publishing visual notifications.</summary>
        public void PresentCommitted(CardZoneViewData data, Action commitData, bool animate = true) =>
            ApplyPresentation(data, () => CreatePresentationUpdate(data, animate), commitData);

        /// <summary>One validated update owns its transient effects; base owns instance reuse and snapshot commit.</summary>
        protected internal class PresentationUpdate : IDisposable
        {
            public virtual void BeforeApply() { }
            public virtual void PresentCard(CardInstanceView view, CardVisualData card, CardShaderAppearance appearance,
                CardZoneCardPose pose, int index, bool created)
            {
                view.Present(card, Vector3.zero, Vector3.zero, appearance, CardZoneLiftSettings.Default, false);
                view.SetPose(pose, CardZoneTransitionSettings.Default, false);
            }
            public virtual void RemoveCard(CardInstanceView view) => Release(view);
            public virtual void AfterCommit() { }
            public virtual void Dispose() { }
        }

        protected virtual PresentationUpdate CreatePresentationUpdate(CardZoneViewData data, bool animate) => new PresentationUpdate();

        protected void ApplyPresentation(CardZoneViewData data, Func<PresentationUpdate> createUpdate, Action commitData = null)
        {
            using (var prepared = new PreparedPresentation(this, data, createUpdate, null, null))
            {
                prepared.ValidateCurrent(); commitData?.Invoke(); prepared.Commit(); prepared.NotifyCommitted();
            }
        }

        public bool TryGetView(string viewId, out CardInstanceView view)
        {
            view = null;
            return viewId != null && views.TryGetValue(viewId, out view) && view != null;
        }

        /// <summary>Reorders existing visuals only. Membership and rule order are unchanged.</summary>
        public void ReorderViews(IEnumerable<string> viewIds, bool animate = true)
        {
            var ids = viewIds.ToArray();
            if (ids.Length != ordered.Length || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length ||
                ids.Any(id => !views.ContainsKey(id))) throw new ArgumentException("Supply each current view exactly once.");
            ordered = ids.Select(id => views[id]).ToArray();
            if (Data != null) Data = new CardZoneViewData(Data.ZoneId, ordered.Select(view => view.Data));
            SetLayout(Layout, MotionSettings, animate);
        }

        public void SetLayout(CardZoneLayoutSettings settings) => SetLayout(settings, MotionSettings);

        public void SetLayout(CardZoneLayoutSettings settings, CardZoneMotionSettings motion, bool animate = true)
        {
            ValidatePresentationSettings();
            var bounds = Data == null ? CardInstanceView.ValidatePrefab(cardPrefab, out _) : cardBounds;
            settings = ResolveLayoutSettings(settings); motion = ResolveMotionSettings(motion);
            ValidateCardCount(ordered.Length);
            float scale = CalculateCardScale(settings, motion, bounds.size);
            var poses = CalculateCardPoses(settings, motion.LayoutSize(bounds.size) * scale, ordered.Length);
            layout = settings; motionSettings = motion;
            for (int i = 0; i < ordered.Length; i++) if (ordered[i] != null)
            {
                ordered[i].ConfigureMotion(motion);
                ordered[i].SetPresentationScale(scale);
                ApplyLayoutPose(ordered[i], poses[i], animate);
            }
            // Crossed transfers may anchor to a later card's layout root. Resolve after all roots move.
            foreach (var view in ordered) if (view != null) view.RefreshTransferPose();
            OnCardScaleChanged(scale);
            layoutRefreshRequested = false;
        }

        protected virtual void OnCardScaleChanged(float scale) { }

        public void RefreshLayout() => SetLayout(Layout);

        public void SetMotionSettings(CardZoneMotionSettings settings) => SetLayout(Layout, settings);

        protected virtual void ApplyLayoutPose(CardInstanceView view, CardZoneCardPose pose, bool animate) =>
            view.SetPose(pose, CardZoneTransitionSettings.Default, false);

        public virtual void CompleteTransitions()
        {
            foreach (var view in ordered) if (view != null) view.CompleteTransition();
        }

        /// <summary>Runtime advances from Update; the editor can advance each zone independently.</summary>
        public void AdvanceMotion(float deltaTime)
        {
            if (!CardZoneRowLayout.Finite(deltaTime) || deltaTime < 0) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (!isActiveAndEnabled) return;
            AnimationTime += deltaTime;
            foreach (var view in ordered) if (view != null) view.AdvanceMotion(deltaTime);
            OnMotionAdvanced(deltaTime);
        }
        protected virtual void OnMotionAdvanced(float deltaTime) { }
        protected virtual void Update()
        {
            // OnValidate may run during serialization; defer all Transform writes to the main update.
            if (layoutRefreshRequested)
            {
                layoutRefreshRequested = false;
                if (Data != null)
                {
                    try { SetLayout(Layout, MotionSettings, animate: Application.isPlaying); }
                    catch (ArgumentException error) { Debug.LogWarning(error.Message, this); }
                }
            }
            if (Application.isPlaying) AdvanceMotion(Time.deltaTime);
        }
        protected virtual void LateUpdate() => RefreshCameraFacing();
        protected virtual void OnDisable()
        {
            RestoreUnfacedRotation(); CompleteTransitions();
            foreach (var view in ordered) if (view != null) { view.CompleteLift(); view.StopMotion(); }
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (faceCamera && facingMode == CardZoneFacingMode.KeepAuthored)
                facingMode = CardZoneFacingMode.LookAtCameraPosition;
            faceCamera = false;
        }

        /// <summary>Compatibility overload: true aims at the camera position; false restores authored rotation.</summary>
        public void SetCameraFacing(bool enabled, Camera camera = null)
        {
            SetCameraFacing(enabled ? CardZoneFacingMode.LookAtCameraPosition : CardZoneFacingMode.KeepAuthored, camera);
        }

        /// <summary>Sets the zone's baseline orientation without changing child animation transforms.</summary>
        public void SetCameraFacing(CardZoneFacingMode mode, Camera camera = null)
        {
            if (!Enum.IsDefined(typeof(CardZoneFacingMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            faceCamera = false; facingMode = mode; facingCamera = camera; RefreshCameraFacing();
        }

        /// <summary>Tracks the camera independently of cards and sway. Runtime calls this from LateUpdate.</summary>
        public void RefreshCameraFacing()
        {
            if (facingMode == CardZoneFacingMode.KeepAuthored || !isActiveAndEnabled) { RestoreUnfacedRotation(); return; }
            var camera = facingCamera != null ? facingCamera : Camera.main;
            if (camera == null) return;
            // Projection and orientation are independent: a perspective hand can remain parallel to the screen.
            var direction = facingMode == CardZoneFacingMode.ParallelCameraPlane
                ? -camera.transform.forward : camera.transform.position - transform.position;
            if (!CardZoneRowLayout.Finite(direction.sqrMagnitude) || direction.sqrMagnitude < 0.00000001f) return;
            direction.Normalize();
            var forward = Vector3.ProjectOnPlane(camera.transform.up, direction);
            // Looking along the camera's up axis makes that reference degenerate; use its forward instead.
            if (forward.sqrMagnitude < 0.00000001f) forward = Vector3.ProjectOnPlane(camera.transform.forward, direction);
            if (!hasUnfacedRotation) { unfacedLocalRotation = transform.localRotation; hasUnfacedRotation = true; }
            transform.rotation = Quaternion.LookRotation(forward.normalized, direction);
        }

        private void RestoreUnfacedRotation()
        {
            if (!hasUnfacedRotation) return;
            transform.localRotation = unfacedLocalRotation; hasUnfacedRotation = false;
        }

        protected static void ValidateOffset(Vector3 offset)
        {
            if (!CardZoneRowLayout.Finite(offset.x) || !CardZoneRowLayout.Finite(offset.y) || !CardZoneRowLayout.Finite(offset.z))
                throw new ArgumentException("Interaction offset must be finite.");
        }

        public virtual void Clear()
        {
            foreach (var view in ordered) Release(view);
            views.Clear(); ordered = new CardInstanceView[0]; Data = null; boundPrefab = null;
            AnimationTime = 0;
        }

        protected static void Release(CardInstanceView view)
        {
            if (view == null) return;
            view.CompleteTransition();
            view.StopMotion();
            view.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(view.gameObject); else DestroyImmediate(view.gameObject);
        }

        protected virtual void OnDestroy() => Clear();

        protected virtual void OnDrawGizmosSelected()
        {
            var oldMatrix = Gizmos.matrix; var oldColor = Gizmos.color;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(layout.Size.x, 0, layout.Size.y));
            Gizmos.color = new Color(0.3f, 0.7f, 0.7f);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(Mathf.Max(0, layout.Size.x - 2 * layout.Padding.x), 0,
                Mathf.Max(0, layout.Size.y - 2 * layout.Padding.y)));
            Gizmos.matrix = oldMatrix; Gizmos.color = oldColor;
        }
    }
}
