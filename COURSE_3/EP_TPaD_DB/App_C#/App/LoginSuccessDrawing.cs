using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace LP_PopovD_App
{
    internal sealed class LoginSuccessDrawing : IDisposable
    {
        internal const int DurationMilliseconds = 2600;
        internal const float ContactSeconds = 1.86f;
        private readonly Size size, displaySize;
        private readonly Bitmap before, clean, reducedBefore, reducedClean, overlayBuffer, letters, glow, printed;
        private readonly Graphics overlayGraphics;
        private readonly List<PanelDissolve> panels = new();
        private readonly SnowSeed[] dust = SnowDrawing.Seeds(380, 81230);
        private readonly PointF[][] dustPaths;
        private const float DustStepSeconds = .03f;
        private readonly SnowSeed[] wind = SnowDrawing.Seeds(252, 81624);
        private readonly SnowSeed[] squall = SnowDrawing.Seeds(176, 73591);
        private readonly SnowSeed[] powder = SnowDrawing.Seeds(132, 22147);
        private readonly PointF[] glyphPoints;
        private readonly float spriteScale, nameScale;
        private readonly RectangleF letterBounds;
        private readonly PointF initial;
        private readonly PointF landed = new(800, 460);
        private readonly RectangleF[] fields;

        // Единая анимация для успешного ВХОДА и завершения РЕГИСТРАЦИИ:
        // кристаллическое рассеивание полей, снежный ветер и фамилия
        // на майке. Отличается только набор исчезающих полей.
        internal LoginSuccessDrawing(Size size, Bitmap before, Bitmap clean, string login,
            bool registration = false)
        {
            displaySize = size;
            int width = Math.Min(size.Width, 960);
            this.size = new Size(width, Math.Max(1, (int)MathF.Round(size.Height * width / (float)size.Width)));
            this.before = before;
            this.clean = clean;
            reducedBefore = Reduced(before, this.size);
            reducedClean = Reduced(clean, this.size);
            overlayBuffer = new Bitmap(this.size.Width, this.size.Height, PixelFormat.Format32bppPArgb);
            overlayBuffer.SetResolution(96, 96);
            overlayGraphics = Graphics.FromImage(overlayBuffer);
            RectangleF canvas = RinkDrawing.GetCanvasBounds(displaySize);
            using GraphicsPath path = new();
            using FontFamily family = new(RinkTypography.FamilyName);
            using StringFormat format = (StringFormat)StringFormat.GenericTypographic.Clone();
            format.FormatFlags |= StringFormatFlags.NoWrap;
            path.AddString(login, family, (int)FontStyle.Regular, 22, PointF.Empty, format);
            RectangleF b = path.GetBounds();
            float fit = Math.Min(1, 430 / Math.Max(1, b.Width));
            using (Matrix center = new(fit, 0, 0, fit, -(b.X + b.Width / 2) * fit, -(b.Y + b.Height / 2) * fit))
                path.Transform(center);
            b = path.GetBounds();
            // Имя увеличивается заметно (до 3.7 раза), но всегда помещается на спине майки.
            nameScale = Math.Min(3.7f, 440 / Math.Max(1, b.Width));
            letterBounds = new RectangleF(b.X - 20, b.Y - 24, b.Width + 40, b.Height + 48);
            // Кэш букв готовится с запасом деталей для их максимального размера
            // на экране, а не для 960-пиксельного буфера снежной анимации.
            spriteScale = Math.Min(18f, Math.Max(1.5f,
                canvas.Width / 1600f * nameScale * 1.45f));
            letters = LetterSprite(path, false);
            glow = LetterSprite(path, true);
            printed = PrintedSprite(letters, canvas);
            path.Flatten(null, .7f);
            glyphPoints = path.PathPoints;
            fields = JerseyDrawing.InputBounds(registration, true);
            initial = new PointF(fields[0].X + fields[0].Width / 2,
                fields[0].Y + fields[0].Height / 2);
            foreach (RectangleF field in fields) AddPanel(field);
            AddPanel(registration ? JerseyDrawing.SubmitBounds : JerseyDrawing.LoginSubmitBounds);
            if (!registration) AddPanel(JerseyDrawing.RememberBounds);
            AddPanel(JerseyDrawing.BackBounds);
            AddPanel(registration ? JerseyDrawing.StatusBounds : JerseyDrawing.LoginStatusBounds);
            // Путь каждого зерна рассчитывается один раз, без численного
            // интегрирования сотен частиц в каждом кадре анимации.
            dustPaths = new PointF[dust.Length][];
            PrepareDustPaths();
        }

        private static Bitmap Reduced(Bitmap original, Size requested)
        {
            Bitmap result = new(requested.Width, requested.Height, PixelFormat.Format32bppPArgb);
            result.SetResolution(96, 96);
            using Graphics g = Graphics.FromImage(result);
            g.CompositingMode = CompositingMode.SourceCopy;
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.DrawImage(original, new Rectangle(Point.Empty, requested), 0, 0,
                original.Width, original.Height, GraphicsUnit.Pixel);
            return result;
        }

        // Маска растворения — пиксельная, с независимым шумом на каждом пикселе.
        // Это исключает квадратные куски и сотни GDI-битмапов/DrawImage на кадр.
        private sealed class PanelDissolve : IDisposable
        {
            private readonly Bitmap source, eroded;
            private readonly int[] original, pixels;
            private readonly byte[] thresholds;
            private readonly Rectangle bounds;
            private readonly float delay;

            internal PanelDissolve(Bitmap background, Rectangle bounds, int seed, float delay)
            {
                this.bounds = bounds;
                this.delay = delay;
                source = background.Clone(bounds, PixelFormat.Format32bppPArgb);
                eroded = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppPArgb);
                eroded.SetResolution(96, 96);
                original = new int[bounds.Width * bounds.Height];
                pixels = new int[original.Length];
                thresholds = new byte[original.Length];
                Random random = new(seed);
                int gridWidth = (bounds.Width + 5) / 6;
                int gridHeight = (bounds.Height + 5) / 6;
                byte[] largeScale = new byte[gridWidth * gridHeight];
                random.NextBytes(largeScale);
                random.NextBytes(thresholds);

                // Смешиваем мелкое зерно с крупной непрерывной неоднородностью
                // для морозной, а не клетчатой картины разрушения поверхности.
                for (int y = 0; y < bounds.Height; y++)
                {
                    int row = y * bounds.Width;
                    for (int x = 0; x < bounds.Width; x++)
                    {
                        int k = row + x;
                        int coarse = largeScale[(y / 6) * gridWidth + x / 6];
                        int variation = (int)(thresholds[k] * .78f + coarse * .22f);
                        thresholds[k] = (byte)Math.Clamp(variation, 1, 254);
                    }
                }

                BitmapData data = source.LockBits(
                    new Rectangle(Point.Empty, source.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
                try
                {
                    for (int y = 0; y < bounds.Height; y++)
                        Marshal.Copy(IntPtr.Add(data.Scan0, data.Stride * y), original,
                            y * bounds.Width, bounds.Width);
                }
                finally { source.UnlockBits(data); }
            }

            internal void Draw(Graphics g, float seconds)
            {
                float age = Math.Clamp((seconds - delay) / .91f, 0, 1);
                if (age <= 0) { g.DrawImageUnscaled(source, bounds.Location); return; }
                if (age >= 1) return;
                float dissolve = SnowDrawing.Smooth(age);

                // Один LockBits на панель; кусочки имеют размер отдельного пикселя,
                // а не видимого прямоугольного тайла.
                for (int k = 0; k < original.Length; k++)
                {
                    float limit = thresholds[k] / 255f;
                    float opacity = SnowDrawing.Smooth((limit - dissolve) * 11f + .5f);
                    uint pixel = unchecked((uint)original[k]);
                    int a = (int)(pixel >> 24);
                    int outA = (int)(a * opacity);
                    if (outA == 0) { pixels[k] = 0; continue; }
                    int red = (int)((pixel >> 16 & 255) * opacity);
                    int green = (int)((pixel >> 8 & 255) * opacity);
                    int blue = (int)((pixel & 255) * opacity);
                    pixels[k] = (outA << 24) | (red << 16) | (green << 8) | blue;
                }
                BitmapData data = eroded.LockBits(new Rectangle(Point.Empty, eroded.Size),
                    ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
                try
                {
                    for (int y = 0; y < bounds.Height; y++)
                        Marshal.Copy(pixels, y * bounds.Width,
                            IntPtr.Add(data.Scan0, data.Stride * y), bounds.Width);
                }
                finally { eroded.UnlockBits(data); }
                g.DrawImageUnscaled(eroded, bounds.Location);
            }

            public void Dispose() { source.Dispose(); eroded.Dispose(); }
        }

        private void AddPanel(RectangleF sceneBounds)
        {
            RectangleF canvas = RinkDrawing.GetCanvasBounds(size);
            float scale = canvas.Width / 1600f;
            Rectangle bounds = Rectangle.Intersect(new Rectangle(Point.Empty, size),
                Rectangle.Ceiling(new RectangleF(
                    canvas.X + (sceneBounds.X - 3) * scale,
                    canvas.Y + (sceneBounds.Y - 3) * scale,
                    (sceneBounds.Width + 6) * scale, (sceneBounds.Height + 6) * scale)));
            if (bounds.Width < 1 || bounds.Height < 1) return;
            int seed = unchecked(bounds.X * 73856093 ^ bounds.Y * 19349663 ^ bounds.Width * 83492791);
            panels.Add(new PanelDissolve(reducedBefore, bounds, seed,
                .055f + .025f * panels.Count));
        }

        private Bitmap LetterSprite(GraphicsPath path, bool halo)
        {
            Bitmap image = new(Math.Max(1, (int)Math.Ceiling(letterBounds.Width * spriteScale)),
                Math.Max(1, (int)Math.Ceiling(letterBounds.Height * spriteScale)), PixelFormat.Format32bppPArgb);
            image.SetResolution(96, 96);
            using Graphics g = Graphics.FromImage(image);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.ScaleTransform(spriteScale, spriteScale);
            g.TranslateTransform(-letterBounds.X, -letterBounds.Y);
            if (halo)
            {
                for (int i = 7; i >= 1; i--)
                {
                    using Pen light = new(Color.FromArgb(7 + (7 - i) * 4, 255, 255, 255), i * 1.8f)
                        { LineJoin = LineJoin.Round };
                    g.DrawPath(light, path);
                }
            }
            using SolidBrush ink = new(halo ? Color.White : JerseyFieldDrawing.Ink);
            g.FillPath(ink, path);
            return image;
        }

        private Bitmap PrintedSprite(Bitmap mask, RectangleF canvas)
        {
            Bitmap result = mask.Clone(new Rectangle(Point.Empty, mask.Size), PixelFormat.Format32bppPArgb);
            // Свет и плетение берутся из ткани в месте будущей надписи, один раз до начала анимации.
            float scale = canvas.Width / 1600;
            Rectangle region = Rectangle.Intersect(new Rectangle(Point.Empty, clean.Size), Rectangle.Ceiling(new RectangleF(
                canvas.X + (landed.X + letterBounds.X * nameScale) * scale,
                canvas.Y + (landed.Y + letterBounds.Y * nameScale) * scale,
                letterBounds.Width * nameScale * scale, letterBounds.Height * nameScale * scale)));
            using Bitmap cloth = new(result.Width, result.Height, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(cloth))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                if (region.Width > 0 && region.Height > 0)
                    g.DrawImage(clean, new Rectangle(Point.Empty, cloth.Size), region, GraphicsUnit.Pixel);
            }
            Rectangle all = new(Point.Empty, result.Size);
            BitmapData data = result.LockBits(all, ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb);
            BitmapData texture = cloth.LockBits(all, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try
            {
                int[] row = new int[result.Width], material = new int[result.Width];
                for (int y = 0; y < result.Height; y++)
                {
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length);
                    Marshal.Copy(IntPtr.Add(texture.Scan0, y * texture.Stride), material, 0, material.Length);
                    for (int x = 0; x < row.Length; x++)
                    {
                        int a = (int)((uint)row[x] >> 24);
                        if (a == 0) continue;
                        uint c = (uint)material[x];
                        // Ткань задаёт лёгкую модуляцию цвета, но не размывает
                        // контур: альфа-канал букв сохраняется без фильтрации.
                        float luminance = (c >> 16 & 255) * .2126f +
                            (c >> 8 & 255) * .7152f + (c & 255) * .0722f;
                        float light = Math.Clamp(.91f + (luminance - 195) / 650f, .83f, 1.05f);
                        int r = Math.Clamp((int)(235 * light * a / 255), 0, a);
                        int green = Math.Clamp((int)(241 * light * a / 255), 0, a);
                        int blue = Math.Clamp((int)(245 * light * a / 255), 0, a);
                        row[x] = a << 24 | r << 16 | green << 8 | blue;
                    }
                    Marshal.Copy(row, 0, IntPtr.Add(data.Scan0, y * data.Stride), row.Length);
                }
            }
            finally { result.UnlockBits(data); cloth.UnlockBits(texture); }
            return result;
        }

        internal Bitmap Render(float milliseconds)
        {
            Bitmap frame = new(displaySize.Width, displaySize.Height, PixelFormat.Format32bppPArgb);
            frame.SetResolution(96, 96);
            using Graphics g = Graphics.FromImage(frame);
            Draw(g, milliseconds);
            return frame;
        }

        internal void Draw(Graphics g, float milliseconds)
        {
            GraphicsState state = g.Save();
            try
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                // Статичная ткань остаётся в нативном разрешении, а динамика — в лёгком буфере.
                Bitmap background = milliseconds <= 0 ? before : clean;
                g.DrawImage(background, new Rectangle(Point.Empty, displaySize), 0, 0,
                    background.Width, background.Height, GraphicsUnit.Pixel);
                if (milliseconds <= 0) return;
                overlayGraphics.CompositingMode = CompositingMode.SourceCopy;
                overlayGraphics.Clear(Color.Transparent);
                overlayGraphics.CompositingMode = CompositingMode.SourceOver;
                DrawCore(overlayGraphics, milliseconds);
                g.CompositingMode = CompositingMode.SourceOver;
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                g.DrawImage(overlayBuffer, new Rectangle(Point.Empty, displaySize),
                    0, 0, size.Width, size.Height, GraphicsUnit.Pixel);
                // Высокочёткие буквы и оптические лучи рисуются в нативном
                // размере окна поверх малого буфера снега и растворения.
                DrawName(g, milliseconds);
            }
            finally { g.Restore(state); }
        }

        private void DrawCore(Graphics g, float milliseconds)
        {
            float time = Math.Clamp(milliseconds, 0, DurationMilliseconds) / 1000;
            foreach (PanelDissolve panel in panels) panel.Draw(g, time);
            RectangleF canvas = RinkDrawing.GetCanvasBounds(size);
            GraphicsState state = g.Save();
            g.TranslateTransform(canvas.X, canvas.Y);
            g.ScaleTransform(canvas.Width / 1600, canvas.Height / 800);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            DrawWind(g, time);
            DrawDust(g, time);
            g.Restore(state);
        }

        private void DrawName(Graphics g, float milliseconds)
        {
            float time = Math.Clamp(milliseconds, 0, DurationMilliseconds) / 1000f;
            using ImageAttributes alpha = new();
            RectangleF canvas = RinkDrawing.GetCanvasBounds(displaySize);
            GraphicsState state = g.Save();
            g.TranslateTransform(canvas.X, canvas.Y);
            g.ScaleTransform(canvas.Width / 1600f, canvas.Height / 800f);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            float falling = Math.Clamp((time - 1.20f) / (ContactSeconds - 1.20f), 0, 1);
            float drop = falling * falling * falling;
            float sinceContact = Math.Max(0, time - ContactSeconds);
            float settle = time >= ContactSeconds ? MathF.Sin(sinceContact * 26) * MathF.Exp(-sinceContact * 13) : 0;
            PointF position = new(initial.X, initial.Y + (landed.Y - initial.Y) * drop + settle * 2.5f);
            // Даже очень длинный логин сначала меньше конечного размера примерно втрое.
            float grow = nameScale * (.34f + .66f *
                SnowDrawing.Smooth((time - .78f) / (ContactSeconds - .78f)));
            float squash = time >= ContactSeconds ? 1 - .10f * MathF.Exp(-sinceContact * 17) : 1;
            float glowStrength = SnowDrawing.Smooth((time - .86f) / .28f) *
                (1 - SnowDrawing.Smooth((time - 1.50f) / .36f));
            float printedStrength = SnowDrawing.Smooth((time - ContactSeconds) / .16f);
            float nameOpacity = SnowDrawing.Smooth((time - .06f) / .70f);
            if (glowStrength > .001f)
            {
                DrawRays(g, position, grow, glowStrength, time);
                DrawSprite(g, glow, position, grow, squash, glowStrength * .86f, alpha);
            }
            if (printedStrength > 0)
            {
                PointF shadow = new(position.X, position.Y + 1);
                DrawSprite(g, printed, shadow, grow, squash, printedStrength * .18f, alpha);
            }
            DrawSprite(g, letters, position, grow, squash, nameOpacity * (1 - printedStrength), alpha);
            DrawSprite(g, printed, position, grow, squash, printedStrength, alpha);
            if (time >= ContactSeconds) DrawLandingSnow(g, sinceContact);
            g.Restore(state);
        }

        private void DrawSprite(Graphics g, Image image, PointF position, float scale, float squash,
            float opacity, ImageAttributes alpha)
        {
            if (opacity <= .001f) return;
            GraphicsState state = g.Save();
            g.TranslateTransform(position.X, position.Y);
            g.ScaleTransform(scale, scale * squash);
            g.TranslateTransform(letterBounds.X, letterBounds.Y);
            alpha.SetColorMatrix(new ColorMatrix { Matrix33 = Math.Clamp(opacity, 0, 1) });
            // Целочисленный источник, субпиксельное движение задаётся матрицей.
            g.ScaleTransform(letterBounds.Width / image.Width, letterBounds.Height / image.Height);
            g.DrawImage(image, new Rectangle(0, 0, image.Width, image.Height),
                0, 0, image.Width, image.Height, GraphicsUnit.Pixel, alpha);
            g.Restore(state);
        }

        private static float RayNoise(int index)
        {
            uint value = unchecked((uint)index * 747796405u + 2891336453u);
            value ^= value >> 16;
            value = unchecked(value * 2246822519u);
            value ^= value >> 13;
            return (value & 65535u) / 65535f;
        }

        private void DrawRays(Graphics g, PointF position, float scale, float strength, float time)
        {
            if (glyphPoints.Length == 0 || strength <= .001f) return;
            // Нерегулярные лучи выходят из разных точек контура букв.
            // Каждый имеет собственные яркость, мерцание, размер и рассеяние.
            using Pen halo = new(Color.White, 5) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using Pen beam = new(Color.White, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using Pen core = new(Color.White, .65f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            for (int i = 0; i < 42; i++)
            {
                float irregularity = RayNoise(i + 11);
                float phase = RayNoise(i + 519) * MathF.Tau;
                PointF contour = glyphPoints[(int)(RayNoise(i + 231) * (glyphPoints.Length - 1))];
                float angle = i * MathF.Tau / 42f +
                    (irregularity - .5f) * .15f + MathF.Sin(time * 3.4f + phase) * .016f;
                float ux = MathF.Cos(angle), uy = MathF.Sin(angle);
                float x = position.X + contour.X * scale;
                float y = position.Y + contour.Y * scale;
                float reachX = Math.Abs(ux) < .0001f ? 10000f : (ux > 0 ? 1600 - x : -x) / ux;
                float reachY = Math.Abs(uy) < .0001f ? 10000f : (uy > 0 ? 800 - y : -y) / uy;
                float edgeDistance = Math.Max(0, Math.Min(reachX, reachY));
                float shimmer = .82f + .18f * MathF.Sin(time * (5 + irregularity * 4) + phase);
                float lengthFactor = i % 6 == 0 ? 1f : .32f + irregularity * .61f;
                float extend = SnowDrawing.Smooth((time - .85f) / .26f);
                float length = edgeDistance * lengthFactor * extend;
                if (length < 1) continue;
                float amount = strength * shimmer * (.47f + .53f * irregularity);
                PointF end = new(x + ux * length, y + uy * length);

                halo.Color = Color.FromArgb((int)(22 * amount), 218, 243, 255);
                halo.Width = 3.5f + 2.4f * irregularity;
                g.DrawLine(halo, x, y, end.X, end.Y);

                // Основная полупрозрачная нить, более яркая у источника.
                for (int segment = 0; segment < 4; segment++)
                {
                    float f0 = segment / 4f, f1 = (segment + 1) / 4f;
                    float light = 1 - segment * .21f;
                    beam.Color = Color.FromArgb((int)(103 * amount * light), 245, 252, 255);
                    beam.Width = 1.1f + (1 - f0) * .7f;
                    g.DrawLine(beam, x + ux * length * f0, y + uy * length * f0,
                        x + ux * length * f1, y + uy * length * f1);
                }

                core.Color = Color.FromArgb((int)(165 * amount), 255, 255, 255);
                g.DrawLine(core, x, y, x + ux * length * (.50f + .25f * irregularity),
                    y + uy * length * (.50f + .25f * irregularity));

                // Редкие вторичные рассеянные лучи, с небольшим отклонением.
                if (i % 3 == 0)
                {
                    float deflection = (i % 2 == 0 ? 1 : -1) * (.055f + irregularity * .065f);
                    float ax = MathF.Cos(angle + deflection), ay = MathF.Sin(angle + deflection);
                    float branch = length * (.24f + irregularity * .17f);
                    float sx = x + ux * length * .13f, sy = y + uy * length * .13f;
                    core.Color = Color.FromArgb((int)(49 * amount), 229, 248, 255);
                    g.DrawLine(core, sx, sy, sx + ax * branch, sy + ay * branch);
                }
            }
        }

        private static void DrawSnowflake(Graphics g, Pen pen, float x, float y, float size, int alpha)
        {
            if (alpha < 4) return;
            pen.Color = Color.FromArgb(Math.Clamp(alpha, 0, 255), 245, 252, 255);
            pen.Width = .6f;
            for (int arm = 0; arm < 6; arm++)
            {
                float a = arm * MathF.Tau / 6;
                float ux = MathF.Cos(a), uy = MathF.Sin(a);
                g.DrawLine(pen, x, y, x + ux * size, y + uy * size);
                float bx = x + ux * size * .67f, by = y + uy * size * .67f;
                for (int branch = -1; branch <= 1; branch += 2)
                {
                    float ba = a + branch * .62f;
                    g.DrawLine(pen, bx, by,
                        bx - MathF.Cos(ba) * size * .28f,
                        by - MathF.Sin(ba) * size * .28f);
                }
            }
        }

        // Единая геометрия воздушных потоков: верхние углы -> область
        // полей ввода -> соответствующие нижние углы. По ней рисуется
        // сама метель и вычисляется поле скорости для обломков интерфейса.
        // Плавная центральная ось потока без петель: по её высоте можно
        // найти участок, к которому притягивается сорванная с поля пыль.
        private static PointF WindBackbone(bool left, float lane, float progress)
        {
            lane = Math.Clamp(lane, 0, 1);
            float t = Math.Clamp(progress, 0, 1);
            float spread = lane - .5f;
            PointF start = new(-115 + lane * 170, -120 + lane * 52);
            PointF approach = new(320 + spread * 160, 105 + lane * 35);
            PointF turn = new(720 + spread * 270, 435 + spread * 140);
            PointF departure = new(370 + spread * 170, 665 + lane * 35);
            PointF finish = new(-135 + lane * 235, 900 + spread * 100);
            PointF sample = t <= .5f
                ? Quadratic(start, approach, turn, 2 * t)
                : Quadratic(turn, departure, finish, 2 * t - 1);
            return left ? sample : new PointF(1600 - sample.X, sample.Y);
        }

        // Снежный поток закручивается крупными кудрями вокруг осевой линии.
        // Ветры обоих углов зеркальны, но слои обладают разными фазами.
        private static PointF WindPath(bool left, float lane, float progress)
        {
            float t = Math.Clamp(progress, 0, 1);
            PointF center = WindBackbone(left, lane, t);
            PointF ahead = WindBackbone(left, lane, Math.Min(1, t + .008f));
            PointF behind = WindBackbone(left, lane, Math.Max(0, t - .008f));
            float dx = ahead.X - behind.X, dy = ahead.Y - behind.Y;
            float length = MathF.Max(.001f, MathF.Sqrt(dx * dx + dy * dy));
            float tx = dx / length, ty = dy / length;
            float angle = t * MathF.Tau * 2.65f + lane * MathF.PI * 1.9f;
            float envelope = MathF.Sin(MathF.PI * t);
            float radius = (37 + lane * 39) * envelope;
            return new PointF(center.X + radius * (-ty * MathF.Cos(angle) + tx * MathF.Sin(angle)),
                center.Y + radius * (tx * MathF.Cos(angle) + ty * MathF.Sin(angle)));
        }

        private static PointF Quadratic(PointF a, PointF b, PointF c, float t)
        {
            float q = 1 - t;
            return new PointF(q * q * a.X + 2 * q * t * b.X + t * t * c.X,
                q * q * a.Y + 2 * q * t * b.Y + t * t * c.Y);
        }

        private static PointF WindTangent(bool left, float lane, float progress)
        {
            float before = Math.Max(0, progress - .006f);
            float after = Math.Min(1, progress + .006f);
            PointF a = WindPath(left, lane, before);
            PointF b = WindPath(left, lane, after);
            float dx = b.X - a.X, dy = b.Y - a.Y;
            float length = MathF.Max(.001f, MathF.Sqrt(dx * dx + dy * dy));
            return new PointF(dx / length, dy / length);
        }

        private static float WindIntensity(float time)
        {
            // Сначала начинается метель, затем сильный порыв подхватывает
            // порошок из растворяющихся полей и постепенно стихает.
            return SnowDrawing.Smooth((time - .055f) / .27f) *
                (1 - SnowDrawing.Smooth((time - 1.57f) / .53f));
        }

        // Ветер как векторное поле, а не заданный заранее путь отдельных частиц.
        // Скорость направлена по касательной к видимой снежной струе;
        // поперечная составляющая затягивает частицы в её широкую зону.
        private static PointF WindVelocity(PointF particle, float time, bool left,
            float lane, float phase, float speed)
        {
            float strength = WindIntensity(time);
            if (strength <= .0001f) return PointF.Empty;

            // У потока монотонная вертикальная координата. Двоичный
            // поиск находит участок видимой струи напротив частицы.
            float low = 0, high = 1;
            for (int j = 0; j < 7; j++)
            {
                float mid = (low + high) * .5f;
                if (WindBackbone(left, lane, mid).Y < particle.Y) low = mid;
                else high = mid;
            }
            float t = (low + high) * .5f;
            PointF central = WindPath(left, lane, t);
            PointF direction = WindTangent(left, lane, t);
            float gust = .82f + .14f * MathF.Sin(time * 10.5f + phase) +
                .09f * MathF.Sin(time * 18f + lane * 9f);
            float velocity = (770 + speed * 190) * strength * gust;
            float pullX = Math.Clamp((central.X - particle.X) * .68f, -205, 205);
            float pullY = Math.Clamp((central.Y - particle.Y) * .42f, -115, 115);
            float eddy = MathF.Sin(time * 14f + phase + particle.Y * .009f) * 38 * strength;
            return new PointF(direction.X * velocity + pullX * strength -
                direction.Y * eddy, direction.Y * velocity + pullY * strength +
                direction.X * eddy);
        }

        private void DrawWind(Graphics g, float time)
        {
            float intensity = WindIntensity(time);
            if (intensity <= .001f) return;
            using SolidBrush grains = new(Color.White);
            using Pen streak = new(Color.White, 1) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using Pen vapor = new(Color.White, 1) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using GraphicsPath ribbon = new();

            // Несколько широких, частично перекрывающихся ветровых рукавов
            // с мягкой дымкой и тонкой яркой сердцевиной, вместо отдельных
            // узких полос и снежинок на пустом фоне.
            for (int side = 0; side < 2; side++)
                for (int band = 0; band < 7; band++)
                {
                    bool left = side == 0;
                    float lane = (band + .5f) / 7;
                    ribbon.Reset();
                    for (int k = 0; k <= 28; k++)
                    {
                        float p = k / 28f;
                        PointF point = WindPath(left, lane, p);
                        float bend = MathF.Sin(time * 8.3f + band * 1.4f + p * 13f) *
                            6.5f * MathF.Sin(p * MathF.PI);
                        point.X += bend;
                        if (k == 0) ribbon.StartFigure();
                        else
                        {
                            PointF prev = WindPath(left, lane, (k - 1) / 28f);
                            float oldBend = MathF.Sin(time * 8.3f + band * 1.4f +
                                (k - 1) / 28f * 13f) * 6.5f *
                                MathF.Sin((k - 1) / 28f * MathF.PI);
                            prev.X += oldBend;
                            ribbon.AddLine(prev, point);
                        }
                    }
                    vapor.Color = Color.FromArgb((int)(7 * intensity), 178, 226, 250);
                    vapor.Width = 66 + band % 3 * 21;
                    g.DrawPath(vapor, ribbon);
                    vapor.Color = Color.FromArgb((int)(13 * intensity), 222, 246, 255);
                    vapor.Width = 22 + band % 3 * 8;
                    g.DrawPath(vapor, ribbon);
                    vapor.Color = Color.FromArgb((int)(30 * intensity), 247, 253, 255);
                    vapor.Width = 1.2f + (band % 3) * .5f;
                    g.DrawPath(vapor, ribbon);
                }

            // Взвесь внутри тех же рукавов. Направление коротких следов
            // рассчитывается из касательной к общей геометрии ветра.
            for (int i = 0; i < squall.Length; i++)
            {
                SnowSeed seed = squall[i];
                float age = (time - .015f - seed.Phase * .42f) / (1.24f + seed.Speed * .24f);
                if (age <= 0 || age >= 1) continue;
                bool left = i % 2 == 0;
                float lane = .08f + seed.X * .84f;
                PointF center = WindPath(left, lane, age);
                PointF direction = WindTangent(left, lane, age);
                float spread = (seed.Y - .5f) * (65 + 95 * MathF.Sin(age * MathF.PI));
                float x = center.X - direction.Y * spread;
                float y = center.Y + direction.X * spread;
                float drift = MathF.Sin(time * 15f + seed.Angle + age * 9f) *
                    12f * MathF.Sin(age * MathF.PI);
                x += -direction.Y * drift;
                y += direction.X * drift;
                float opacity = intensity * MathF.Sin(age * MathF.PI) *
                    (.35f + seed.Speed * .38f);
                SnowDrawing.Grain(g, grains, streak, x, y,
                    seed.Radius * 1.28f, direction.X * (3 + seed.Speed * 4),
                    direction.Y * (3 + seed.Speed * 4), opacity);
                if (i % 7 == 0)
                    DrawSnowflake(g, streak, x, y, 2 + seed.Radius * 1.8f,
                        (int)(200 * opacity));
            }

            // Мелкая пыль также живёт внутри широкого потока.
            for (int i = 0; i < wind.Length; i++)
            {
                SnowSeed seed = wind[i];
                float age = (time - .075f - seed.Phase * .34f) / (1.20f + seed.Speed * .19f);
                if (age <= 0 || age >= 1) continue;
                bool left = (i & 1) == 0;
                float lane = .03f + seed.Y * .94f;
                PointF center = WindPath(left, lane, age);
                PointF direction = WindTangent(left, lane, age);
                float offset = (seed.X - .5f) * 170;
                float x = center.X - direction.Y * offset;
                float y = center.Y + direction.X * offset;
                SnowDrawing.Grain(g, grains, streak, x, y,
                    seed.Radius * .85f, direction.X * 4, direction.Y * 4,
                    intensity * MathF.Sin(age * MathF.PI) * (.22f + seed.Speed * .27f));
            }
        }

        // Временные траектории предрассчитаны интегрированием общей
        // модели ветра. Это НЕ индивидуальные кривые к нижним углам:
        // положение каждой частицы определяется исключительно скоростью
        // поля WindVelocity в точках пройденного пути.
        private void PrepareDustPaths()
        {
            const int samples = 50;
            for (int i = 0; i < dust.Length; i++)
            {
                SnowSeed seed = dust[i];
                float born = .11f + seed.Phase * .60f;
                RectangleF field = fields[i % fields.Length];
                float startX = field.Left + seed.X * field.Width;
                float startY = field.Top + seed.Y * field.Height;
                bool left = startX < 800;
                float lane = .10f + seed.Y * .80f;
                PointF[] route = new PointF[samples];
                PointF position = new(startX, startY);
                route[0] = position;
                for (int sample = 1; sample < samples; sample++)
                {
                    float at = born + (sample - .5f) * DustStepSeconds;
                    PointF velocity = WindVelocity(position, at, left, lane, seed.Angle, seed.Speed);
                    position.X += velocity.X * DustStepSeconds;
                    position.Y += velocity.Y * DustStepSeconds;
                    route[sample] = position;
                }
                dustPaths[i] = route;
            }
        }

        // После рождения частиц их движение берётся из рассчитанного
        // поля скорости. Поэтому они зависают до прихода ветра и затем
        // подхватываются его порывами, а не сами летят в угол.
        private void DrawDust(Graphics g, float time)
        {
            if (time > 2.15f) return;
            using SolidBrush grains = new(Color.White);
            using Pen streak = new(Color.White, 1) { StartCap = LineCap.Round, EndCap = LineCap.Round };

            for (int i = 0; i < dust.Length; i++)
            {
                SnowSeed seed = dust[i];
                float born = .11f + seed.Phase * .60f;
                float age = time - born;
                if (age <= 0 || age >= 1.45f) continue;

                PointF[] route = dustPaths[i];
                float frame = age / DustStepSeconds;
                int index = Math.Clamp((int)frame, 0, route.Length - 2);
                float part = Math.Clamp(frame - index, 0, 1);
                PointF position = new(
                    route[index].X + (route[index + 1].X - route[index].X) * part,
                    route[index].Y + (route[index + 1].Y - route[index].Y) * part);

                if (position.X < -24 || position.X > 1624 ||
                    position.Y < -24 || position.Y > 824) continue;

                RectangleF field = fields[i % fields.Length];
                float originX = field.Left + seed.X * field.Width;
                PointF velocityNow = WindVelocity(position, time, originX < 800,
                    .10f + seed.Y * .80f, seed.Angle, seed.Speed);
                float speed = MathF.Sqrt(velocityNow.X * velocityNow.X +
                    velocityNow.Y * velocityNow.Y);
                float strength = Math.Clamp(speed / 750f, 0, 1) *
                    (1 - SnowDrawing.Smooth((age - .94f) / .51f)) *
                    SnowDrawing.Smooth(age / .12f);

                SnowDrawing.Grain(g, grains, streak, position.X, position.Y,
                    seed.Radius * (.28f + (1 - Math.Min(age, 1)) * .59f),
                    velocityNow.X * .010f, velocityNow.Y * .010f,
                    strength * (.46f + seed.Speed * .35f), i % 23 == 0);
            }
        }

        private void DrawLandingSnow(Graphics g, float time)
        {
            using SolidBrush brush = new(Color.White);
            using Pen streak = new(Color.White, 1) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            float haze = (1 - MathF.Exp(-time * 45)) * MathF.Exp(-time * 7);
            for (int i = 0; i < 7; i++)
            {
                float width = 18 + time * 78;
                brush.Color = Color.FromArgb((int)(17 * haze), 242, 250, 255);
                g.FillEllipse(brush, landed.X + (i - 3) * Math.Min(30, letterBounds.Width * nameScale / 7) - width / 2,
                    landed.Y + 10 - time * 20 - width * .15f, width, width * .30f);
            }
            for (int i = 0; i < powder.Length; i++)
            {
                SnowSeed seed = powder[i];
                float age = time - seed.Phase * .065f;
                if (age <= 0 || age > .75f) continue;
                float side = seed.X < .5f ? -1 : 1;
                PointF anchor = glyphPoints.Length == 0 ? PointF.Empty : glyphPoints[(i * 29) % glyphPoints.Length];
                float vx = side * (35 + seed.Speed * 76), vy = -(20 + seed.Y * 70);
                float drag = (1 - MathF.Exp(-age * 4)) / 4;
                float x = landed.X + anchor.X * nameScale + vx * drag;
                float y = landed.Y + Math.Max(0, anchor.Y * nameScale) + vy * drag + 58 * age * age;
                float opacity = (1 - MathF.Exp(-age * 100)) * MathF.Exp(-age * 5) * (.35f + seed.Y * .6f);
                SnowDrawing.Grain(g, brush, streak, x, y, seed.Radius * .75f,
                    vx * MathF.Exp(-age * 4) * .012f, (vy + 116 * age) * .012f, opacity, i % 17 == 0);
            }
        }

        public void Dispose()
        {
            overlayGraphics.Dispose(); overlayBuffer.Dispose();
            reducedBefore.Dispose(); reducedClean.Dispose();
            before.Dispose(); clean.Dispose(); letters.Dispose(); glow.Dispose(); printed.Dispose();
            foreach (PanelDissolve panel in panels) panel.Dispose();
            panels.Clear();
        }
    }
}
