using System.Drawing.Drawing2D;

namespace LP_PopovD_App
{
    // Круглая область нажатия. Видимы только надпись и лёгкая подсветка.
    public class RinkMenuButton : Button
    {
        private bool hovered;
        private bool pressed;
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
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
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
            using Bitmap layer = new(Width, Height,
                System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            layer.SetResolution(96, 96);
            using (Graphics detail = Graphics.FromImage(layer))
            {
                detail.SmoothingMode = SmoothingMode.AntiAlias;
                detail.PixelOffsetMode = PixelOffsetMode.HighQuality;
                DrawButton(detail, hovered, pressed, Focused);
            }
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.None;
            g.DrawImage(layer, ClientRectangle, 0, 0, layer.Width, layer.Height, GraphicsUnit.Pixel);
        }

        protected virtual void DrawButton(Graphics g, bool hovered, bool pressed, bool focused)
        {
            if (hovered || pressed || focused)
            {
                int alpha = pressed ? 38 : 17;
                using SolidBrush glow = new(Color.FromArgb(alpha, RinkDrawing.Blue));
                g.FillEllipse(glow, 5, 5, Math.Max(1, Width - 10), Math.Max(1, Height - 10));
            }
            Color ink = pressed ? Color.FromArgb(9, 49, 96) : ForeColor;
            RinkTypography.Draw(g, Text, new RectangleF(0, 0, Width, Height), ink);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hovered = false;
            pressed = false;
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
    }
}
