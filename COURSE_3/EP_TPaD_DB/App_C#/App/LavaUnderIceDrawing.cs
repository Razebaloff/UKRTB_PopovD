using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace LP_PopovD_App
{
    // Застывшая корка, пористые русла и горячие прожилки генерируются
    // процедурным бесшовным шумом один раз. Движутся только смещения
    // двух фактур; ледяная поверхность не пересоздаётся на каждом кадре.
    internal static class LavaUnderIceDrawing
    {
        private const int Tile = 512;
        private static Bitmap? river, glints;
        private static readonly Dictionary<(AppScreen Screen, bool CodeRequested), Bitmap> foreground = new();

        private static uint Hash(int x, int y, int seed)
        {
            uint v = unchecked((uint)(x * 374761393) ^ (uint)(y * 668265263) ^ (uint)seed);
            v = unchecked((v ^ (v >> 13)) * 1274126177u);
            return v ^ (v >> 16);
        }

        private static float Noise(float x, float y, int frequency, int seed)
        {
            // Периодическая сетка: края изображения соединяются без швов.
            float fx = x * frequency, fy = y * frequency;
            int px = (int)MathF.Floor(fx), py = (int)MathF.Floor(fy);
            float tx = fx - px, ty = fy - py;
            tx = tx * tx * (3 - 2 * tx);
            ty = ty * ty * (3 - 2 * ty);
            int x0 = ((px % frequency) + frequency) % frequency;
            int y0 = ((py % frequency) + frequency) % frequency;
            int x1 = (x0 + 1) % frequency, y1 = (y0 + 1) % frequency;
            float a = (Hash(x0, y0, seed) & 65535u) / 65535f;
            float b = (Hash(x1, y0, seed) & 65535u) / 65535f;
            float c = (Hash(x0, y1, seed) & 65535u) / 65535f;
            float d = (Hash(x1, y1, seed) & 65535u) / 65535f;
            return (a + (b - a) * tx) * (1 - ty) + (c + (d - c) * tx) * ty;
        }

        private static float Smooth(float a, float b, float x)
        {
            float t = Math.Clamp((x - a) / (b - a), 0, 1);
            return t * t * (3 - 2 * t);
        }

        private static Bitmap MakeTexture(bool highlights)
        {
            Bitmap bitmap = new(Tile, Tile, PixelFormat.Format32bppPArgb);
            bitmap.SetResolution(96, 96);
            int[] pixels = new int[Tile * Tile];
            for (int y = 0; y < Tile; y++)
            {
                float v = y / (float)Tile;
                for (int x = 0; x < Tile; x++)
                {
                    float u = x / (float)Tile;
                    // Независимые искривления разрывают ровные полосы.
                    float warpX = (Noise(u, v, 5, 93) - .5f) * .095f +
                        (Noise(u, v, 13, 42) - .5f) * .026f;
                    float warpY = (Noise(u, v, 6, 103) - .5f) * .11f +
                        (Noise(u, v, 17, 57) - .5f) * .031f;
                    float sx = u + warpX, sy = v + warpY;
                    float bed = Noise(sx, sy, 4, 193) * .46f +
                        Noise(sx, sy, 9, 343) * .31f +
                        Noise(sx, sy, 21, 571) * .17f +
                        Noise(sx, sy, 43, 811) * .06f;
                    float branches = Noise(sx + .12f, sy - .07f, 11, 291) * .58f +
                        Noise(sx, sy, 29, 313) * .42f;
                    float contour = MathF.Abs(bed - (.47f + .063f * (branches - .5f)));
                    float width = .028f + .021f * Noise(sx, sy, 8, 17);
                    float wide = 1 - Smooth(width, width + .12f, contour);
                    float core = 1 - Smooth(width * .20f, width * 1.45f, contour);
                    float cracks = 1 - Smooth(.015f, .09f,
                        MathF.Abs(branches - .50f));
                    float crust = Noise(sx, sy, 39, 619);
                    float flicker = Noise(sx, sy, 24, 992);

                    float heat = highlights
                        ? Math.Clamp(core * .75f + cracks * wide * .40f - .06f, 0, 1)
                        : Math.Clamp(wide * (.70f + cracks * .30f) + core * .28f, 0, 1);
                    float grit = .64f + .36f * crust;
                    float alpha = highlights
                        ? (10 + 97 * heat * grit) * MathF.Pow(heat, 1.1f)
                        : (16 + 84 * heat) * heat * (.72f + .28f * flicker);
                    int a = Math.Clamp((int)alpha, 0, 155);
                    float temperature = Math.Clamp(heat * .83f + flicker * .17f, 0, 1);
                    int red = (int)(highlights ? 255 : 204 + 51 * temperature);
                    int green = (int)(highlights ? 130 + 111 * temperature : 42 + 96 * temperature);
                    int blue = (int)(highlights ? 26 + 73 * temperature : 13 + 31 * temperature);
                    // Format32bppPArgb: все каналы строго умножены на alpha.
                    pixels[y * Tile + x] = a << 24 |
                        (red * a / 255) << 16 |
                        (green * a / 255) << 8 |
                        (blue * a / 255);
                }
            }

            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, Tile, Tile),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            try
            {
                for (int y = 0; y < Tile; y++)
                    Marshal.Copy(pixels, y * Tile, IntPtr.Add(data.Scan0, y * data.Stride), Tile);
            }
            finally { bitmap.UnlockBits(data); }
            return bitmap;
        }

        // Поверх магмы всегда остаются реальные элементы арены:
        // флаги, клубные эмблемы, клюшки, майка и разметка.
        // Одна прозрачная карта создаётся отдельно для каждого экрана
        // в логических координатах, без прямоугольных вырезов.
        private static Bitmap Foreground(AppScreen screen, bool codeRequested)
        {
            var key = (screen, codeRequested);
            if (foreground.TryGetValue(key, out Bitmap? old)) return old;
            Bitmap bitmap = new(1600, 800, PixelFormat.Format32bppPArgb);
            bitmap.SetResolution(96, 96);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                switch (screen)
                {
                    case AppScreen.Entry:
                        EntryDrawing.Draw(g);
                        break;
                    case AppScreen.Login:
                        JerseyDrawing.Draw(g, registration: false, includeControls: false,
                            codeRequested: codeRequested);
                        break;
                    case AppScreen.Registration:
                        JerseyDrawing.Draw(g, registration: true, includeControls: false);
                        break;
                    case AppScreen.Menu:
                        RinkDrawing.Draw(g);
                        break;
                }
            }
            foreground.Add(key, bitmap);
            return bitmap;
        }

        internal static void Draw(Graphics g, Size size, AppScreen screen, bool codeRequested, double seconds,
            bool withoutSticks = false)
        {
            if (size.Width < 2 || size.Height < 2) return;
            Bitmap lower = river ??= MakeTexture(highlights: false);
            Bitmap upper = glints ??= MakeTexture(highlights: true);
            GraphicsState saved = g.Save();
            g.CompositingMode = CompositingMode.SourceOver;

            // Лава распространяется на весь фон, в том числе вокруг
            // майки и центрального круга. Удалены все ExcludeClip и
            // прямоугольные зоны без эффекта.
            using (TextureBrush current = new(lower, WrapMode.Tile))
            {
                current.TranslateTransform((float)(seconds * 17 % Tile),
                    (float)(seconds * 7 % Tile));
                g.FillRectangle(current, 0, 0, size.Width, size.Height);
            }
            using (TextureBrush turbulence = new(upper, WrapMode.Tile))
            {
                turbulence.TranslateTransform((float)(-seconds * 9 % Tile),
                    (float)(seconds * 14 % Tile));
                g.FillRectangle(turbulence, 0, 0, size.Width, size.Height);
            }
            using (SolidBrush ice = new(Color.FromArgb(26, 204, 230, 245)))
                g.FillRectangle(ice, 0, 0, size.Width, size.Height);

            // Точное восстановление объекта поверх лавы по альфа-маске,
            // а не по его ограничивающему прямоугольнику.
            RectangleF canvas = RinkDrawing.GetCanvasBounds(size);
            Bitmap top = Foreground(screen, codeRequested);
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            if (withoutSticks && screen != AppScreen.Menu)
            {
                // Для вращения клюшек сам фон захватывается без них.
                // Неподвижные клюшки отрисуются отдельно после захвата.
                using Bitmap noSticks = ForegroundWithoutSticks(screen, codeRequested);
                g.DrawImage(noSticks, canvas, new Rectangle(0, 0, 1600, 800), GraphicsUnit.Pixel);
            }
            else g.DrawImage(top, canvas, new Rectangle(0, 0, 1600, 800), GraphicsUnit.Pixel);
            g.Restore(saved);
        }

        private static Bitmap ForegroundWithoutSticks(AppScreen screen, bool codeRequested)
        {
            Bitmap bitmap = new(1600, 800, PixelFormat.Format32bppPArgb);
            bitmap.SetResolution(96, 96);
            using Graphics g = Graphics.FromImage(bitmap);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            switch (screen)
            {
                case AppScreen.Entry: EntryDrawing.Draw(g, includeSticks: false); break;
                case AppScreen.Login:
                    JerseyDrawing.Draw(g, registration: false, includeControls: false,
                        includeSticks: false, codeRequested: codeRequested);
                    break;
                case AppScreen.Registration:
                    JerseyDrawing.Draw(g, registration: true, includeControls: false, includeSticks: false);
                    break;
                case AppScreen.Menu: RinkDrawing.Draw(g); break;
            }
            return bitmap;
        }

        internal static void Dispose()
        {
            river?.Dispose();
            glints?.Dispose();
            river = glints = null;
            foreach (Bitmap bitmap in foreground.Values) bitmap.Dispose();
            foreground.Clear();
        }
    }
}
