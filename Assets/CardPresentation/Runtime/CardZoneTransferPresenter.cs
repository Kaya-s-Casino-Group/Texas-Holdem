using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CardPresentation
{
    /// <summary>Explicit hand-to-played visual handoff. Gameplay supplies the two committed snapshots.</summary>
    [ExecuteAlways, DisallowMultipleComponent, AddComponentMenu("Cards/Card Zone Transfer Presenter")]
    public sealed class CardZoneTransferPresenter : MonoBehaviour
    {
        [SerializeField] private HandZoneView source;
        [SerializeField] private PlayedZoneView destination;
        [SerializeField] private CardZoneTransferSettings settings = CardZoneTransferSettings.Default;
        [SerializeField] private Transform controlPoint;
        private readonly List<CardInstanceView> flights = new List<CardInstanceView>();
        private bool committing;
        public HandZoneView Source => source;
        public PlayedZoneView Destination => destination;
        public CardZoneTransferSettings Settings => settings;
        public Transform ControlPoint => controlPoint;
        public bool IsTransferring => flights.Any(card => card != null && card.IsTransferring);

        public void Configure(HandZoneView from, PlayedZoneView to, CardZoneTransferSettings configuration, Transform control = null)
        {
            if (from == null || to == null) throw new ArgumentException("请选择手牌区和出牌区。");
            configuration.Validate();
            if (committing) throw new InvalidOperationException("Cannot configure a route during its commit.");
            if (source != from || destination != to) CompleteTransfers();
            source = from; destination = to; settings = configuration; controlPoint = control;
        }

        /// <summary>Validates both presentations before commitData. Notifications follow both visual commits.
        /// commitData must only assign prevalidated local state; never invoke external callbacks or modify views.</summary>
        public void PresentTransfer(CardZoneViewData sourceAfter, CardZoneViewData destinationAfter,
            IEnumerable<string> transferredIds, bool animate = true, Action commitData = null)
        {
            if (committing) throw new InvalidOperationException("A transfer is already committing.");
            if (source == null || destination == null || source.Data == null || destination.Data == null)
                throw new InvalidOperationException("出牌路径的两个牌区需要先绑定数据。");
            if (sourceAfter == null || destinationAfter == null || transferredIds == null) throw new ArgumentNullException("Transfer snapshots");
            settings.Validate();
            if (sourceAfter.ZoneId != source.Data.ZoneId || destinationAfter.ZoneId != destination.Data.ZoneId || sourceAfter.ZoneId == destinationAfter.ZoneId)
                throw new ArgumentException("Transfer endpoints must retain distinct zone identities.");
            var ids = transferredIds.ToArray(); var set = new HashSet<string>(ids, StringComparer.Ordinal);
            if (set.Count != ids.Length || ids.Any(string.IsNullOrEmpty)) throw new ArgumentException("Transfer identities must be unique.");
            var oldSource = source.Data.Cards.Select(card => card.ViewId).ToArray();
            var oldDestination = destination.Data.Cards.Select(card => card.ViewId).ToArray();
            if (!oldSource.Where(set.Contains).SequenceEqual(ids) ||
                !oldSource.Where(id => !set.Contains(id)).SequenceEqual(sourceAfter.Cards.Select(card => card.ViewId)) ||
                !oldDestination.Concat(ids).SequenceEqual(destinationAfter.Cards.Select(card => card.ViewId)) ||
                oldSource.Intersect(oldDestination).Any())
                throw new ArgumentException("出牌必须按手牌顺序移除，并追加到出牌区列表。");
            destination.ValidateCardCount(destinationAfter.Cards.Count);
            if (source.CardPrefab != destination.CardPrefab) throw new InvalidOperationException("复用视觉实例要求两区使用同一个卡牌 Prefab。");
            ValidateTransform(source.transform); ValidateTransform(destination.transform);
            Vector3? control = controlPoint != null ? controlPoint.position : (Vector3?)null;
            if (control.HasValue && !Finite(control.Value)) throw new ArgumentException("路径控制点必须为有限位置。");
            if (!CardZoneRowLayout.Finite(ids.Length * settings.Interval + settings.FlightSeconds + settings.LandingPause + settings.ResolvedReorderSeconds))
                throw new ArgumentException("Invalid transfer timing.");
            var imports = new Dictionary<string, CardInstanceView>(StringComparer.Ordinal);
            var starts = new CardInstanceView.WorldPose[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                if (!source.TryGetView(ids[i], out var view) || !view.IsTransitionVisible)
                    throw new InvalidOperationException("出牌卡牌必须已有可见的视觉实例；请等待排队中的抽牌起飞。");
                imports.Add(ids[i], view); starts[i] = view.CaptureWorldPose();
            }
            using (var from = source.PreparePresentation(sourceAfter, animate, exports: set))
            using (var to = destination.PreparePresentation(destinationAfter, animate, imports: imports))
            {
                from.ValidateCurrent(); to.ValidateCurrent();
                committing = true;
                try
                {
                    commitData?.Invoke();
                    // Stop hand motion only after capturing its full displayed pose, including lift/flip/sway.
                    foreach (var view in imports.Values) view.CompleteTransition();
                    from.Commit(); to.Commit();
                    bool fly = animate && settings.Enabled && isActiveAndEnabled && source.isActiveAndEnabled && destination.isActiveAndEnabled;
                    flights.RemoveAll(view => view == null || !view.IsTransferring);
                    for (int i = 0; i < ids.Length; i++) if (fly)
                    {
                        var view = imports[ids[i]];
                        var landing = ids.Length > 1 ? imports[ids[ids.Length - 1 - i]].transform : null;
                        float wait = (ids.Length - 1 - i) * settings.Interval;
                        float lift = i == ids.Length - 1 - i ? 0 : .012f * (i + 1) / ids.Length;
                        view.BeginTransfer(starts[i], settings, i * settings.Interval, destination, control, landing, wait, lift);
                        flights.Add(view);
                    }
                    from.NotifyCommitted(); to.NotifyCommitted();
                }
                finally { committing = false; }
            }
        }

        // A single TRS can preserve rotated cards across uniformly scaled zones; reject sheared/mirrored frames up front.
        private static void ValidateTransform(Transform transform)
        {
            var m = transform.localToWorldMatrix;
            Vector3 x = m.GetColumn(0), y = m.GetColumn(1), z = m.GetColumn(2);
            float size = x.magnitude;
            if (!Finite(transform.position) || !Finite(x) || !Finite(y) || !Finite(z) || size < .000001f ||
                Mathf.Abs(y.magnitude - size) > size * .0001f || Mathf.Abs(z.magnitude - size) > size * .0001f ||
                Mathf.Abs(Vector3.Dot(x, y)) > size * size * .0001f ||
                Mathf.Abs(Vector3.Dot(x, z)) > size * size * .0001f || Mathf.Abs(Vector3.Dot(y, z)) > size * size * .0001f ||
                Vector3.Dot(Vector3.Cross(x, y), z) <= 0)
                throw new InvalidOperationException("跨区出牌要求两区使用正向等比缩放，且父级不产生剪切。");
        }
        private static bool Finite(Vector3 value) => CardZoneRowLayout.Finite(value.x) && CardZoneRowLayout.Finite(value.y) && CardZoneRowLayout.Finite(value.z);

        public void CompleteTransfers()
        {
            foreach (var view in flights) if (view != null) view.CompleteTransfer();
            flights.Clear();
        }
        private void OnDisable() => CompleteTransfers();
        private void OnDestroy() => CompleteTransfers();

        private void OnDrawGizmosSelected()
        {
            if (source == null || destination == null || source.Data == null) return;
            IReadOnlyList<CardZoneCardPose> slots;
            try { slots = destination.GetSlotPoses(); } catch (ArgumentException) { return; }
            var color = Gizmos.color; var matrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.identity; Gizmos.color = new Color(.4f, 1, .6f);
            int firstSlot = destination.Data?.Cards.Count ?? 0;
            var selected = source.Views.Where(card => card != null && card.Data.Interaction.IsSelected).ToArray();
            int count = Math.Min(selected.Length, slots.Count - firstSlot);
            for (int index = 0; index < count; index++)
            {
                var card = selected[index];
                var start = card.SwayRoot.position;
                var end = destination.transform.TransformPoint(slots[firstSlot + count - 1 - index].Position);
                var control = controlPoint != null ? controlPoint.position : (start + end) * .5f + destination.transform.up * settings.ArcHeight * destination.AnimationWorldScale * 2;
                var previous = start;
                for (int i = 1; i <= 24; i++)
                {
                    float t = i / 24f;
                    var next = (1 - t) * (1 - t) * start + 2 * (1 - t) * t * control + t * t * end;
                    Gizmos.DrawLine(previous, next); previous = next;
                }
                Gizmos.DrawWireSphere(end, .004f);
                Gizmos.color = new Color(1, .75f, .25f);
                Gizmos.DrawLine(end, destination.transform.TransformPoint(slots[firstSlot + index].Position));
                Gizmos.color = new Color(.4f, 1, .6f);
            }
            Gizmos.color = color; Gizmos.matrix = matrix;
        }
    }
}
