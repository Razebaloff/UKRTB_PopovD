using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace LP_PopovD_App
{
    internal static class StickFeedbackDrawing
    {
        private static readonly PointF Center = new(800, 58);
        internal static readonly RectangleF AnimationBounds = new(741, -16, 118, 148);
        private static readonly SnowSeed[] snow = SnowDrawing.Seeds(224, 72419);
        private static Bitmap? left, right;
        private static Bitmap? scheduleLeft, scheduleRight, leftHalo, rightHalo;
        private static int spriteSide;

        internal static void Prepare(Size size)
        {
            int side = Math.Max(64, (int)Math.Ceiling(RinkDrawing.GetCanvasBounds(size).Width / 1600 * 110));
            if (spriteSide == side && left != null) return;
            Dispose();
            left = Sprite(side, false);
            right = Sprite(side, true);
            scheduleLeft = ForDarkBackground(left);
            scheduleRight = ForDarkBackground(right);
            leftHalo = Halo(left);
            rightHalo = Halo(right);
            spriteSide = side;
        }

        private static Bitmap ForDarkBackground(Bitmap source)
        {
            Bitmap image = new(source.Width, source.Height, PixelFormat.Format32bppPArgb);
            image.SetResolution(96, 96);
            using Graphics g = Graphics.FromImage(image);
            using ImageAttributes tint = new();
            tint.SetColorMatrix(new ColorMatrix { Matrix00 = 1.15f, Matrix11 = 1.15f, Matrix22 = 1.15f,
                Matrix40 = .17f, Matrix41 = .23f, Matrix42 = .29f });
            g.DrawImage(source, new Rectangle(Point.Empty, image.Size), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, tint);
            return image;
        }

        private static Bitmap Halo(Bitmap source)
        {
            int width = source.Width, height = source.Height;
            int[] original = new int[width * height], result = new int[original.Length];
            BitmapData data = source.LockBits(new Rectangle(Point.Empty, source.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try { for (int y = 0; y < height; y++) Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), original, y * width, width); }
            finally { source.UnlockBits(data); }
            int radius = Math.Max(1, width / 60);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int alpha = 0;
                    for (int dy = -radius; dy <= radius; dy++)
                        for (int dx = -radius; dx <= radius; dx++)
                            if (x + dx >= 0 && x + dx < width && y + dy >= 0 && y + dy < height)
                                alpha = Math.Max(alpha, (int)((uint)original[(y + dy) * width + x + dx] >> 24));
                    alpha = alpha * 130 / 255;
                    result[y * width + x] = alpha << 24 | alpha * 145 / 255 << 16 | alpha * 220 / 255 << 8 | alpha;
                }
            Bitmap image = new(width, height, PixelFormat.Format32bppPArgb);
            image.SetResolution(96, 96);
            data = image.LockBits(new Rectangle(Point.Empty, image.Size), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            try { for (int y = 0; y < height; y++) Marshal.Copy(result, y * width, IntPtr.Add(data.Scan0, y * data.Stride), width); }
            finally { image.UnlockBits(data); }
            return image;
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

        internal static Rectangle PixelBounds(Size size, bool schedule = false)
        {
            if (schedule)
                return Rectangle.Intersect(new Rectangle(Point.Empty, size), Rectangle.Ceiling(new RectangleF(
                    (1448 + AnimationBounds.X - Center.X) * size.Width / 1600f - 3,
                    (73 + AnimationBounds.Y - Center.Y) * size.Height / 900f - 3,
                    AnimationBounds.Width * size.Width / 1600f + 6,
                    AnimationBounds.Height * size.Height / 900f + 6)));
            RectangleF c = RinkDrawing.GetCanvasBounds(size);
            float s = c.Width / RinkDrawing.Width;
            return Rectangle.Intersect(new Rectangle(Point.Empty, size), Rectangle.Ceiling(new RectangleF(
                c.X + AnimationBounds.X * s - 2, c.Y + AnimationBounds.Y * s - 2,
                AnimationBounds.Width * s + 4, AnimationBounds.Height * s + 4)));
        }

        internal static void Draw(Graphics g, Size size, float? progress = null, bool fadeAtEnd = false, bool schedule = false)
        {
            if (size.Width < 1 || size.Height < 1) return;
            Prepare(size);
            RectangleF canvas = RinkDrawing.GetCanvasBounds(size);
            GraphicsState saved = g.Save();
            CompositingMode mode = g.CompositingMode;
            g.CompositingMode = CompositingMode.SourceOver;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            if (schedule)
            {
                // В расписании шапка имеет собственную разметку 1600×900.
                // Клюшки стоят справа от заголовка и не теряются в буквах.
                g.TranslateTransform(size.Width * 1448 / 1600f, size.Height * 73 / 900f);
                g.ScaleTransform(size.Width / 1600f, size.Height / 900f);
                g.TranslateTransform(-Center.X, -Center.Y);
            }
            else
            {
                g.TranslateTransform(canvas.X, canvas.Y);
                g.ScaleTransform(canvas.Width / RinkDrawing.Width, canvas.Height / RinkDrawing.Height);
            }
            g.SetClip(AnimationBounds, CombineMode.Intersect);
            float p = Math.Clamp(progress ?? 0, 0, 1);
            float turn = p >= 1 ? 0 : 360 * SnowDrawing.Smooth(p);
            float envelope = MathF.Sin(MathF.PI * p);
            float opacity = fadeAtEnd && progress.HasValue
                ? SnowDrawing.Smooth(p / .10f) * (1 - SnowDrawing.Smooth((p - .84f) / .16f)) : 1;
            float scale = 1 - .23f * SnowDrawing.Smooth(p / .18f) * SnowDrawing.Smooth((1 - p) / .18f);
            using ImageAttributes alpha = new();
            float snowStrength = envelope * opacity * (schedule ? .52f : 1);
            if (p > 0 && p < 1) DrawBlizzard(g, p, snowStrength, foreground: false);
            if (schedule)
            {
                DrawTurned(g, leftHalo!, turn, scale, opacity, alpha);
                DrawTurned(g, rightHalo!, -turn, scale, opacity, alpha);
            }
            if (envelope > .01f)
            {
                DrawTurned(g, left!, turn - 10 * envelope, scale, envelope * .10f * opacity, alpha);
                DrawTurned(g, right!, -turn + 10 * envelope, scale, envelope * .10f * opacity, alpha);
            }
            DrawTurned(g, schedule ? scheduleLeft! : left!, turn, scale, opacity, alpha);
            DrawTurned(g, schedule ? scheduleRight! : right!, -turn, scale, opacity, alpha);
            if (p > 0 && p < 1) DrawBlizzard(g, p, snowStrength * (schedule ? .55f : 1), foreground: true);
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

        // Прозрачная пыль гаснет у границы области клюшек: никакой
        // прямоугольной кромки от обрезанных снежных шлейфов.
        private static float SoftEdge(float x, float y)
        {
            float dx = Math.Min(x - AnimationBounds.Left, AnimationBounds.Right - x) / 16f;
            float dy = Math.Min(y - AnimationBounds.Top, AnimationBounds.Bottom - y) / 15f;
            return SnowDrawing.Smooth(Math.Clamp(Math.Min(dx, dy), 0, 1));
        }

        // Не отдельные плавающие снежинки, а плотные движущиеся воздушные струи.
        // Дальняя пелена обтекает клюшки, ближняя проносится поверх них.
        // Clip установлен в Draw и защищает флаги по обе стороны.
        private static void DrawBlizzard(Graphics g, float progress, float strength, bool foreground)
        {
            if (strength <= .001f) return;
            float p = Math.Clamp(progress, 0, 1);
            float wind = MathF.Pow(strength, .72f);

            using Pen plume = new(Color.White, 1) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using Pen core = new(Color.White, 1) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            int streams = foreground ? 7 : 12;
            for (int i = 0; i < streams; i++)
            {
                float side = (i & 1) == 0 ? -1 : 1;
                float phase = p * 9.5f + i * 1.127f;
                float y = 16 + i * (79f / streams) + 4 * MathF.Sin(phase);
                float sourceX = Center.X + side * (38 + 4 * MathF.Sin(phase * .8f));
                float endX = Center.X - side * (36 + 4 * MathF.Cos(phase));
                float bend = 7 * MathF.Sin(phase * 1.3f);
                PointF from = new(sourceX, y);
                PointF c1 = new(Center.X + side * 18, y - 12 + bend);
                PointF c2 = new(Center.X - side * 19, y + 14 + bend);
                PointF to = new(endX, y + 5 * MathF.Sin(phase + 1.9f));
                using GraphicsPath ribbon = new();
                ribbon.AddBezier(from, c1, c2, to);

                // Широкая распылённая голубая пелена, внутри более яркие прожилки.
                plume.Color = Color.FromArgb((int)(14 * wind), 100, 164, 198);
                plume.Width = foreground ? 12 : 21;
                g.DrawPath(plume, ribbon);
                plume.Color = Color.FromArgb((int)(23 * wind), 216, 246, 255);
                plume.Width = foreground ? 6 : 11;
                g.DrawPath(plume, ribbon);
                core.Color = Color.FromArgb((int)((foreground ? 92 : 65) * wind),
                    249, 254, 255);
                core.Width = foreground ? .95f : 1.5f;
                g.DrawPath(core, ribbon);
            }

            using SolidBrush cloud = new(Color.White);
            int wisps = foreground ? 12 : 24;
            for (int i = 0; i < wisps; i++)
            {
                SnowSeed seed = snow[(i * 7 + (foreground ? 43 : 11)) % snow.Length];
                float spin = seed.Angle + p * (6 + seed.Speed * 4);
                float radius = 12 + seed.X * 26 + 2 * MathF.Sin(p * 11 + i);
                float x = Center.X + MathF.Cos(spin) * radius;
                float y = Center.Y + MathF.Sin(spin) * radius * 1.12f;
                float size = 6 + seed.Y * (foreground ? 7 : 10);
                float feather = SoftEdge(x, y);
                cloud.Color = Color.FromArgb((int)((foreground ? 8 : 11) * wind * feather), 232, 248, 255);
                g.FillEllipse(cloud, x - size, y - size * .30f, size * 2, size * .70f);
            }

            // Пыль с тангенциальной скоростью вокруг центра: снег не висит неподвижно.
            using SolidBrush grains = new(Color.White);
            using Pen trace = new(Color.White, 1) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using Pen crystal = new(Color.White, .55f) { StartCap = LineCap.Round };
            for (int i = foreground ? 0 : 1; i < snow.Length; i += 2)
            {
                SnowSeed seed = snow[i];
                float age = (seed.Phase + p * (1.25f + seed.Speed * .68f)) % 1;
                float spiral = seed.Angle + p * (9 + seed.Speed * 6) + 2.8f * age;
                float radius = 5 + age * (41 + seed.X * 19);
                float turbulence = MathF.Sin(p * 22 + seed.Angle) * 3 * age;
                float x = Center.X + MathF.Cos(spiral) * radius + turbulence;
                float y = Center.Y + MathF.Sin(spiral) * radius * 1.08f + 8 * (age - .5f);
                float vx = -MathF.Sin(spiral) * (2 + 3 * seed.Speed);
                float vy = MathF.Cos(spiral) * (1.7f + 2.3f * seed.Speed);
                float opacity = wind * SoftEdge(x, y) *
                    MathF.Sin(age * MathF.PI) * (.47f + seed.Y * .5f);
                SnowDrawing.Grain(g, grains, trace, x, y,
                    seed.Radius * (foreground ? .94f : .80f), vx, vy, opacity, false);

                // Небольшие шестиугольные кристаллы снежинок видны внутри метели.
                if (i % 18 == (foreground ? 0 : 1) && seed.Radius > .9f)
                    DrawCrystal(g, crystal, x, y, 1.8f + seed.Radius * 1.35f, opacity * .85f);
            }
        }

        private static void DrawCrystal(Graphics g, Pen pen, float x, float y, float radius, float opacity)
        {
            int alpha = (int)Math.Clamp(210 * opacity, 0, 255);
            if (alpha < 8) return;
            pen.Color = Color.FromArgb(alpha, 250, 254, 255);
            for (int i = 0; i < 6; i++)
            {
                float angle = i * MathF.Tau / 6;
                float dx = MathF.Cos(angle), dy = MathF.Sin(angle);
                g.DrawLine(pen, x, y, x + dx * radius, y + dy * radius);
                float bx = x + dx * radius * .60f, by = y + dy * radius * .60f;
                float ox = -dy * radius * .27f, oy = dx * radius * .27f;
                g.DrawLine(pen, bx, by, bx + ox - dx * radius * .16f, by + oy - dy * radius * .16f);
                g.DrawLine(pen, bx, by, bx - ox - dx * radius * .16f, by - oy - dy * radius * .16f);
            }
        }

        internal static void Dispose()
        {
            left?.Dispose(); right?.Dispose(); left = right = null; spriteSide = 0;
            scheduleLeft?.Dispose(); scheduleRight?.Dispose(); leftHalo?.Dispose(); rightHalo?.Dispose();
            scheduleLeft = scheduleRight = leftHalo = rightHalo = null;
        }
    }
}
