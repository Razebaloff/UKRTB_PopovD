using System.Drawing.Drawing2D;

namespace LP_PopovD_App
{
    internal static class HeaderDrawing
    {
        internal static void Draw(Graphics g)
        {
            DrawFlag(g, new RectangleF(632, 28, 106, 64), true);
            DrawFlag(g, new RectangleF(862, 28, 106, 64), false);
            DrawStick(g, false);
            DrawStick(g, true);
            using Font title = new("Segoe UI", 42, FontStyle.Bold, GraphicsUnit.Pixel);
            using StringFormat format = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using SolidBrush halo = new(Color.FromArgb(200, 239, 250, 255));
            using SolidBrush ink = new(RinkDrawing.Blue);
            RectangleF bounds = new(120, 112, 1360, 65);
            g.DrawString("ТЕРРИТОРИЯ ХОККЕЯ", title, halo,
                new RectangleF(bounds.X + 1, bounds.Y + 2, bounds.Width, bounds.Height), format);
            g.DrawString("ТЕРРИТОРИЯ ХОККЕЯ", title, ink, bounds, format);
        }

        private static void DrawFlag(Graphics g, RectangleF r, bool bashkortostan)
        {
            Color[] colors = bashkortostan
                ? new[] { Color.FromArgb(0, 96, 174), Color.White, Color.FromArgb(0, 148, 74) }
                : new[] { Color.White, Color.FromArgb(0, 57, 166), Color.FromArgb(213, 43, 30) };
            using SolidBrush shadow = new(Color.FromArgb(32, 11, 42, 64));
            g.FillRectangle(shadow, r.X + 2, r.Y + 3, r.Width, r.Height);
            for (int i = 0; i < 3; i++)
            {
                using SolidBrush stripe = new(colors[i]);
                g.FillRectangle(stripe, r.X, r.Y + r.Height * i / 3, r.Width, r.Height / 3 + 0.2f);
            }
            if (bashkortostan)
            {
                float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
                using Pen gold = new(Color.FromArgb(211, 166, 40), 0.85f);
                using SolidBrush flower = new(gold.Color);
                g.DrawEllipse(gold, cx - 8, cy - 8, 16, 16);
                g.DrawLine(gold, cx, cy + 6, cx, cy - 4);
                for (int i = 0; i < 7; i++)
                {
                    float angle = (205 + i * 21.6667f) * MathF.PI / 180;
                    float x = cx + MathF.Cos(angle) * 5.4f;
                    float y = cy + MathF.Sin(angle) * 5.4f;
                    g.DrawLine(gold, cx, cy + 4, x, y);
                    g.FillEllipse(flower, x - 1.1f, y - 1.1f, 2.2f, 2.2f);
                }
            }
            using LinearGradientBrush satin = new(r, Color.FromArgb(36, 255, 255, 255),
                Color.FromArgb(15, 0, 15, 30), 25f);
            g.FillRectangle(satin, r);
            using Pen border = new(Color.FromArgb(105, 67, 98, 119), 0.8f);
            g.DrawRectangle(border, r.X, r.Y, r.Width, r.Height);
        }

        private static void DrawStick(Graphics g, bool mirrored)
        {
            GraphicsState state = g.Save();
            if (mirrored) { g.TranslateTransform(1600, 0); g.ScaleTransform(-1, 1); }
            // Рукоятка сверху, перо снизу; пересекаются именно нижние перья.
            using Pen shaft = new(Color.FromArgb(30, 42, 52), 5) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using Pen edge = new(Color.FromArgb(137, 158, 171), 1);
            g.DrawLine(shaft, 774, 22, 808, 91);
            g.DrawLine(edge, 772.7f, 22, 806.7f, 91);
            using Pen tape = new(Color.FromArgb(224, 233, 239), 1.5f);
            for (int i = 0; i < 6; i++)
            {
                float y = 26 + i * 2.8f, x = 774 + (y - 22) * 34 / 69;
                g.DrawLine(tape, x - 2, y + 1, x + 2, y - 1);
            }
            using GraphicsPath blade = new();
            blade.AddPolygon(new[] { new PointF(806, 86), new PointF(813, 94),
                new PointF(783, 108), new PointF(777, 107), new PointF(776, 101) });
            using SolidBrush rubber = new(Color.FromArgb(23, 34, 43));
            g.FillPath(rubber, blade);
            g.DrawPath(edge, blade);
            g.Restore(state);
        }
    }
}
