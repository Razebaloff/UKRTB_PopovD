using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace LP_PopovD_App
{
    // Фон создаётся сразу в пикселях окна. Низкоразрешённого фонового изображения нет.
    internal static class NativeIceDrawing
    {
        private static Bitmap? cached;
        private static Size cachedSize;

        private static uint Hash(int x, int y)
        {
            uint n = unchecked((uint)x * 374761393u + (uint)y * 668265263u + 12573u);
            n = (n ^ (n >> 13)) * 1274126177u;
            return n ^ (n >> 16);
        }

        private static float Noise(float x, float y)
        {
            int ix = (int)MathF.Floor(x), iy = (int)MathF.Floor(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);
            float a = (Hash(ix, iy) & 65535) / 65535f;
            float b = (Hash(ix + 1, iy) & 65535) / 65535f;
            float c = (Hash(ix, iy + 1) & 65535) / 65535f;
            float d = (Hash(ix + 1, iy + 1) & 65535) / 65535f;
            return (a + (b - a) * fx) * (1 - fy) + (c + (d - c) * fx) * fy;
        }

        private static Bitmap Create(Size size)
        {
            Bitmap image = new(size.Width, size.Height, PixelFormat.Format32bppArgb);
            image.SetResolution(96, 96);
            BitmapData data = image.LockBits(new Rectangle(Point.Empty, size),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                int[] row = new int[size.Width];
                for (int y = 0; y < size.Height; y++)
                {
                    float v = y / (float)size.Height;
                    for (int x = 0; x < size.Width; x++)
                    {
                        float u = x / (float)size.Width;
                        float edge = Math.Max(Math.Abs(u * 2 - 1), Math.Abs(v * 2 - 1));
                        float shade = MathF.Pow(edge, 12) * 66;
                        float cloud = (Noise(u * 9, v * 7) - 0.5f) * 11;
                        float grain = ((Hash(x, y) & 255) / 255f - 0.5f) * 3.2f;
                        int r = (int)Math.Clamp(137 + cloud + grain - shade, 0, 255);
                        int g = (int)Math.Clamp(177 + cloud + grain - shade * 0.9f, 0, 255);
                        int b = (int)Math.Clamp(202 + cloud + grain - shade * 0.65f, 0, 255);
                        row[x] = unchecked((int)0xff000000) | r << 16 | g << 8 | b;
                    }
                    Marshal.Copy(row, 0, IntPtr.Add(data.Scan0, y * data.Stride), row.Length);
                }
            }
            finally { image.UnlockBits(data); }

            using Graphics g2 = Graphics.FromImage(image);
            g2.SmoothingMode = SmoothingMode.AntiAlias;
            g2.PixelOffsetMode = PixelOffsetMode.HighQuality;
            DrawSkateTracks(g2, size);
            DrawCrystals(g2, size);
            return image;
        }

        private static void DrawSkateTracks(Graphics g, Size size)
        {
            Random random = new(46273);
            using Pen channel = new(Color.FromArgb(38, 38, 87, 120), 0.8f);
            using Pen glint = new(Color.FromArgb(98, 238, 249, 255), 0.65f);
            for (int i = 0; i < 82; i++)
            {
                float x = random.NextSingle() * size.Width;
                float y = random.NextSingle() * size.Height;
                float length = size.Width * (0.045f + random.NextSingle() * 0.23f);
                float direction = -0.85f + random.NextSingle() * 1.7f;
                float ex = x + MathF.Cos(direction) * length;
                float ey = y + MathF.Sin(direction) * length;
                float bend = (random.NextSingle() - 0.5f) * length * 0.11f;
                float nx = -MathF.Sin(direction) * bend, ny = MathF.Cos(direction) * bend;
                using GraphicsPath path = new();
                path.AddBezier(x, y, x + (ex - x) * 0.33f + nx, y + (ey - y) * 0.33f + ny,
                    x + (ex - x) * 0.67f + nx, y + (ey - y) * 0.67f + ny, ex, ey);
                g.DrawPath(channel, path);
                using Matrix offset = new();
                offset.Translate(0, -0.8f);
                path.Transform(offset);
                g.DrawPath(glint, path);
            }
            using SolidBrush snow = new(Color.FromArgb(90, 243, 251, 255));
            for (int i = 0; i < size.Width * size.Height / 900; i++)
            {
                float x = random.NextSingle() * size.Width, y = random.NextSingle() * size.Height;
                float dot = 0.4f + random.NextSingle() * 1.5f;
                g.FillEllipse(snow, x, y, dot, dot * 0.65f);
            }
        }

        private static void DrawCrystals(Graphics g, Size size)
        {
            Random random = new(83291);
            float unit = Math.Min(size.Width / 1600f, size.Height / 900f);
            for (int edge = 0; edge < 4; edge++)
            {
                float length = edge < 2 ? size.Width : size.Height;
                for (float along = -20 * unit; along < length; along += (18 + random.NextSingle() * 24) * unit)
                {
                    float depth = (20 + random.NextSingle() * 40) * unit;
                    float width = (26 + random.NextSingle() * 37) * unit;
                    PointF A(float a, float d) => edge switch
                    {
                        0 => new PointF(a, d),
                        1 => new PointF(a, size.Height - d),
                        2 => new PointF(d, a),
                        _ => new PointF(size.Width - d, a)
                    };
                    PointF[] polygon = { A(along - width * 0.3f, 0), A(along + width * 0.6f, 0),
                        A(along + width, depth * 0.35f), A(along + width * 0.38f, depth),
                        A(along - width * 0.2f, depth * 0.5f) };
                    using GraphicsPath crystal = new();
                    crystal.AddPolygon(polygon);
                    RectangleF bounds = crystal.GetBounds();
                    if (bounds.Width < 1 || bounds.Height < 1) continue;
                    using LinearGradientBrush body = new(bounds, Color.FromArgb(8, 37, 70),
                        Color.FromArgb(91, 158, 203), 125);
                    g.FillPath(body, crystal);
                    PointF core = A(along + width * 0.25f, depth * 0.3f);
                    for (int p = 0; p < polygon.Length; p++)
                    {
                        using SolidBrush facet = new(Color.FromArgb(35 + random.Next(90),
                            151 + random.Next(55), 197 + random.Next(40), 233 + random.Next(20)));
                        g.FillPolygon(facet, new[] { core, polygon[p], polygon[(p + 1) % polygon.Length] });
                    }
                    using Pen rim = new(Color.FromArgb(160, 194, 229, 249), Math.Max(0.65f, unit * 0.8f));
                    g.DrawPath(rim, crystal);
                    using Pen split = new(Color.FromArgb(110, 224, 246, 255), Math.Max(0.5f, unit * 0.5f));
                    g.DrawLine(split, core, polygon[3]);
                    using SolidBrush frost = new(Color.FromArgb(130, 234, 249, 255));
                    for (int p = 0; p < 6; p++)
                    {
                        PointF point = A(along + random.NextSingle() * width, random.NextSingle() * depth * 0.45f);
                        float dot = (0.5f + random.NextSingle()) * unit;
                        g.FillEllipse(frost, point.X, point.Y, dot, dot);
                    }
                }
            }
        }

        internal static void Draw(Graphics g, Size size)
        {
            if (size.Width < 1 || size.Height < 1) return;
            if (cached == null || cachedSize != size)
            {
                cached?.Dispose();
                cached = Create(size);
                cachedSize = size;
            }
            GraphicsState state = g.Save();
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.None;
            g.DrawImage(cached, new Rectangle(Point.Empty, size),
                0, 0, size.Width, size.Height, GraphicsUnit.Pixel);
            g.Restore(state);
        }

        internal static void Dispose() { cached?.Dispose(); cached = null; }
    }
}
