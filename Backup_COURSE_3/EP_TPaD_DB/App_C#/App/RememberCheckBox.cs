using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace LP_PopovD_App
{
    internal sealed class RememberCheckBox : CheckBox
    {
        private Font? labelFont;
        private float fontPixels;
        private bool hovered;
        protected override bool ShowFocusCues => false;

        internal RememberCheckBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.Opaque | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Text = "ЗАПОМНИТЬ МЕНЯ";
            AccessibleName = "Запомнить меня";
            AccessibleRole = AccessibleRole.CheckButton;
            Cursor = Cursors.Hand;
            AutoSize = false;
        }

        internal void SetScale(float scale)
        {
            float pixels = Math.Max(12, 17 * scale);
            // Control.Font сравнивает значения шрифтов и может оставить прежний объект.
            // При том же размере сохраняем его живым, как у остальных элементов формы.
            if (labelFont != null && Math.Abs(pixels - fontPixels) < .05f) return;
            Font? old = labelFont;
            Font replacement = new(RinkTypography.FamilyName, pixels, FontStyle.Bold, GraphicsUnit.Pixel);
            Font = replacement;
            labelFont = replacement;
            fontPixels = pixels;
            old?.Dispose();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (Parent is Form1 form) form.PaintScene(e.Graphics, Bounds);
            else e.Graphics.Clear(JerseyFieldDrawing.FallbackFill);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            OnPaintBackground(e);
            RememberCheckDrawing.Draw(e.Graphics, ClientRectangle, Checked, hovered || Focused, Font);
        }


        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }
        protected override void Dispose(bool disposing) { if (disposing) labelFont?.Dispose(); base.Dispose(disposing); }
    }
}
