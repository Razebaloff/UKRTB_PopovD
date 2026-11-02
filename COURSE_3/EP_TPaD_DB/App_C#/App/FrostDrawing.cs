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
            int count = Math.Clamp((int)(size.Width * size.Height / 145f), 170, 850);
            using Pen crystal = new(Color.White, 1) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using Pen twig = new(Color.White, 1) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using SolidBrush sparkle = new(Color.White);

            for (int i = 0; i < count; i++)
            {
                float x, y, ux, uy, depth;
                if (round)
                {
                    // Кристаллы рождаются также в середине шайбы, не только по ободу.
                    float a = random.NextSingle() * MathF.Tau;
                    float fraction = i % 3 == 0
                        ? .73f + random.NextSingle() * .26f
                        : MathF.Sqrt(random.NextSingle()) * .94f;
                    x = cx + MathF.Cos(a) * radius * fraction;
                    y = cy + MathF.Sin(a) * radius * fraction;
                    float direction = a + MathF.PI + (random.NextSingle() - .5f) * 2.9f;
                    ux = MathF.Cos(direction);
                    uy = MathF.Sin(direction);
                    depth = radius * (.035f + random.NextSingle() * .14f);
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
                    // Часть инея появляется на самой ткани кнопки, а не по одному контуру.
                    if (i % 3 != 0)
                    {
                        x = size.Width * (.07f + random.NextSingle() * .86f);
                        y = size.Height * (.12f + random.NextSingle() * .76f);
                        float angle = random.NextSingle() * MathF.Tau;
                        ux = MathF.Cos(angle); uy = MathF.Sin(angle);
                    }
                    depth = size.Height * (.04f + random.NextSingle() * .18f);
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
            float p = SnowDrawing.Smooth(Math.Clamp(amount, 0, 1));
            GraphicsState saved = g.Save();
            RectangleF outer = new(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, bounds.Height - 2);
            using GraphicsPath silhouette = Shape(outer, round);
            g.SetClip(silhouette, CombineMode.Intersect);
            using SolidBrush frostMist = new(Color.FromArgb((int)(19 * p), 217, 241, 254));
            g.FillPath(frostMist, silhouette);
            using ImageAttributes alpha = new();
            alpha.SetColorMatrix(new ColorMatrix { Matrix33 = .18f + .77f * p });
            g.InterpolationMode = InterpolationMode.Bilinear;

            void Paint()
            {
                g.DrawImage(texture, bounds, 0, 0, texture.Width, texture.Height,
                    GraphicsUnit.Pixel, alpha);
            }

            if (p >= .995f)
            {
                Paint(); // После завершения прогресса покрыта ВСЯ поверхность.
            }
            else
            {
                float radius = Math.Min(outer.Width, outer.Height) * .5f;
                float inset = Math.Max(.1f, radius * p);
                using GraphicsPath outerBand = new() { FillMode = FillMode.Alternate };
                outerBand.AddPath(silhouette, false);
                RectangleF center = RectangleF.Inflate(outer, -inset, -inset);
                if (center.Width > 1 && center.Height > 1)
                {
                    using GraphicsPath inner = Shape(center, round);
                    outerBand.AddPath(inner, false);
                }
                GraphicsState bandState = g.Save();
                g.SetClip(outerBand, CombineMode.Intersect);
                Paint();
                g.Restore(bandState);

                // Отдельные очаги кристаллизации, которые растут не синхронно.
                // Их форма и координаты повторяемы, но хаотично распределены.
                using GraphicsPath islands = new(FillMode.Winding);
                Random random = new(unchecked(bounds.Width * 7927 + bounds.Height * 1013 + (round ? 1987 : 1297)));
                for (int i = 0; i < 46; i++)
                {
                    float threshold = random.NextSingle() * .75f;
                    float growth = Math.Clamp((p - threshold) / (.9f - threshold), 0, 1);
                    float span = (3 + random.NextSingle() * (round ? 27 : 15)) * growth;
                    float x = outer.Left + random.NextSingle() * outer.Width;
                    float y = outer.Top + random.NextSingle() * outer.Height;
                    if (span > .1f)
                        islands.AddEllipse(x - span, y - span * (.5f + random.NextSingle() * .45f),
                            span * 2, span * (1.1f + random.NextSingle() * .9f));
                }
                if (islands.PointCount > 0)
                {
                    GraphicsState islandState = g.Save();
                    g.SetClip(islands, CombineMode.Intersect);
                    Paint();
                    g.Restore(islandState);
                }
            }
            g.Restore(saved);
        }
    }
}
