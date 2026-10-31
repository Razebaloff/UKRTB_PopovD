using System.Drawing.Drawing2D;

namespace LP_PopovD_App
{
    internal static class JerseyDrawing
    {
        internal static readonly Color InputFill = Color.FromArgb(244, 248, 250);
        internal static readonly Color InputInk = Color.FromArgb(18, 52, 79);
        internal static readonly RectangleF SubmitBounds = new(600, 608, 400, 60);
        internal static readonly RectangleF BackBounds = new(52, 190, 174, 52);
        internal static readonly RectangleF StatusBounds = new(540, 571, 520, 31);

        internal static RectangleF[] InputBounds(bool registration) => registration
            ? new[] { new RectangleF(560, 310, 480, 64), new RectangleF(560, 406, 480, 64),
                new RectangleF(560, 502, 480, 64) }
            : new[] { new RectangleF(560, 330, 480, 64), new RectangleF(560, 448, 480, 64) };

        internal static GraphicsPath Capsule(RectangleF r)
        {
            GraphicsPath path = new();
            float d = Math.Min(r.Width, r.Height);
            path.AddArc(r.Left, r.Top, d, d, 90, 180);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 180);
            path.CloseFigure();
            return path;
        }

        private static GraphicsPath Shirt()
        {
            GraphicsPath path = new();
            path.AddBezier(708, 206, 745, 226, 855, 226, 892, 206);
            path.AddLine(892, 206, 994, 230);
            path.AddBezier(994, 230, 1056, 247, 1128, 293, 1160, 372);
            path.AddLine(1160, 372, 1240, 654);
            path.AddLine(1240, 654, 1142, 698);
            path.AddLine(1142, 698, 1063, 495);
            path.AddLine(1063, 495, 1088, 744);
            path.AddBezier(1088, 744, 944, 767, 656, 767, 512, 744);
            path.AddLine(512, 744, 537, 495);
            path.AddLine(537, 495, 458, 698);
            path.AddLine(458, 698, 360, 654);
            path.AddLine(360, 654, 440, 372);
            path.AddBezier(440, 372, 472, 293, 544, 247, 606, 230);
            path.CloseFigure();
            return path;
        }

        internal static void Draw(Graphics g, bool registration, bool includeControls, string status = "", RectangleF? viewport = null)
        {
            HeaderDrawing.Draw(g, viewport);
            if (viewport.HasValue && !viewport.Value.IntersectsWith(new RectangleF(345, 192, 915, 590)))
            {
                if (includeControls && viewport.Value.IntersectsWith(new RectangleF(49, 187, 180, 58)))
                    DrawCapsule(g, BackBounds, "НАЗАД", false);
                return;
            }
            using GraphicsPath shirt = Shirt();
            using GraphicsPath shadow = (GraphicsPath)shirt.Clone();
            using (Matrix move = new()) { move.Translate(5, 12); shadow.Transform(move); }
            using SolidBrush shade = new(Color.FromArgb(42, 6, 25, 42));
            g.FillPath(shade, shadow);
            using LinearGradientBrush fabric = new(new RectangleF(354, 206, 892, 550),
                Color.FromArgb(16, 40, 61), Color.FromArgb(31, 76, 105), 15);
            g.FillPath(fabric, shirt);
            GraphicsState state = g.Save();
            g.SetClip(shirt, CombineMode.Intersect);
            using SolidBrush stripe = new(Color.FromArgb(231, 242, 249));
            using SolidBrush blue = new(Color.FromArgb(26, 108, 159));
            g.FillRectangle(stripe, 500, 680, 600, 27);
            g.FillRectangle(blue, 500, 715, 600, 17);
            PointF[] cuff = { new(370, 619), new(468, 663), new(463, 681), new(365, 637) };
            g.FillPolygon(stripe, cuff);
            g.FillPolygon(stripe, cuff.Select(p => new PointF(1600 - p.X, p.Y)).ToArray());
            using Pen sleeveFold = new(Color.FromArgb(28, 138, 184, 211), 7);
            g.DrawBezier(sleeveFold, 464, 380, 441, 468, 406, 545, 401, 616);
            g.DrawBezier(sleeveFold, 1136, 380, 1159, 468, 1194, 545, 1199, 616);
            using Pen knit = new(Color.FromArgb(14, 201, 222, 234), 0.6f);
            for (int y = 216; y < 760; y += 4)
            {
                if (viewport.HasValue && (y + 2 < viewport.Value.Top || y - 1 > viewport.Value.Bottom)) continue;
                for (int x = 360 + (y % 8 == 0 ? 2 : 0); x < 1240; x += 6)
                {
                    if (viewport.HasValue && (x + 3 < viewport.Value.Left || x - 1 > viewport.Value.Right)) continue;
                    g.DrawLine(knit, x, y, x + 2, y + 1);
                }
            }
            using Pen fold = new(Color.FromArgb(44, 0, 9, 18), 12);
            using Pen foldLight = new(Color.FromArgb(22, 155, 200, 224), 7);
            g.DrawBezier(fold, 541, 445, 555, 530, 535, 629, 545, 748);
            g.DrawBezier(foldLight, 557, 447, 571, 538, 550, 625, 559, 746);
            g.DrawBezier(fold, 1059, 445, 1045, 530, 1065, 629, 1055, 748);
            g.DrawBezier(foldLight, 1043, 447, 1029, 538, 1050, 625, 1041, 746);
            using Pen seam = new(Color.FromArgb(105, 117, 162, 189), 1.4f);
            g.DrawBezier(seam, 608, 237, 656, 280, 625, 385, 537, 495);
            g.DrawBezier(seam, 992, 237, 944, 280, 975, 385, 1063, 495);
            g.DrawLine(seam, 529, 735, 1071, 735);
            g.Restore(state);
            using Pen outline = new(Color.FromArgb(178, 9, 31, 50), 2);
            g.DrawPath(outline, shirt);
            using Pen collar = new(Color.FromArgb(28, 110, 159), 12);
            g.DrawBezier(collar, 708, 208, 747, 232, 853, 232, 892, 208);
            using Pen collarStitch = new(Color.FromArgb(206, 222, 235), 1.4f);
            g.DrawBezier(collarStitch, 711, 211, 750, 234, 850, 234, 889, 211);

            using Font title = new(RinkTypography.FamilyName, 28, FontStyle.Bold, GraphicsUnit.Pixel);
            using SolidBrush white = new(Color.FromArgb(239, 247, 252));
            using StringFormat centered = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(registration ? "РЕГИСТРАЦИЯ" : "ВХОД", title, white, new RectangleF(580, 253, 440, 45), centered);
            if (!string.IsNullOrEmpty(status))
            {
                using Font message = new(RinkTypography.FamilyName, 17, FontStyle.Regular, GraphicsUnit.Pixel);
                using SolidBrush warning = new(Color.FromArgb(255, 218, 169));
                g.DrawString(status, message, warning, StatusBounds, centered);
            }
            if (!includeControls) return;
            string[] prompts = registration
                ? new[] { "ВВЕДИТЕ ЛОГИН", "ВВЕДИТЕ ПАРОЛЬ", "ВВЕДИТЕ ЭЛЕКТРОННУЮ ПОЧТУ" }
                : new[] { "ВВЕДИТЕ ЛОГИН", "ВВЕДИТЕ ПАРОЛЬ" };
            RectangleF[] inputs = InputBounds(registration);
            for (int i = 0; i < inputs.Length; i++)
                DrawCapsule(g, inputs[i], prompts[i], false);
            DrawCapsule(g, SubmitBounds, registration ? "ЗАРЕГИСТРИРОВАТЬСЯ" : "ВОЙТИ", true);
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
