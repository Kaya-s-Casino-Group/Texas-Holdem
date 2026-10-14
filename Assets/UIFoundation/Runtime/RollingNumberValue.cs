using System;

namespace UIFoundation
{
    /// <summary>Numeric interpolation only. No game state, score objects, UI or clock dependency.</summary>
    public sealed class RollingNumberValue
    {
        public decimal Current { get; private set; }
        public decimal Target { get; private set; }
        private decimal start;
        public bool Retarget(decimal value)
        {
            if (value == Target) return false;
            start = Current; Target = value; return true;
        }
        public void SetProgress(float progress)
        {
            if (float.IsNaN(progress) || float.IsInfinity(progress) || progress < 0 || progress > 1)
                throw new ArgumentOutOfRangeException(nameof(progress));
            if (progress == 1) { Current = Target; return; }
            decimal t = (decimal)progress;
            // Same-sign subtraction stays in range; opposite-sign weighted terms cannot overflow their sum.
            Current = (start >= 0) == (Target >= 0) ? start + (Target - start) * t : start * (1 - t) + Target * t;
        }
        public void SnapTo(decimal value) { start = Current = Target = value; }
    }
}
