using System.Drawing.Drawing2D;

namespace LP_PopovD_App
{
    // Круглая область нажатия. Видимы только надпись и лёгкая подсветка.
    public class RinkMenuButton : Button
    {
        private bool hovered;
        private bool pressed;
        private Bitmap? buttonLayer, captionLayer;
        private readonly FrostHoverAnimation frost;
        private FrostSpeck[]? frostSpecks;
        // При наведении на шайбу растут только трещины во льду ВОКРУГ,
        // не появляется голубая прозрачная плёнка на самой шайбе.
        protected virtual bool TracksHoverCracks => this is PuckButton;
        protected float HoverCrackAmount => TracksHoverCracks ? frost.Amount : 0;
        protected virtual void OnFrostFrame() { }
        private (Size Size, string Text, int Color, bool Hovered, bool Pressed, bool Focused) layerKey;
        private (Size Size, string Text, int Color, bool Pressed) captionKey;
        protected override bool ShowFocusCues => false;

        public RinkMenuButton()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor |
                ControlStyles.UserPaint | ControlStyles.Opaque | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Color.Transparent;
            ForeColor = RinkDrawing.Blue;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.PushButton;
            frost = new FrostHoverAnimation(() =>
            {
                if (IsDisposed || !IsHandleCreated) return;
                Invalidate();
                OnFrostFrame();
            }, this is PuckButton ? 900 : 285);
            frost.SetVisible(Visible);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            frostSpecks = null;
            if (Width < 2 || Height < 2)
                return;
            using GraphicsPath shape = new();
            shape.AddEllipse(0, 0, Width - 1, Height - 1);
            Region? previous = Region;
            Region = new Region(shape);
            previous?.Dispose();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Лёд и надпись рисуются вместе в OnPaint, в том числе при наведении.
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            if (Parent is Form1 form)
                form.PaintScene(g, Bounds);
            else
            {
                using SolidBrush background = new(Parent?.BackColor ?? Color.FromArgb(225, 241, 250));
                g.FillRectangle(background, ClientRectangle);
            }
            if (Width < 1 || Height < 1)
                return;
            var key = (ClientSize, Text, ForeColor.ToArgb(), hovered, pressed, Focused);
            if (buttonLayer == null || key != layerKey)
            {
                buttonLayer?.Dispose();
                buttonLayer = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                buttonLayer.SetResolution(96, 96);
                using Graphics detail = Graphics.FromImage(buttonLayer);
                detail.SmoothingMode = SmoothingMode.AntiAlias;
                detail.PixelOffsetMode = PixelOffsetMode.HighQuality;
                DrawButton(detail, hovered, pressed, Focused);
                layerKey = key;
            }
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.None;
            g.DrawImage(buttonLayer, ClientRectangle, 0, 0, buttonLayer.Width, buttonLayer.Height, GraphicsUnit.Pixel);
            // Микрокристаллы оседают, растут и тают независимо. Каждый
            // следующий цикл выбирает новые точки из детерминированного seed.
            frostSpecks ??= FrostDrawing.CreateSpecks(ClientSize, true,
                8491 + Text.Length * 71);
            float ambient = frost.Ambient;
            if (ambient > .015f)
                FrostDrawing.DrawSpecks(g, ClientRectangle, frostSpecks,
                    FrostHoverAnimation.Seconds, ambient, true);
            // Построение контура надписи — дорогая операция GDI+.
            // Выполняем её при изменении текста/размера/нажатия, а
            // промежуточные кадры инея используют готовый прозрачный спрайт.
            var labelKey = (ClientSize, Text, ForeColor.ToArgb(), pressed);
            if (captionLayer == null || captionKey != labelKey)
            {
                captionLayer?.Dispose();
                captionLayer = new Bitmap(Width, Height,
                    System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                captionLayer.SetResolution(96, 96);
                using Graphics type = Graphics.FromImage(captionLayer);
                type.SmoothingMode = SmoothingMode.AntiAlias;
                type.PixelOffsetMode = PixelOffsetMode.HighQuality;
                DrawCaptionOnTop(type, pressed);
                captionKey = labelKey;
            }
            g.DrawImageUnscaled(captionLayer, 0, 0);
            // Клавиатурный фокус обозначается подсветкой фактуры в
            // DrawButton, без белого пунктирного кольца вокруг шайбы.
        }

        protected virtual void DrawButton(Graphics g, bool hovered, bool pressed, bool focused)
        {
            if (hovered || pressed || focused)
            {
                int alpha = pressed ? 38 : 17;
                using SolidBrush glow = new(Color.FromArgb(alpha, RinkDrawing.Blue));
                g.FillEllipse(glow, 5, 5, Math.Max(1, Width - 10), Math.Max(1, Height - 10));
            }
        }

        protected virtual void DrawCaptionOnTop(Graphics g, bool down)
        {
            Color ink = down ? Color.FromArgb(9, 49, 96) : ForeColor;
            RinkTypography.Draw(g, Text, new RectangleF(0, 0, Width, Height), ink);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hovered = true;
            if (TracksHoverCracks) frost.SetTarget(true);
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hovered = false;
            pressed = false;
            if (TracksHoverCracks) frost.SetTarget(false);
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            pressed = e.Button == MouseButtons.Left;
            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            pressed = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            Invalidate();
            base.OnGotFocus(e);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            pressed = false;
            Invalidate();
            base.OnLostFocus(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
            {
                pressed = true;
                Invalidate();
            }
            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            pressed = false;
            Invalidate();
            base.OnKeyUp(e);
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            frost?.SetVisible(Visible);
            base.OnVisibleChanged(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                frost?.Dispose();
                captionLayer?.Dispose();
                buttonLayer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
