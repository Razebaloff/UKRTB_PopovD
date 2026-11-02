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
        private readonly FrostHoverAnimation frost;
        private Bitmap? frostTexture, iceTexture, captionLayer;
        private Size frostTextureSize;
        private (Size Size, string Text, int Foreground, bool Primary, float FontPixels) captionKey;
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
            frost = new FrostHoverAnimation(() => { if (!IsDisposed && IsHandleCreated) Invalidate(); });
            frost.SetVisible(Visible);
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
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            frostTexture?.Dispose();
            iceTexture?.Dispose();
            frostTexture = iceTexture = null;
            frostTextureSize = Size.Empty;
            captionLayer?.Dispose();
            captionLayer = null;
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            frost?.SetVisible(Visible);
            base.OnVisibleChanged(e);
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
            g.FillPath(fill, shape);
            g.DrawPath(edge, shape);
            // Сначала нарастает иней по краям овальной тканевой вставки,
            // затем поверх него остаётся хорошо читаемая надпись.
            float ambient = frost.Ambient * (1 - frost.Amount);
            if (ambient > .002f || frost.Amount > .002f)
            {
                if (frostTexture == null || frostTextureSize != ClientSize)
                {
                    frostTexture?.Dispose();
                    frostTextureSize = ClientSize;
                    frostTexture = FrostDrawing.CreateTexture(ClientSize, false, 3751 + Text.Length * 109);
                    iceTexture = FrostDrawing.CreateIceLayer(frostTexture, false);
                }
                if (ambient > .002f)
                    FrostDrawing.DrawLayer(g, frostTexture, ClientRectangle, SnowDrawing.Smooth(ambient));
                if (frost.Amount > .002f && iceTexture != null)
                    FrostDrawing.DrawLayer(g, iceTexture, ClientRectangle, frost.Amount);
            }
            var labelKey = (ClientSize, Text, ForeColor.ToArgb(), Primary, fontPixels);
            if (captionLayer == null || captionKey != labelKey)
            {
                captionLayer?.Dispose();
                captionLayer = new Bitmap(Width, Height,
                    System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                captionLayer.SetResolution(96, 96);
                using Graphics label = Graphics.FromImage(captionLayer);
                label.SmoothingMode = SmoothingMode.AntiAlias;
                label.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                using SolidBrush ink = new(Primary ? Color.White : JerseyDrawing.InputInk);
                using StringFormat center = new()
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                float measured = label.MeasureString(Text, Font, new PointF(0, 0), center).Width;
                if (measured > Width - 20)
                {
                    using Font fitted = new(RinkTypography.FamilyName,
                        Math.Max(11, Font.Size * (Width - 20) / Math.Max(1, measured)),
                        FontStyle.Bold, GraphicsUnit.Pixel);
                    label.DrawString(Text, fitted, ink, ClientRectangle, center);
                }
                else label.DrawString(Text, Font, ink, ClientRectangle, center);
                captionKey = labelKey;
            }
            g.DrawImageUnscaled(captionLayer, 0, 0);
        }
        protected override void OnMouseEnter(EventArgs e) { hovered = true; frost.SetTarget(true); Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = pressed = false; frost.SetTarget(false); Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = e.Button == MouseButtons.Left; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                frost?.Dispose();
                frostTexture?.Dispose();
                iceTexture?.Dispose();
                captionLayer?.Dispose();
                labelFont?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
