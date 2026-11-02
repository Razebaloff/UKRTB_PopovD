using System.Drawing.Drawing2D;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace LP_PopovD_App
{
    internal sealed class CapsuleInput : UserControl
    {
        private readonly TextBox input = new();
        private readonly Label cue = new();
        private Font? inputFont;
        private Font? cueFont;
        private float textPixels;
        private IntPtr fabricBrush;
        protected override bool ShowFocusCues => false;

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal RectangleF SceneBounds { get; set; } = new(560, 330, 480, 64);

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
            input.BackColor = JerseyFieldDrawing.FallbackFill;
            input.ForeColor = JerseyFieldDrawing.Ink;
            input.UseSystemPasswordChar = password;
            input.MaxLength = maxLength;
            input.AccessibleName = name;
            input.TabIndex = 0;
            if (password) input.ImeMode = ImeMode.Disable;
            cue.Text = placeholder;
            cue.TextAlign = ContentAlignment.MiddleCenter;
            cue.BackColor = Color.Transparent;
            cue.ForeColor = JerseyFieldDrawing.CueInk;
            cue.TabStop = false;
            cue.Cursor = Cursors.IBeam;
            cue.AccessibleName = placeholder;
            cue.Click += (_, _) => input.Focus();
            input.Enter += (_, _) => { cue.Visible = false; Invalidate(); };
            input.Leave += (_, _) => { UpdateCue(); Invalidate(); };
            input.TextChanged += (_, _) => { UpdateCue(); input.Invalidate(); ValueChanged?.Invoke(this, EventArgs.Empty); };
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
            cueFont = new Font(RinkTypography.FamilyName, Math.Max(12, 21 * scale), FontStyle.Bold, GraphicsUnit.Pixel);
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
            ClearFabricBrush();
            int inset = Math.Max(12, Height / 3);
            int textHeight = Math.Min(Height - 8, input.PreferredHeight);
            input.Bounds = new Rectangle(inset, (Height - textHeight) / 2,
                Math.Max(1, Width - inset * 2), Math.Max(1, textHeight));
            cue.Bounds = new Rectangle(inset, 4, Math.Max(1, Width - inset * 2), Math.Max(1, Height - 8));
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); LayoutInput(); }
        protected override void OnLocationChanged(EventArgs e) { ClearFabricBrush(); base.OnLocationChanged(e); }
        protected override void OnParentChanged(EventArgs e) { ClearFabricBrush(); base.OnParentChanged(e); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); input.Focus(); }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (Parent is Form1 form) form.PaintScene(e.Graphics, Bounds);
            else e.Graphics.Clear(JerseyFieldDrawing.FallbackFill);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            OnPaintBackground(e);
            base.OnPaint(e);

        }

        // Стандартный EDIT сохраняет каретку, выделение, Tab, IME и маскировку пароля.
        // Его фон — точный фрагмент сцены, а обычные символы рисуются поверх ткани.
        protected override void WndProc(ref Message m)
        {
            const int WmCtlColorEdit = 0x0133, WmCtlColorStatic = 0x0138;
            if ((m.Msg == WmCtlColorEdit || m.Msg == WmCtlColorStatic) &&
                input.IsHandleCreated && m.LParam == input.Handle)
            {
                EnsureFabricBrush();
                if (fabricBrush != IntPtr.Zero)
                {
                    SetTextColor(m.WParam, ColorTranslator.ToWin32(input.ForeColor));
                    SetBkMode(m.WParam, 1); // TRANSPARENT: без одноцветных прямоугольников под буквами.
                    SetBrushOrgEx(m.WParam, 0, 0, IntPtr.Zero);
                    m.Result = fabricBrush;
                    return;
                }
            }
            base.WndProc(ref m);
        }

        private void EnsureFabricBrush()
        {
            if (fabricBrush != IntPtr.Zero || Parent is not Form1 form || input.Width < 1 || input.Height < 1) return;
            using Bitmap background = new(input.Width, input.Height, PixelFormat.Format32bppPArgb);
            background.SetResolution(96, 96);
            using (Graphics g = Graphics.FromImage(background))
                form.PaintScene(g, new Rectangle(Left + input.Left, Top + input.Top, input.Width, input.Height));
            IntPtr bitmap = background.GetHbitmap();
            try { fabricBrush = CreatePatternBrush(bitmap); }
            finally { DeleteObject(bitmap); }
        }

        private void ClearFabricBrush()
        {
            if (fabricBrush == IntPtr.Zero) return;
            DeleteObject(fabricBrush); fabricBrush = IntPtr.Zero;
            input.Invalidate();
        }

        internal void RefreshFabric() { ClearFabricBrush(); Invalidate(true); }

        protected override void OnHandleDestroyed(EventArgs e) { ClearFabricBrush(); base.OnHandleDestroyed(e); }

        [DllImport("gdi32.dll")] private static extern IntPtr CreatePatternBrush(IntPtr bitmap);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
        [DllImport("gdi32.dll")] private static extern int SetBkMode(IntPtr dc, int mode);
        [DllImport("gdi32.dll")] private static extern int SetTextColor(IntPtr dc, int color);
        [DllImport("gdi32.dll")] private static extern bool SetBrushOrgEx(IntPtr dc, int x, int y, IntPtr previous);

        protected override void Dispose(bool disposing)
        {
            if (disposing) { ClearFabricBrush(); inputFont?.Dispose(); cueFont?.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
