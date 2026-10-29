namespace LP_PopovD_App
{
    public class PuckButton : RinkMenuButton
    {
        public PuckButton()
        {
            Text = "Войти или\nзарегистрироваться";
            AccessibleName = "Войти или зарегистрироваться";
            ForeColor = Color.FromArgb(242, 249, 255);
        }

        protected override void DrawButton(Graphics g, bool hovered, bool pressed, bool focused)
        {
            EntryDrawing.DrawPuck(g, ClientRectangle, Text, hovered || focused, pressed);
        }
    }
}
