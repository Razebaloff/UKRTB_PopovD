using System.Drawing.Drawing2D;

namespace LP_PopovD_App
{
    internal static class JerseyDrawing
    {
        internal static readonly Color InputFill = Color.FromArgb(244, 248, 250);
        internal static readonly Color InputInk = Color.FromArgb(18, 52, 79);
        internal static readonly RectangleF SubmitBounds = new(600, 608, 400, 60);
        internal static readonly RectangleF LoginSubmitBounds = new(600, 646, 400, 60);
        internal static readonly RectangleF BackBounds = new(52, 190, 174, 52);
        internal static readonly RectangleF StatusBounds = new(540, 571, 520, 31);
        internal static readonly RectangleF LoginStatusBounds = new(500, 608, 600, 30);
        internal static readonly RectangleF RememberBounds = new(632, 575, 336, 28);

        internal static RectangleF[] InputBounds(bool registration, bool codeRequested = false) => registration
            ? new[] { new RectangleF(560, 310, 480, 64), new RectangleF(560, 406, 480, 64),
                new RectangleF(560, 502, 480, 64) }
            : codeRequested
                ? new[] { new RectangleF(560, 330, 480, 64), new RectangleF(560, 418, 480, 64), new RectangleF(560, 506, 480, 64) }
                : new[] { new RectangleF(560, 330, 480, 64), new RectangleF(560, 418, 480, 64) };

        internal static GraphicsPath Capsule(RectangleF r)
        {
            GraphicsPath path = new();
            float d = Math.Min(r.Width, r.Height);
            path.AddArc(r.Left, r.Top, d, d, 90, 180);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 180);
            path.CloseFigure();
            return path;
        }

        internal static void Draw(Graphics g, bool registration, bool includeControls, string status = "", RectangleF? viewport = null,
            bool includeSticks = true, bool codeRequested = false, bool authPanels = true)
        {
            HeaderDrawing.Draw(g, viewport, includeSticks);
            if (viewport.HasValue && !viewport.Value.IntersectsWith(JerseyFabricDrawing.PaintBounds))
            {
                if (includeControls && viewport.Value.IntersectsWith(new RectangleF(49, 187, 180, 58)))
                    DrawCapsule(g, BackBounds, "НАЗАД", false);
                return;
            }
            RectangleF[] inputs = InputBounds(registration, codeRequested);
            JerseyFabricDrawing.Draw(g, viewport, authPanels ? inputs : null);

            using Font title = new(RinkTypography.FamilyName, 28, FontStyle.Bold, GraphicsUnit.Pixel);
            using SolidBrush white = new(Color.FromArgb(239, 247, 252));
            using StringFormat centered = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(registration ? "РЕГИСТРАЦИЯ" : "ВХОД", title, white, new RectangleF(580, 253, 440, 45), centered);
            if (!string.IsNullOrEmpty(status))
            {
                using Font message = new(RinkTypography.FamilyName, 17, FontStyle.Regular, GraphicsUnit.Pixel);
                using SolidBrush warning = new(Color.FromArgb(255, 218, 169));
                g.DrawString(status, message, warning, registration ? StatusBounds : LoginStatusBounds, centered);
            }
            if (!includeControls) return;
            string[] prompts = registration
                ? new[] { "ВВЕДИТЕ ЛОГИН", "ВВЕДИТЕ ПАРОЛЬ", "ВВЕДИТЕ ЭЛЕКТРОННУЮ ПОЧТУ" }
                : codeRequested ? new[] { "ВВЕДИТЕ ЛОГИН", "ВВЕДИТЕ ПАРОЛЬ", "КОД ИЗ ЭЛЕКТРОННОЙ ПОЧТЫ" }
                : new[] { "ВВЕДИТЕ ЛОГИН", "ВВЕДИТЕ ПАРОЛЬ" };
            for (int i = 0; i < inputs.Length; i++)
                JerseyFieldDrawing.DrawCue(g, inputs[i], prompts[i]);
            if (!registration) RememberCheckDrawing.Draw(g, RememberBounds, false, false);
            DrawCapsule(g, registration ? SubmitBounds : LoginSubmitBounds,
                registration ? "ЗАРЕГИСТРИРОВАТЬСЯ" : codeRequested ? "ВОЙТИ" : "ПРИСЛАТЬ КОД", true);
            DrawCapsule(g, BackBounds, "НАЗАД", false);
        }

        internal static void DrawCapsule(Graphics g, RectangleF bounds, string text, bool primary)
        {
            using GraphicsPath path = Capsule(bounds);
            using SolidBrush fill = new(primary ? Color.FromArgb(17, 91, 142) : InputFill);
            using Pen edge = new(primary ? Color.FromArgb(177, 214, 237) : Color.FromArgb(146, 181, 203), 1.8f);
            g.FillPath(fill, path);
            g.DrawPath(edge, path);
            using Font font = new(RinkTypography.FamilyName, primary ? 22 : 21,
                FontStyle.Bold, GraphicsUnit.Pixel);
            using SolidBrush ink = new(primary ? Color.White : InputInk);
            using StringFormat centered = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(text, font, ink, bounds, centered);
        }
    }
}
