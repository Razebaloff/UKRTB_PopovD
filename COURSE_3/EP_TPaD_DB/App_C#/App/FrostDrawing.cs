using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace LP_PopovD_App
{
    // Один короткий таймер на активный hover, не перерисовывает лёд или резину.
    internal sealed class FrostHoverAnimation : IDisposable
    {
        private readonly System.Windows.Forms.Timer timer;
        private readonly Stopwatch clock = new();
        private readonly Action invalidate;
        private float from, target;

        internal float Amount { get; private set; }

        internal FrostHoverAnimation(Action invalidate)
        {
            this.invalidate = invalidate;
            timer = new System.Windows.Forms.Timer { Interval = 16 };
            timer.Tick += (_, _) => Tick();
        }

        internal void SetTarget(bool hovered)
        {
            float next = hovered ? 1 : 0;
            if (Math.Abs(target - next) < .001f) return;
            TickAmount();
            from = Amount;
            target = next;
            clock.Restart();
            timer.Start();
            invalidate();
        }

        private void TickAmount()
        {
            if (!clock.IsRunning) return;
            float p = Math.Clamp((float)(clock.Elapsed.TotalMilliseconds / 285.0), 0, 1);
            Amount = from + (target - from) * SnowDrawing.Smooth(p);
            if (p >= 1)
            {
                Amount = target;
                timer.Stop();
                clock.Reset();
            }
        }

        private void Tick()
        {
            TickAmount();
            invalidate();
        }

        internal void Reset()
        {
            timer.Stop();
            clock.Reset();
            Amount = from = target = 0;
        }

        public void Dispose() => timer.Dispose();
    }

    // Статическую фактуру инея готовим один раз на размер кнопки.
    // При наведении анимируется лишь ширина замёрзшего края и прозрачность.
    internal static class FrostDrawing
    {
        private static GraphicsPath Shape(RectangleF r, bool round)
        {
            if (round)
            {
                GraphicsPath path = new();
                path.AddEllipse(r);
                return path;
            }
            return JerseyDrawing.Capsule(r);
        }

        internal static Bitmap CreateTexture(Size size, bool round, int seed)
        {
            Bitmap bitmap = new(Math.Max(1, size.Width), Math.Max(1, size.Height), PixelFormat.Format32bppPArgb);
            bitmap.SetResolution(96, 96);
            if (size.Width < 5 || size.Height < 5) return bitmap;
            using Graphics g = Graphics.FromImage(bitmap);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF rect = new(1, 1, size.Width - 2, size.Height - 2);
            using GraphicsPath shape = Shape(rect, round);
            g.SetClip(shape);
            using SolidBrush frost = new(Color.FromArgb(36, 205, 236, 255));
            g.FillPath(frost, shape);
            Random random = new(seed);
            float cx = size.Width * .5f, cy = size.Height * .5f;
            float radius = Math.Min(size.Width, size.Height) * .5f - 2;
            int count = Math.Clamp((int)(size.Width * size.Height / 195f), 110, 500);
            using Pen crystal = new(Color.White, 1) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using Pen twig = new(Color.White, 1) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using SolidBrush sparkle = new(Color.White);

            for (int i = 0; i < count; i++)
            {
                float x, y, ux, uy, depth;
                if (round)
                {
                    float a = (float)(random.NextDouble() * MathF.Tau);
                    float edge = .94f + .06f * random.NextSingle();
                    x = cx + MathF.Cos(a) * radius * edge;
                    y = cy + MathF.Sin(a) * radius * edge;
                    ux = -MathF.Cos(a); uy = -MathF.Sin(a);
                    depth = radius * (.05f + random.NextSingle() * .35f);
                }
                else
                {
                    float r = size.Height / 2f - 2;
                    if (random.Next(4) < 3)
                    {
                        bool upper = random.Next(2) == 0;
                        x = r + random.NextSingle() * Math.Max(1, size.Width - 2 * r);
                        y = upper ? 2 : size.Height - 2;
                        ux = (random.NextSingle() - .5f) * .42f;
                        uy = upper ? 1 : -1;
                    }
                    else
                    {
                        bool left = random.Next(2) == 0;
                        float a = (random.NextSingle() - .5f) * MathF.PI;
                        x = (left ? r + -r * MathF.Cos(a) : size.Width - r + r * MathF.Cos(a));
                        y = cy + r * MathF.Sin(a);
                        ux = left ? MathF.Cos(a) : -MathF.Cos(a);
                        uy = -MathF.Sin(a);
                    }
                    depth = size.Height * (.08f + random.NextSingle() * .36f);
                }

                float x2 = x + ux * depth, y2 = y + uy * depth;
                float alpha = 55 + random.Next(155);
                crystal.Color = Color.FromArgb((int)alpha, 224, 247, 255);
                crystal.Width = .55f + random.NextSingle() * 1.25f;
                g.DrawLine(crystal, x, y, x2, y2);
                twig.Color = Color.FromArgb((int)(alpha * .80f), 245, 253, 255);
                twig.Width = Math.Max(.45f, crystal.Width * .52f);
                float sideX = -uy, sideY = ux;
                for (int k = 1; k <= 3; k++)
                {
                    float t = .25f + .16f * k;
                    float bx = x + (x2 - x) * t;
                    float by = y + (y2 - y) * t;
                    float branch = depth * (.07f + .035f * (4 - k));
                    g.DrawLine(twig, bx, by, bx + (sideX + ux * .33f) * branch, by + (sideY + uy * .33f) * branch);
                    g.DrawLine(twig, bx, by, bx + (-sideX + ux * .33f) * branch, by + (-sideY + uy * .33f) * branch);
                }
                if (i % 4 == 0)
                {
                    float dot = .6f + random.NextSingle() * 1.9f;
                    sparkle.Color = Color.FromArgb((int)(alpha * .70f), 248, 254, 255);
                    g.FillEllipse(sparkle, x2 - dot / 2, y2 - dot / 2, dot, dot);
                }
            }
            return bitmap;
        }

        internal static void Draw(Graphics g, Bitmap texture, Rectangle bounds, float amount, bool round)
        {
            if (amount <= .002f || bounds.Width < 5 || bounds.Height < 5) return;
            float p = SnowDrawing.Smooth(amount);
            float inset = round ? Math.Min(bounds.Width, bounds.Height) * .30f * p
                                : bounds.Height * .40f * p;
            GraphicsState saved = g.Save();
            using GraphicsPath band = new() { FillMode = FillMode.Alternate };
            RectangleF outer = new(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, bounds.Height - 2);
            using (GraphicsPath border = Shape(outer, round)) band.AddPath(border, false);
            RectangleF inside = RectangleF.Inflate(outer, -Math.Max(.5f, inset), -Math.Max(.5f, inset));
            if (inside.Width > 4 && inside.Height > 4)
            {
                using GraphicsPath hole = Shape(inside, round);
                band.AddPath(hole, false);
            }
            g.SetClip(band, CombineMode.Intersect);
            using ImageAttributes attributes = new();
            attributes.SetColorMatrix(new ColorMatrix { Matrix33 = Math.Clamp(.35f + p * .65f, 0, 1) });
            g.InterpolationMode = InterpolationMode.Bilinear;
            g.DrawImage(texture, bounds, 0, 0, texture.Width, texture.Height, GraphicsUnit.Pixel, attributes);
            g.Restore(saved);
        }
    }
}
