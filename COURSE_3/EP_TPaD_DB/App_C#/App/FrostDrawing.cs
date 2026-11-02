using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace LP_PopovD_App
{
    // Один UI-таймер для всех кнопок: кадры синхронны, а количество
    // отдельных WinForms Timer не растёт с числом видимых элементов.
    internal sealed class FrostHoverAnimation : IDisposable
    {
        private static readonly Stopwatch sharedTime = Stopwatch.StartNew();
        private static readonly HashSet<FrostHoverAnimation> active = new();
        private static readonly System.Windows.Forms.Timer sharedTimer = CreateTimer();
        private readonly Stopwatch transition = new();
        private readonly Action invalidate;
        private float from, target;
        private bool visible, disposed;
        private float lastAmbient = -1;

        internal float Amount { get; private set; }

        private static System.Windows.Forms.Timer CreateTimer()
        {
            System.Windows.Forms.Timer timer = new() { Interval = 16 };
            timer.Tick += (_, _) => TickAll();
            return timer;
        }

        private static void TickAll()
        {
            // На UI-потоке без дополнительных задач и ThreadPool.
            foreach (FrostHoverAnimation item in active)
            {
                item.UpdateAmount();
                float ambient = item.Ambient;
                bool hovering = item.transition.IsRunning;
                if (hovering || MathF.Abs(item.lastAmbient - ambient) >= .003f)
                {
                    item.lastAmbient = ambient;
                    item.invalidate();
                }
            }
        }

        internal float Ambient
        {
            get
            {
                if (!visible) return 0;
                float t = (float)(sharedTime.Elapsed.TotalSeconds % 6.6) / 6.6f;
                return t < .34f ? .80f * SnowDrawing.Smooth(t / .34f)
                    : t < .48f ? .80f
                    : t < .80f ? .80f * (1 - SnowDrawing.Smooth((t - .48f) / .32f))
                    : 0f;
            }
        }

        internal FrostHoverAnimation(Action invalidate) => this.invalidate = invalidate;

        internal void SetVisible(bool value)
        {
            if (disposed || visible == value) return;
            visible = value;
            if (value)
            {
                active.Add(this);
                if (!sharedTimer.Enabled) sharedTimer.Start();
                lastAmbient = -1;
                invalidate();
            }
            else
            {
                active.Remove(this);
                if (active.Count == 0) sharedTimer.Stop();
                Reset();
            }
        }

        internal void SetTarget(bool hovered)
        {
            if (disposed) return;
            float next = hovered ? 1 : 0;
            if (MathF.Abs(target - next) < .001f) return;
            UpdateAmount();
            from = Amount;
            target = next;
            transition.Restart();
            invalidate();
        }

        private void UpdateAmount()
        {
            if (!transition.IsRunning) return;
            float p = Math.Clamp((float)(transition.Elapsed.TotalMilliseconds / 285f), 0, 1);
            Amount = from + (target - from) * SnowDrawing.Smooth(p);
            if (p >= 1)
            {
                Amount = target;
                transition.Reset();
            }
        }

        internal void Reset()
        {
            transition.Reset();
            Amount = from = target = 0;
            lastAmbient = -1;
        }

        public void Dispose()
        {
            if (disposed) return;
            SetVisible(false);
            disposed = true;
        }
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

        // На каждом кадре выполняется только одна альфа-композиция
        // заранее подготовленной кристаллической текстуры: без сотен
        // случайных фигур, сложных масок и повторного рендеринга фактуры.
        internal static void DrawLayer(Graphics g, Bitmap texture, Rectangle bounds, float opacity)
        {
            if (opacity <= .002f || bounds.Width < 5 || bounds.Height < 5) return;
            GraphicsState saved = g.Save();
            using ImageAttributes alpha = new();
            alpha.SetColorMatrix(new ColorMatrix
            {
                Matrix33 = Math.Clamp(opacity, 0, 1)
            });
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.None;
            g.DrawImage(texture, bounds, 0, 0, texture.Width, texture.Height,
                GraphicsUnit.Pixel, alpha);
            g.Restore(saved);
        }

        internal static void Draw(Graphics g, Bitmap texture, Rectangle bounds, float amount, bool round)
            => DrawLayer(g, texture, bounds, SnowDrawing.Smooth(Math.Clamp(amount, 0, 1)));

        // Ледяная корка готовится ОДИН раз при изменении размера кнопки.
        // На каждом кадре её прозрачность плавно интерполируется.
        internal static Bitmap CreateIceLayer(Bitmap frost, bool round)
        {
            Bitmap result = new(frost.Width, frost.Height, PixelFormat.Format32bppPArgb);
            result.SetResolution(96, 96);
            using Graphics g = Graphics.FromImage(result);
            DrawIceShell(g, frost, new Rectangle(0, 0, result.Width, result.Height), 1, round);
            return result;
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
