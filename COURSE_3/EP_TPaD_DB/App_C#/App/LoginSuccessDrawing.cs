using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace LP_PopovD_App
{
    internal sealed class LoginSuccessDrawing : IDisposable
    {
        internal const int DurationMilliseconds = 2600;
        internal const float ContactSeconds = 1.86f;
        private readonly Size size;
        private readonly Bitmap before, clean, letters, glow, printed;
        private readonly List<(Bitmap Image, Rectangle Bounds, float Delay)> pieces = new();
        private readonly SnowSeed[] wind = SnowDrawing.Seeds(252, 81624);
        private readonly SnowSeed[] powder = SnowDrawing.Seeds(132, 22147);
        private readonly PointF[] glyphPoints;
        private readonly float spriteScale, nameScale;
        private readonly RectangleF letterBounds;
        private readonly PointF initial = new(800, 362), landed = new(800, 460);
        private readonly RectangleF[] fields = JerseyDrawing.InputBounds(false, true);

        internal LoginSuccessDrawing(Size size, Bitmap before, Bitmap clean, string login)
        {
            this.size = size;
            this.before = before;
            this.clean = clean;
            RectangleF canvas = RinkDrawing.GetCanvasBounds(size);
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
            nameScale = Math.Min(1.68f, 440 / Math.Max(1, b.Width));
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

        private void AddPieces(RectangleF sceneBounds, int columns)
        {
            RectangleF canvas = RinkDrawing.GetCanvasBounds(size);
            float scale = canvas.Width / 1600;
            Rectangle bounds = Rectangle.Intersect(new Rectangle(Point.Empty, size), Rectangle.Ceiling(new RectangleF(
                canvas.X + (sceneBounds.X - 3) * scale, canvas.Y + (sceneBounds.Y - 3) * scale,
                (sceneBounds.Width + 6) * scale, (sceneBounds.Height + 6) * scale)));
            for (int i = 0; i < columns; i++)
            {
                int left = bounds.Left + bounds.Width * i / columns;
                int right = bounds.Left + bounds.Width * (i + 1) / columns;
                Rectangle strip = new(left, bounds.Top, right - left, bounds.Height);
                if (strip.Width < 1 || strip.Height < 1) continue;
                Bitmap image = before.Clone(strip, PixelFormat.Format32bppPArgb);
                float delay = .06f + Math.Abs(i - (columns - 1) / 2f) / columns * .16f;
                pieces.Add((image, strip, delay));
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
            Rectangle region = Rectangle.Intersect(new Rectangle(Point.Empty, clean.Size), Rectangle.Ceiling(new RectangleF(
                canvas.X + (landed.X + letterBounds.X * nameScale) * scale,
                canvas.Y + (landed.Y + letterBounds.Y * nameScale) * scale,
                letterBounds.Width * nameScale * scale, letterBounds.Height * nameScale * scale)));
            using Bitmap cloth = new(result.Width, result.Height, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(cloth))
                g.DrawImage(clean, new Rectangle(Point.Empty, cloth.Size), region, GraphicsUnit.Pixel);
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
            CompositingMode mode = g.CompositingMode;
            try
            {
                g.CompositingMode = CompositingMode.SourceOver;
                DrawCore(g, milliseconds);
            }
            finally { g.Restore(state); g.CompositingMode = mode; }
        }

        private void DrawCore(Graphics g, float milliseconds)
        {
            float time = Math.Clamp(milliseconds, 0, DurationMilliseconds) / 1000;
            if (time <= 0) { g.DrawImageUnscaled(before, 0, 0); return; }
            g.DrawImageUnscaled(clean, 0, 0);
            using ImageAttributes alpha = new();
            foreach (var piece in pieces)
            {
                float opacity = 1 - SnowDrawing.Smooth((time - piece.Delay) / .70f);
                if (opacity <= .001f) continue;
                alpha.SetColorMatrix(new ColorMatrix { Matrix33 = opacity });
                g.DrawImage(piece.Image, piece.Bounds, 0, 0, piece.Image.Width, piece.Image.Height, GraphicsUnit.Pixel, alpha);
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
            float grow = 1 + (nameScale - 1) * SnowDrawing.Smooth(falling);
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
            using Pen halo = new(Color.White, 2.3f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using Pen light = new(Color.White, .6f);
            for (int i = 0; i < 28; i++)
            {
                PointF point = glyphPoints[i * glyphPoints.Length / 28];
                float angle = MathF.Atan2(point.Y * 4, point.X) + MathF.Sin(i * 1.7f) * .3f;
                float pulse = .65f + .35f * MathF.Sin(time * 5 + i * 2.4f);
                float length = (7 + i % 5 * 3) * strength * pulse;
                float x = position.X + point.X * scale, y = position.Y + point.Y * scale;
                float dx = MathF.Cos(angle) * length, dy = MathF.Sin(angle) * length;
                halo.Color = Color.FromArgb((int)(24 * strength * pulse), 255, 255, 255);
                light.Color = Color.FromArgb((int)(122 * strength * pulse), 255, 255, 255);
                g.DrawLine(halo, x, y, x + dx, y + dy);
                g.DrawLine(light, x, y, x + dx * .88f, y + dy * .88f);
            }
        }

        private void DrawWind(Graphics g, float time)
        {
            if (time > 1.65f) return;
            using SolidBrush brush = new(Color.White);
            using Pen streak = new(Color.White, 1) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            for (int i = 0; i < wind.Length; i++)
            {
                SnowSeed seed = wind[i];
                float age = (time - .08f - seed.Phase * .43f) / (1.04f + seed.Y * .18f);
                if (age <= 0 || age >= 1) continue;
                RectangleF field = fields[i % fields.Length];
                float side = seed.X < .5f ? -1 : 1;
                float startX = field.X + seed.X * field.Width, startY = field.Y + seed.Y * field.Height;
                float endX = side < 0 ? -40 : 1640, endY = 820 + seed.Y * 80;
                float q = 1 - age;
                float controlX = startX + side * (80 + seed.Speed * 70);
                float controlY = startY + 90;
                float x = q * q * startX + 2 * q * age * controlX + age * age * endX;
                float y = q * q * startY + 2 * q * age * controlY + age * age * endY;
                x += MathF.Sin(seed.Angle + age * 12) * 8 * MathF.Sin(age * MathF.PI);
                y += MathF.Cos(seed.Angle + age * 9) * 5 * MathF.Sin(age * MathF.PI);
                float dx = (2 * q * (controlX - startX) + 2 * age * (endX - controlX)) * .012f;
                float dy = (2 * q * (controlY - startY) + 2 * age * (endY - controlY)) * .012f;
                SnowDrawing.Grain(g, brush, streak, x, y, seed.Radius, dx, dy,
                    MathF.Sin(age * MathF.PI) * (.28f + seed.Speed * .37f), i % 19 == 0);
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
            before.Dispose(); clean.Dispose(); letters.Dispose(); glow.Dispose(); printed.Dispose();
            foreach (var piece in pieces) piece.Image.Dispose();
            pieces.Clear();
        }
    }
}
