using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace LP_PopovD_App
{
    internal static class JerseyFabricDrawing
    {
        // Широкая хоккейная посадка оставляет все поля внутри спинки.
        internal static readonly RectangleF BodyBounds = new(290, 196, 1020, 572);
        internal static readonly RectangleF PaintBounds = new(278, 184, 1044, 602);
        private static Fabric? material;

        private sealed class Fabric
        {
            internal readonly int Width, Height;
            internal readonly int[] Pixels;
            internal readonly byte[] Shadow;
            internal readonly Rectangle Crop;

            internal Fabric()
            {
                Bitmap image = ArtworkResources.Get("JerseyBack");
                Width = image.Width; Height = image.Height;
                Pixels = new int[Width * Height];
                byte[] alpha = new byte[Pixels.Length];
                BitmapData data = image.LockBits(new Rectangle(Point.Empty, image.Size),
                    ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                int left = Width, top = Height, right = -1, bottom = -1;
                try
                {
                    int[] row = new int[Width];
                    for (int y = 0; y < Height; y++)
                    {
                        Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, Width);
                        for (int x = 0; x < Width; x++)
                        {
                            uint c = (uint)row[x];
                            int a = (int)(c >> 24), r = (int)(c >> 16 & 255), g = (int)(c >> 8 & 255), b = (int)(c & 255);
                            int i = y * Width + x;
                            alpha[i] = (byte)a;
                            Pixels[i] = a << 24 | (r * a + 127) / 255 << 16 | (g * a + 127) / 255 << 8 | (b * a + 127) / 255;
                            if (a < 32) continue;
                            left = Math.Min(left, x); top = Math.Min(top, y);
                            right = Math.Max(right, x); bottom = Math.Max(bottom, y);
                        }
                    }
                }
                finally { image.UnlockBits(data); }
                if (right < left) throw new InvalidDataException("Пустое изображение хоккейной майки.");
                Crop = Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
                Shadow = BlurAlpha(alpha, Width, Height, 6);
            }

            private static byte[] BlurAlpha(byte[] alpha, int width, int height, int radius)
            {
                int diameter = radius * 2 + 1;
                byte[] horizontal = new byte[alpha.Length], result = new byte[alpha.Length];
                for (int y = 0; y < height; y++)
                {
                    int sum = 0, row = y * width;
                    for (int k = 0; k <= radius; k++) sum += alpha[row + k];
                    for (int x = 0; x < width; x++)
                    {
                        horizontal[row + x] = (byte)(sum / diameter);
                        if (x - radius >= 0) sum -= alpha[row + x - radius];
                        if (x + radius + 1 < width) sum += alpha[row + x + radius + 1];
                    }
                }
                for (int x = 0; x < width; x++)
                {
                    int sum = 0;
                    for (int k = 0; k <= radius; k++) sum += horizontal[k * width + x];
                    for (int y = 0; y < height; y++)
                    {
                        result[y * width + x] = (byte)(sum / diameter);
                        if (y - radius >= 0) sum -= horizontal[(y - radius) * width + x];
                        if (y + radius + 1 < height) sum += horizontal[(y + radius + 1) * width + x];
                    }
                }
                return result;
            }

            internal void Read(float x, float y, out float a, out float r, out float g, out float b)
            {
                a = r = g = b = 0;
                if (x < 0 || y < 0 || x >= Width - 1 || y >= Height - 1) return;
                int ix = (int)x, iy = (int)y, i = iy * Width + ix;
                float fx = x - ix, fy = y - iy;
                uint p00 = (uint)Pixels[i], p10 = (uint)Pixels[i + 1];
                uint p01 = (uint)Pixels[i + Width], p11 = (uint)Pixels[i + Width + 1];
                float w00 = (1 - fx) * (1 - fy), w10 = fx * (1 - fy), w01 = (1 - fx) * fy, w11 = fx * fy;
                a = (p00 >> 24) * w00 + (p10 >> 24) * w10 + (p01 >> 24) * w01 + (p11 >> 24) * w11;
                r = (p00 >> 16 & 255) * w00 + (p10 >> 16 & 255) * w10 + (p01 >> 16 & 255) * w01 + (p11 >> 16 & 255) * w11;
                g = (p00 >> 8 & 255) * w00 + (p10 >> 8 & 255) * w10 + (p01 >> 8 & 255) * w01 + (p11 >> 8 & 255) * w11;
                b = (p00 & 255) * w00 + (p10 & 255) * w10 + (p01 & 255) * w01 + (p11 & 255) * w11;
            }

            internal float ShadowAt(float x, float y)
            {
                if (x < 0 || y < 0 || x >= Width - 1 || y >= Height - 1) return 0;
                int ix = (int)x, iy = (int)y, i = iy * Width + ix;
                float fx = x - ix, fy = y - iy;
                return (Shadow[i] * (1 - fx) + Shadow[i + 1] * fx) * (1 - fy) +
                    (Shadow[i + Width] * (1 - fx) + Shadow[i + Width + 1] * fx) * fy;
            }
        }

        internal static float InkLight(float x, float y)
        {
            material ??= new Fabric();
            Fabric fabric = material;
            float u = fabric.Crop.Left + (x - BodyBounds.X) * fabric.Crop.Width / BodyBounds.Width - .5f;
            float v = fabric.Crop.Top + (y - BodyBounds.Y) * fabric.Crop.Height / BodyBounds.Height - .5f;
            fabric.Read(u, v, out float alpha, out float r, out float green, out float b);
            if (alpha < 16) return 1;
            float luminance = (r * .2126f + green * .7152f + b * .0722f) * 255 / alpha;
            return Math.Clamp(.77f + luminance * .01f, .80f, 1.08f);
        }

        internal static void Draw(Graphics g, RectangleF? viewport)
        {
            material ??= new Fabric();
            Fabric fabric = material;
            RectangleF visible = viewport.HasValue ? RectangleF.Intersect(PaintBounds, viewport.Value) : PaintBounds;
            if (visible.IsEmpty) return;
            using Matrix transform = g.Transform;
            float[] m = transform.Elements;
            float sx = m[0], sy = m[3], dx = m[4], dy = m[5];
            if (sx <= 0 || sy <= 0 || Math.Abs(m[1]) > .001f || Math.Abs(m[2]) > .001f)
                throw new NotSupportedException("Для майки нужен прямой масштаб без поворота.");
            int left = (int)Math.Floor(visible.Left * sx + dx), top = (int)Math.Floor(visible.Top * sy + dy);
            int right = (int)Math.Ceiling(visible.Right * sx + dx), bottom = (int)Math.Ceiling(visible.Bottom * sy + dy);
            int width = right - left, height = bottom - top;
            if (width < 1 || height < 1) return;
            using Bitmap patch = new(width, height, PixelFormat.Format32bppPArgb);
            patch.SetResolution(96, 96);
            BitmapData data = patch.LockBits(new Rectangle(Point.Empty, patch.Size), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            float sourceScaleX = fabric.Crop.Width / BodyBounds.Width, sourceScaleY = fabric.Crop.Height / BodyBounds.Height;
            float detail = Math.Clamp((Math.Min(sx, sy) - 3) / 6, 0, 1);
            try
            {
                Parallel.For(0, height, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 8) },
                    () => new int[width], (y, _, row) =>
                    {
                        float wy = (top + y + .5f - dy) / sy;
                        float v = fabric.Crop.Top + (wy - BodyBounds.Y) * sourceScaleY - .5f;
                        for (int x = 0; x < width; x++)
                        {
                            float wx = (left + x + .5f - dx) / sx;
                            float u = fabric.Crop.Left + (wx - BodyBounds.X) * sourceScaleX - .5f;
                            fabric.Read(u, v, out float a, out float r, out float green, out float b);
                            if (a > 16 && detail > 0)
                            {
                                // Тонкое плетение строится в целевых пикселях, а не увеличивается из PNG.
                                float tx = (wx - BodyBounds.X) * 2.8f, ty = (wy - BodyBounds.Y) * 3.1f;
                                int ix = (int)MathF.Floor(tx), iy = (int)MathF.Floor(ty);
                                float fx = tx - ix, fy = ty - iy;
                                float yarn = ((ix + iy) & 1) == 0 ? 1 - Math.Abs(fx * 2 - 1) : 1 - Math.Abs(fy * 2 - 1);
                                float pore = Math.Max(0, 1 - ((fx - .5f) * (fx - .5f) + (fy - .5f) * (fy - .5f)) * 34);
                                float thread = ((yarn - .5f) * 2.2f - pore * 1.3f) * detail * a / 255;
                                r = Math.Clamp(r + thread, 0, a); green = Math.Clamp(green + thread, 0, a); b = Math.Clamp(b + thread, 0, a);
                            }
                            float shadow = fabric.ShadowAt(u - 5 * sourceScaleX, v - 10 * sourceScaleY) * .24f * (1 - a / 255);
                            float outputAlpha = a + shadow;
                            int aa = (int)Math.Clamp(outputAlpha, 0, 255);
                            int rr = (int)Math.Clamp(r + shadow * 4 / 255, 0, aa);
                            int gg = (int)Math.Clamp(green + shadow * 18 / 255, 0, aa);
                            int bb = (int)Math.Clamp(b + shadow * 30 / 255, 0, aa);
                            row[x] = aa << 24 | rr << 16 | gg << 8 | bb;
                        }
                        Marshal.Copy(row, 0, IntPtr.Add(data.Scan0, y * data.Stride), row.Length);
                        return row;
                    }, _ => { });
            }
            finally { patch.UnlockBits(data); }
            GraphicsState state = g.Save();
            g.ResetTransform();
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.None;
            g.DrawImageUnscaled(patch, left, top);
            g.Restore(state);
        }
    }
}
