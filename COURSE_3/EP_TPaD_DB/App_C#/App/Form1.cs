using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace LP_PopovD_App
{
    public partial class Form1 : Form
    {
        private bool isFullScreen;
        private Rectangle windowedBounds;
        private AppScreen screen = AppScreen.Entry;
        private Bitmap? sceneCache;
        private Bitmap? transitionFrom;
        private Bitmap? transitionTo;
        private PointF transitionOrigin;
        private readonly Stopwatch transitionClock = new();
        private readonly System.Windows.Forms.Timer transitionTimer;
        private readonly CapsuleInput loginInput;
        private readonly CapsuleInput passwordInput;
        private readonly CapsuleInput emailInput;
        private readonly CapsuleButton submitButton;
        private readonly CapsuleButton backButton;
        private string authStatus = "";
        private bool scheduleFocus;

        public Form1()
        {
            InitializeComponent();
            DoubleBuffered = true;
            KeyPreview = true;
            SetStyle(ControlStyles.ResizeRedraw, true);
            transitionTimer = new System.Windows.Forms.Timer(components!) { Interval = 15 };
            transitionTimer.Tick += (_, _) =>
            {
                if (transitionClock.ElapsedMilliseconds >= TransitionDrawing.DurationMilliseconds)
                    FinishTransition();
                else Invalidate();
            };
            loginInput = new CapsuleInput("Логин", "ВВЕДИТЕ ЛОГИН") { TabIndex = 0 };
            passwordInput = new CapsuleInput("Пароль", "ВВЕДИТЕ ПАРОЛЬ", password: true, maxLength: 128) { TabIndex = 1 };
            emailInput = new CapsuleInput("Электронная почта", "ВВЕДИТЕ ЭЛЕКТРОННУЮ ПОЧТУ", maxLength: 254) { TabIndex = 2 };
            submitButton = new CapsuleButton { Primary = true, TabIndex = 3, AccessibleName = "Отправить форму" };
            backButton = new CapsuleButton { Text = "НАЗАД", TabIndex = 4, AccessibleName = "Назад" };
            foreach (Control control in new Control[] { loginInput, passwordInput, emailInput, submitButton, backButton })
            {
                control.Visible = false;
                Controls.Add(control);
            }
            loginInput.ValueChanged += ClearStatus;
            passwordInput.ValueChanged += ClearStatus;
            emailInput.ValueChanged += ClearStatus;
            entryButton.Click += (_, _) => Navigate(AppScreen.Login, Center(entryButton));
            registrationButton.Click += (_, _) => Navigate(AppScreen.Registration, Center(registrationButton));
            entryScheduleButton.Click += (_, _) => Navigate(AppScreen.Menu, Center(entryScheduleButton), true);
            backButton.Click += (_, _) => Navigate(AppScreen.Entry, Center(backButton));
            submitButton.Click += SubmitAuth;
            LayoutSceneControls();
            ShowScreenControls();
        }

        private static PointF Center(Control control)
            => new(control.Left + control.Width / 2f, control.Top + control.Height / 2f);

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            SetFullScreen(true);
            entryButton.Focus();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (transitionFrom != null) FinishTransition();
            ClearSceneCache();
            if (loginInput != null) LayoutSceneControls();
            Invalidate(true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (transitionFrom != null && transitionTo != null)
            {
                float progress = Math.Clamp(transitionClock.ElapsedMilliseconds /
                    (float)TransitionDrawing.DurationMilliseconds, 0, 1);
                TransitionDrawing.Draw(e.Graphics, ClientSize, transitionFrom, transitionTo, transitionOrigin, progress);
            }
            else PaintScene(e.Graphics, ClientRectangle);
        }

        internal void PaintScene(Graphics graphics) => PaintScene(graphics, ClientRectangle);

        internal void PaintScene(Graphics graphics, Rectangle sourceBounds)
        {
            if (ClientSize.Width < 1 || ClientSize.Height < 1) return;
            if (sceneCache == null)
            {
                sceneCache = new Bitmap(ClientSize.Width, ClientSize.Height, PixelFormat.Format32bppPArgb);
                sceneCache.SetResolution(96, 96);
                using Graphics g = Graphics.FromImage(sceneCache);
                SceneDrawing.Draw(g, ClientSize, screen, includeControls: false, status: authStatus);
            }
            GraphicsState state = graphics.Save();
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.None;
            graphics.DrawImage(sceneCache, new Rectangle(Point.Empty, sourceBounds.Size),
                sourceBounds, GraphicsUnit.Pixel);
            graphics.Restore(state);
        }

        private void ClearSceneCache() { sceneCache?.Dispose(); sceneCache = null; }

        private void DisposeArtwork()
        {
            ClearSceneCache();
            transitionFrom?.Dispose();
            transitionTo?.Dispose();
            transitionFrom = transitionTo = null;
        }

        private Rectangle ToPixels(RectangleF bounds)
        {
            RectangleF canvas = RinkDrawing.GetCanvasBounds(ClientSize);
            float scale = canvas.Width / RinkDrawing.Width;
            return Rectangle.Round(new RectangleF(canvas.X + bounds.X * scale,
                canvas.Y + bounds.Y * scale, bounds.Width * scale, bounds.Height * scale));
        }

        private void LayoutSceneControls()
        {
            PuckButton[] entry = { entryButton, registrationButton, entryScheduleButton };
            for (int i = 0; i < entry.Length; i++)
            {
                PointF center = EntryDrawing.PuckCenters[i];
                entry[i].Bounds = ToPixels(new RectangleF(center.X - 165, center.Y - 165, 330, 330));
            }
            Button[] menu = { rentButton, profileButton, informationButton, scheduleButton };
            for (int i = 0; i < menu.Length; i++)
            {
                PointF center = RinkDrawing.MenuCenters[i];
                float d = RinkDrawing.ButtonDiameter;
                menu[i].Bounds = ToPixels(new RectangleF(center.X - d / 2, center.Y - d / 2, d, d));
            }
            bool registration = screen == AppScreen.Registration;
            RectangleF[] fields = JerseyDrawing.InputBounds(registration);
            CapsuleInput[] inputs = { loginInput, passwordInput, emailInput };
            float scale = RinkDrawing.GetCanvasBounds(ClientSize).Width / RinkDrawing.Width;
            for (int i = 0; i < inputs.Length; i++)
            {
                inputs[i].SetScale(scale);
                if (i < fields.Length) inputs[i].Bounds = ToPixels(fields[i]);
            }
            submitButton.Text = registration ? "ЗАРЕГИСТРИРОВАТЬСЯ" : "ВОЙТИ";
            submitButton.AccessibleName = registration ? "Зарегистрироваться" : "Войти";
            submitButton.SetScale(scale);
            backButton.SetScale(scale);
            submitButton.Bounds = ToPixels(JerseyDrawing.SubmitBounds);
            backButton.Bounds = ToPixels(JerseyDrawing.BackBounds);
        }

        private void HideScreenControls()
        {
            foreach (Control control in Controls) control.Visible = false;
            AcceptButton = null;
        }

        private void ShowScreenControls()
        {
            bool entry = screen == AppScreen.Entry, menu = screen == AppScreen.Menu;
            bool auth = screen is AppScreen.Login or AppScreen.Registration;
            entryButton.Visible = registrationButton.Visible = entryScheduleButton.Visible = entry;
            rentButton.Visible = profileButton.Visible = informationButton.Visible = scheduleButton.Visible = menu;
            loginInput.Visible = passwordInput.Visible = submitButton.Visible = auth;
            emailInput.Visible = screen == AppScreen.Registration;
            backButton.Visible = !entry;
            AcceptButton = entry ? entryButton : auth ? submitButton : null;
        }

        private Bitmap CaptureScreen()
        {
            Bitmap image = new(ClientSize.Width, ClientSize.Height, PixelFormat.Format32bppPArgb);
            image.SetResolution(96, 96);
            try
            {
                DrawToBitmap(image, ClientRectangle);
                return image;
            }
            catch (Exception error) when (error is ExternalException or ArgumentException or NotSupportedException)
            {
                image.Dispose();
                // Резервная отрисовка не читает и не сохраняет введённый пароль.
                return SceneDrawing.Render(ClientSize, screen);
            }
        }

        private void Navigate(AppScreen target, PointF origin, bool focusSchedule = false)
        {
            if (transitionFrom != null || screen == target || ClientSize.Width < 1 || ClientSize.Height < 1) return;
            Bitmap before = CaptureScreen();
            HideScreenControls();
            ActiveControl = null;
            loginInput.Value = passwordInput.Value = emailInput.Value = "";
            authStatus = "";
            screen = target;
            scheduleFocus = focusSchedule;
            ClearSceneCache();
            LayoutSceneControls();
            ShowScreenControls();
            Bitmap after;
            try { after = CaptureScreen(); }
            catch { before.Dispose(); throw; }
            HideScreenControls();
            transitionFrom = before;
            transitionTo = after;
            transitionOrigin = origin;
            transitionClock.Restart();
            transitionTimer.Start();
            Invalidate(true);
        }

        private void FinishTransition()
        {
            transitionTimer.Stop();
            transitionClock.Stop();
            transitionFrom?.Dispose();
            transitionTo?.Dispose();
            transitionFrom = transitionTo = null;
            ShowScreenControls();
            if (screen == AppScreen.Entry) entryButton.Focus();
            else if (screen == AppScreen.Menu) { if (scheduleFocus) scheduleButton.Focus(); else rentButton.Focus(); }
            else backButton.Focus(); // Подсказки полей видны до первого нажатия или Tab.
            Invalidate(true);
        }

        private void ClearStatus(object? sender, EventArgs e)
        {
            if (authStatus.Length == 0) return;
            authStatus = "";
            ClearSceneCache();
            Invalidate();
        }

        private void SubmitAuth(object? sender, EventArgs e)
        {
            if (screen is not AppScreen.Login and not AppScreen.Registration) return;
            var issue = AuthInputRules.Validate(screen == AppScreen.Registration,
                loginInput.Value, passwordInput.Value, emailInput.Value);
            if (issue.Field >= 0)
            {
                authStatus = issue.Message;
                new[] { loginInput, passwordInput, emailInput }[issue.Field].FocusInput();
            }
            else
            {
                // UI не выдаёт заполненную форму за успешную авторизацию.
                authStatus = "Сервис авторизации ещё не подключён.";
            }
            ClearSceneCache();
            Invalidate(true);
        }

        private void SetFullScreen(bool enabled)
        {
            if (transitionFrom != null) FinishTransition();
            SuspendLayout();
            if (enabled)
            {
                windowedBounds = Bounds;
                WindowState = FormWindowState.Normal;
                FormBorderStyle = FormBorderStyle.None;
                Bounds = Screen.FromControl(this).Bounds;
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
            LayoutSceneControls();
            Invalidate(true);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                if (transitionFrom != null) FinishTransition();
                else if (screen != AppScreen.Entry) Navigate(AppScreen.Entry, Center(backButton));
                else Close();
                e.Handled = e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.F11)
            {
                SetFullScreen(!isFullScreen);
                e.Handled = e.SuppressKeyPress = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            transitionTimer.Stop();
            base.OnFormClosing(e);
        }
    }
}

