using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace LP_PopovD_App
{
    // Фоновый иней появляется и тает циклически даже без наведения.
    // При hover фон уступает место плотной прозрачной ледяной корке.
    internal sealed class FrostHoverAnimation : IDisposable
    {
        private static readonly Stopwatch sharedTime = Stopwatch.StartNew();
        private readonly System.Windows.Forms.Timer timer;
        private readonly Stopwatch transition = new();
        private readonly Action invalidate;
        private float from, target;
        private bool active;
        private readonly float phase;
        private static int nextPhase;

        internal float Amount { get; private set; }
        internal float Ambient
        {
            get
            {
                if (!active) return 0;
                float t = (float)((sharedTime.Elapsed.TotalSeconds +
                    phase * 6.6) % 6.6) / 6.6f;
                // Замерзание, непродолжительное удержание, таяние и пауза.
                return t < .34f ? .80f * SnowDrawing.Smooth(t / .34f)
                    : t < .48f ? .80f
                    : t < .80f ? .80f * (1 - SnowDrawing.Smooth((t - .48f) / .32f))
                    : 0f;
            }
        }

        internal FrostHoverAnimation(Action invalidate)
        {
            this.invalidate = invalidate;
            phase = (System.Threading.Interlocked.Increment(ref nextPhase) * .173f) % 1f;
            timer = new System.Windows.Forms.Timer { Interval = 65 };
            timer.Tick += (_, _) => Tick();
        }

        internal void SetVisible(bool visible)
        {
            if (active == visible) return;
            active = visible;
            if (visible) timer.Start();
            else { timer.Stop(); Reset(); }
        }

        internal void SetTarget(bool hovered)
        {
            float next = hovered ? 1 : 0;
            if (Math.Abs(target - next) < .001f) return;
            UpdateAmount();
            from = Amount;
            target = next;
            transition.Restart();
            invalidate();
        }

        private void UpdateAmount()
        {
            if (!transition.IsRunning) return;
            float p = Math.Clamp((float)(transition.Elapsed.TotalMilliseconds / 340f), 0, 1);
            Amount = from + (target - from) * SnowDrawing.Smooth(p);
            if (p >= 1) { Amount = target; transition.Reset(); }
        }

        private void Tick()
        {
            UpdateAmount();
            invalidate();
        }

        internal void Reset()
        {
            transition.Reset();
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

        // При наведении создаётся голубоватая полупрозрачная ледяная корка:
        // фактура кристаллов, блестящий обод, хаотичные внутренние трещинки.
        // Контрастный текст рисуется родительской кнопкой ПОСЛЕ этого слоя.
        internal static void DrawIceShell(Graphics g, Bitmap texture, Rectangle bounds, float opacity, bool round)
        {
            if (opacity < .002f || bounds.Width < 5 || bounds.Height < 5) return;
            float p = SnowDrawing.Smooth(opacity);
            GraphicsState saved = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF outer = new(bounds.Left + 1, bounds.Top + 1, bounds.Width - 2, bounds.Height - 2);
            using GraphicsPath shape = Shape(outer, round);
            g.SetClip(shape, CombineMode.Intersect);
            using LinearGradientBrush ice = new(outer,
                Color.FromArgb((int)(145 * p), 184, 229, 252),
                Color.FromArgb((int)(85 * p), 101, 177, 217), 38);
            g.FillPath(ice, shape);
            using ImageAttributes alpha = new();
            alpha.SetColorMatrix(new ColorMatrix { Matrix33 = Math.Clamp(.88f * p, 0, 1) });
            g.InterpolationMode = InterpolationMode.Bilinear;
            g.DrawImage(texture, bounds, 0, 0, texture.Width, texture.Height, GraphicsUnit.Pixel, alpha);
            using Pen edge = new(Color.FromArgb((int)(200 * p), 245, 252, 255),
                Math.Max(1f, bounds.Height * .018f)) { LineJoin = LineJoin.Round };
            g.DrawPath(edge, shape);
            Random random = new(unchecked(bounds.Width * 1567 + bounds.Height * 1163 + (round ? 7291 : 113)));
            using Pen fracture = new(Color.White, .8f);
            int count = round ? 19 : 12;
            for (int i = 0; i < count; i++)
            {
                float x = bounds.Left + random.NextSingle() * bounds.Width;
                float y = bounds.Top + random.NextSingle() * bounds.Height;
                float angle = random.NextSingle() * MathF.Tau;
                float reach = (round ? bounds.Width : bounds.Height) *
                    (.025f + random.NextSingle() * .09f);
                fracture.Color = Color.FromArgb((int)((35 + random.Next(90)) * p), 244, 252, 255);
                g.DrawLine(fracture, x, y, x + MathF.Cos(angle) * reach,
                    y + MathF.Sin(angle) * reach);
            }
            g.Restore(saved);
        }
    }
}
