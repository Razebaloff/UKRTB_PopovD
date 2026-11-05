using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace LP_PopovD_App
{
    internal sealed record VerificationMovingField(Bitmap Before, Bitmap After, Rectangle From, Rectangle To);

    // Раскрытие второго шага на той же майке: готовые спрайты полей
    // поднимаются, а новый ввод собирается из прилетающей микропыли.
    internal sealed class VerificationRevealDrawing : IDisposable
    {
        internal const int DurationMilliseconds = 1600;
        private readonly Bitmap background, code, assembled;
        private readonly VerificationMovingField[] fields;
        private readonly Rectangle codeBounds;
        private readonly int[] original, pixels;
        private readonly byte[] arrival;
        private readonly Particle[] particles;
        private sealed record Particle(PointF[] Path, float Arrival, float Radius, Color Color);

        internal VerificationRevealDrawing(Bitmap background, VerificationMovingField[] fields,
            Bitmap code, Rectangle codeBounds)
        {
            this.background = background;
            this.fields = fields;
            this.code = code;
            this.codeBounds = codeBounds;
            assembled = new Bitmap(code.Width, code.Height, PixelFormat.Format32bppPArgb);
            assembled.SetResolution(96, 96);
            original = new int[code.Width * code.Height];
            pixels = new int[original.Length];
            arrival = new byte[original.Length];
            BitmapData data = code.LockBits(new Rectangle(Point.Empty, code.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try
            {
                for (int y = 0; y < code.Height; y++)
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), original, y * code.Width, code.Width);
            }
            finally { code.UnlockBits(data); }
            Random random = new(41829);
            for (int y = 0; y < code.Height; y++)
                for (int x = 0; x < code.Width; x++)
                    arrival[y * code.Width + x] = (byte)(255 * (.28f * x / code.Width +
                        .12f * (1 - y / (float)code.Height) + .60f * random.NextSingle()));

            List<Particle> dust = new();
            float scale = background.Width / 1600f;
            for (int i = 0; i < 660; i++)
            {
                int x = random.Next(code.Width), y = random.Next(code.Height), index = y * code.Width + x;
                int argb = original[index];
                if ((uint)argb >> 24 < 100) continue;
                PointF end = new(codeBounds.X + x, codeBounds.Y + y);
                // Основные потоки идут из двух нижних углов, часть пыли
                // подхватывается ветром у левого края экрана.
                PointF start = (i % 3) switch
                {
                    0 => new(-18 * scale, background.Height + random.NextSingle() * 80 * scale),
                    1 => new(background.Width + 18 * scale, background.Height + random.NextSingle() * 80 * scale),
                    _ => new(-18 * scale, background.Height * (.25f + random.NextSingle() * .35f))
                };
                float side = start.X < background.Width / 2f ? 1 : -1;
                PointF c1 = new(start.X + side * background.Width * .44f,
                    start.Y - background.Height * (.28f + random.NextSingle() * .14f));
                PointF c2 = new(end.X - side * background.Width * .18f,
                    end.Y + background.Height * (.12f + random.NextSingle() * .12f));
                PointF[] path = new PointF[49];
                for (int step = 0; step < path.Length; step++)
                {
                    float p = step / (float)(path.Length - 1), q = 1 - p;
                    float curl = MathF.Sin(p * MathF.Tau * 1.5f + i) * MathF.Sin(p * MathF.PI) * 16 * scale;
                    path[step] = new(q * q * q * start.X + 3 * q * q * p * c1.X + 3 * q * p * p * c2.X + p * p * p * end.X + curl,
                        q * q * q * start.Y + 3 * q * q * p * c1.Y + 3 * q * p * p * c2.Y + p * p * p * end.Y - curl * .7f);
                }
                dust.Add(new Particle(path, 640 + arrival[index] / 255f * 780,
                    (.65f + random.NextSingle() * 1.3f) * scale, Color.FromArgb(argb)));
            }
            particles = dust.ToArray();
        }

        private static RectangleF Between(Rectangle a, Rectangle b, float p) => new(
            a.X + (b.X - a.X) * p, a.Y + (b.Y - a.Y) * p,
            a.Width + (b.Width - a.Width) * p, a.Height + (b.Height - a.Height) * p);

        internal void Draw(Graphics g, float milliseconds)
        {
            GraphicsState saved = g.Save();
            try
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.DrawImage(background, new Rectangle(Point.Empty, background.Size), 0, 0,
                    background.Width, background.Height, GraphicsUnit.Pixel);
                g.CompositingMode = CompositingMode.SourceOver;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                float move = SnowDrawing.Smooth(milliseconds / 520f);
                using ImageAttributes alpha = new();
                foreach (VerificationMovingField field in fields)
                {
                    Rectangle destination = Rectangle.Round(Between(field.From, field.To, move));
                    g.DrawImage(field.Before, destination, 0, 0, field.Before.Width, field.Before.Height, GraphicsUnit.Pixel);
                    alpha.SetColorMatrix(new ColorMatrix { Matrix33 = move });
                    g.DrawImage(field.After, destination, 0, 0, field.After.Width, field.After.Height, GraphicsUnit.Pixel, alpha);
                }
                DrawWind(g, milliseconds);
                Assemble(milliseconds);
                g.DrawImage(assembled, codeBounds, 0, 0, assembled.Width, assembled.Height, GraphicsUnit.Pixel);
            }
            finally { g.Restore(saved); }
        }

        private void Assemble(float milliseconds)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                int amount = (int)(255 * SnowDrawing.Smooth((milliseconds - 640 - arrival[i] / 255f * 780) / 150));
                int c = original[i];
                int a = (int)((uint)c >> 24) * amount / 255;
                int r = (c >> 16 & 255) * amount / 255;
                int green = (c >> 8 & 255) * amount / 255;
                int b = (c & 255) * amount / 255;
                pixels[i] = a << 24 | r << 16 | green << 8 | b;
            }
            BitmapData data = assembled.LockBits(new Rectangle(Point.Empty, assembled.Size),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            try
            {
                for (int y = 0; y < assembled.Height; y++)
                    Marshal.Copy(pixels, y * assembled.Width, IntPtr.Add(data.Scan0, y * data.Stride), assembled.Width);
            }
            finally { assembled.UnlockBits(data); }
        }

        private void DrawWind(Graphics g, float milliseconds)
        {
            using SolidBrush grain = new(Color.White);
            using Pen streak = new(Color.White, Math.Max(.65f, background.Width / 2000f))
            { StartCap = LineCap.Round, EndCap = LineCap.Round };
            foreach (Particle particle in particles)
            {
                float p = (milliseconds - particle.Arrival + 640) / 640;
                if (p <= 0 || p >= 1) continue;
                float step = p * (particle.Path.Length - 1);
                int index = Math.Min(particle.Path.Length - 2, (int)step);
                PointF a = particle.Path[index], b = particle.Path[index + 1];
                float fraction = step - index;
                PointF point = new(a.X + (b.X - a.X) * fraction, a.Y + (b.Y - a.Y) * fraction);
                float visibility = SnowDrawing.Smooth(p / .14f) * (1 - SnowDrawing.Smooth((p - .88f) / .12f));
                Color c = particle.Color;
                grain.Color = Color.FromArgb((int)(220 * visibility), Math.Max(175, (int)c.R), Math.Max(205, (int)c.G), Math.Max(224, (int)c.B));
                streak.Color = Color.FromArgb((int)(65 * visibility), 203, 237, 255);
                PointF tail = particle.Path[Math.Max(0, index - 2)];
                g.DrawLine(streak, tail, point);
                float radius = particle.Radius;
                g.FillEllipse(grain, point.X - radius, point.Y - radius, radius * 2, radius * 2);
            }
        }

        public void Dispose()
        {
            background.Dispose(); code.Dispose(); assembled.Dispose();
            foreach (VerificationMovingField field in fields) { field.Before.Dispose(); field.After.Dispose(); }
        }
    }
}
