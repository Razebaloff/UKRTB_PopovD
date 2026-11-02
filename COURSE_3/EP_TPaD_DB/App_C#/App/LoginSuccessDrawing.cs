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
        private readonly List<(Bitmap Image, Rectangle Bounds, float Delay, float Drift, float Sway)> pieces = new();
        private readonly SnowSeed[] wind = SnowDrawing.Seeds(252, 81624);
        private readonly SnowSeed[] squall = SnowDrawing.Seeds(176, 73591);
        private readonly SnowSeed[] powder = SnowDrawing.Seeds(132, 22147);
        private readonly PointF[] glyphPoints;
        private readonly float spriteScale, nameScale;
        private readonly RectangleF letterBounds;
        private readonly PointF initial = new(800, 362), landed = new(800, 460);
        private readonly RectangleF[] fields = JerseyDrawing.InputBounds(false, true);

        internal LoginSuccessDrawing(Size size, Bitmap before, Bitmap clean, string login)
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
            RectangleF canvas = RinkDrawing.GetCanvasBounds(this.size);
            spriteScale = Math.Max(1, canvas.Width / 1600 * 1.5f);
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
            letters = LetterSprite(path, false);
            glow = LetterSprite(path, true);
            printed = PrintedSprite(letters, canvas);
            path.Flatten(null, .7f);
            glyphPoints = path.PathPoints;
            foreach (RectangleF field in fields) AddPieces(field, 12);
            AddPieces(JerseyDrawing.LoginSubmitBounds, 10);
            AddPieces(JerseyDrawing.RememberBounds, 6);
            AddPieces(JerseyDrawing.BackBounds, 4);
            AddPieces(JerseyDrawing.LoginStatusBounds, 8);
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

        private void AddPieces(RectangleF sceneBounds, int columns)
        {
            RectangleF canvas = RinkDrawing.GetCanvasBounds(size);
            float scale = canvas.Width / 1600;
            Rectangle bounds = Rectangle.Intersect(new Rectangle(Point.Empty, size), Rectangle.Ceiling(new RectangleF(
                canvas.X + (sceneBounds.X - 3) * scale, canvas.Y + (sceneBounds.Y - 3) * scale,
                (sceneBounds.Width + 6) * scale, (sceneBounds.Height + 6) * scale)));
            // Мелкие фрагменты с разными задержками и траекториями, а не исчезающие полосы.
            const int rows = 3;
            for (int row = 0; row < rows; row++)
                for (int col = 0; col < columns; col++)
                {
                    int left = bounds.Left + bounds.Width * col / columns;
                    int right = bounds.Left + bounds.Width * (col + 1) / columns;
                    int top = bounds.Top + bounds.Height * row / rows;
                    int bottom = bounds.Top + bounds.Height * (row + 1) / rows;
                    Rectangle tile = new(left, top, right - left, bottom - top);
                    if (tile.Width < 1 || tile.Height < 1) continue;
                    Bitmap image = reducedBefore.Clone(tile, PixelFormat.Format32bppPArgb);
                    uint hash = (uint)(col * 2246822519u + row * 3266489917u + bounds.Left * 668265263u);
                    float jitter = (hash & 255) / 255f;
                    float delay = .10f + .20f * jitter + .07f * row;
                    pieces.Add((image, tile, delay, .62f + jitter * .75f, (hash >> 8 & 255) / 255f));
                }
        }

        private Bitmap LetterSprite(GraphicsPath path, bool halo)
        {
            Bitmap image = new(Math.Max(1, (int)Math.Ceiling(letterBounds.Width * spriteScale)),
                Math.Max(1, (int)Math.Ceiling(letterBounds.Height * spriteScale)), PixelFormat.Format32bppPArgb);
            image.SetResolution(96, 96);
            using Graphics g = Graphics.FromImage(image);
            g.SmoothingMode = SmoothingMode.AntiAlias;
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
            Rectangle region = Rectangle.Intersect(new Rectangle(Point.Empty, reducedClean.Size), Rectangle.Ceiling(new RectangleF(
                canvas.X + (landed.X + letterBounds.X * nameScale) * scale,
                canvas.Y + (landed.Y + letterBounds.Y * nameScale) * scale,
                letterBounds.Width * nameScale * scale, letterBounds.Height * nameScale * scale)));
            using Bitmap cloth = new(result.Width, result.Height, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(cloth))
                g.DrawImage(reducedClean, new Rectangle(Point.Empty, cloth.Size), region, GraphicsUnit.Pixel);
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
                        float light = Math.Clamp(.57f + ((c >> 16 & 255) * .2126f + (c >> 8 & 255) * .7152f +
                            (c & 255) * .0722f) / 75, .62f, 1);
                        int r = (int)(235 * light * a / 255), green = (int)(241 * light * a / 255), b = (int)(245 * light * a / 255);
                        row[x] = a << 24 | r << 16 | green << 8 | b;
                    }
                    Marshal.Copy(row, 0, IntPtr.Add(data.Scan0, y * data.Stride), row.Length);
                }
            }
            finally { result.UnlockBits(data); cloth.UnlockBits(texture); }
            return result;
        }

        internal Bitmap Render(float milliseconds)
        {
            Bitmap frame = new(size.Width, size.Height, PixelFormat.Format32bppPArgb);
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
                g.DrawImageUnscaled(milliseconds <= 0 ? before : clean, 0, 0);
                if (milliseconds <= 0) return;
                overlayGraphics.CompositingMode = CompositingMode.SourceCopy;
                overlayGraphics.Clear(Color.Transparent);
                overlayGraphics.CompositingMode = CompositingMode.SourceOver;
                DrawCore(overlayGraphics, milliseconds);
                g.CompositingMode = CompositingMode.SourceOver;
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                g.DrawImage(overlayBuffer, new Rectangle(Point.Empty, displaySize),
                    0, 0, size.Width, size.Height, GraphicsUnit.Pixel);
            }
            finally { g.Restore(state); }
        }

        private void DrawCore(Graphics g, float milliseconds)
        {
            float time = Math.Clamp(milliseconds, 0, DurationMilliseconds) / 1000;
            using ImageAttributes alpha = new();
            foreach (var piece in pieces)
            {
                float age = (time - piece.Delay) / (1.06f + .22f * piece.Drift);
                if (age >= 1) continue;
                float moved = SnowDrawing.Smooth(Math.Clamp(age, 0, 1));
                float opacity = 1 - SnowDrawing.Smooth((age - .12f) / .74f);
                if (opacity <= .002f) continue;
                float side = piece.Bounds.X + piece.Bounds.Width / 2f < size.Width / 2f ? -1 : 1;
                float reach = size.Width * (.20f + .20f * piece.Drift);
                float dx = side * reach * moved +
                    MathF.Sin(age * 13 + piece.Sway * MathF.Tau) * 13 * moved;
                float dy = size.Height * (.35f + .30f * piece.Drift) * moved;
                float scale = 1 - .38f * moved;
                Rectangle dest = new((int)MathF.Round(piece.Bounds.X + dx),
                    (int)MathF.Round(piece.Bounds.Y + dy),
                    Math.Max(1, (int)MathF.Round(piece.Bounds.Width * scale)),
                    Math.Max(1, (int)MathF.Round(piece.Bounds.Height * scale)));
                alpha.SetColorMatrix(new ColorMatrix { Matrix33 = Math.Clamp(opacity, 0, 1) });
                g.DrawImage(piece.Image, dest, 0, 0, piece.Image.Width, piece.Image.Height,
                    GraphicsUnit.Pixel, alpha);
            }
            RectangleF canvas = RinkDrawing.GetCanvasBounds(size);
            GraphicsState state = g.Save();
            g.TranslateTransform(canvas.X, canvas.Y);
            g.ScaleTransform(canvas.Width / 1600, canvas.Height / 800);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            DrawWind(g, time);
            float falling = Math.Clamp((time - 1.20f) / (ContactSeconds - 1.20f), 0, 1);
            float drop = falling * falling * falling;
            float sinceContact = Math.Max(0, time - ContactSeconds);
            float settle = time >= ContactSeconds ? MathF.Sin(sinceContact * 26) * MathF.Exp(-sinceContact * 13) : 0;
            PointF position = new(initial.X, initial.Y + (landed.Y - initial.Y) * drop + settle * 2.5f);
            float grow = 1 + (nameScale - 1) *
                SnowDrawing.Smooth((time - .78f) / (ContactSeconds - .78f));
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

        private void DrawRays(Graphics g, PointF position, float scale, float strength, float time)
        {
            if (glyphPoints.Length == 0) return;
            using Pen halo = new(Color.White, 3.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using Pen light = new(Color.White, .9f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            // 36 расходящихся лучей доходят до краёв экрана, с убывающей яркостью.
            for (int i = 0; i < 36; i++)
            {
                PointF point = glyphPoints[i * glyphPoints.Length / 36];
                float angle = i * MathF.Tau / 36 + MathF.Sin(i * 2.1f + time * 4) * .028f;
                float ux = MathF.Cos(angle), uy = MathF.Sin(angle);
                float x = position.X + point.X * scale;
                float y = position.Y + point.Y * scale;
                float widthReach = Math.Abs(ux) < .001f ? 10000 : (ux > 0 ? 1600 - x : -x) / ux;
                float heightReach = Math.Abs(uy) < .001f ? 10000 : (uy > 0 ? 800 - y : -y) / uy;
                float pulse = .8f + .2f * MathF.Sin(time * 8 + i * 2.4f);
                float reach = Math.Max(0, Math.Min(widthReach, heightReach)) *
                    (.92f + .08f * pulse) * SnowDrawing.Smooth((time - .87f) / .22f);
                halo.Color = Color.FromArgb((int)(32 * strength * pulse), 245, 252, 255);
                light.Color = Color.FromArgb((int)(125 * strength * pulse), 255, 255, 255);
                g.DrawLine(halo, x, y, x + ux * reach, y + uy * reach);
                for (int segment = 0; segment < 3; segment++)
                {
                    light.Color = Color.FromArgb((int)(100 * strength * pulse * (1 - segment * .28f)), 255, 255, 255);
                    float a = reach * segment / 3, b = reach * (segment + 1) / 3;
                    g.DrawLine(light, x + ux * a, y + uy * a, x + ux * b, y + uy * b);
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

        private void DrawWind(Graphics g, float time)
        {
            if (time > 2.10f) return;
            using SolidBrush brush = new(Color.White);
            using Pen streak = new(Color.White, 1) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            // Две снежные струи выходят из верхних углов, закручиваются у майки
            // и уходят в соответствующие нижние углы.
            for (int i = 0; i < squall.Length; i++)
            {
                SnowSeed seed = squall[i];
                float age = (time - .035f - seed.Phase * .42f) / (1.18f + seed.Speed * .28f);
                if (age <= 0 || age >= 1) continue;
                bool left = i % 2 == 0;
                float startX = left ? -45 + seed.X * 200 : 1645 - seed.X * 200;
                float startY = -110 + seed.Y * 125;
                float controlX = left ? 650 + seed.X * 165 : 950 - seed.X * 165;
                float controlY = 190 + seed.Y * 110;
                float endX = left ? -125 + seed.X * 180 : 1725 - seed.X * 180;
                float endY = 810 + seed.Y * 110;
                float q = 1 - age;
                float x = q * q * startX + 2 * q * age * controlX + age * age * endX;
                float y = q * q * startY + 2 * q * age * controlY + age * age * endY;
                float eddy = MathF.Sin(seed.Angle + age * 19) * 11 * MathF.Sin(age * MathF.PI);
                x += eddy;
                float dx = (2 * q * (controlX - startX) + 2 * age * (endX - controlX)) * .018f;
                float dy = (2 * q * (controlY - startY) + 2 * age * (endY - controlY)) * .018f;
                float opacity = MathF.Sin(age * MathF.PI) * (.38f + seed.Speed * .38f);
                SnowDrawing.Grain(g, brush, streak, x, y, seed.Radius * 1.25f, dx, dy, opacity);
                if (i % 4 == 0)
                    DrawSnowflake(g, streak, x, y, 2.4f + seed.Radius * 1.8f, (int)(opacity * 210));
            }
            // Сорванная с полей ледяная пыль повторяет поток ветра к нижним краям.
            for (int i = 0; i < wind.Length; i++)
            {
                SnowSeed seed = wind[i];
                float age = (time - .15f - seed.Phase * .38f) / (1.05f + seed.Y * .21f);
                if (age <= 0 || age >= 1) continue;
                RectangleF field = fields[i % fields.Length];
                float side = seed.X < .5f ? -1 : 1;
                float startX = field.X + seed.X * field.Width, startY = field.Y + seed.Y * field.Height;
                float endX = side < 0 ? -55 : 1655, endY = 825 + seed.Y * 80;
                float controlX = startX + side * (170 + seed.Speed * 100), controlY = startY + 85;
                float q = 1 - age;
                float x = q * q * startX + 2 * q * age * controlX + age * age * endX;
                float y = q * q * startY + 2 * q * age * controlY + age * age * endY;
                x += MathF.Sin(seed.Angle + age * 15) * 12 * MathF.Sin(age * MathF.PI);
                float dx = (2 * q * (controlX - startX) + 2 * age * (endX - controlX)) * .013f;
                float dy = (2 * q * (controlY - startY) + 2 * age * (endY - controlY)) * .013f;
                float opacity = MathF.Sin(age * MathF.PI) * (.30f + seed.Speed * .42f);
                SnowDrawing.Grain(g, brush, streak, x, y, seed.Radius, dx, dy, opacity);
                if (i % 17 == 0)
                    DrawSnowflake(g, streak, x, y, 2 + seed.Radius * 1.5f, (int)(opacity * 170));
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
            foreach (var piece in pieces) piece.Image.Dispose();
            pieces.Clear();
        }
    }
}
