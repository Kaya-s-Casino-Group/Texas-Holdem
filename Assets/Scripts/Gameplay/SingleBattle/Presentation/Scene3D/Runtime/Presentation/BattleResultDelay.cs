namespace Poker.SingleBattle
{
    /// <summary>Reveal a terminal UI only after presentation has stayed idle for the full delay.</summary>
    public sealed class BattleResultDelay
    {
        public const double Seconds = 1.5;
        private double idleSince = double.NaN;
        public void Reset() => idleSince = double.NaN;
        public bool Ready(bool finished, bool presenting, double now)
        {
            if (!finished || presenting) { Reset(); return false; }
            if (double.IsNaN(idleSince)) idleSince = now;
            return now - idleSince >= Seconds;
        }
    }
}
