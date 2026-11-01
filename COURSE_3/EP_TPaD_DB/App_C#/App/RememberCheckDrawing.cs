using System.Drawing.Drawing2D;

namespace LP_PopovD_App
{
    internal static class RememberCheckDrawing
    {
        internal static void Draw(Graphics g, RectangleF bounds, bool check, bool highlighted, Font? font = null)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float s = bounds.Height / 28;
            float side = 20 * s;
            RectangleF box = new(bounds.X + 2 * s, bounds.Y + (bounds.Height - side) / 2, side, side);
            using SolidBrush fill = new(highlighted ? Color.FromArgb(67, 102, 129) : Color.FromArgb(28, 53, 77));
            using Pen edge = new(Color.FromArgb(205, 226, 240), Math.Max(1, 1.3f * s));
            g.FillRectangle(fill, box);
            g.DrawRectangle(edge, box.X, box.Y, box.Width, box.Height);
            if (check)
            {
                using Pen tick = new(Color.White, Math.Max(1.6f, 2.3f * s))
                    { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                g.DrawLines(tick, new[] { new PointF(box.X + 4 * s, box.Y + 10 * s),
                    new PointF(box.X + 8 * s, box.Y + 14 * s), new PointF(box.X + 16 * s, box.Y + 5 * s) });
            }
            using Font fallback = new(RinkTypography.FamilyName, 17 * s, FontStyle.Bold, GraphicsUnit.Pixel);
            using SolidBrush ink = new(Color.FromArgb(230, 242, 250));
            using StringFormat format = new() { LineAlignment = StringAlignment.Center };
            g.DrawString("ЗАПОМНИТЬ МЕНЯ", font ?? fallback, ink,
                new RectangleF(box.Right + 11 * s, bounds.Y, bounds.Width - side - 15 * s, bounds.Height), format);
        }
    }
}
