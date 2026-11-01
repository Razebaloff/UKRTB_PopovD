using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace LP_PopovD_App
{
    internal static class TransitionDrawing
    {
        internal const int DurationMilliseconds = 440;
        internal const int RevealMilliseconds = 200;
        private static readonly SnowSeed[] snow = SnowDrawing.Seeds(144, 63141);

        private static void Frame(Graphics g, Image image, Rectangle destination, RectangleF source,
            float opacity, ImageAttributes alpha)
        {
            if (opacity <= 0) return;
            alpha.SetColorMatrix(new ColorMatrix { Matrix33 = Math.Clamp(opacity, 0, 1) });
            g.DrawImage(image, destination, source.X, source.Y, source.Width, source.Height, GraphicsUnit.Pixel, alpha);
        }

        internal static void Draw(Graphics g, Size size, Image from, Image to, PointF origin, float progress, bool revealOnly = false)
        {
            float p = Math.Clamp(progress, 0, 1);
            Rectangle destination = new(Point.Empty, size);
            if (p <= 0) { g.DrawImage(from, destination, 0, 0, from.Width, from.Height, GraphicsUnit.Pixel); return; }
            if (p >= 1) { g.DrawImage(to, destination, 0, 0, to.Width, to.Height, GraphicsUnit.Pixel); return; }
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            using ImageAttributes alpha = new();
            if (revealOnly)
            {
                g.DrawImageUnscaled(from, 0, 0);
                Frame(g, to, destination, new RectangleF(0, 0, to.Width, to.Height), SnowDrawing.Smooth(p), alpha);
                return;
            }
            float enter = SnowDrawing.Smooth(p / .85f);
            float zoom = 1 + 9 * enter * enter;
            float cx = from.Width / 2f + (origin.X - from.Width / 2f) * enter;
            float cy = from.Height / 2f + (origin.Y - from.Height / 2f) * enter;
            float width = from.Width / zoom, height = from.Height / zoom;
            RectangleF source = new(Math.Clamp(cx - width / 2, 0, from.Width - width),
                Math.Clamp(cy - height / 2, 0, from.Height - height), width, height);
            g.DrawImage(from, destination, source, GraphicsUnit.Pixel);
            float reveal = SnowDrawing.Smooth((p - .20f) / .80f);
            float arrivalZoom = 1 + .14f * (1 - reveal);
            RectangleF arrival = new(to.Width * (1 - 1 / arrivalZoom) / 2, to.Height * (1 - 1 / arrivalZoom) / 2,
                to.Width / arrivalZoom, to.Height / arrivalZoom);
            Frame(g, to, destination, arrival, reveal, alpha);
            DrawSnow(g, size, p);
        }

        private static void DrawSnow(Graphics g, Size size, float p)
        {
            float strength = MathF.Sin(MathF.PI * p);
            float scale = Math.Min(size.Width / 1600f, size.Height / 900f);
            using Pen streak = new(Color.White, 1) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using SolidBrush brush = new(Color.White);
            for (int i = 0; i < snow.Length; i++)
            {
                SnowSeed seed = snow[i];
                float age = (seed.Phase + p * seed.Speed * 1.8f) % 1;
                float inset = .015f + age * .13f;
                PointF point = (i % 4) switch
                {
                    0 => new(size.Width * inset, size.Height * seed.Y),
                    1 => new(size.Width * (1 - inset), size.Height * seed.Y),
                    2 => new(size.Width * seed.X, size.Height * inset),
                    _ => new(size.Width * seed.X, size.Height * (1 - inset))
                };
                SnowDrawing.Grain(g, brush, streak, point.X, point.Y, seed.Radius * scale,
                    (point.X - size.Width / 2f) * .045f * seed.Speed,
                    (point.Y - size.Height / 2f) * .045f * seed.Speed,
                    strength * MathF.Sin(age * MathF.PI) * .8f, i % 11 == 0);
            }
        }
    }
}
