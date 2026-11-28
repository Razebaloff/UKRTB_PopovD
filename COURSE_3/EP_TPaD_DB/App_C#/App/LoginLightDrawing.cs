using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace LP_PopovD_App
{
    internal sealed class LoginLightDrawing : IDisposable
    {
        private readonly record struct Ray(float Angle, float Length, float Width, float Opacity);
        private readonly Ray[] rays = new Ray[17];
        private readonly Bitmap shaft = CreateShaft();

        internal LoginLightDrawing()
        {
            Random random = new(7214);
            for (int i = 0; i < rays.Length; i++)
            {
                float angle = (i + random.NextSingle() * .78f) * 360f / rays.Length;
                float length = i % 4 == 0 ? 1050 : 400 + 490 * random.NextSingle();
                rays[i] = new Ray(angle, length, 24 + 45 * random.NextSingle(),
                    .17f + .13f * random.NextSingle());
            }
        }

        // Один профиль рассеяния для всех лучей. Свет сходит к нулю как вдоль луча,
        // так и поперёк него, поэтому у пучка нет видимого штриха или резкого конца.
        private static Bitmap CreateShaft()
        {
            const int width = 256, height = 64;
            int[] pixels = new int[width * height];
            Color tint = RinkDrawing.IceLight;
            for (int x = 0; x < width; x++)
            {
                float u = x / (float)(width - 1);
                float along = SnowDrawing.Smooth(u / .035f) * (1 - u) * (1 - u);
                float spread = .09f + .91f * MathF.Sqrt(u);
                for (int y = 0; y < height; y++)
                {
                    float offset = (2f * y / (height - 1) - 1) / spread;
                    float across = Math.Max(0, 1 - offset * offset);
                    int a = (int)(255 * along * across * across);
                    pixels[y * width + x] = a << 24 | (tint.R * a / 255) << 16 |
                        (tint.G * a / 255) << 8 | tint.B * a / 255;
                }
            }
            Bitmap image = new(width, height, PixelFormat.Format32bppPArgb);
            image.SetResolution(96, 96);
            BitmapData data = image.LockBits(new Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            try { Marshal.Copy(pixels, 0, data.Scan0, pixels.Length); }
            finally { image.UnlockBits(data); }
            return image;
        }

        internal void Draw(Graphics g, PointF center, float strength, float extension, ImageAttributes opacity)
        {
            ColorMatrix fade = new();
            foreach (Ray ray in rays)
            {
                GraphicsState state = g.Save();
                g.TranslateTransform(center.X, center.Y);
                g.RotateTransform(ray.Angle);
                g.ScaleTransform(ray.Length * extension / shaft.Width, ray.Width / shaft.Height);
                fade.Matrix33 = ray.Opacity * strength;
                opacity.SetColorMatrix(fade);
                g.DrawImage(shaft, new Rectangle(0, -shaft.Height / 2, shaft.Width, shaft.Height),
                    0, 0, shaft.Width, shaft.Height, GraphicsUnit.Pixel, opacity);
                g.Restore(state);
            }
        }

        public void Dispose() => shaft.Dispose();
    }
}
