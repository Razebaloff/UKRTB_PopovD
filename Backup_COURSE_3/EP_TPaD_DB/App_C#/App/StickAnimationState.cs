namespace LP_PopovD_App
{
    internal sealed class StickAnimationState
    {
        internal const int DurationMilliseconds = 540;
        private Action? pendingAction;
        private long startedAt = -1;
        internal bool Active => pendingAction != null;

        internal bool TryBegin(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            if (Active) return false;
            pendingAction = action;
            startedAt = -1;
            return true;
        }

        internal float Progress(long now)
        {
            if (!Active) return 1;
            // Отсчёт начинается с первого нарисованного кадра.
            if (startedAt < 0) startedAt = now;
            return Math.Clamp((now - startedAt) / (float)DurationMilliseconds, 0, 1);
        }

        internal bool TryFinish(long now, out Action? action)
        {
            action = null;
            if (!Active || startedAt < 0 || now - startedAt < DurationMilliseconds) return false;
            action = pendingAction;
            Cancel();
            return true;
        }

        internal void Cancel() { pendingAction = null; startedAt = -1; }
    }
}
