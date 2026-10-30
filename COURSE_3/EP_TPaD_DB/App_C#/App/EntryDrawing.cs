using System.Drawing.Drawing2D;

namespace LP_PopovD_App
{
    internal static class EntryDrawing
    {
        internal const float PuckDiameter = 330;
        internal static readonly PointF[] PuckCenters =
        {
            new(400, 350), new(1200, 350), new(800, 605)
        };
        internal static readonly string[] Captions = { "ВОЙТИ", "ЗАРЕГИСТРИРОВАТЬСЯ", "РАСПИСАНИЕ" };

        internal static void Draw(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            HeaderDrawing.Draw(g);
            foreach (PointF center in PuckCenters)
            {
                for (int i = 5; i >= 0; i--)
                {
                    float spread = 3 + i * 4;
                    using SolidBrush shadow = new(Color.FromArgb(10, 12, 32, 48));
                    g.FillEllipse(shadow, center.X - PuckDiameter / 2 - spread,
                        center.Y - PuckDiameter / 2 + 12 - spread / 2,
                        PuckDiameter + spread * 2, PuckDiameter + spread);
                }
            }
        }

        private static bool InsideFace(float x, float y)
        {
            float dx = (x - 165) / 159;
            float dy = (y - 158) / 154;
            return dx * dx + dy * dy < 1;
        }

        internal static void DrawPuck(Graphics g, Rectangle bounds, string text, bool highlighted, bool pressed)
        {
            GraphicsState state = g.Save();
            float scale = Math.Min(bounds.Width, bounds.Height) / 330f;
            g.TranslateTransform(bounds.X, bounds.Y);
            g.ScaleTransform(scale, scale);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF body = new(3, 5, 324, 322);
            using LinearGradientBrush rim = new(body, Color.FromArgb(64, 69, 74),
                Color.FromArgb(3, 5, 7), 75f);
            g.FillEllipse(rim, body);

            // Рифлёный резиновый торец под верхней рабочей поверхностью.
            using Pen groove = new(Color.FromArgb(100, 97, 103, 109), 0.65f);
            for (int x = 7; x < 325; x += 3)
            {
                float normalizedX = (x - 165f) / 162f;
                float bottom = 166 + 161 * MathF.Sqrt(1 - normalizedX * normalizedX);
                if (bottom > 250)
                    g.DrawLine(groove, x, 250, x, bottom - 0.6f);
            }

            RectangleF face = new(5, 3, 320, 310);
            using LinearGradientBrush rubber = new(face,
                highlighted ? Color.FromArgb(44, 48, 53) : Color.FromArgb(31, 35, 39),
                Color.FromArgb(5, 7, 9), 62f);
            g.FillEllipse(rubber, face);
            Random random = new(1703);
            using SolidBrush grain = new(Color.FromArgb(32, 135, 142, 148));
            for (int i = 0; i < 4800; i++)
            {
                float x = random.NextSingle() * 330;
                float y = random.NextSingle() * 320;
                float dot = 0.25f + random.NextSingle() * 0.5f;
                if (InsideFace(x, y) && InsideFace(x + dot, y + dot))
                    g.FillEllipse(grain, x, y, dot, dot);
            }
            using Pen scuff = new(Color.FromArgb(20, 166, 174, 181), 0.45f);
            for (int i = 0; i < 85; i++)
            {
                float x = random.NextSingle() * 330;
                float y = random.NextSingle() * 310;
                float end = x + 2 + random.NextSingle() * 13;
                if (InsideFace(x, y) && InsideFace(end, y - 2))
                    g.DrawLine(scuff, x, y, end, y - 2);
            }
            using Pen bevel = new(highlighted ? Color.FromArgb(193, 211, 224) :
                Color.FromArgb(115, 126, 135), 1.4f);
            using Pen inner = new(Color.FromArgb(42, 174, 184, 193), 0.6f);
            g.DrawArc(bevel, face, 188, 140);
            g.DrawEllipse(inner, 9, 7, 312, 302);
            if (pressed)
            {
                using SolidBrush shade = new(Color.FromArgb(38, 0, 0, 0));
                g.FillEllipse(shade, face);
            }
            // Единая строка занимает почти всю ширину лица шайбы.
            RinkTypography.Draw(g, text, new RectangleF(10, 3 + (pressed ? 2 : 0), 310, 310),
                Color.White, outline: false);
            g.Restore(state);
        }
    }
}
