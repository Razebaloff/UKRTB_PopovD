using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace LP_PopovD_App
{
    // Геометрия букв и мягкий ореол строятся один раз на размер окна.
    // Белая светящаяся основа букв и мягкий ореол кэшируются вместе.
    // В кадре плавно меняется общая яркость без движения светового пятна.
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
            byte[] diffuse = Blur(Blur(alpha, mask.Width, mask.Height, radius), mask.Width, mask.Height, radius);
            byte[] close = Blur(alpha, mask.Width, mask.Height, Math.Max(1, (int)MathF.Ceiling(1.5f * scale)));
            for (int i = 0; i < pixels.Length; i++)
            {
                // Яркое ближнее свечение и широкое рассеяние вокруг букв.
                int nearLight = Math.Min(245, close[i] * 3 / 2);
                int farLight = Math.Min(195, diffuse[i] * 2);
                int a = (nearLight + farLight - nearLight * farLight / 255) * (255 - alpha[i]) / 255;
                pixels[i] = a << 24 | a << 16 | a << 8 | a;
            }
            halo = new Bitmap(mask.Width, mask.Height, PixelFormat.Format32bppPArgb);
            halo.SetResolution(96, 96);
            data = halo.LockBits(new Rectangle(Point.Empty, halo.Size), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            try { for (int y = 0; y < halo.Height; y++) Marshal.Copy(pixels, y * halo.Width, IntPtr.Add(data.Scan0, y * data.Stride), halo.Width); }
            finally { halo.UnlockBits(data); }
            using (Graphics light = Graphics.FromImage(halo))
            {
                light.TranslateTransform(-Bounds.X, -Bounds.Y);
                light.SmoothingMode = SmoothingMode.AntiAlias;
                light.PixelOffsetMode = PixelOffsetMode.HighQuality;
                // Тонкая холодная грань сохраняет читаемость на светлом льду.
                using Pen edge = new(Color.FromArgb(190, 24, 51, 71), Math.Max(1.2f, 1.05f * scale))
                    { LineJoin = LineJoin.Round };
                light.DrawPath(edge, glyphs);
                using SolidBrush white = new(Color.White);
                light.FillPath(white, glyphs);
            }
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
            try
            {
                g.CompositingMode = CompositingMode.SourceOver;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                // Одна интенсивность для всего слоя. Положение и форма
                // света неподвижны; нет бегущего блика или светового пятна.
                float intensity = .92f + .08f * MathF.Sin(seconds * 1.25f);
                using ImageAttributes opacity = new();
                opacity.SetColorMatrix(new ColorMatrix { Matrix33 = intensity });
                g.DrawImage(halo, Bounds, 0, 0, halo.Width, halo.Height, GraphicsUnit.Pixel, opacity);
            }
            finally { g.Restore(state); }
        }

        public void Dispose() { glyphs.Dispose(); halo.Dispose(); }
    }
}
