using System.Drawing.Drawing2D;

namespace LP_PopovD_App
{
    internal static class RinkTypography
    {
        internal const string FamilyName = "Arial";
        private static float sharedPuckRadius;

        private static GraphicsPath PuckCaption(string text)
        {
            if (text == "ЗАРЕГИСТРИРОВАТЬСЯ") text = "ЗАРЕГИСТРИ-\nРОВАТЬСЯ";
            string[] lines = text.Split('\n');
            using FontFamily family = new(FamilyName);
            using StringFormat format = (StringFormat)StringFormat.GenericTypographic.Clone();
            format.FormatFlags |= StringFormatFlags.NoWrap;
            List<GraphicsPath> parts = new();
            try
            {
                float height = 0;
                foreach (string line in lines)
                {
                    GraphicsPath part = new();
                    part.AddString(line, family, (int)FontStyle.Bold, 100, PointF.Empty, format);
                    parts.Add(part);
                    height += part.GetBounds().Height;
                }
                height += (parts.Count - 1) * 12;
                float top = -height / 2;
                GraphicsPath result = new();
                foreach (GraphicsPath part in parts)
                {
                    RectangleF b = part.GetBounds();
                    using Matrix move = new();
                    move.Translate(-b.X - b.Width / 2, top - b.Y);
                    part.Transform(move);
                    result.AddPath(part, false);
                    top += b.Height + 12;
                }
                return result;
            }
            finally { foreach (GraphicsPath part in parts) part.Dispose(); }
        }

        internal static float PuckEmSize(float diameter)
        {
            if (sharedPuckRadius == 0)
                foreach (string caption in EntryDrawing.Captions)
                {
                    using GraphicsPath path = PuckCaption(caption);
                    path.Flatten(null, 0.05f);
                    foreach (PointF point in path.PathPoints)
                        sharedPuckRadius = Math.Max(sharedPuckRadius,
                            MathF.Sqrt(point.X * point.X + point.Y * point.Y));
                }
            return 100 * diameter * 0.465f / sharedPuckRadius;
        }

        internal static void DrawPuckCaption(Graphics g, string text, RectangleF bounds)
        {
            using GraphicsPath path = PuckCaption(text);
            float scale = PuckEmSize(Math.Min(bounds.Width, bounds.Height)) / 100;
            using Matrix transform = new(scale, 0, 0, scale,
                bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            path.Transform(transform);
            using SolidBrush ink = new(Color.White);
            using Pen weight = new(Color.White, Math.Max(0.25f, scale * 0.8f)) { LineJoin = LineJoin.Miter };
            g.DrawPath(weight, path);
            g.FillPath(ink, path);
        }

        // Размер рассчитывается по реальным контурам букв, а не по фиксированному шрифту.
        internal static void Draw(Graphics g, string text, RectangleF circle, Color color, bool outline = true)
        {
            string[] lines = text.Replace("\r", "").Split('\n');
            using FontFamily family = new(FamilyName);
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
                // Небольшое утолщение с угловатыми стыками, без изменения пропорций букв.
                using Pen weight = new(color, Math.Max(0.25f, scale * 0.8f)) { LineJoin = LineJoin.Miter };
                g.DrawPath(weight, combined);
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
