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

        protected override void DrawButton(Graphics g, bool hovered, bool pressed, bool focused)
        {
            EntryDrawing.DrawPuck(g, ClientRectangle, Text, hovered || focused, pressed);
        }
    }
}
