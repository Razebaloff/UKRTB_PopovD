using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace LP_PopovD_App
{
    public partial class Form1 : Form
    {
        private bool isFullScreen, shown, loading = true, preparingAction, changingValues;
        private Rectangle windowedBounds;
        private AppScreen screen = AppScreen.Entry;
        private SceneSurfaceCache? surfaces;
        private Bitmap? sceneCache, transitionFrom, transitionTo, stickBackdrop;
        private PointF transitionOrigin;
        private TransitionFrameRenderer? transitionRenderer;
        private bool revealOnly, focusCodeAfterTransition, scheduleFocus, exporting;
        private readonly Stopwatch transitionClock = new(), stickClock = Stopwatch.StartNew(),
            successClock = new();
        private readonly System.Windows.Forms.Timer transitionTimer, stickTimer, successTimer;
        private readonly StickAnimationState stickAnimation = new();
        private readonly LoginFlowState loginFlow = new();
        private readonly HockeyAccountService accounts = new();
        private readonly RememberedLoginStore preferences = new();
        private LoginSuccessDrawing? loginSuccess;
        private Button? feedbackSource;
        private readonly CapsuleInput loginInput, passwordInput, emailInput, codeInput;
        private readonly RememberCheckBox rememberCheck;
        private readonly CapsuleButton submitButton, backButton;
        private readonly ScheduleBoard scheduleBoard;
        private AppScreen scheduleReturnScreen = AppScreen.Entry;
        private string authStatus = "", rememberedLogin = "", successLogin = "";
        private bool successRemember, successFromRegistration;
        private string? authenticatedLogin;
        // Имя вошедшего пользователя хранится в памяти текущей сессии.
        // Регистрация устанавливает его после записи аккаунта в SQLite.
        internal string? AuthenticatedLogin => authenticatedLogin;

        private bool Busy => loading || preparingAction || stickAnimation.Active || transitionFrom != null || loginSuccess != null;
        private SceneSurfaceKey CurrentKey => new(screen,
            screen == AppScreen.Login ? loginFlow.CodeRequested :
            screen == AppScreen.Registration && accounts.RegistrationCodeRequested);

        public Form1()
        {
            InitializeComponent();
            DoubleBuffered = true;
            KeyPreview = true;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint, true);
            transitionTimer = new System.Windows.Forms.Timer(components!) { Interval = 15 };
            transitionTimer.Tick += (_, _) =>
            {
                int duration = revealOnly ? TransitionDrawing.RevealMilliseconds : TransitionDrawing.DurationMilliseconds;
                if (transitionClock.IsRunning && transitionClock.ElapsedMilliseconds >= duration) FinishTransition();
                else if (revealOnly) Invalidate(ToPixels(new RectangleF(495, 495, 610, 220)));
                else Invalidate();
            };
            stickTimer = new System.Windows.Forms.Timer(components!) { Interval = 15 };
            stickTimer.Tick += (_, _) =>
            {
                if (stickAnimation.TryFinish(stickClock.ElapsedMilliseconds, out Action? action))
                {
                    stickTimer.Stop();
                    stickBackdrop?.Dispose(); stickBackdrop = null;
                    ShowScreenControls();
                    if (feedbackSource?.CanFocus == true) feedbackSource.Focus();
                    feedbackSource = null;
                    Invalidate(true);
                    Update();
                    action?.Invoke();
                }
                else Invalidate(StickFeedbackDrawing.PixelBounds(ClientSize));
            };
            successTimer = new System.Windows.Forms.Timer(components!) { Interval = 15 };
            successTimer.Tick += (_, _) =>
            {
                if (successClock.IsRunning && successClock.ElapsedMilliseconds >= LoginSuccessDrawing.DurationMilliseconds)
                    CompleteLoginAnimation();
                else Invalidate();
            };
            loginInput = new CapsuleInput("Логин", "ВВЕДИТЕ ЛОГИН") { TabIndex = 0 };
            passwordInput = new CapsuleInput("Пароль", "ВВЕДИТЕ ПАРОЛЬ", password: true, maxLength: 128) { TabIndex = 1 };
            emailInput = new CapsuleInput("Электронная почта", "ВВЕДИТЕ ЭЛЕКТРОННУЮ ПОЧТУ", maxLength: 254) { TabIndex = 2 };
            codeInput = new CapsuleInput("Демонстрационный код", "ВВЕДИТЕ ЛЮБОЙ КОД", maxLength: 128) { TabIndex = 2 };
            rememberCheck = new RememberCheckBox { TabIndex = 3 };
            submitButton = new CapsuleButton { Primary = true, TabIndex = 4 };
            backButton = new CapsuleButton(crackOnHover: true)
            { Text = "НАЗАД", TabIndex = 5, AccessibleName = "Назад" };
            scheduleBoard = new ScheduleBoard { TabIndex = 0 };
            Controls.Add(scheduleBoard);
            foreach (Control control in new Control[] { loginInput, passwordInput, emailInput, codeInput, rememberCheck, submitButton, backButton })
            {
                control.Visible = false;
                Controls.Add(control);
            }
            foreach (Control control in Controls)
                if (control is Button button) button.Click += HandleAnimatedButtonClick;
            loginInput.ValueChanged += CredentialsChanged;
            passwordInput.ValueChanged += CredentialsChanged;
            emailInput.ValueChanged += CredentialsChanged;
            codeInput.ValueChanged += ClearStatus;
            rememberedLogin = preferences.Load();
            rememberCheck.CheckedChanged += async (_, _) =>
            {
                if (changingValues || rememberCheck.Checked) return;
                rememberedLogin = "";
                await preferences.SaveAsync("", false);
            };
            HideScreenControls();
            LayoutSceneControls();
        }

        private static PointF Center(Control control)
            => new(control.Left + control.Width / 2f, control.Top + control.Height / 2f);

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            shown = true;
            SetFullScreen(true);
            await LoadSceneAsync();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (IsDisposed || Disposing || loginInput == null || ClientSize.Width < 1 || ClientSize.Height < 1) return;
            if (surfaces?.Size == ClientSize)
            {
                LayoutSceneControls();
                Invalidate(true);
                return;
            }
            // Если регистрация уже сохранена в БД, изменение размера
            // окна не должно возвращать пользователя на форму с
            // использованным кодом. Сразу продолжаем в меню.
            bool registeredDuringAnimation = successFromRegistration && loginSuccess != null;
            if (registeredDuringAnimation)
            {
                authenticatedLogin = successLogin;
                screen = AppScreen.Menu;
                accounts.Reset();
                loginFlow.Reset();
            }
            CancelButtonAnimation();
            DropTransition();
            CancelLoginAnimation();
            preparingAction = false;
            sceneCache = null;
            surfaces?.Dispose();
            surfaces = new SceneSurfaceCache(ClientSize);
            loading = true;
            HideScreenControls();
            LayoutSceneControls();
            if (shown && IsHandleCreated) _ = LoadSceneAsync();
            Invalidate();
        }

        private async Task LoadSceneAsync()
        {
            if (!shown || IsDisposed || Disposing || ClientSize.Width < 1 || ClientSize.Height < 1) return;
            surfaces ??= new SceneSurfaceCache(ClientSize);
            SceneSurfaceCache cache = surfaces;
            try
            {
                Bitmap? image = await cache.GetAsync(CurrentKey);
                if (image == null || IsDisposed || Disposing || cache != surfaces) return;
                sceneCache = image;
                loading = false;
                RefreshInputFabric();
                ShowScreenControls();
                if (screen == AppScreen.Entry) entryButton.Focus();
                Invalidate(true);
                _ = WarmScenesAsync(cache);
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { ShowRenderError(error, cache); }
        }

        private async Task WarmScenesAsync(SceneSurfaceCache cache)
        {
            SceneSurfaceKey[] keys = { new(AppScreen.Login), new(AppScreen.Login, true),
                new(AppScreen.Login, true, true), new(AppScreen.Registration),
                new(AppScreen.Registration, true), new(AppScreen.Registration, true, true),
                new(AppScreen.Menu), new(AppScreen.Entry) };
            try
            {
                foreach (SceneSurfaceKey key in keys)
                {
                    if (cache != surfaces || IsDisposed || Disposing) return;
                    await cache.GetAsync(key);
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (Exception error) { Debug.WriteLine("Scene preparation failed: " + error.GetType().Name); }
        }

        private void ShowRenderError(Exception error, SceneSurfaceCache cache)
        {
            if (IsDisposed || Disposing || cache != surfaces) return;
            loading = false;
            preparingAction = false;
            authStatus = "Не удалось подготовить изображение.";
            Invalidate();
            Debug.WriteLine("Scene rendering failed: " + error.GetType().Name);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (transitionFrom != null && transitionTo != null)
            {
                bool first = !transitionClock.IsRunning;
                if (first) transitionClock.Restart();
                int duration = revealOnly ? TransitionDrawing.RevealMilliseconds : TransitionDrawing.DurationMilliseconds;
                transitionRenderer!.Draw(e.Graphics, first ? 0 : (float)transitionClock.Elapsed.TotalMilliseconds / duration);
                return;
            }
            if (loginSuccess != null)
            {
                bool first = !successClock.IsRunning;
                if (first) successClock.Restart();
                loginSuccess.Draw(e.Graphics, first ? 0 : (float)successClock.Elapsed.TotalMilliseconds);
                return;
            }
            if (stickAnimation.Active && stickBackdrop != null)
            {
                e.Graphics.DrawImageUnscaled(stickBackdrop, 0, 0);
                StickFeedbackDrawing.Draw(e.Graphics, ClientSize, stickAnimation.Progress(stickClock.ElapsedMilliseconds),
                    fadeAtEnd: screen == AppScreen.Menu);
                return;
            }
            PaintScene(e.Graphics, ClientRectangle);
            // Трещины являются частью льда под шайбами, а не содержимым
            // круглых HWND: родительская сцена рисует их за границами кнопки.
            if (screen == AppScreen.Entry && !loading)
            {
                entryButton.PaintCrackedIce(e.Graphics);
                registrationButton.PaintCrackedIce(e.Graphics);
                entryScheduleButton.PaintCrackedIce(e.Graphics);
            }
            if (screen != AppScreen.Entry && !loading)
                backButton.PaintCrackedIce(e.Graphics);
        }

        private async void HandleAnimatedButtonClick(object? sender, EventArgs e)
        {
            if (sender is not Button button || Busy || surfaces == null) return;
            preparingAction = true;
            SceneSurfaceCache cache = surfaces;
            try
            {
                AppScreen? target = button == entryButton ? AppScreen.Login :
                    button == registrationButton ? AppScreen.Registration :
                    button == entryScheduleButton || button == scheduleButton ? AppScreen.Schedule :
                    button == backButton ? (screen == AppScreen.Schedule ? scheduleReturnScreen : AppScreen.Entry) : null;
                if (target.HasValue) await cache.GetAsync(new(target.Value));
                if (button == submitButton && screen == AppScreen.Login)
                {
                    await cache.GetAsync(new(AppScreen.Login, true));
                    if (loginFlow.CodeRequested)
                    {
                        await cache.GetAsync(new(AppScreen.Login, true, true));
                        await cache.GetAsync(new(AppScreen.Menu));
                    }
                }
                if (button == submitButton && screen == AppScreen.Registration)
                {
                    if (!accounts.RegistrationCodeRequested)
                        await cache.GetAsync(new(AppScreen.Registration, true));
                    else
                    {
                        await cache.GetAsync(new(AppScreen.Registration, true, true));
                        await cache.GetAsync(new(AppScreen.Menu));
                    }
                }
                if (cache != surfaces || IsDisposed || Disposing) return;
                StickFeedbackDrawing.Prepare(ClientSize);
                stickBackdrop?.Dispose();
                // Сразу снимаем фон без неподвижных клюшек. Прямоугольное
                // стирание поверх готового снимка создавало видимый квадрат.
                stickBackdrop = CaptureScreen(withoutSticks: true);
                Action action = () => { };
                if (target.HasValue) action = () => _ = NavigateAsync(target.Value, Center(button));
                else if (button == submitButton) action = () => _ = SubmitAuthAsync();
                feedbackSource = button;
                HideScreenControls();
                stickAnimation.TryBegin(action);
                stickTimer.Start();
                Invalidate(true);
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { ShowRenderError(error, cache); ShowScreenControls(); }
            finally { if (cache == surfaces) preparingAction = false; }
        }

        private void CancelButtonAnimation()
        {
            if (stickTimer == null) return;
            stickTimer.Stop();
            stickAnimation.Cancel();
            stickBackdrop?.Dispose(); stickBackdrop = null;
            feedbackSource = null;
            if (loginInput != null) ShowScreenControls();
        }

        internal void PaintScene(Graphics graphics) => PaintScene(graphics, ClientRectangle);

        internal void PaintScene(Graphics graphics, Rectangle sourceBounds, bool withoutSticks = false)
        {
            GraphicsState saved = graphics.Save();
            if (sceneCache == null)
            {
                graphics.Clear(Color.FromArgb(111, 151, 178));
                if (sourceBounds == ClientRectangle)
                {
                    using Font font = new(RinkTypography.FamilyName, 22, FontStyle.Bold, GraphicsUnit.Pixel);
                    using SolidBrush ink = new(Color.White);
                    using StringFormat format = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    graphics.DrawString(authStatus.Length == 0 ? "ЗАГРУЗКА…" : authStatus, font, ink, ClientRectangle, format);
                }
                graphics.Restore(saved);
                return;
            }
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.None;
            graphics.DrawImage(sceneCache, new Rectangle(Point.Empty, sourceBounds.Size), sourceBounds, GraphicsUnit.Pixel);
            graphics.TranslateTransform(-sourceBounds.X, -sourceBounds.Y);
            // Для небольших кнопок вдали от заголовка не перерисовываем
            // клюшки: фоновая сцена берётся из готового bitmap-кэша.
            if (screen != AppScreen.Menu && !withoutSticks &&
                sourceBounds.IntersectsWith(StickFeedbackDrawing.PixelBounds(ClientSize)))
                StickFeedbackDrawing.Draw(graphics, ClientSize);
            if (authStatus.Length > 0 && screen is AppScreen.Login or AppScreen.Registration)
            {
                RectangleF canvas = RinkDrawing.GetCanvasBounds(ClientSize);
                graphics.TranslateTransform(canvas.X, canvas.Y);
                graphics.ScaleTransform(canvas.Width / 1600, canvas.Height / 800);
                using Font font = new(RinkTypography.FamilyName, 17, FontStyle.Regular, GraphicsUnit.Pixel);
                using SolidBrush ink = new(Color.FromArgb(255, 223, 178));
                using StringFormat format = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                graphics.DrawString(authStatus, font, ink,
                    screen == AppScreen.Login ? JerseyDrawing.LoginStatusBounds : JerseyDrawing.StatusBounds, format);
            }
            graphics.Restore(saved);
        }

        private void RefreshInputFabric()
        {
            foreach (Control control in Controls)
                if (control is CapsuleInput field) field.RefreshFabric();
        }

        private void DisposeArtwork()
        {
            surfaces?.Dispose(); surfaces = null; sceneCache = null;
            transitionRenderer?.Dispose(); transitionRenderer = null;
            transitionFrom?.Dispose(); transitionTo?.Dispose(); transitionFrom = transitionTo = null;
            stickBackdrop?.Dispose(); stickBackdrop = null;
            loginSuccess?.Dispose(); loginSuccess = null;
            stickAnimation.Cancel(); stickClock.Stop();
            StickFeedbackDrawing.Dispose();
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
            bool codeRequested = registration ? accounts.RegistrationCodeRequested : loginFlow.CodeRequested;
            RectangleF[] fields = JerseyDrawing.InputBounds(registration, codeRequested);
            CapsuleInput[] inputs = registration
                ? new[] { loginInput, passwordInput, emailInput, codeInput }
                : new[] { loginInput, passwordInput, codeInput };
            float scale = RinkDrawing.GetCanvasBounds(ClientSize).Width / RinkDrawing.Width;
            for (int i = 0; i < inputs.Length; i++)
            {
                inputs[i].SetScale(scale);
                if (i >= fields.Length) continue;
                inputs[i].SceneBounds = fields[i];
                inputs[i].Bounds = ToPixels(fields[i]);
            }
            rememberCheck.SetScale(scale);
            rememberCheck.Bounds = ToPixels(JerseyDrawing.RememberBounds);
            scheduleBoard.Bounds = ToPixels(new RectangleF(110, 125, 1380, 600));
            submitButton.Text = registration
                ? (accounts.RegistrationCodeRequested ? "ЗАРЕГИСТРИРОВАТЬСЯ" : "ПРИСЛАТЬ КОД НА ЭЛ. ПОЧТУ")
                : loginFlow.CodeRequested ? "ВОЙТИ" : "ПРИСЛАТЬ КОД НА ЭЛ. ПОЧТУ";
            submitButton.AccessibleName = submitButton.Text;
            submitButton.SetScale(scale);
            backButton.SetScale(scale);
            submitButton.Bounds = ToPixels(registration ? JerseyDrawing.SubmitBounds : JerseyDrawing.LoginSubmitBounds);
            backButton.Bounds = ToPixels(screen == AppScreen.Schedule
                ? new RectangleF(52, 60, 174, 52) : JerseyDrawing.BackBounds);
        }

        private void HideScreenControls()
        {
            foreach (Control control in Controls) control.Visible = false;
            AcceptButton = null;
        }

        private void ShowScreenControls(bool forCapture = false)
        {
            if (!forCapture && (loading || stickAnimation.Active || transitionFrom != null || loginSuccess != null))
            { HideScreenControls(); return; }
            bool entry = screen == AppScreen.Entry, menu = screen == AppScreen.Menu;
            bool schedule = screen == AppScreen.Schedule;
            bool auth = screen is AppScreen.Login or AppScreen.Registration;
            entryButton.Visible = registrationButton.Visible = entryScheduleButton.Visible = entry;
            rentButton.Visible = profileButton.Visible = informationButton.Visible = scheduleButton.Visible = menu;
            scheduleBoard.Visible = schedule;
            loginInput.Visible = passwordInput.Visible = submitButton.Visible = auth;
            emailInput.Visible = screen == AppScreen.Registration;
            codeInput.Visible = (screen == AppScreen.Login && loginFlow.CodeRequested) ||
                (screen == AppScreen.Registration && accounts.RegistrationCodeRequested);
            rememberCheck.Visible = screen == AppScreen.Login;
            backButton.Visible = !entry;
            ConfigureTabNavigation();
            // Enter на кнопке вызывает именно выбранную Tab кнопку.
            // В формах авторизации Enter в TextBox выполняет Submit.
            AcceptButton = auth ? submitButton : null;
        }

        private void ConfigureTabNavigation()
        {
            // Невидимые элементы автоматически исключаются из Tab;
            // индекс активных задаётся явно для каждого интерфейса.
            if (screen == AppScreen.Entry)
            {
                entryButton.TabIndex = 0;
                registrationButton.TabIndex = 1;
                entryScheduleButton.TabIndex = 2;
            }
            else if (screen == AppScreen.Menu)
            {
                rentButton.TabIndex = 0;
                profileButton.TabIndex = 1;
                informationButton.TabIndex = 2;
                scheduleButton.TabIndex = 3;
                backButton.TabIndex = 4;
            }
            else if (screen == AppScreen.Schedule)
            {
                scheduleBoard.TabIndex = 0;
                backButton.TabIndex = 1;
            }
            else
            {
                loginInput.TabIndex = 0;
                passwordInput.TabIndex = 1;
                bool registration = screen == AppScreen.Registration;
                emailInput.TabIndex = 2;
                codeInput.TabIndex = registration ? 3 : 2;
                int next = registration
                    ? (accounts.RegistrationCodeRequested ? 4 : 3)
                    : (loginFlow.CodeRequested ? 3 : 2);
                if (!registration)
                    rememberCheck.TabIndex = next++;
                submitButton.TabIndex = next++;
                backButton.TabIndex = next;
            }

            foreach (Control control in Controls)
            {
                // CapsuleInput — это контейнер. Остановка по Tab
                // требуется только на его вложенном TextBox.
                control.TabStop = control.Visible && control is not CapsuleInput;
            }
        }

        // Единственный порядок клавиатурной навигации на экране.
        // Контейнеры CapsuleInput не имеют TabStop, но фокус
        // непосредственно устанавливается на вложенный TextBox.
        private Control[] CurrentTabOrder() => screen switch
        {
            AppScreen.Entry => new Control[]
                { entryButton, registrationButton, entryScheduleButton },
            AppScreen.Menu => new Control[]
                { rentButton, profileButton, informationButton, scheduleButton, backButton },
            AppScreen.Schedule => new Control[]
                { scheduleBoard, backButton },
            AppScreen.Registration when accounts.RegistrationCodeRequested => new Control[]
                { loginInput, passwordInput, emailInput, codeInput, submitButton, backButton },
            AppScreen.Registration => new Control[]
                { loginInput, passwordInput, emailInput, submitButton, backButton },
            AppScreen.Login when loginFlow.CodeRequested => new Control[]
                { loginInput, passwordInput, codeInput, rememberCheck, submitButton, backButton },
            _ => new Control[]
                { loginInput, passwordInput, rememberCheck, submitButton, backButton }
        };

        internal bool MoveFocusByTab(bool backwards)
        {
            if (Busy || IsDisposed || Disposing) return false;
            Control[] elements = CurrentTabOrder()
                .Where(control => control.Visible && control.Enabled)
                .ToArray();
            if (elements.Length == 0) return false;

            int index = Array.FindIndex(elements, control =>
                control.Focused || control.ContainsFocus);
            // Индекс -1: к первому (Shift+Tab — к последнему).
            int start = index < 0 ? (backwards ? 0 : -1) : index;
            for (int step = 1; step <= elements.Length; step++)
            {
                int next = (start + (backwards ? -step : step) + elements.Length * 2)
                    % elements.Length;
                Control candidate = elements[next];
                if (candidate is CapsuleInput field)
                    field.FocusInput();
                else
                    candidate.Focus();
                if (candidate.ContainsFocus || candidate.Focused) return true;
            }
            return false;
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if ((keyData & Keys.KeyCode) == Keys.Tab &&
                (keyData & ~(Keys.KeyCode | Keys.Shift)) == Keys.None &&
                MoveFocusByTab((keyData & Keys.Shift) != 0))
                return true;
            return base.ProcessDialogKey(keyData);
        }

        private Bitmap CaptureScreen(bool withoutSticks = false)
        {
            // Не вызываем DrawToBitmap для всей полноэкранной формы: это заставляло
            // WinForms синхронно обходить и перерисовывать все дочерние HWND.
            // Готовую сцену копируем один раз, а текст/поля снимаем небольшими плитками.
            Bitmap image = new(ClientSize.Width, ClientSize.Height, PixelFormat.Format32bppPArgb);
            image.SetResolution(96, 96);
            using Graphics g = Graphics.FromImage(image);
            PaintScene(g, ClientRectangle, withoutSticks);
            foreach (Control control in Controls)
            {
                if (!control.Visible || control.Width < 1 || control.Height < 1 ||
                    !ClientRectangle.IntersectsWith(control.Bounds)) continue;
                using Bitmap tile = new(control.Width, control.Height, PixelFormat.Format32bppPArgb);
                tile.SetResolution(96, 96);
                try
                {
                    control.DrawToBitmap(tile, new Rectangle(Point.Empty, control.Size));
                    g.DrawImageUnscaled(tile, control.Left, control.Top);
                }
                catch (Exception error) when (error is ExternalException or ArgumentException or NotSupportedException)
                {
                    // Несовместимый нативный контрол не должен прерывать переход;
                    // его фоновая ткань уже есть в готовой сцене.
                    Debug.WriteLine("Control snapshot unavailable: " + error.GetType().Name);
                }
            }
            return image;
        }


        private void BeginTransition(Bitmap before, Bitmap after, PointF origin, bool soft = false)
        {
            HideScreenControls();
            ActiveControl = null;
            transitionFrom = before;
            transitionTo = after;
            transitionOrigin = origin;
            transitionRenderer = new TransitionFrameRenderer(ClientSize, before, after, origin, soft,
                soft ? ToPixels(new RectangleF(495, 495, 610, 220)) : null);
            revealOnly = soft;
            transitionClock.Reset();
            transitionTimer.Start();
            Invalidate(true);
        }

        private void ClearValues()
        {
            changingValues = true;
            try { loginInput.Value = passwordInput.Value = emailInput.Value = codeInput.Value = ""; }
            finally { changingValues = false; }
        }

        private async Task NavigateAsync(AppScreen target, PointF origin, bool focusSchedule = false, Bitmap? suppliedBefore = null)
        {
            if (Busy || surfaces == null || screen == target) { suppliedBefore?.Dispose(); return; }
            SceneSurfaceCache cache = surfaces;
            preparingAction = true;
            Bitmap? before = suppliedBefore;
            try
            {
                Bitmap? next = await cache.GetAsync(new(target));
                if (next == null || cache != surfaces || IsDisposed || Disposing) return;
                before ??= CaptureScreen();
                HideScreenControls();
                ClearValues();
                loginFlow.Reset();
                accounts.Reset();
                authStatus = "";
                if (target == AppScreen.Schedule && screen != AppScreen.Schedule)
                    scheduleReturnScreen = screen;
                screen = target;
                if (target == AppScreen.Entry)
                    authenticatedLogin = null;
                scheduleFocus = focusSchedule;
                focusCodeAfterTransition = false;
                if (target == AppScreen.Login)
                {
                    changingValues = true;
                    try { loginInput.Value = rememberedLogin; rememberCheck.Checked = rememberedLogin.Length > 0; }
                    finally { changingValues = false; }
                }
                sceneCache = next;
                LayoutSceneControls();
                RefreshInputFabric();
                ShowScreenControls(forCapture: true);
                Bitmap after = CaptureScreen();
                BeginTransition(before, after, origin);
                before = null;
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { ShowRenderError(error, cache); ShowScreenControls(); }
            finally { before?.Dispose(); if (cache == surfaces) preparingAction = false; }
        }

        private void DropTransition()
        {
            if (transitionTimer == null) return;
            transitionTimer.Stop(); transitionClock.Reset();
            transitionRenderer?.Dispose(); transitionRenderer = null;
            transitionFrom?.Dispose(); transitionTo?.Dispose(); transitionFrom = transitionTo = null;
        }

        private void FinishTransition()
        {
            DropTransition();
            ShowScreenControls();
            if (screen == AppScreen.Entry) entryButton.Focus();
            else if (screen == AppScreen.Menu) { if (scheduleFocus) scheduleButton.Focus(); else rentButton.Focus(); }
            else if (screen == AppScreen.Schedule) scheduleBoard.Focus();
            else if (focusCodeAfterTransition) { codeInput.FocusInput(); focusCodeAfterTransition = false; }
            else if (screen is AppScreen.Login or AppScreen.Registration) loginInput.FocusInput();
            else backButton.Focus();
            Invalidate(true);
        }

        private void ClearStatus(object? sender, EventArgs e)
        {
            if (authStatus.Length == 0) return;
            authStatus = "";
            Invalidate(ToPixels(screen == AppScreen.Login ? JerseyDrawing.LoginStatusBounds : JerseyDrawing.StatusBounds));
        }

        private async void CredentialsChanged(object? sender, EventArgs e)
        {
            if (changingValues) return;
            ClearStatus(sender, e);
            if (surfaces == null || Busy || screen is not (AppScreen.Login or AppScreen.Registration)) return;
            bool registration = screen == AppScreen.Registration;
            if (registration ? !accounts.RegistrationCodeRequested : !loginFlow.CodeRequested) return;

            // Изменение логина/пароля/почты инвалидирует ранее
            // высланный код. Код нельзя использовать для других данных.
            accounts.Reset();
            loginFlow.Reset();
            changingValues = true;
            try { codeInput.Value = ""; } finally { changingValues = false; }

            SceneSurfaceCache cache = surfaces;
            try
            {
                Bitmap? image = await cache.GetAsync(new(screen));
                if (image == null || cache != surfaces || IsDisposed || Disposing) return;
                sceneCache = image;
                LayoutSceneControls();
                RefreshInputFabric();
                ShowScreenControls();
                authStatus = "Данные изменены. Запросите код повторно.";
                Invalidate(true);
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                Debug.WriteLine("Updating verification form: " + error.GetType().Name);
                authStatus = "Не удалось обновить форму.";
                Invalidate(true);
            }
        }

        private async Task SubmitAuthAsync()
        {
            if (Busy || surfaces == null || screen is not (AppScreen.Login or AppScreen.Registration)) return;
            bool registration = screen == AppScreen.Registration;
            var issue = AuthInputRules.Validate(registration,
                loginInput.Value, passwordInput.Value, emailInput.Value);
            if (issue.Field >= 0)
            {
                authStatus = issue.Message;
                new[] { loginInput, passwordInput, emailInput }[issue.Field].FocusInput();
                Invalidate(true);
                return;
            }

            SceneSurfaceCache cache = surfaces;
            bool registrationStored = false;
            preparingAction = true;
            try
            {
                if (registration)
                {
                    if (!accounts.RegistrationCodeRequested)
                    {
                        // Демо: открываем поле после проверки данных, но не
                        // отправляем письмо. SQLite при этом остаётся реальной.
                        Bitmap? next = await cache.GetAsync(new(AppScreen.Registration, true));
                        if (next == null || cache != surfaces || IsDisposed || Disposing) return;
                        authStatus = "Демо: открываем поле кода без отправки письма.";
                        Invalidate(true);
                        await accounts.RequestRegistrationAsync(
                            loginInput.Value.Trim(), passwordInput.Value, emailInput.Value.Trim());
                        Bitmap before = CaptureScreen();
                        sceneCache = next;
                        authStatus = "Письмо не отправляется. Введите любой код.";
                        LayoutSceneControls();
                        RefreshInputFabric();
                        ShowScreenControls(forCapture: true);
                        Bitmap after = CaptureScreen();
                        focusCodeAfterTransition = true;
                        // В регистрации меняются позиции всех полей, а не
                        // только нижняя область — нужен полный переход.
                        BeginTransition(before, after, Center(submitButton));
                    }
                    else if (string.IsNullOrWhiteSpace(codeInput.Value))
                    {
                        authStatus = "Введите любое непустое значение кода.";
                        codeInput.FocusInput();
                        Invalidate(true);
                    }
                    else
                    {
                        // Точно такая же завершающая анимация, что и
                        // при входе: рассеивание четырёх полей, снег,
                        // фамилия на майке и переход сразу в Menu.
                        // Изображения готовим ДО записи в SQLite, чтобы
                        // после успешного INSERT не зависеть от рендера.
                        Bitmap? cleanSurface = await cache.GetAsync(
                            new(AppScreen.Registration, true, true));
                        await cache.GetAsync(new(AppScreen.Menu));
                        if (cleanSurface == null || cache != surfaces || IsDisposed || Disposing) return;
                        await accounts.CompleteRegistrationAsync(codeInput.Value.Trim());
                        registrationStored = true;
                        StartSuccessAnimation(cleanSurface, registration: true);
                    }
                }
                else if (!loginFlow.CodeRequested)
                {
                    Bitmap? next = await cache.GetAsync(new(AppScreen.Login, true));
                    if (next == null || cache != surfaces || IsDisposed || Disposing) return;
                    authStatus = "Проверяем логин и пароль в базе данных…";
                    Invalidate(true);
                    // Нельзя войти по произвольному логину/паролю.
                    // Идентификационные данные сверяются с SQLite,
                    // а поле кода в деморежиме принимает любое непустое значение.
                    // Деморежим входа: принимаем любые непустые логин и
                    // пароль, даже если их нет в базе пользователей.
                    if (HockeyAccountService.DemoVerificationEnabled)
                        await accounts.RequestDemoLoginAsync(loginInput.Value.Trim());
                    else
                        await accounts.RequestLoginAsync(loginInput.Value.Trim(), passwordInput.Value);
                    Bitmap before = CaptureScreen();
                    loginFlow.RequestCode();
                    authStatus = "Демо: письмо не отправляется. Введите любой код.";
                    sceneCache = next;
                    LayoutSceneControls();
                    RefreshInputFabric();
                    ShowScreenControls(forCapture: true);
                    Bitmap after = CaptureScreen();
                    focusCodeAfterTransition = true;
                    BeginTransition(before, after, Center(submitButton), soft: true);
                }
                else if (string.IsNullOrWhiteSpace(codeInput.Value))
                {
                    authStatus = "Введите любое непустое значение кода.";
                    codeInput.FocusInput();
                    Invalidate(true);
                }
                else
                {
                    Bitmap? cleanSurface = await cache.GetAsync(new(AppScreen.Login, true, true));
                    await cache.GetAsync(new(AppScreen.Menu));
                    if (cleanSurface == null || cache != surfaces || IsDisposed || Disposing) return;
                    accounts.CompleteLogin(codeInput.Value.Trim());
                    if (!loginFlow.TryConfirm(codeInput.Value)) return;
                    StartSuccessAnimation(cleanSurface, registration: false);
                }
            }
            catch (OperationCanceledException) { }
            catch (InvalidOperationException error)
            {
                authStatus = error.Message;
                bool expiredOrLocked = registration
                    ? !accounts.RegistrationCodeRequested
                    : loginFlow.CodeRequested && !accounts.LoginCodeRequested;
                if (expiredOrLocked)
                {
                    // Истёкший или заблокированный код не должен оставлять
                    // форму в состоянии, где новый запрос невозможен.
                    if (!registration) loginFlow.Reset();
                    try
                    {
                        Bitmap? initial = await cache.GetAsync(new(screen));
                        if (initial != null && cache == surfaces && !IsDisposed && !Disposing)
                        {
                            sceneCache = initial;
                            changingValues = true;
                            try { codeInput.Value = ""; }
                            finally { changingValues = false; }
                            LayoutSceneControls();
                            RefreshInputFabric();
                        }
                    }
                    catch (Exception restoreError)
                    {
                        Debug.WriteLine("Verification layout reset failed: " +
                            restoreError.GetType().Name);
                    }
                }
                ShowScreenControls();
                Invalidate(true);
            }
            catch (Exception error)
            {
                Debug.WriteLine("Account operation failed: " + error.GetType().Name);
                authStatus = registrationStored
                    ? "Учётная запись создана. Открываем меню без анимации."
                    : "Не удалось обратиться к базе данных.";
                ShowScreenControls();
                Invalidate(true);
            }
            finally
            {
                if (cache == surfaces) preparingAction = false;
            }

            // Если рендер анимации недоступен, регистрация уже
            // завершилась в SQLite: не требуем зарегистрироваться снова.
            if (registrationStored && loginSuccess == null &&
                cache == surfaces && !IsDisposed && !Disposing)
            {
                authenticatedLogin = loginInput.Value.Trim();
                await NavigateAsync(AppScreen.Menu, Center(submitButton));
            }
        }

        private void StartSuccessAnimation(Bitmap cleanSurface, bool registration)
        {
            // Обе ветки используют одну последовательность LoginSuccessDrawing.
            // Конфигурация регистрации учитывает дополнительное поле почты.
            successLogin = loginInput.Value.Trim();
            successRemember = !registration && rememberCheck.Checked;
            Bitmap before = CaptureScreen();
            Bitmap clean;
            try
            {
                clean = cleanSurface.Clone(new Rectangle(Point.Empty, cleanSurface.Size),
                    PixelFormat.Format32bppPArgb);
            }
            catch
            {
                before.Dispose();
                if (!registration) loginFlow.CancelAnimation();
                throw;
            }
            try
            {
                using (Graphics graphics = Graphics.FromImage(clean))
                    StickFeedbackDrawing.Draw(graphics, ClientSize);
                loginSuccess = new LoginSuccessDrawing(ClientSize, before, clean,
                    successLogin, registration);
            }
            catch
            {
                before.Dispose();
                clean.Dispose();
                if (!registration) loginFlow.CancelAnimation();
                throw;
            }

            successFromRegistration = registration;
            HideScreenControls();
            ActiveControl = null;
            successClock.Reset();
            successTimer.Start();
            Invalidate(true);
        }

        private void CompleteLoginAnimation()
        {
            if (loginSuccess == null) return;
            successTimer.Stop(); successClock.Reset();
            Bitmap? final = null;
            try { final = loginSuccess.Render(LoginSuccessDrawing.DurationMilliseconds); }
            catch (Exception error)
            {
                // Не блокируем аутентифицированного пользователя из-за
                // сбоя только графического рендера последнего кадра.
                Debug.WriteLine("Success animation rendering failed: " + error.GetType().Name);
            }
            finally { loginSuccess.Dispose(); loginSuccess = null; }

            // После сохранения регистрации аккаунт уже создан: считать
            // пользователя вошедшим, не спрашивая пароль второй раз.
            authenticatedLogin = successLogin;
            if (!successFromRegistration)
            {
                rememberedLogin = successRemember ? successLogin : "";
                _ = preferences.SaveAsync(successLogin, successRemember);
            }
            successFromRegistration = false;
            PointF origin = Center(submitButton);
            _ = NavigateAsync(AppScreen.Menu, origin, suppliedBefore: final);
        }

        private void CancelLoginAnimation()
        {
            if (successTimer == null) return;
            successTimer.Stop(); successClock.Reset();
            loginSuccess?.Dispose(); loginSuccess = null;
            successFromRegistration = false;
            loginFlow.CancelAnimation();
        }

        private void SetFullScreen(bool enabled)
        {
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
            LayoutSceneControls();
            Invalidate(true);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                if (preparingAction) { e.Handled = e.SuppressKeyPress = true; return; }
                if (stickAnimation.Active) { CancelButtonAnimation(); ShowScreenControls(); Invalidate(true); }
                else if (loginSuccess != null)
                {
                    // Регистрация уже записана в SQLite: Esc ускоряет
                    // завершение анимации, но не отменяет автоматический вход.
                    if (successFromRegistration) CompleteLoginAnimation();
                    else { CancelLoginAnimation(); ShowScreenControls(); codeInput.FocusInput(); Invalidate(true); }
                }
                else if (transitionFrom != null) FinishTransition();
                else if (screen != AppScreen.Entry)
                    _ = NavigateAsync(screen == AppScreen.Schedule ? scheduleReturnScreen : AppScreen.Entry,
                        Center(backButton));
                else Close();
                e.Handled = e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.F11)
            {
                SetFullScreen(!isFullScreen);
                e.Handled = e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.F12)
            {
                ExportCurrentScreen();
                e.Handled = e.SuppressKeyPress = true;
            }
            base.OnKeyDown(e);
        }

        private async void ExportCurrentScreen()
        {
            if (exporting || Busy) return;
            AppScreen exportScreen = screen;
            using SaveFileDialog dialog = new()
            {
                Title = "Сохранить экран в 32K — 30720 × 17280",
                Filter = "PNG без потерь (*.png)|*.png",
                FileName = $"{exportScreen}_32K.png",
                DefaultExt = "png",
                AddExtension = true,
                OverwritePrompt = true
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            exporting = true;
            string oldTitle = Text;
            Text = "ТЕРРИТОРИЯ ХОККЕЯ — сохранение 32K…";
            try
            {
                // Отдельный процесс сохраняет отзывчивость окна и не делит GDI-кэши с UI.
                string executable = Environment.ProcessPath ?? throw new InvalidOperationException("Не найден путь приложения.");
                ProcessStartInfo start = new(executable)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                };
                if (string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet", StringComparison.OrdinalIgnoreCase))
                    start.ArgumentList.Add(typeof(Program).Assembly.Location);
                start.ArgumentList.Add(SceneExport.Argument(exportScreen));
                start.ArgumentList.Add(dialog.FileName);
                start.ArgumentList.Add(SceneExport.Width32K.ToString(System.Globalization.CultureInfo.InvariantCulture));
                using Process renderer = Process.Start(start) ?? throw new InvalidOperationException("Не удалось запустить экспорт.");
                Task<string> errors = renderer.StandardError.ReadToEndAsync();
                await renderer.WaitForExitAsync();
                string details = await errors;
                if (renderer.ExitCode != 0)
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(details) ? "Процесс отрисовки завершился с ошибкой." : details.Trim());
                if (!IsDisposed && !Disposing)
                    MessageBox.Show(this, $"Сохранено: 30720 × 17280 пикселей.\n{dialog.FileName}",
                        "Экспорт 32K", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception error)
            {
                if (!IsDisposed && !Disposing)
                    MessageBox.Show(this, error.Message, "Не удалось сохранить 32K", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                exporting = false;
                if (!IsDisposed && !Disposing) Text = oldTitle;
            }
        }


        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (e.Cancel) return;
            transitionTimer.Stop(); stickTimer.Stop(); successTimer.Stop();
            surfaces?.Dispose(); surfaces = null;
            stickAnimation.Cancel();
        }
    }
}

