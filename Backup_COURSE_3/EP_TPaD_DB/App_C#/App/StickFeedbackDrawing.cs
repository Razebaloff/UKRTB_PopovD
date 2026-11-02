using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace LP_PopovD_App
{
    internal static class StickFeedbackDrawing
    {
        private static readonly PointF Center = new(800, 58);
        internal static readonly RectangleF AnimationBounds = new(741, -16, 118, 148);
        private static readonly SnowSeed[] snow = SnowDrawing.Seeds(148, 72419);
        private static Bitmap? left, right;
        private static int spriteSide;

        internal static void Prepare(Size size)
        {
            int side = Math.Max(64, (int)Math.Ceiling(RinkDrawing.GetCanvasBounds(size).Width / 1600 * 110));
            if (spriteSide == side && left != null) return;
            Dispose();
            left = Sprite(side, false);
            right = Sprite(side, true);
            spriteSide = side;
        }

        private static Bitmap Sprite(int side, bool mirrored)
        {
            Bitmap image = new(side, side, PixelFormat.Format32bppPArgb);
            image.SetResolution(96, 96);
            using Graphics g = Graphics.FromImage(image);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.ScaleTransform(side / 100f, side / 100f);
            g.TranslateTransform(-750, -8);
            HeaderDrawing.DrawStick(g, mirrored);
            return image;
        }

        internal static Rectangle PixelBounds(Size size)
        {
            RectangleF c = RinkDrawing.GetCanvasBounds(size);
            float s = c.Width / RinkDrawing.Width;
            return Rectangle.Intersect(new Rectangle(Point.Empty, size), Rectangle.Ceiling(new RectangleF(
                c.X + AnimationBounds.X * s - 2, c.Y + AnimationBounds.Y * s - 2,
                AnimationBounds.Width * s + 4, AnimationBounds.Height * s + 4)));
        }

        internal static void Draw(Graphics g, Size size, float? progress = null, bool fadeAtEnd = false)
        {
            if (size.Width < 1 || size.Height < 1) return;
            Prepare(size);
            RectangleF canvas = RinkDrawing.GetCanvasBounds(size);
            GraphicsState saved = g.Save();
            CompositingMode mode = g.CompositingMode;
            g.CompositingMode = CompositingMode.SourceOver;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.TranslateTransform(canvas.X, canvas.Y);
            g.ScaleTransform(canvas.Width / RinkDrawing.Width, canvas.Height / RinkDrawing.Height);
            g.SetClip(AnimationBounds, CombineMode.Intersect);
            float p = Math.Clamp(progress ?? 0, 0, 1);
            float turn = p >= 1 ? 0 : 360 * SnowDrawing.Smooth(p);
            float envelope = MathF.Sin(MathF.PI * p);
            float opacity = fadeAtEnd && progress.HasValue
                ? SnowDrawing.Smooth(p / .10f) * (1 - SnowDrawing.Smooth((p - .84f) / .16f)) : 1;
            float scale = 1 - .23f * SnowDrawing.Smooth(p / .18f) * SnowDrawing.Smooth((1 - p) / .18f);
            using ImageAttributes alpha = new();
            if (p > 0 && p < 1) DrawSnow(g, p, envelope * opacity);
            if (envelope > .01f)
            {
                DrawTurned(g, left!, turn - 10 * envelope, scale, envelope * .10f * opacity, alpha);
                DrawTurned(g, right!, -turn + 10 * envelope, scale, envelope * .10f * opacity, alpha);
            }
            DrawTurned(g, left!, turn, scale, opacity, alpha);
            DrawTurned(g, right!, -turn, scale, opacity, alpha);
            g.Restore(saved);
            g.CompositingMode = mode;
        }

        private static void DrawTurned(Graphics g, Image image, float angle, float scale, float opacity, ImageAttributes alpha)
        {
            if (opacity <= .001f) return;
            GraphicsState saved = g.Save();
            g.TranslateTransform(Center.X, Center.Y);
            g.RotateTransform(angle);
            g.ScaleTransform(scale, scale);
            g.TranslateTransform(-Center.X, -Center.Y);
            alpha.SetColorMatrix(new ColorMatrix { Matrix33 = opacity });
            g.DrawImage(image, new Rectangle(750, 8, 100, 100), 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, alpha);
            g.Restore(saved);
        }

        private static void DrawSnow(Graphics g, float p, float strength)
        {
            using SolidBrush brush = new(Color.White);
            using Pen streak = new(Color.White, 1) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            for (int i = 0; i < 7; i++)
            {
                float angle = i * 2.39996f + p * 8;
                float radius = 14 + i * 4 + p * 13;
                brush.Color = Color.FromArgb((int)(12 * strength), 237, 249, 255);
                g.FillEllipse(brush, Center.X + MathF.Cos(angle) * radius - 13,
                    Center.Y + MathF.Sin(angle) * radius * .75f - 7, 26, 14);
            }
            for (int i = 0; i < snow.Length; i++)
            {
                SnowSeed seed = snow[i];
                float age = (seed.Phase + p * (1 + seed.Speed * .45f)) % 1;
                float angle = seed.Angle + p * (8 + seed.Speed * 5) + age * 2;
                float radius = 7 + age * (34 + seed.X * 14);
                float x = Center.X + MathF.Cos(angle) * radius;
                float y = Center.Y + MathF.Sin(angle) * radius * .83f + MathF.Sin(p * 12 + seed.Angle) * 2;
                SnowDrawing.Grain(g, brush, streak, x, y, seed.Radius * .72f,
                    -MathF.Sin(angle) * (1 + seed.Speed * 2), MathF.Cos(angle) * (1 + seed.Speed),
                    strength * MathF.Sin(age * MathF.PI) * (.38f + seed.Y * .6f), i % 13 == 0);
            }
        }

        internal static void Dispose()
        {
            left?.Dispose(); right?.Dispose(); left = right = null; spriteSide = 0;
        }
    }
}
