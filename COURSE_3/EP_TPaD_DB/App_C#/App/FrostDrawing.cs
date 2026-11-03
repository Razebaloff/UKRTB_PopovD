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
        private int lastAmbientFrame = -1;

        internal float Amount { get; private set; }
        internal static float Seconds => (float)sharedTime.Elapsed.TotalSeconds;

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
                // Отдельные микрокристаллы изменяются независимо.
                // Ограничение фонового эффекта ~45 кадрами/с снижает
                // стоимость перерисовок. Hover остаётся на 16 мс.
                int frame = (int)(Seconds * 45);
                bool hovering = item.transition.IsRunning;
                if (hovering || (item.Amount < .999f && item.lastAmbientFrame != frame))
                {
                    item.lastAmbientFrame = frame;
                    item.invalidate();
                }
            }
        }

        // Каждый очаг инея живёт самостоятельно, общий коэффициент больше
        // не заставляет всю поверхность замерзать и таять одновременно.
        internal float Ambient => visible ? 1f : 0f;

        internal FrostHoverAnimation(Action invalidate) => this.invalidate = invalidate;

        internal void SetVisible(bool value)
        {
            if (disposed || visible == value) return;
            visible = value;
            if (value)
            {
                active.Add(this);
                if (!sharedTimer.Enabled) sharedTimer.Start();
                lastAmbientFrame = -1;
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
            lastAmbientFrame = -1;
        }

        public void Dispose()
        {
            if (disposed) return;
            SetVisible(false);
            disposed = true;
        }
    }

    // Независимые очаги; Seed меняет точку оседания после каждого цикла.
    internal readonly struct FrostSpeck
    {
        internal readonly uint Seed;
        internal readonly float Phase, Grow, Hold, Melt, Size, Opacity;
        internal readonly int Variant;

        internal FrostSpeck(uint seed, float phase, float grow, float hold,
            float melt, float size, float opacity, int variant)
        {
            Seed = seed;
            Phase = phase;
            Grow = grow;
            Hold = hold;
            Melt = melt;
            Size = size;
            Opacity = opacity;
            Variant = variant;
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

        private const float SpeckCycle = 6.8f;
        private const int SpeckLevels = 9;
        private const int SpriteSize = 56;
        private static readonly Bitmap?[,] speckSprites = new Bitmap[3, SpeckLevels];

        private static uint Mix(uint seed)
        {
            seed ^= seed >> 16;
            seed = unchecked(seed * 0x7feb352du);
            seed ^= seed >> 15;
            seed = unchecked(seed * 0x846ca68bu);
            return seed ^ (seed >> 16);
        }

        private static float Unit(uint hash) => (hash & 0x00ffffffu) / 16777215f;

        internal static FrostSpeck[] CreateSpecks(Size size, bool round, int seed)
        {
            int count = round
                ? Math.Clamp(size.Width * size.Height / 1600, 35, 74)
                : Math.Clamp(size.Width * size.Height / 1500, 22, 50);
            Random random = new(seed);
            FrostSpeck[] result = new FrostSpeck[count];
            for (int i = 0; i < count; i++)
                result[i] = new FrostSpeck(
                    unchecked((uint)random.NextInt64()),
                    random.NextSingle() * SpeckCycle,
                    .24f + random.NextSingle() * .40f,
                    .12f + random.NextSingle() * .38f,
                    .45f + random.NextSingle() * .69f,
                    .055f + random.NextSingle() * .085f,
                    .55f + random.NextSingle() * .45f,
                    random.Next(3));
            return result;
        }

        private static Bitmap GetSpeckSprite(int variant, int level)
        {
            Bitmap? cached = speckSprites[variant, level];
            if (cached != null) return cached;
            // 3 формы и 9 градаций прозрачности готовятся только при первом
            // использовании. Кадр не создаёт GraphicsPath или ImageAttributes.
            Bitmap image = new(SpriteSize, SpriteSize, PixelFormat.Format32bppPArgb);
            image.SetResolution(96, 96);
            using (Graphics g = Graphics.FromImage(image))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float intensity = (level + 1f) / SpeckLevels;
                using GraphicsPath bloom = new();
                bloom.AddEllipse(3, 3, SpriteSize - 6, SpriteSize - 6);
                using (PathGradientBrush gradient = new(bloom))
                {
                    gradient.CenterPoint = new PointF(28, 28);
                    gradient.CenterColor = Color.FromArgb((int)(105 * intensity), 214, 239, 255);
                    gradient.SurroundColors = new[] { Color.Transparent };
                    g.FillPath(gradient, bloom);
                }

                Random random = new(variant * 739 + 57);
                using Pen branch = new(Color.FromArgb((int)(212 * intensity), 238, 251, 255), 1f)
                    { StartCap = LineCap.Round, EndCap = LineCap.Round };
                using SolidBrush dot = new(Color.FromArgb((int)(168 * intensity), 246, 254, 255));
                int arms = variant == 1 ? 5 : 6;
                for (int k = 0; k < arms; k++)
                {
                    float angle = MathF.Tau * k / arms + variant * .21f;
                    float dx = MathF.Cos(angle), dy = MathF.Sin(angle);
                    float radius = 13 + random.NextSingle() * 8;
                    g.DrawLine(branch, 28, 28, 28 + dx * radius, 28 + dy * radius);
                    for (int fork = 1; fork <= 2; fork++)
                    {
                        float t = fork * .35f + .12f;
                        float bx = 28 + dx * radius * t, by = 28 + dy * radius * t;
                        float d = 3 + radius * .09f;
                        float a1 = angle + .75f, a2 = angle - .75f;
                        g.DrawLine(branch, bx, by, bx + MathF.Cos(a1) * d, by + MathF.Sin(a1) * d);
                        g.DrawLine(branch, bx, by, bx + MathF.Cos(a2) * d, by + MathF.Sin(a2) * d);
                    }
                }
                g.FillEllipse(dot, 25, 25, 6, 6);
            }
            speckSprites[variant, level] = image;
            return image;
        }

        internal static void DrawSpecks(Graphics g, Rectangle bounds,
            FrostSpeck[] specks, float seconds, float intensity, bool round)
        {
            if (bounds.Width < 5 || bounds.Height < 5 || intensity < .015f) return;
            GraphicsState state = g.Save();
            using GraphicsPath clip = Shape(new RectangleF(bounds.X + 1, bounds.Y + 1,
                bounds.Width - 2, bounds.Height - 2), round);
            g.SetClip(clip, CombineMode.Intersect);
            g.InterpolationMode = InterpolationMode.Bilinear;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            float diameter = Math.Min(bounds.Width, bounds.Height);
            for (int i = 0; i < specks.Length; i++)
            {
                FrostSpeck speck = specks[i];
                float cycles = (seconds - speck.Phase) / SpeckCycle;
                int generation = (int)MathF.Floor(cycles);
                float age = (cycles - generation) * SpeckCycle;
                float total = speck.Grow + speck.Hold + speck.Melt;
                if (age > total) continue;

                float alpha;
                if (age < speck.Grow)
                    alpha = SnowDrawing.Smooth(age / speck.Grow);
                else if (age < speck.Grow + speck.Hold)
                    alpha = 1;
                else
                    alpha = 1 - SnowDrawing.Smooth((age - speck.Grow - speck.Hold) / speck.Melt);
                alpha *= intensity * speck.Opacity;
                int level = Math.Clamp((int)(alpha * SpeckLevels), 0, SpeckLevels - 1);
                if (alpha < .07f) continue;

                uint key = Mix(speck.Seed ^ unchecked((uint)generation * 0x9e3779b9u));
                float u = Unit(Mix(key ^ 0xa24baed4u));
                float v = Unit(Mix(key ^ 0x48a38b1fu));
                float x, y;
                if (round)
                {
                    float radius = MathF.Sqrt(u) * .92f;
                    float angle = v * MathF.Tau;
                    x = .5f + MathF.Cos(angle) * radius * .46f;
                    y = .5f + MathF.Sin(angle) * radius * .46f;
                }
                else
                {
                    x = .08f + u * .84f;
                    y = .16f + v * .68f;
                }

                float side = diameter * speck.Size;
                float growth = .77f + .23f * SnowDrawing.Smooth(age / speck.Grow);
                float span = side * growth;
                float cx = bounds.Left + x * bounds.Width;
                float cy = bounds.Top + y * bounds.Height;
                Bitmap particle = GetSpeckSprite(speck.Variant, level);
                g.DrawImage(particle, new RectangleF(cx - span / 2, cy - span / 2, span, span));
            }
            g.Restore(state);
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
