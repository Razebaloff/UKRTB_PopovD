using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace LP_PopovD_App
{
    // Геометрия букв и мягкий ореол строятся один раз на размер окна.
    // В кадре меняются лишь прозрачность ореола и положение белого блика.
    internal sealed class TitleGlowDrawing : IDisposable
    {
        private readonly GraphicsPath glyphs;
        private readonly Bitmap halo;
        internal Size Size { get; }
        internal Rectangle Bounds { get; }

        internal TitleGlowDrawing(Size size, bool menu)
        {
            Size = size;
            glyphs = menu ? RinkTypography.CreateMenuTitlePath() : HeaderDrawing.CreateTitlePath();
            RectangleF canvas = RinkDrawing.GetCanvasBounds(size);
            float scale = canvas.Width / RinkDrawing.Width;
            using Matrix fit = new(scale, 0, 0, scale, canvas.X, canvas.Y);
            glyphs.Transform(fit);
            int radius = Math.Clamp((int)MathF.Ceiling(5 * scale), 3, 18);
            Bounds = Rectangle.Ceiling(RectangleF.Inflate(glyphs.GetBounds(), radius * 3 + 2, radius * 3 + 2));
            using Bitmap mask = new(Bounds.Width, Bounds.Height, PixelFormat.Format32bppPArgb);
            mask.SetResolution(96, 96);
            using (Graphics g = Graphics.FromImage(mask))
            {
                g.TranslateTransform(-Bounds.X, -Bounds.Y);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                using SolidBrush white = new(Color.White);
                g.FillPath(white, glyphs);
            }
            int[] pixels = new int[mask.Width * mask.Height];
            BitmapData data = mask.LockBits(new Rectangle(Point.Empty, mask.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try { for (int y = 0; y < mask.Height; y++) Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), pixels, y * mask.Width, mask.Width); }
            finally { mask.UnlockBits(data); }
            byte[] alpha = pixels.Select(color => (byte)((uint)color >> 24)).ToArray();
            alpha = Blur(Blur(alpha, mask.Width, mask.Height, radius), mask.Width, mask.Height, radius);
            for (int i = 0; i < pixels.Length; i++)
            {
                int a = Math.Min(220, alpha[i] * 2);
                pixels[i] = a << 24 | a << 16 | a << 8 | a;
            }
            halo = new Bitmap(mask.Width, mask.Height, PixelFormat.Format32bppPArgb);
            halo.SetResolution(96, 96);
            data = halo.LockBits(new Rectangle(Point.Empty, halo.Size), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            try { for (int y = 0; y < halo.Height; y++) Marshal.Copy(pixels, y * halo.Width, IntPtr.Add(data.Scan0, y * data.Stride), halo.Width); }
            finally { halo.UnlockBits(data); }
        }

        private static byte[] Blur(byte[] input, int width, int height, int radius)
        {
            int diameter = radius * 2 + 1;
            byte[] temporary = new byte[input.Length], result = new byte[input.Length];
            for (int y = 0; y < height; y++)
            {
                int sum = 0, row = y * width;
                for (int x = 0; x <= radius && x < width; x++) sum += input[row + x];
                for (int x = 0; x < width; x++)
                {
                    temporary[row + x] = (byte)(sum / diameter);
                    if (x - radius >= 0) sum -= input[row + x - radius];
                    if (x + radius + 1 < width) sum += input[row + x + radius + 1];
                }
            }
            for (int x = 0; x < width; x++)
            {
                int sum = 0;
                for (int y = 0; y <= radius && y < height; y++) sum += temporary[y * width + x];
                for (int y = 0; y < height; y++)
                {
                    result[y * width + x] = (byte)(sum / diameter);
                    if (y - radius >= 0) sum -= temporary[(y - radius) * width + x];
                    if (y + radius + 1 < height) sum += temporary[(y + radius + 1) * width + x];
                }
            }
            return result;
        }

        internal void Draw(Graphics g, float seconds)
        {
            GraphicsState state = g.Save();
            g.CompositingMode = CompositingMode.SourceOver;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            float pulse = .62f + .18f * MathF.Sin(seconds * .93f);
            using ImageAttributes opacity = new();
            opacity.SetColorMatrix(new ColorMatrix { Matrix33 = pulse });
            g.DrawImage(halo, Bounds, 0, 0, halo.Width, halo.Height, GraphicsUnit.Pixel, opacity);
            RectangleF inkBounds = glyphs.GetBounds();
            float center = .5f + .29f * MathF.Sin(seconds * .65f);
            using LinearGradientBrush sheen = new(inkBounds, Color.Gray, Color.White, 0f);
            sheen.InterpolationColors = new ColorBlend
            {
                Positions = new[] { 0f, center - .18f, center, center + .18f, 1f },
                Colors = new[] { Color.FromArgb(28, 178, 178, 178), Color.FromArgb(48, 210, 210, 210),
                    Color.FromArgb((int)(150 + 36 * pulse), 255, 255, 255),
                    Color.FromArgb(48, 210, 210, 210), Color.FromArgb(28, 178, 178, 178) }
            };
            g.FillPath(sheen, glyphs);
            g.Restore(state);
        }

        public void Dispose() { glyphs.Dispose(); halo.Dispose(); }
    }
}
