namespace LP_PopovD_App
{
    // Только состояние демонстрации. Пароль и код здесь не хранятся.
    internal sealed class LoginFlowState
    {
        internal bool CodeRequested { get; private set; }
        internal bool Animating { get; private set; }

        internal void RequestCode() { if (!Animating) CodeRequested = true; }
        internal bool TryConfirm(string code)
        {
            if (!CodeRequested || Animating || string.IsNullOrWhiteSpace(code)) return false;
            Animating = true;
            return true;
        }
        internal void CancelAnimation() => Animating = false;
        internal void Reset() { CodeRequested = Animating = false; }
    }
}
