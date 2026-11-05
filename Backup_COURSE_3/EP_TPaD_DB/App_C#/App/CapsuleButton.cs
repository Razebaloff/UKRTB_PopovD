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
        private readonly bool crackOnHover;
        private Bitmap? crackLayer;
        private Size crackSize;
        private float lastCrackAmount;
        internal float CrackAmount => crackOnHover ? frost.Amount : 0;

        internal void PaintCrackedIce(Graphics background)
        {
            if (!crackOnHover || !Visible || CrackAmount < .003f ||
                Width < 10 || Height < 10) return;
            if (crackLayer == null || crackSize != ClientSize)
            {
                crackLayer?.Dispose();
                crackSize = ClientSize;
                crackLayer = CapsuleCrackDrawing.Create(ClientSize, 8171);
            }
            CapsuleCrackDrawing.Draw(background, crackLayer, Bounds, CrackAmount);
        }
        private FrostSpeck[]? frostSpecks;
        private Bitmap? captionLayer;
        private (Size Size, string Text, int Foreground, bool Primary, float FontPixels) captionKey;
        [DefaultValue(false)]
        internal bool Primary { get; set; }
        protected override bool ShowFocusCues => false;

        internal CapsuleButton(bool crackOnHover = false)
        {
            this.crackOnHover = crackOnHover;
            SetStyle(ControlStyles.UserPaint | ControlStyles.Opaque | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            UseMnemonic = false;
            AccessibleRole = AccessibleRole.PushButton;
            frost = new FrostHoverAnimation(() =>
            {
                if (IsDisposed || !IsHandleCreated || FindForm()?.WindowState == FormWindowState.Minimized) return;
                Invalidate();
                float amount = CrackAmount;
                if (crackOnHover && Parent != null && Visible && Math.Abs(amount - lastCrackAmount) >= .0005f)
                {
                    lastCrackAmount = amount;
                    if (Parent is Form1 form) form.InvalidateCracks(CapsuleCrackDrawing.Bounds(Bounds));
                    else Parent.Invalidate(CapsuleCrackDrawing.Bounds(Bounds), invalidateChildren: false);
                }
            }, crackOnHover ? 900 : 285);
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
            // Окно кнопки имеет ту же овальную форму, что и отрисовка.
            // Углы не закрашивают фон расположенного под ней расписания.
            if (Width > 2 && Height > 2)
            {
                using GraphicsPath shape = JerseyDrawing.Capsule(new RectangleF(0, 0, Width, Height));
                Region? previous = Region;
                Region = new Region(shape);
                previous?.Dispose();
            }
            frostSpecks = null;
            crackLayer?.Dispose();
            crackLayer = null;
            crackSize = Size.Empty;
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
            frostSpecks ??= FrostDrawing.CreateSpecks(ClientSize, false,
                3751 + Text.Length * 109);
            // На всех овальных кнопках — только тонкие морозные прожилки.
            // Без квадратной вырезки: мягкое затухание узоров под надписью
            // рассчитывается отдельно для каждой прожилки в FrostDrawing.
            float ambient = frost.Ambient;
            if (ambient > .015f)
                FrostDrawing.DrawSpecks(g, ClientRectangle, frostSpecks,
                    FrostHoverAnimation.Seconds, ambient, false, protectCaption: Primary);
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
            // Размер надписи уже учитывает масштаб сцены: переносим
            // пиксели без повторного пересчёта по DPI монитора.
            g.DrawImage(captionLayer, ClientRectangle, 0, 0,
                captionLayer.Width, captionLayer.Height, GraphicsUnit.Pixel);
            // Фокус выделяется небольшим изменением цвета самой кнопки.
            // Отдельная белая контурная линия не создаётся.
        }
        protected override void OnMouseEnter(EventArgs e)
        {
            hovered = true;
            if (crackOnHover) frost.SetTarget(true);
            Invalidate();
            base.OnMouseEnter(e);
        }
        protected override void OnMouseLeave(EventArgs e)
        {
            hovered = pressed = false;
            if (crackOnHover) frost.SetTarget(false);
            Invalidate();
            base.OnMouseLeave(e);
        }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = e.Button == MouseButtons.Left; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                frost?.Dispose();
                crackLayer?.Dispose();
                captionLayer?.Dispose();
                labelFont?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
