using System.Drawing.Drawing2D;

namespace LP_PopovD_App
{
    internal static class EntryDrawing
    {
        internal const float PuckDiameter = 460;

        internal static void Draw(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using Font title = new("Segoe UI", 38, FontStyle.Bold, GraphicsUnit.Pixel);
            using SolidBrush ink = new(RinkDrawing.Blue);
            using StringFormat format = new()
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString("ТЕРРИТОРИЯ ХОККЕЯ", title, ink,
                new RectangleF(100, 70, 1400, 80), format);

            // Мягкая тень под шайбой, центр которой совпадает с центром окна.
            for (int i = 3; i >= 0; i--)
            {
                float spread = 6 + i * 7;
                using SolidBrush shadow = new(Color.FromArgb(14, 17, 45, 68));
                g.FillEllipse(shadow, (RinkDrawing.Width - PuckDiameter) / 2 - spread,
                    (RinkDrawing.Height - PuckDiameter) / 2 + 15 - spread / 2,
                    PuckDiameter + spread * 2, PuckDiameter + spread);
            }
        }

        internal static void DrawPuck(Graphics g, Rectangle bounds, string text, bool highlighted, bool pressed)
        {
            float size = Math.Min(bounds.Width, bounds.Height);
            float edge = size * 0.012f;
            RectangleF body = new(edge, edge, size - edge * 2, size - edge * 2);
            using LinearGradientBrush rim = new(body, Color.FromArgb(72, 81, 91),
                Color.FromArgb(7, 11, 16), LinearGradientMode.Vertical);
            g.FillEllipse(rim, body);

            RectangleF face = new(size * 0.035f, size * 0.025f, size * 0.93f, size * 0.93f);
            using LinearGradientBrush rubber = new(face,
                highlighted ? Color.FromArgb(46, 56, 65) : Color.FromArgb(35, 43, 51),
                Color.FromArgb(9, 14, 20), 65f);
            g.FillEllipse(rubber, face);
            using HatchBrush grain = new(HatchStyle.Percent10,
                Color.FromArgb(24, 132, 143, 153), Color.Transparent);
            g.FillEllipse(grain, face);
            using Pen bevel = new(highlighted ? Color.FromArgb(143, 170, 189) :
                Color.FromArgb(89, 103, 115), Math.Max(1, size * 0.004f));
            g.DrawArc(bevel, face, 195, 135);

            if (pressed)
            {
                using SolidBrush shade = new(Color.FromArgb(35, 0, 0, 0));
                g.FillEllipse(shade, face);
            }
            RectangleF caption = new(size * 0.09f,
                size * 0.09f + (pressed ? size * 0.006f : 0), size * 0.82f, size * 0.82f);
            RinkTypography.Draw(g, text, caption, Color.FromArgb(242, 249, 255), outline: false);
        }
    }
}
