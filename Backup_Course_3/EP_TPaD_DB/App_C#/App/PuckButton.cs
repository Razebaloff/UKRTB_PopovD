namespace LP_PopovD_App
{
    public class PuckButton : RinkMenuButton
    {
        private Bitmap? crackTexture;
        private Size crackSize;
        private float lastCrackAmount;

        internal void PaintCrackedIce(Graphics background)
        {
            if (!Visible || HoverCrackAmount <= .002f || Width < 12 || Height < 12) return;
            if (crackTexture == null || crackSize != ClientSize)
            {
                crackTexture?.Dispose();
                crackSize = ClientSize;
                crackTexture = PuckCrackDrawing.Create(ClientSize, 1951 + Text.Length * 41);
            }
            PuckCrackDrawing.Draw(background, crackTexture, Bounds, HoverCrackAmount);
        }

        protected override void OnFrostFrame()
        {
            // Трещины выходят за круглый Region кнопки, поэтому
            // перерисовываем только окружающее её кольцо на Form1.
            float amount = HoverCrackAmount;
            if (Parent == null || !Visible || Math.Abs(amount - lastCrackAmount) < .0005f) return;
            lastCrackAmount = amount;
            using Region ring = new(PuckCrackDrawing.Bounds(Bounds));
            if (Region != null)
            {
                using Region body = Region.Clone();
                body.Translate(Left, Top);
                ring.Exclude(body);
            }
            Parent.Invalidate(ring, invalidateChildren: false);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            crackTexture?.Dispose();
            crackTexture = null;
            crackSize = Size.Empty;
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Parent != null)
                Parent.Invalidate(PuckCrackDrawing.Bounds(Bounds));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) crackTexture?.Dispose();
            base.Dispose(disposing);
        }

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
