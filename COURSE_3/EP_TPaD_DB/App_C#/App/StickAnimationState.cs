namespace LP_PopovD_App
{
    internal sealed class StickAnimationState
    {
        internal const int DurationMilliseconds = 560;
        internal int RemainingTurns { get; private set; }
        internal bool Active => RemainingTurns > 0;
        private long startedAt = -1;

        internal void QueueTurn() => RemainingTurns++;

        internal void Advance(long now)
        {
            if (!Active) return;
            // Первый оборот начинается с первого видимого кадра, после подготовки перехода.
            if (startedAt < 0) { startedAt = now; return; }
            long completed = Math.Max(0, now - startedAt) / DurationMilliseconds;
            if (completed >= RemainingTurns) Clear();
            else
            {
                RemainingTurns -= (int)completed;
                startedAt += completed * DurationMilliseconds;
            }
        }

        internal float Progress(long now)
        {
            Advance(now);
            return Active ? Math.Clamp((now - startedAt) / (float)DurationMilliseconds, 0, 1) : 1;
        }

        internal void Clear() { RemainingTurns = 0; startedAt = -1; }
    }
}
