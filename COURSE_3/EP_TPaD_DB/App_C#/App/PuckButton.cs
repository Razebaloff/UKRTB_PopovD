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
            EntryDrawing.DrawPuck(g, ClientRectangle, Text, hovered || Focused, pressed, drawCaption: false);
        }

        protected override void DrawCaptionOnTop(Graphics g, bool down)
        {
            float diameter = Math.Min(Width, Height);
            float dx = (Width - diameter) / 2;
            float dy = (Height - diameter) / 2;
            RinkTypography.DrawPuckCaption(g, Text,
                new RectangleF(dx + diameter * 10 / 330f,
                    dy + diameter * (down ? 12 : 10) / 330f,
                    diameter * 310 / 330f, diameter * 310 / 330f));
        }
    }
}
