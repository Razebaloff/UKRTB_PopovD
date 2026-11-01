using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace LP_PopovD_App
{
    internal sealed class RememberCheckBox : CheckBox
    {
        private Font? labelFont;
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
            Font? old = labelFont;
            labelFont = new Font(RinkTypography.FamilyName, Math.Max(12, 17 * scale), FontStyle.Bold, GraphicsUnit.Pixel);
            Font = labelFont;
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
