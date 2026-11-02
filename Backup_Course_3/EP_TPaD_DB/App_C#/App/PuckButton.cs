namespace LP_PopovD_App
{
    public class PuckButton : RinkMenuButton
    {
        public PuckButton()
        {
            Text = "ВОЙТИ";
            AccessibleName = "Войти";
            ForeColor = Color.White;
        }

        // Фокус обозначается оттенком резины, без стандартной пунктирной рамки Windows.
        protected override bool ShowFocusCues => false;

        protected override void DrawButton(Graphics g, bool hovered, bool pressed, bool focused)
        {
            EntryDrawing.DrawPuck(g, ClientRectangle, Text, hovered || Focused, pressed);
        }
    }
}
