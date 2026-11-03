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

        // Вместо похожих на снежинки звёздочек — ветвящиеся ледяные
        // прожилки, как при образовании морозного узора на стекле.
        // Фаза, длина, положение и время таяния каждой прожилки независимы.
        private const float SpeckCycle = 3.65f;
        private const int GrowthStages = 9;
        private const int OpacityStages = 7;
        private const int SpriteSize = 80;
        private const int FilamentVariants = 5;
        private static readonly Bitmap?[,,] filamentSprites =
            new Bitmap[FilamentVariants, GrowthStages, OpacityStages];

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
            int area = Math.Max(1, size.Width) * Math.Max(1, size.Height);
            int count = round ? Math.Clamp(area / 1200, 42, 108)
                : Math.Clamp(area / 330, 48, 124);
            Random random = new(seed);
            FrostSpeck[] result = new FrostSpeck[count];
            for (int i = 0; i < count; i++)
                result[i] = new FrostSpeck(
                    unchecked((uint)random.NextInt64()),
                    random.NextSingle() * SpeckCycle,
                    .43f + random.NextSingle() * .47f,
                    .16f + random.NextSingle() * .35f,
                    .58f + random.NextSingle() * .53f,
                    round ? .18f + random.NextSingle() * .16f
                        : 1.20f + random.NextSingle() * .78f,
                    .65f + random.NextSingle() * .35f,
                    random.Next(FilamentVariants));
            return result;
        }

        private static Bitmap GetFilamentSprite(int variant, int growth, int opacity)
        {
            Bitmap? cached = filamentSprites[variant, growth, opacity];
            if (cached != null) return cached;
            Bitmap result = new(SpriteSize, SpriteSize, PixelFormat.Format32bppPArgb);
            result.SetResolution(96, 96);
            using Graphics g = Graphics.FromImage(result);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            float alpha = (opacity + 1f) / OpacityStages;
            float extension = (growth + 1f) / GrowthStages;
            float angle = variant * MathF.Tau / FilamentVariants + .21f;
            float ux = MathF.Cos(angle), uy = MathF.Sin(angle);
            float nx = -uy, ny = ux;
            Random random = new(4761 + variant * 3109);
            PointF[] spine = new PointF[8];
            for (int k = 0; k < spine.Length; k++)
            {
                float along = -28 + k * 8.1f;
                float bend = (random.NextSingle() - .5f) * (k == 0 ? 0 : 11);
                spine[k] = new PointF(40 + ux * along + nx * bend,
                    40 + uy * along + ny * bend);
            }

            using Pen shadow = new(Color.FromArgb((int)(75 * alpha), 63, 136, 176), 3.1f)
                { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using Pen gleam = new(Color.FromArgb((int)(218 * alpha), 240, 252, 255), 1.25f)
                { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using Pen fine = new(Color.FromArgb((int)(182 * alpha), 213, 242, 255), .82f)
                { StartCap = LineCap.Round, EndCap = LineCap.Round };

            // Основная ломаная нарастает последовательно вдоль прожилки.
            for (int k = 0; k < spine.Length - 1; k++)
            {
                float from = k / (float)(spine.Length - 1);
                float to = (k + 1f) / (spine.Length - 1);
                float p = Math.Clamp((extension - from) / (to - from), 0, 1);
                if (p <= 0) break;
                PointF first = spine[k], second = spine[k + 1];
                float ex = first.X + (second.X - first.X) * p;
                float ey = first.Y + (second.Y - first.Y) * p;
                g.DrawLine(shadow, first.X, first.Y, ex, ey);
                g.DrawLine(gleam, first.X + .7f, first.Y - .6f, ex + .7f, ey - .6f);

                if (k == 0 || k == spine.Length - 2) continue;
                // Отдельные ответвления возникают только когда ледяной
                // фронт достигает соответствующей точки.
                float branchProgress = Math.Clamp((extension - from) * 4.7f, 0, 1);
                if (branchProgress <= 0) continue;
                for (int side = -1; side <= 1; side += 2)
                {
                    float deviation = side * (.53f + random.NextSingle() * .45f);
                    float direction = angle + deviation;
                    float dist = (8 + random.NextSingle() * 10) * branchProgress;
                    float dx = MathF.Cos(direction), dy = MathF.Sin(direction);
                    PointF tip = new(first.X + dx * dist, first.Y + dy * dist);
                    g.DrawLine(fine, first, tip);
                    if (branchProgress > .52f)
                    {
                        float limb = (branchProgress - .52f) * 12f;
                        float wing = direction + side * .65f;
                        g.DrawLine(fine, tip.X - dx * 3, tip.Y - dy * 3,
                            tip.X - dx * 3 + MathF.Cos(wing) * limb,
                            tip.Y - dy * 3 + MathF.Sin(wing) * limb);
                    }
                }
            }

            filamentSprites[variant, growth, opacity] = result;
            return result;
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
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            float diameter = Math.Min(bounds.Width, bounds.Height);
            for (int i = 0; i < specks.Length; i++)
            {
                FrostSpeck speck = specks[i];
                float cycles = (seconds - speck.Phase) / SpeckCycle;
                int generation = (int)MathF.Floor(cycles);
                float age = (cycles - generation) * SpeckCycle;
                float total = speck.Grow + speck.Hold + speck.Melt;
                if (age > total) continue;

                float growth = SnowDrawing.Smooth(Math.Clamp(age / speck.Grow, 0, 1));
                float fade = age < speck.Grow + speck.Hold ? 1 :
                    1 - SnowDrawing.Smooth((age - speck.Grow - speck.Hold) / speck.Melt);
                float strength = fade * speck.Opacity * intensity;
                if (strength < .07f) continue;
                int growthStep = Math.Clamp((int)(growth * GrowthStages), 0, GrowthStages - 1);
                int opacityStep = Math.Clamp((int)(strength * OpacityStages), 0, OpacityStages - 1);

                uint key = Mix(speck.Seed ^ unchecked((uint)generation * 0x9e3779b9u));
                float u = Unit(Mix(key ^ 0xa24baed4u));
                float v = Unit(Mix(key ^ 0x48a38b1fu));
                float x, y;
                if (round)
                {
                    float radius = MathF.Sqrt(u) * .91f;
                    float polar = v * MathF.Tau;
                    x = .5f + MathF.Cos(polar) * radius * .46f;
                    y = .5f + MathF.Sin(polar) * radius * .46f;
                }
                else
                {
                    x = .06f + u * .88f;
                    y = .15f + v * .70f;
                }

                // Овальные кнопки намного ниже шайб: масштабируем
                // прожилки по высоте капсулы, а не по её площади.
                float span = round ? diameter * speck.Size : diameter * speck.Size;
                float cx = bounds.Left + x * bounds.Width;
                float cy = bounds.Top + y * bounds.Height;
                Bitmap filament = GetFilamentSprite(speck.Variant, growthStep, opacityStep);
                g.DrawImage(filament, new RectangleF(cx - span / 2,
                    cy - span / 2, span, span));
            }
            g.Restore(state);
        }

        // Ледяная корка растёт отдельными асимметричными участками,
        // пока не соединится в цельную поверхность. В кадре создаются
        // только альфа-композиции готовых слоёв.
        internal static Bitmap[] CreateIceGrowthFrames(Bitmap fullIce, bool round)
        {
            const int framesCount = 16;
            Bitmap[] frames = new Bitmap[framesCount];
            Size size = fullIce.Size;
            for (int frame = 0; frame < frames.Length; frame++)
            {
                Bitmap layer = new(size.Width, size.Height, PixelFormat.Format32bppPArgb);
                layer.SetResolution(96, 96);
                frames[frame] = layer;
                if (frame == 0) continue;
                using Graphics g = Graphics.FromImage(layer);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                if (frame == framesCount - 1)
                {
                    g.DrawImageUnscaled(fullIce, 0, 0);
                    continue;
                }
                float progress = frame / (float)(framesCount - 1);
                Random random = new(unchecked(size.Width * 1439 +
                    size.Height * 751 + (round ? 1171 : 257)));
                using GraphicsPath islands = new(FillMode.Winding);
                int nuclei = round ? 30 : Math.Clamp(size.Width / 14, 20, 54);
                for (int i = 0; i < nuclei; i++)
                {
                    float x = size.Width * (.08f + random.NextSingle() * .84f);
                    float y = size.Height * (.08f + random.NextSingle() * .84f);
                    float delay = random.NextSingle() * .41f;
                    float grown = SnowDrawing.Smooth((progress - delay) / (1 - delay));
                    float radius = (round ? Math.Min(size.Width, size.Height) * .48f
                        : size.Height * 1.5f) * grown;
                    if (radius < .2f) continue;
                    float taper = .70f + random.NextSingle() * .62f;
                    islands.AddEllipse(x - radius, y - radius * taper,
                        radius * 2, radius * taper * 2);
                }
                if (islands.PointCount == 0) continue;
                g.SetClip(islands, CombineMode.Intersect);
                g.DrawImageUnscaled(fullIce, 0, 0);
            }
            return frames;
        }

        internal static void DrawIceGrowth(Graphics g, Bitmap[] stages, Rectangle bounds,
            float amount)
        {
            if (amount <= .001f || stages.Length < 2) return;
            float p = SnowDrawing.Smooth(Math.Clamp(amount, 0, 1));
            float frame = p * (stages.Length - 1);
            int index = Math.Clamp((int)frame, 0, stages.Length - 1);
            if (index > 0)
                g.DrawImageUnscaled(stages[index], bounds.Location);
            if (index < stages.Length - 1)
            {
                float blend = frame - index;
                if (blend > .002f)
                    DrawLayer(g, stages[index + 1], bounds, blend);
            }
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
