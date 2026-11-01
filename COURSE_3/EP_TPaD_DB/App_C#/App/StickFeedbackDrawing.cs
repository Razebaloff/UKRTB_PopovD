using System.Drawing.Drawing2D;

namespace LP_PopovD_App
{
    internal static class StickFeedbackDrawing
    {
        private static readonly PointF Center = new(800, 58);

        private static float Smooth(float value)
        {
            value = Math.Clamp(value, 0, 1);
            return value * value * (3 - 2 * value);
        }

        internal static Rectangle PixelBounds(Size size)
        {
            RectangleF c = RinkDrawing.GetCanvasBounds(size);
            float s = c.Width / RinkDrawing.Width;
            return Rectangle.Ceiling(new RectangleF(c.X + 680 * s, c.Y - 32 * s, 240 * s, 180 * s));
        }

        internal static void Draw(Graphics g, Size size, float? progress = null, bool fadeAtEnd = false)
        {
            if (size.Width < 1 || size.Height < 1) return;
            RectangleF canvas = RinkDrawing.GetCanvasBounds(size);
            GraphicsState saved = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TranslateTransform(canvas.X, canvas.Y);
            g.ScaleTransform(canvas.Width / RinkDrawing.Width, canvas.Height / RinkDrawing.Height);
            if (!progress.HasValue || progress.Value <= 0 || (progress.Value >= 1 && !fadeAtEnd))
                HeaderDrawing.DrawSticks(g);
            else
            {
                float p = Math.Clamp(progress.Value, 0, 1);
                float angle = 360 * Smooth(p);
                float opacity = fadeAtEnd ? 1 - Smooth((p - .8f) / .2f) : 1;
                float shrink = 1 - .09f * MathF.Sin(MathF.PI * p);
                DrawWave(g, p, angle);
                float trail = MathF.Sin(MathF.PI * p) * opacity;
                for (int i = 2; i >= 1; i--)
                {
                    float behind = angle - i * 13 * MathF.Sin(MathF.PI * p);
                    DrawTurned(g, false, behind, shrink, trail * .09f / i);
                    DrawTurned(g, true, -behind, shrink, trail * .09f / i);
                }
                DrawTurned(g, false, angle, shrink, opacity);
                DrawTurned(g, true, -angle, shrink, opacity);
            }
            g.Restore(saved);
        }

        private static void DrawTurned(Graphics g, bool mirrored, float angle, float scale, float opacity)
        {
            if (opacity <= 0) return;
            GraphicsState saved = g.Save();
            g.TranslateTransform(Center.X, Center.Y);
            g.RotateTransform(angle);
            g.ScaleTransform(scale, scale);
            g.TranslateTransform(-Center.X, -Center.Y);
            HeaderDrawing.DrawStick(g, mirrored, opacity);
            g.Restore(saved);
        }

        private static void DrawWave(Graphics g, float progress, float angle)
        {
            float q = Math.Clamp((progress - .08f) / .92f, 0, 1);
            float strength = MathF.Sin(MathF.PI * q) * (1 - q);
            if (strength <= .001f) return;
            float radius = 24 + 72 * (1 - (1 - q) * (1 - q));
            RectangleF oval = new(Center.X - radius, Center.Y - radius * .54f, radius * 2, radius * 1.08f);
            using Pen haze = new(Color.FromArgb((int)(31 * strength), 163, 222, 250), 11 * (1 - q) + 2)
                { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using Pen front = new(Color.FromArgb((int)(143 * strength), 230, 249, 255), 1.8f * (1 - q) + .55f)
                { StartCap = LineCap.Round, EndCap = LineCap.Round };
            for (int i = 0; i < 2; i++)
            {
                float start = angle * .3f + i * 180 - 35;
                g.DrawArc(haze, oval, start, 125);
                g.DrawArc(front, oval, start, 125);
            }
            oval.Inflate(-6, -3);
            using Pen wake = new(Color.FromArgb((int)(43 * strength), 185, 231, 252), .8f);
            g.DrawArc(wake, oval, 150 - angle * .2f, 128);
        }
    }
}
