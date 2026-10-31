using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace LP_PopovD_App
{
    // Непрерывный материал льда: пиксели считаются в координатах полного кадра.
    // Один и тот же свет, рельеф и рисунок сохраняются в окне и в полосах 32K.
    internal static class NativeIceDrawing
    {
        private static Bitmap? cached;
        private static Size cachedSize;
        private static IceWorld? world;
        private static Size worldSize;

        // Рост габаритов 40%. Высота острой вершины не превышает 1,35 / 0,92 ≈ 1,468
        // прежней максимальной высоты глыбы, то есть остаётся в пределах 1,5 раза.
        internal const float LumpScale = 1.40f;
        internal const float LumpHeightScale = 1.35f;

        private readonly record struct Lump(float X, float Y, float Rx, float Ry,
            float Cos, float Sin, float Height, int Seed);

        private sealed class IceMaterial
        {
            private readonly byte[] pixels;
            private readonly int width, height;
            private readonly float mean;

            internal IceMaterial(float unit)
            {
                Bitmap image = ArtworkResources.Get("IceSurface");
                int w = image.Width, h = image.Height;
                byte[] level = new byte[w * h];
                BitmapData data = image.LockBits(new Rectangle(Point.Empty, image.Size),
                    ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    int[] row = new int[w];
                    for (int y = 0; y < h; y++)
                    {
                        Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, w);
                        for (int x = 0; x < w; x++)
                        {
                            int c = row[x];
                            level[y * w + x] = (byte)((((c >> 16) & 255) * 54 + ((c >> 8) & 255) * 183 + (c & 255) * 19) >> 8);
                        }
                    }
                }
                finally { image.UnlockBits(data); }
                // MIP-уровень убирает муар при уменьшении материала в обычном окне.
                while (w > 2 && h > 2 && w / (70 * unit) > 2)
                {
                    int nw = w / 2, nh = h / 2;
                    byte[] smaller = new byte[nw * nh];
                    for (int y = 0; y < nh; y++)
                        for (int x = 0; x < nw; x++)
                        {
                            int i = y * 2 * w + x * 2;
                            smaller[y * nw + x] = (byte)((level[i] + level[i + 1] + level[i + w] + level[i + w + 1]) / 4);
                        }
                    level = smaller; w = nw; h = nh;
                }
                pixels = level; width = w; height = h;
                mean = (float)level.Average(v => (int)v);
            }

            private static float Mirror(float t)
            {
                t -= MathF.Floor(t / 2) * 2;
                return t <= 1 ? t : 2 - t;
            }

            internal float Sample(float x, float y)
            {
                float u = Mirror(x / 70) * (width - 1), v = Mirror(y / 70) * (height - 1);
                int ix = Math.Min((int)u, width - 2), iy = Math.Min((int)v, height - 2);
                float fx = u - ix, fy = v - iy;
                int i = iy * width + ix;
                float a = pixels[i] * (1 - fx) + pixels[i + 1] * fx;
                float b = pixels[i + width] * (1 - fx) + pixels[i + width + 1] * fx;
                return (a * (1 - fy) + b * fy - mean) / 45;
            }
        }

        private sealed class IceWorld
        {
            internal readonly float Unit, Width, Height;
            internal readonly int Columns, Rows;
            internal readonly Lump[][] Cells;
            internal readonly IceMaterial Material;
            internal readonly List<Lump> Lumps = new();
            internal const int CellSize = 80;

            internal IceWorld(Size size)
            {
                Unit = Math.Min(size.Width / 1600f, size.Height / 900f);
                Width = size.Width / Unit;
                Height = size.Height / Unit;
                Material = new IceMaterial(Unit);
                Columns = (int)Math.Ceiling(Width / CellSize);
                Rows = (int)Math.Ceiling(Height / CellSize);
                Random random = new(83291);
                for (int edge = 0; edge < 4; edge++)
                    for (int layer = 0; layer < 2; layer++)
                    {
                        float length = edge < 2 ? Width : Height;
                        for (float along = -25; along < length + 40; along += 26 + random.NextSingle() * 26)
                        {
                            float a = along + (random.NextSingle() - .5f) * 18;
                            float inward = (layer == 0 ? -2 : 24) + random.NextSingle() * 15;
                            float angle = (random.NextSingle() - .5f) * 1.1f + (edge < 2 ? 0 : MathF.PI / 2);
                            float x = edge switch { 0 or 1 => a, 2 => inward, _ => Width - inward };
                            float y = edge switch { 0 => inward, 1 => Height - inward, _ => a };
                            Lumps.Add(new Lump(x, y, (23 + random.NextSingle() * 30) * LumpScale,
                                (12 + random.NextSingle() * 17) * LumpScale, MathF.Cos(angle), MathF.Sin(angle),
                                ((layer == 0 ? 7 : 4) + random.NextSingle() * 10) * LumpHeightScale, random.Next(1, 5000)));
                        }
                    }

                List<Lump>[] bins = Enumerable.Range(0, Columns * Rows).Select(_ => new List<Lump>()).ToArray();
                foreach (Lump lump in Lumps)
                {
                    float radius = Math.Max(lump.Rx, lump.Ry) * 1.3f + 12;
                    int left = Math.Clamp((int)MathF.Floor((lump.X - radius) / CellSize), 0, Columns - 1);
                    int right = Math.Clamp((int)((lump.X + radius) / CellSize), 0, Columns - 1);
                    int top = Math.Clamp((int)MathF.Floor((lump.Y - radius) / CellSize), 0, Rows - 1);
                    int bottom = Math.Clamp((int)((lump.Y + radius) / CellSize), 0, Rows - 1);
                    for (int y = top; y <= bottom; y++)
                        for (int x = left; x <= right; x++) bins[y * Columns + x].Add(lump);
                }
                Cells = bins.Select(b => b.ToArray()).ToArray();
            }
        }

        private static uint Hash(int x, int y)
        {
            uint n = unchecked((uint)x * 374761393u + (uint)y * 668265263u + 12573u);
            n = unchecked((n ^ (n >> 13)) * 1274126177u);
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

        private static int Shade(IceWorld scene, float x, float y, int pixelX, int pixelY)
        {
            float distance = Math.Min(Math.Min(x, scene.Width - x), Math.Min(y, scene.Height - y));
            float edge = Math.Clamp(1 - distance / 135, 0, 1);
            edge *= edge;
            float cloud = Noise(x * .008f, y * .008f) - .5f;
            float ripple = Noise(x * .046f, y * .046f) - .5f;
            float frost = Noise(x * .56f, y * .56f) - .5f;
            float micro = Noise(x * 4.1f, y * 4.1f) - .5f;
            float grain = ((Hash(pixelX, pixelY) & 7) - 3.5f) * .13f;
            float texture = ripple * 7 + frost * 3.8f + micro * 1.3f + grain;
            texture += (scene.Material.Sample(x, y) * .65f +
                scene.Material.Sample(x * .53f + y * .51f + 39, y * .53f - x * .51f + 71) * .35f) * 7;
            float r = 137 + cloud * 28 + texture - edge * 31;
            float g = 177 + cloud * 26 + texture - edge * 29;
            float b = 199 + cloud * 20 + texture - edge * 23;

            if (distance < 105 * LumpScale)
            {
                int column = Math.Clamp((int)(x / IceWorld.CellSize), 0, scene.Columns - 1);
                int row = Math.Clamp((int)(y / IceWorld.CellSize), 0, scene.Rows - 1);
                float height = 0, nx = 0, ny = 0, nz = 1, shadow = 0, localU = 0, localV = 0;
                int selectedSeed = 0, overlap = 0;
                foreach (ref readonly Lump lump in scene.Cells[row * scene.Columns + column].AsSpan())
                {
                    float dx = x - lump.X, dy = y - lump.Y;
                    float u = (dx * lump.Cos + dy * lump.Sin) / lump.Rx;
                    float v = (-dx * lump.Sin + dy * lump.Cos) / lump.Ry;
                    float q = u * u + v * v;
                    float su = ((dx - 4 * LumpHeightScale) * lump.Cos + (dy - 7 * LumpHeightScale) * lump.Sin) / (lump.Rx * 1.10f);
                    float sv = (-(dx - 4 * LumpHeightScale) * lump.Sin + (dy - 7 * LumpHeightScale) * lump.Cos) / (lump.Ry * 1.10f);
                    shadow = Math.Max(shadow, Math.Clamp(1 - su * su - sv * sv, 0, 1) * .22f);
                    if (q >= 2.1f) continue;
                    float cut = 1 - Math.Abs(u), cutU = -Math.Sign(u), cutV = 0;
                    float candidate = 1 - Math.Abs(v);
                    if (candidate < cut) { cut = candidate; cutU = 0; cutV = -Math.Sign(v); }
                    float diagonal = .72f * u + .70f * v;
                    candidate = 1.08f - Math.Abs(diagonal);
                    if (candidate < cut) { cut = candidate; cutU = -.72f * Math.Sign(diagonal); cutV = -.70f * Math.Sign(diagonal); }
                    diagonal = .85f * u - .53f * v;
                    candidate = 1.02f - Math.Abs(diagonal);
                    if (candidate < cut) { cut = candidate; cutU = -.85f * Math.Sign(diagonal); cutV = .53f * Math.Sign(diagonal); }
                    // Дополнительный срез одного угла даёт разные естественные сколы.
                    // Он только уменьшает исходный контур, не расширяя его сверх LumpScale.
                    float chipU = (.68f + (lump.Seed & 7) / 7f * .20f) * ((lump.Seed & 64) == 0 ? 1 : -1);
                    float chipV = (.40f + ((lump.Seed >> 3) & 7) / 7f * .12f) * ((lump.Seed & 128) == 0 ? 1 : -1);
                    candidate = .94f + lump.Seed % 5 * .01f - chipU * u - chipV * v;
                    if (candidate < cut) { cut = candidate; cutU = -chipU; cutV = -chipV; }
                    if (cut <= 0) continue;
                    overlap++;
                    float bevelWidth = lump.Seed % 4 == 0 ? .20f : .085f;
                    float bevel = Math.Clamp(cut / bevelWidth, 0, 1), root = MathF.Sqrt(bevel);
                    float profile = root * .45f + bevel * .55f;
                    // Смещённая вершина и сходящиеся плоскости вместо округлой макушки.
                    float pu = u - ((lump.Seed & 15) / 15f - .5f) * .32f;
                    float pv = v - (((lump.Seed >> 4) & 15) / 15f - .5f) * .28f;
                    float ridge = .75f * pu + .66f * pv;
                    float roof = Math.Clamp(1 - .32f * Math.Abs(pu) - .36f * Math.Abs(pv) - .12f * Math.Abs(ridge), .20f, 1);
                    float roofU = roof <= .20f ? 0 : -.32f * Math.Sign(pu) - .09f * Math.Sign(ridge);
                    float roofV = roof <= .20f ? 0 : -.36f * Math.Sign(pv) - .0792f * Math.Sign(ridge);
                    float z = lump.Height * profile * roof;
                    if (z <= height) continue;
                    height = z;
                    float slope = bevel < 1 ? (.225f / Math.Max(.035f, root) + .55f) / bevelWidth : 0;
                    float du = lump.Height * (slope * cutU * roof + profile * roofU);
                    float dv = lump.Height * (slope * cutV * roof + profile * roofV);
                    nx = -(du * lump.Cos / lump.Rx - dv * lump.Sin / lump.Ry);
                    ny = -(du * lump.Sin / lump.Rx + dv * lump.Cos / lump.Ry);
                    float inverse = 1 / MathF.Sqrt(nx * nx + ny * ny + 1);
                    nx *= inverse; ny *= inverse; nz = inverse;
                    localU = u; localV = v; selectedSeed = lump.Seed;
                }
                if (height > 0)
                {
                    float diffuse = Math.Max(0, -.44f * nx - .60f * ny + .67f * nz);
                    float half = Math.Max(0, -.24f * nx - .32f * ny + .916f * nz);
                    float specular = MathF.Pow(half, 36 + selectedSeed % 33) * 105;
                    float grazing = 1 - nz;
                    float reflection = grazing * grazing * (36 + grazing * 48);
                    float vein = Math.Abs(Noise(localU * 6 + selectedSeed, localV * 5) - .5f);
                    float milky = Math.Max(0, 1 - vein * 48) * (9 + frost * 10);
                    float hoar = Math.Max(0, Noise(x * 1.8f + selectedSeed, y * 1.8f) - .62f) * 52;
                    specular *= 1 - Math.Clamp(hoar / 45, 0, .45f);
                    float inclusions = scene.Material.Sample(x + nx * height * 1.5f + selectedSeed,
                        y + ny * height * 1.5f) * 25;
                    float occlusion = Math.Min(15, Math.Max(0, overlap - 1) * 2.4f);
                    r = r * .19f + (51 + diffuse * 115) * .81f + specular + reflection + milky + hoar + inclusions - occlusion;
                    g = g * .19f + (109 + diffuse * 103) * .81f + specular + reflection + milky + hoar + inclusions - occlusion;
                    b = b * .19f + (146 + diffuse * 85) * .81f + specular + reflection + milky + hoar + inclusions - occlusion;
                }
                else
                {
                    r *= 1 - shadow; g *= 1 - shadow * .86f; b *= 1 - shadow * .72f;
                }
            }
            return unchecked((int)0xff000000) | (int)Math.Clamp(r, 0, 255) << 16 |
                (int)Math.Clamp(g, 0, 255) << 8 | (int)Math.Clamp(b, 0, 255);
        }

        internal static Bitmap RenderRegion(Size size, Rectangle region)
        {
            if (size.Width < 1 || size.Height < 1 || region.Width < 1 || region.Height < 1 ||
                !new Rectangle(Point.Empty, size).Contains(region))
                throw new ArgumentOutOfRangeException(nameof(region));
            if (world == null || worldSize != size)
            {
                world = new IceWorld(size);
                worldSize = size;
            }
            IceWorld scene = world;
            Bitmap image = new(region.Width, region.Height, PixelFormat.Format32bppArgb);
            image.SetResolution(96, 96);
            try
            {
                BitmapData data = image.LockBits(new Rectangle(Point.Empty, region.Size),
                    ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try
                {
                    Parallel.For(0, region.Height,
                        new ParallelOptions { MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 8) },
                        () => new int[region.Width], (y, _, row) =>
                        {
                            int gy = region.Y + y;
                            float wy = (gy + .5f) / scene.Unit;
                            for (int x = 0; x < region.Width; x++)
                            {
                                int gx = region.X + x;
                                row[x] = Shade(scene, (gx + .5f) / scene.Unit, wy, gx, gy);
                            }
                            Marshal.Copy(row, 0, IntPtr.Add(data.Scan0, y * data.Stride), row.Length);
                            return row;
                        }, _ => { });
                }
                finally { image.UnlockBits(data); }
                using Graphics g = Graphics.FromImage(image);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TranslateTransform(-region.X, -region.Y);
                g.ScaleTransform(scene.Unit, scene.Unit);
                DrawSurfaceMarks(g, scene, new RectangleF(region.X / scene.Unit, region.Y / scene.Unit,
                    region.Width / scene.Unit, region.Height / scene.Unit));
                return image;
            }
            catch { image.Dispose(); throw; }
        }

        private static void DrawSurfaceMarks(Graphics g, IceWorld scene, RectangleF viewport)
        {
            RectangleF visible = viewport;
            visible.Inflate(4, 4);
            Random random = new(46273);
            for (int i = 0; i < 145; i++)
            {
                float x = random.NextSingle() * scene.Width, y = random.NextSingle() * scene.Height;
                float length = 30 + random.NextSingle() * 300, angle = random.NextSingle() * MathF.Tau;
                float ex = x + MathF.Cos(angle) * length, ey = y + MathF.Sin(angle) * length;
                float bend = (random.NextSingle() - .5f) * length * .32f;
                float dx = -MathF.Sin(angle) * bend, dy = MathF.Cos(angle) * bend;
                int opacity = random.Next(16, 47);
                float width = .08f + random.NextSingle() * .18f;
                using GraphicsPath path = new();
                path.AddBezier(x, y, x + (ex - x) * .33f + dx, y + (ey - y) * .33f + dy,
                    x + (ex - x) * .67f + dx, y + (ey - y) * .67f + dy, ex, ey);
                if (!visible.IntersectsWith(path.GetBounds())) continue;
                using Pen groove = new(Color.FromArgb(opacity, 38, 91, 117), width + .12f);
                using Pen glint = new(Color.FromArgb(opacity + 12, 239, 250, 254), width);
                g.DrawPath(groove, path);
                using Matrix shift = new();
                shift.Translate(0, -.18f);
                path.Transform(shift);
                g.DrawPath(glint, path);
            }

            foreach (Lump lump in scene.Lumps)
            {
                float radius = Math.Max(lump.Rx, lump.Ry);
                if (!visible.IntersectsWith(new RectangleF(lump.X - radius, lump.Y - radius, radius * 2, radius * 2))) continue;
                Random cracks = new(lump.Seed);
                for (int k = 0; k < 3; k++)
                {
                    float start = -.70f + cracks.NextSingle() * .35f;
                    float offset = (cracks.NextSingle() - .5f) * .9f;
                    PointF[] points = new PointF[7];
                    for (int p = 0; p < points.Length; p++)
                    {
                        float u = start + p * .16f;
                        float v = offset + (cracks.NextSingle() - .5f) * .11f;
                        points[p] = new(lump.X + u * lump.Rx * lump.Cos - v * lump.Ry * lump.Sin,
                            lump.Y + u * lump.Rx * lump.Sin + v * lump.Ry * lump.Cos);
                    }
                    using Pen fissure = new(Color.FromArgb(68, 35, 83, 107), .17f);
                    using Pen lip = new(Color.FromArgb(103, 229, 247, 251), .08f);
                    g.DrawLines(fissure, points);
                    g.TranslateTransform(0, -.18f);
                    g.DrawLines(lip, points);
                    g.TranslateTransform(0, .18f);
                }
            }

            random = new Random(18421);
            using SolidBrush snow = new(Color.FromArgb(94, 240, 249, 250));
            using SolidBrush shadow = new(Color.FromArgb(28, 36, 81, 104));
            for (int i = 0; i < 7000; i++)
            {
                float x = random.NextSingle() * scene.Width, y = random.NextSingle() * scene.Height;
                float diameter = .12f + random.NextSingle() * .65f;
                float distance = Math.Min(Math.Min(x, scene.Width - x), Math.Min(y, scene.Height - y));
                if (i > 1300 && distance > 78 || !visible.Contains(x, y)) continue;
                g.FillEllipse(shadow, x + .13f, y + .18f, diameter, diameter * .65f);
                g.FillEllipse(snow, x, y, diameter, diameter * .65f);
            }
        }

        internal static void Draw(Graphics g, Size size)
        {
            if (size.Width < 1 || size.Height < 1) return;
            if (cached == null || cachedSize != size)
            {
                cached?.Dispose();
                cached = RenderRegion(size, new Rectangle(Point.Empty, size));
                cachedSize = size;
            }
            GraphicsState state = g.Save();
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.None;
            g.DrawImage(cached, new Rectangle(Point.Empty, size), 0, 0, size.Width, size.Height, GraphicsUnit.Pixel);
            g.Restore(state);
        }

        internal static void Dispose() { cached?.Dispose(); cached = null; world = null; }
    }
}
