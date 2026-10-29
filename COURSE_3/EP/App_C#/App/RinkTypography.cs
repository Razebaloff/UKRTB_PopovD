using System.Drawing.Drawing2D;

namespace LP_PopovD_App
{
    internal static class RinkTypography
    {
        // Размер рассчитывается по реальным контурам букв, а не по фиксированному шрифту.
        internal static void Draw(Graphics g, string text, RectangleF circle, Color color, bool outline = true)
        {
            string[] lines = text.Replace("\r", "").Split('\n');
            using FontFamily family = new("Segoe UI");
            using StringFormat format = (StringFormat)StringFormat.GenericTypographic.Clone();
            format.FormatFlags |= StringFormatFlags.NoWrap;
            const float em = 100;
            const float gap = 12;
            List<GraphicsPath> glyphs = new();
            try
            {
                float totalHeight = 0;
                foreach (string line in lines)
                {
                    GraphicsPath path = new();
                    path.AddString(line, family, (int)FontStyle.Bold, em, PointF.Empty, format);
                    glyphs.Add(path);
                    totalHeight += path.GetBounds().Height;
                }
                totalHeight += gap * (glyphs.Count - 1);
                float top = -totalHeight / 2;
                float outerRadius = 0;
                using GraphicsPath combined = new();
                foreach (GraphicsPath path in glyphs)
                {
                    RectangleF bounds = path.GetBounds();
                    using Matrix position = new();
                    position.Translate(-bounds.Left - bounds.Width / 2, top - bounds.Top);
                    path.Transform(position);
                    RectangleF positioned = path.GetBounds();
                    float farY = Math.Max(Math.Abs(positioned.Top), Math.Abs(positioned.Bottom));
                    float cornerDistance = MathF.Sqrt(positioned.Width * positioned.Width / 4 + farY * farY);
                    outerRadius = Math.Max(outerRadius, cornerDistance);
                    combined.AddPath(path, false);
                    top += bounds.Height + gap;
                }

                if (outerRadius < 0.1f)
                    return;

                float radius = Math.Min(circle.Width, circle.Height) * 0.465f;
                float scale = radius / outerRadius;
                using Matrix fit = new(scale, 0, 0, scale,
                    circle.X + circle.Width / 2, circle.Y + circle.Height / 2);
                combined.Transform(fit);

                // На фактурном льду светлый контур улучшает читаемость.
                if (outline)
                {
                    using Pen halo = new(Color.FromArgb(210, 239, 250, 255),
                        Math.Max(1, circle.Width * 0.009f)) { LineJoin = LineJoin.Round };
                    g.DrawPath(halo, combined);
                }
                using SolidBrush ink = new(color);
                g.FillPath(ink, combined);
            }
            finally
            {
                foreach (GraphicsPath path in glyphs)
                    path.Dispose();
            }
        }
    }
}
