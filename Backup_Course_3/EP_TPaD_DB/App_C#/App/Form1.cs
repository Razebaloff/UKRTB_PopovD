using System.Drawing.Drawing2D;

namespace LP_PopovD_App
{
    public partial class Form1 : Form
    {
        private bool isFullScreen;
        private bool menuOpened;
        private Rectangle windowedBounds;
        private Bitmap? iceArtwork;
        private Bitmap? sceneCache;

        public Form1()
        {
            InitializeComponent();
            entryButton.Click += OpenMenu;
            AcceptButton = entryButton;
            DoubleBuffered = true;
            KeyPreview = true;
            SetStyle(ControlStyles.ResizeRedraw, true);
            using Stream? stream = typeof(Form1).Assembly
                .GetManifestResourceStream("LP_PopovD_App.IceFrame.jpg");
            if (stream != null)
            {
                using Image image = Image.FromStream(stream);
                iceArtwork = new Bitmap(image);
            }
            ClearSceneCache();
            LayoutMenuButtons();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            SetFullScreen(true);
            entryButton.Focus();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ClearSceneCache();
            if (rentButton != null)
                LayoutMenuButtons();
            Invalidate(true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            PaintScene(e.Graphics);
        }

        // Форма и кнопки используют одну сцену без отдельных цветных подложек.
        internal void PaintScene(Graphics graphics)
        {
            PaintScene(graphics, ClientRectangle);
        }

        internal void PaintScene(Graphics graphics, Rectangle sourceBounds)
        {
            if (ClientSize.Width < 1 || ClientSize.Height < 1)
                return;

            if (sceneCache == null)
            {
                sceneCache = new Bitmap(ClientSize.Width, ClientSize.Height);
                using Graphics g = Graphics.FromImage(sceneCache);
                g.Clear(Color.FromArgb(225, 241, 250));
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                if (iceArtwork != null)
                    g.DrawImage(iceArtwork, ClientRectangle);

                RectangleF canvas = RinkDrawing.GetCanvasBounds(ClientSize);
                g.TranslateTransform(canvas.X, canvas.Y);
                g.ScaleTransform(canvas.Width / RinkDrawing.Width,
                    canvas.Height / RinkDrawing.Height);
                if (menuOpened)
                    RinkDrawing.Draw(g);
                else
                    EntryDrawing.Draw(g);
            }
            graphics.DrawImage(sceneCache,
                new Rectangle(Point.Empty, sourceBounds.Size), sourceBounds, GraphicsUnit.Pixel);
        }

        private void ClearSceneCache()
        {
            sceneCache?.Dispose();
            sceneCache = null;
        }

        private void DisposeArtwork()
        {
            ClearSceneCache();
            iceArtwork?.Dispose();
            iceArtwork = null;
        }

        private void LayoutMenuButtons()
        {
            RectangleF canvas = RinkDrawing.GetCanvasBounds(ClientSize);
            float scale = canvas.Width / RinkDrawing.Width;
            float puckSize = EntryDrawing.PuckDiameter * scale;
            entryButton.Bounds = Rectangle.Round(new RectangleF(
                (ClientSize.Width - puckSize) / 2, (ClientSize.Height - puckSize) / 2,
                puckSize, puckSize));
            Button[] buttons = { rentButton, profileButton, informationButton, scheduleButton };
            for (int i = 0; i < buttons.Length; i++)
            {
                PointF center = RinkDrawing.MenuCenters[i];
                float diameter = RinkDrawing.ButtonDiameter * scale;
                buttons[i].Bounds = Rectangle.Round(new RectangleF(
                    canvas.X + center.X * scale - diameter / 2,
                    canvas.Y + center.Y * scale - diameter / 2,
                    diameter, diameter));
            }
        }

        private void OpenMenu(object? sender, EventArgs e)
        {
            if (menuOpened)
                return;
            menuOpened = true;
            ClearSceneCache();
            entryButton.Visible = false;
            AcceptButton = null;
            rentButton.Visible = profileButton.Visible =
                informationButton.Visible = scheduleButton.Visible = true;
            Invalidate(true);
            rentButton.Focus();
        }

        private void SetFullScreen(bool enabled)
        {
            SuspendLayout();
            if (enabled)
            {
                windowedBounds = Bounds;
                Rectangle screenBounds = Screen.FromControl(this).Bounds;
                WindowState = FormWindowState.Normal;
                FormBorderStyle = FormBorderStyle.None;
                Bounds = screenBounds;
            }
            else
            {
                FormBorderStyle = FormBorderStyle.Sizable;
                WindowState = FormWindowState.Normal;
                Bounds = windowedBounds;
            }
            isFullScreen = enabled;
            ResumeLayout(true);
            ClearSceneCache();
            LayoutMenuButtons();
            Invalidate(true);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.F11)
            {
                SetFullScreen(!isFullScreen);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            base.OnKeyDown(e);
        }
    }
}

