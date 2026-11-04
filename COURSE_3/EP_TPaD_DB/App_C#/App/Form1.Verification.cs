using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace LP_PopovD_App
{
    public partial class Form1
    {
        private static Bitmap CaptureVisual(Control control, bool oval = true)
        {
            using Bitmap raw = new(control.Width, control.Height, PixelFormat.Format32bppPArgb);
            raw.SetResolution(96, 96);
            control.DrawToBitmap(raw, control.ClientRectangle);
            Bitmap result = new(control.Width, control.Height, PixelFormat.Format32bppPArgb);
            result.SetResolution(96, 96);
            using Graphics g = Graphics.FromImage(result);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (oval)
            {
                using GraphicsPath shape = JerseyDrawing.Capsule(
                    new RectangleF(1, 1, control.Width - 2, control.Height - 2));
                g.SetClip(shape);
            }
            g.DrawImage(raw, control.ClientRectangle, 0, 0, raw.Width, raw.Height, GraphicsUnit.Pixel);
            return result;
        }

        private void BeginVerificationReveal(Bitmap next, Bitmap cleanSurface, bool registration)
        {
            List<Bitmap> owned = new();
            try
            {
                CapsuleInput[] moving = registration
                    ? new[] { loginInput, passwordInput, emailInput }
                    : new[] { loginInput, passwordInput };
                Rectangle[] from = moving.Select(field => field.Bounds).ToArray();
                Bitmap[] before = moving.Select(field =>
                {
                    Bitmap image = CaptureVisual(field); owned.Add(image); return image;
                }).ToArray();
                sceneCache = next;
                LayoutSceneControls();
                RefreshInputFabric();
                ShowScreenControls(forCapture: true);
                VerificationMovingField[] fields = moving.Select((field, index) =>
                {
                    Bitmap after = CaptureVisual(field); owned.Add(after);
                    return new VerificationMovingField(before[index], after, from[index], field.Bounds);
                }).ToArray();
                Bitmap code = CaptureVisual(codeInput); owned.Add(code);
                Bitmap background = cleanSurface.Clone(new Rectangle(Point.Empty, cleanSurface.Size), PixelFormat.Format32bppPArgb);
                background.SetResolution(96, 96); owned.Add(background);
                using (Graphics g = Graphics.FromImage(background))
                {
                    StickFeedbackDrawing.Draw(g, ClientSize);
                    // Неподвижные элементы остаются на исходном экране.
                    Control[] fixedControls = registration ? new Control[] { submitButton, backButton }
                        : new Control[] { submitButton, backButton, rememberCheck };
                    foreach (Control control in fixedControls)
                    {
                        using Bitmap image = CaptureVisual(control, control is not RememberCheckBox);
                        g.DrawImage(image, control.Bounds, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel);
                    }
                    GraphicsState state = g.Save();
                    RectangleF canvas = RinkDrawing.GetCanvasBounds(ClientSize);
                    g.TranslateTransform(canvas.X, canvas.Y);
                    g.ScaleTransform(canvas.Width / 1600, canvas.Height / 800);
                    using Font font = new(RinkTypography.FamilyName, 17, FontStyle.Regular, GraphicsUnit.Pixel);
                    using SolidBrush ink = new(Color.FromArgb(255, 223, 178));
                    using StringFormat format = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(authStatus, font, ink, registration ? JerseyDrawing.StatusBounds : JerseyDrawing.LoginStatusBounds, format);
                    g.Restore(state);
                }
                verificationReveal = new VerificationRevealDrawing(background, fields, code, codeInput.Bounds);
                owned.Clear(); // Владение спрайтами передано анимации.
                focusCodeAfterTransition = true;
                HideScreenControls();
                ActiveControl = null;
                transitionClock.Reset();
                transitionTimer.Start();
                Invalidate(true);
            }
            finally { foreach (Bitmap image in owned) image.Dispose(); }
        }
    }
}
