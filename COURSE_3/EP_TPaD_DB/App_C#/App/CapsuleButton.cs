using System.Drawing.Drawing2D;
using System.ComponentModel;

namespace LP_PopovD_App
{
    internal sealed class CapsuleButton : Button
    {
        private bool hovered;
        private bool pressed;
        private Font? labelFont;
        private float fontPixels;
        [DefaultValue(false)]
        internal bool Primary { get; set; }
        protected override bool ShowFocusCues => false;

        internal CapsuleButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.Opaque | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            UseMnemonic = false;
            AccessibleRole = AccessibleRole.PushButton;
        }
        internal void SetScale(float scale)
        {
            float pixels = Math.Max(12, 22 * scale);
            if (Math.Abs(pixels - fontPixels) < 0.05f) return;
            fontPixels = pixels;
            Font? old = labelFont;
            labelFont = new Font(RinkTypography.FamilyName, pixels, FontStyle.Bold, GraphicsUnit.Pixel);
            Font = labelFont;
            old?.Dispose();
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (Parent is Form1 form) form.PaintScene(e.Graphics, Bounds);
            else e.Graphics.Clear(Color.FromArgb(137, 177, 202));
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            OnPaintBackground(e);
            if (Width < 3 || Height < 3) return;
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using GraphicsPath shape = JerseyDrawing.Capsule(new RectangleF(1, 1, Width - 2, Height - 2));
            Color color = Primary ? Color.FromArgb(17, 91, 142) : JerseyDrawing.InputFill;
            if (hovered || Focused) color = Primary ? Color.FromArgb(24, 113, 167) : Color.FromArgb(222, 240, 250);
            if (pressed) color = Primary ? Color.FromArgb(10, 67, 108) : Color.FromArgb(206, 227, 240);
            using SolidBrush fill = new(color);
            using Pen edge = new(Color.FromArgb(145, 188, 214), 1.5f);
            using SolidBrush ink = new(Primary ? Color.White : JerseyDrawing.InputInk);
            using StringFormat center = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.FillPath(fill, shape);
            g.DrawPath(edge, shape);
            g.DrawString(Text, Font, ink, ClientRectangle, center);
        }
        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = e.Button == MouseButtons.Left; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void Dispose(bool disposing) { if (disposing) labelFont?.Dispose(); base.Dispose(disposing); }
    }
}
