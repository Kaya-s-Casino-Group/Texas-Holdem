using System;
using System.Collections.Generic;

namespace BattlePresentation
{
    /// <summary>A generation of callbacks and owned work. Invalidated before cleanup starts.</summary>
    public sealed class PresentationScope : IDisposable
    {
        private readonly List<Action> cleanup = new List<Action>();
        public string Id { get; }
        public bool IsActive { get; private set; } = true;
        public PresentationScope(string id) { Id = id; }
        public void Own(Action release)
        {
            if (release == null) return;
            if (IsActive) cleanup.Add(release); else release();
        }
        public void Own(IDisposable resource) { if (resource != null) Own(resource.Dispose); }
        public void Dispose()
        {
            if (!IsActive) return;
            IsActive = false;
            List<Exception> errors = null;
            for (var i = cleanup.Count - 1; i >= 0; --i)
            { try { cleanup[i](); } catch (Exception error) { if (errors == null) errors = new List<Exception>(); errors.Add(error); } }
            cleanup.Clear();
            if (errors != null) throw new AggregateException("Presentation cleanup failed.", errors);
        }
    }

    /// <summary>Exclusive channels transfer ownership. Stale owners cannot restore old coordinates/materials.</summary>
    public sealed class PresentationChannels
    {
        private readonly Dictionary<string, ChannelLease> owners = new Dictionary<string, ChannelLease>();
        public bool IsOwned(string channel) => owners.TryGetValue(channel, out var value) && value.IsCurrent;
        public bool TryAcquire(string channel, PresentationScope scope, out ChannelLease lease, Action release = null)
        {
            lease = null;
            if (IsOwned(channel)) return false;
            lease = Acquire(channel, scope, release);
            return true;
        }
        public ChannelLease Acquire(string channel, PresentationScope scope, Action release = null)
        {
            if (scope == null || !scope.IsActive) throw new InvalidOperationException("An active scope is required.");
            if (owners.TryGetValue(channel, out var old)) old.Revoke();
            var lease = new ChannelLease(this, channel, scope, release);
            owners[channel] = lease;
            scope.Own(lease);
            return lease;
        }
        internal bool Owns(ChannelLease lease) => owners.TryGetValue(lease.Channel, out var value) && ReferenceEquals(value, lease);
        internal void Remove(ChannelLease lease) { if (Owns(lease)) owners.Remove(lease.Channel); }
    }

    public sealed class ChannelLease : IDisposable
    {
        private readonly PresentationChannels owner;
        private readonly PresentationScope scope;
        private readonly Action release;
        private bool revoked;
        internal string Channel { get; }
        public bool IsCurrent => !revoked && scope.IsActive && owner.Owns(this);
        internal ChannelLease(PresentationChannels owner, string channel, PresentationScope scope, Action release)
        { this.owner = owner; Channel = channel; this.scope = scope; this.release = release; }
        public bool TryWrite(Action write) { if (!IsCurrent) return false; write(); return true; }
        internal void Revoke() { revoked = true; }
        public void Dispose()
        {
            if (revoked) return;
            var owns = owner.Owns(this);
            revoked = true;
            owner.Remove(this);
            if (owns) release?.Invoke();
        }
    }
}
