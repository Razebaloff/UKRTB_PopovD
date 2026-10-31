using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace LP_PopovD_App
{
    internal static class TransitionDrawing
    {
        internal const int DurationMilliseconds = 580;
        private static float Smooth(float value)
        {
            value = Math.Clamp(value, 0, 1);
            return value * value * (3 - 2 * value);
        }

        private static void Frame(Graphics g, Image image, Rectangle destination, RectangleF source, float opacity)
        {
            if (opacity <= 0) return;
            using ImageAttributes attributes = new();
            attributes.SetColorMatrix(new ColorMatrix { Matrix33 = Math.Clamp(opacity, 0, 1) });
            g.DrawImage(image, destination, source.X, source.Y, source.Width, source.Height,
                GraphicsUnit.Pixel, attributes);
        }

        internal static void Draw(Graphics g, Size size, Image from, Image to, PointF origin, float progress)
        {
            progress = Math.Clamp(progress, 0, 1);
            g.Clear(Color.FromArgb(12, 32, 50));
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            float enter = Math.Clamp(progress / 0.62f, 0, 1);
            float zoom = 1 + 18 * enter * enter * enter;
            float move = Smooth(enter);
            float cx = from.Width / 2f + (origin.X - from.Width / 2f) * move;
            float cy = from.Height / 2f + (origin.Y - from.Height / 2f) * move;
            float width = from.Width / zoom, height = from.Height / zoom;
            RectangleF source = new(Math.Clamp(cx - width / 2, 0, from.Width - width),
                Math.Clamp(cy - height / 2, 0, from.Height - height), width, height);
            float reveal = Smooth((progress - 0.42f) / 0.58f);
            Rectangle destination = new(Point.Empty, size);
            Frame(g, from, destination, source, 1 - reveal);
            float arrivalZoom = 1 + 0.35f * (1 - reveal);
            RectangleF arrival = new(to.Width * (1 - 1 / arrivalZoom) / 2,
                to.Height * (1 - 1 / arrivalZoom) / 2, to.Width / arrivalZoom, to.Height / arrivalZoom);
            Frame(g, to, destination, arrival, reveal);
            DrawSnow(g, size, progress);
        }

        private static void DrawSnow(Graphics g, Size size, float progress)
        {
            float strength = MathF.Sin(MathF.PI * progress);
            if (strength < 0.005f) return;
            Random random = new(63141);
            float scale = Math.Min(size.Width / 1600f, size.Height / 900f);
            using Pen streak = new(Color.White, 1);
            using SolidBrush snow = new(Color.White);
            for (int i = 0; i < 108; i++)
            {
                int edge = i % 4;
                float phase = (random.NextSingle() + progress * (1.4f + random.NextSingle())) % 1;
                float along = random.NextSingle();
                float inset = (0.015f + phase * 0.11f);
                PointF point = edge switch
                {
                    0 => new PointF(size.Width * inset, size.Height * along),
                    1 => new PointF(size.Width * (1 - inset), size.Height * along),
                    2 => new PointF(size.Width * along, size.Height * inset),
                    _ => new PointF(size.Width * along, size.Height * (1 - inset))
                };
                float dx = (point.X - size.Width / 2f) * 0.1f;
                float dy = (point.Y - size.Height / 2f) * 0.1f;
                int alpha = (int)((36 + random.Next(135)) * strength * (1 - phase * 0.45f));
                streak.Color = Color.FromArgb(alpha, 238, 249, 255);
                streak.Width = Math.Max(0.55f, (0.4f + random.NextSingle() * 1.25f) * scale);
                g.DrawLine(streak, point.X, point.Y, point.X + dx, point.Y + dy);
                snow.Color = Color.FromArgb(alpha, 247, 253, 255);
                float dot = (0.8f + random.NextSingle() * 2.2f) * scale;
                g.FillEllipse(snow, point.X, point.Y, dot, dot);
            }
        }
    }
}
