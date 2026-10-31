using System.Drawing.Drawing2D;
using System.ComponentModel;

namespace LP_PopovD_App
{
    internal sealed class CapsuleInput : UserControl
    {
        private readonly TextBox input = new();
        private readonly Label cue = new();
        private Font? inputFont;
        private Font? cueFont;
        private float textPixels;

        internal event EventHandler? ValueChanged;
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal string Value { get => input.Text; set => input.Text = value; }
        internal void FocusInput() => input.Focus();

        internal CapsuleInput(string name, string placeholder, bool password = false, int maxLength = 64)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.Opaque | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            TabStop = false;
            Cursor = Cursors.IBeam;
            AccessibleName = name;
            input.BorderStyle = BorderStyle.None;
            input.TextAlign = HorizontalAlignment.Center;
            input.BackColor = JerseyDrawing.InputFill;
            input.ForeColor = JerseyDrawing.InputInk;
            input.UseSystemPasswordChar = password;
            input.MaxLength = maxLength;
            input.AccessibleName = name;
            input.TabIndex = 0;
            if (password) input.ImeMode = ImeMode.Disable;
            cue.Text = placeholder;
            cue.TextAlign = ContentAlignment.MiddleCenter;
            cue.BackColor = JerseyDrawing.InputFill;
            cue.ForeColor = Color.FromArgb(73, 100, 120);
            cue.TabStop = false;
            cue.Cursor = Cursors.IBeam;
            cue.AccessibleName = placeholder;
            cue.Click += (_, _) => input.Focus();
            input.Enter += (_, _) => { cue.Visible = false; Invalidate(); };
            input.Leave += (_, _) => { UpdateCue(); Invalidate(); };
            input.TextChanged += (_, _) => { UpdateCue(); ValueChanged?.Invoke(this, EventArgs.Empty); };
            Controls.Add(input);
            Controls.Add(cue);
            cue.BringToFront();
            SetScale(1);
        }

        internal void SetScale(float scale)
        {
            float pixels = Math.Max(12, 22 * scale);
            if (Math.Abs(textPixels - pixels) < 0.05f) return;
            textPixels = pixels;
            Font? oldInput = inputFont, oldCue = cueFont;
            inputFont = new Font(RinkTypography.FamilyName, pixels, FontStyle.Regular, GraphicsUnit.Pixel);
            cueFont = new Font(RinkTypography.FamilyName, pixels * 0.92f, FontStyle.Bold, GraphicsUnit.Pixel);
            input.Font = inputFont;
            cue.Font = cueFont;
            oldInput?.Dispose();
            oldCue?.Dispose();
            LayoutInput();
        }

        private void UpdateCue()
        {
            cue.Visible = input.TextLength == 0 && !input.Focused;
            if (cue.Visible) cue.BringToFront();
        }

        private void LayoutInput()
        {
            if (Width < 3 || Height < 3) return;
            int inset = Math.Max(12, Height / 3);
            int textHeight = Math.Min(Height - 8, input.PreferredHeight);
            input.Bounds = new Rectangle(inset, (Height - textHeight) / 2,
                Math.Max(1, Width - inset * 2), Math.Max(1, textHeight));
            cue.Bounds = new Rectangle(inset, 4, Math.Max(1, Width - inset * 2), Math.Max(1, Height - 8));
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); LayoutInput(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); input.Focus(); }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (Parent is Form1 form) form.PaintScene(e.Graphics, Bounds);
            else e.Graphics.Clear(Color.FromArgb(22, 53, 78));
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            OnPaintBackground(e);
            base.OnPaint(e);
            if (Width < 3 || Height < 3) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using GraphicsPath shape = JerseyDrawing.Capsule(new RectangleF(1.5f, 1.5f, Width - 3, Height - 3));
            using SolidBrush fill = new(JerseyDrawing.InputFill);
            using Pen edge = new(ContainsFocus ? Color.FromArgb(49, 155, 213) : Color.FromArgb(147, 181, 203),
                ContainsFocus ? 2.5f : 1.5f);
            e.Graphics.FillPath(fill, shape);
            e.Graphics.DrawPath(edge, shape);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { inputFont?.Dispose(); cueFont?.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
