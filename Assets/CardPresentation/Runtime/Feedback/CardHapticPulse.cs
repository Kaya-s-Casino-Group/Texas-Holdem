using System;

namespace CardPresentation
{
    // Each sound restarts one short pulse. A later sound gets its own request and deadline.
    internal sealed class CardHapticPulse
    {
        private double stopAt;
        private Action stop;
        private object owner;
        internal void Start(object source, double now, float seconds, Action begin, Action end)
        {
            Stop();
            owner = source; stopAt = now + seconds; stop = end;
            try { begin(); }
            catch { Stop(); throw; }
        }
        internal void Tick(double now) { if (stop != null && now >= stopAt) Stop(); }
        internal void StopOwner(object source) { if (ReferenceEquals(owner, source)) Stop(); }
        internal void Stop()
        {
            var end = stop; stop = null; owner = null;
            end?.Invoke();
        }
    }
}
