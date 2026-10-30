using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace LP_PopovD_App
{
    internal static class ArtworkResources
    {
        private static readonly Dictionary<string, Bitmap> images = new();

        internal static Bitmap Get(string name)
        {
            if (images.TryGetValue(name, out Bitmap? image))
                return image;
            using Stream stream = typeof(ArtworkResources).Assembly
                .GetManifestResourceStream("LP_PopovD_App.Assets." + name + ".png")
                ?? throw new FileNotFoundException("Изображение не включено в проект: " + name);
            using Image original = Image.FromStream(stream);
            image = CopyPixels(original);
            images.Add(name, image);
            return image;
        }

        private static Bitmap CopyPixels(Image original)
        {
            Bitmap copy = new(original.Width, original.Height, PixelFormat.Format32bppArgb);
            copy.SetResolution(96, 96);
            using Graphics g = Graphics.FromImage(copy);
            g.PageUnit = GraphicsUnit.Pixel;
            g.CompositingMode = CompositingMode.SourceCopy;
            // У исходников DPI от 25 до 600. Копируем именно пиксели, без физического масштаба.
            g.DrawImage(original, new Rectangle(0, 0, copy.Width, copy.Height),
                0, 0, original.Width, original.Height, GraphicsUnit.Pixel);
            return copy;
        }

        internal static Bitmap GetEmblem(string name)
        {
            string key = name + ":emblem";
            if (images.TryGetValue(key, out Bitmap? emblem))
                return emblem;

            using Bitmap cutout = CopyPixels(Get(name));
            int width = cutout.Width, height = cutout.Height;
            Rectangle full = new(0, 0, width, height);
            BitmapData data = cutout.LockBits(full, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            Rectangle bounds;
            try
            {
                int[] pixels = new int[width * height];
                for (int y = 0; y < height; y++)
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), pixels, y * width, width);

                // У этих двух источников непрозрачный фон. Белые детали внутри эмблем сохраняются.
                if (name is "Astra" or "Azamatov")
                    RemoveOuterBackground(pixels, width, height);

                int left = width, top = height, right = -1, bottom = -1;
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                        if ((uint)pixels[y * width + x] >> 24 > 8)
                        {
                            left = Math.Min(left, x);
                            top = Math.Min(top, y);
                            right = Math.Max(right, x);
                            bottom = Math.Max(bottom, y);
                        }
                if (right < left)
                    throw new InvalidDataException("Пустая эмблема: " + name);
                bounds = Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
                for (int y = 0; y < height; y++)
                    Marshal.Copy(pixels, y * width, IntPtr.Add(data.Scan0, y * data.Stride), width);
            }
            finally
            {
                cutout.UnlockBits(data);
            }

            emblem = cutout.Clone(bounds, PixelFormat.Format32bppPArgb);
            emblem.SetResolution(96, 96);
            images.Add(key, emblem);
            return emblem;
        }

        private static void RemoveOuterBackground(int[] pixels, int width, int height)
        {
            bool[] visited = new bool[pixels.Length];
            Queue<int> edge = new();
            void Visit(int index)
            {
                if (visited[index])
                    return;
                visited[index] = true;
                uint color = (uint)pixels[index];
                int r = (int)(color >> 16 & 255), g = (int)(color >> 8 & 255), b = (int)(color & 255);
                bool white = Math.Min(r, Math.Min(g, b)) >= 238 &&
                    Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b)) <= 12;
                if (color >> 24 <= 8 || white)
                {
                    pixels[index] = 0;
                    edge.Enqueue(index);
                }
            }

            for (int x = 0; x < width; x++)
            {
                Visit(x);
                Visit((height - 1) * width + x);
            }
            for (int y = 0; y < height; y++)
            {
                Visit(y * width);
                Visit(y * width + width - 1);
            }
            while (edge.TryDequeue(out int index))
            {
                int x = index % width, y = index / width;
                if (x > 0) Visit(index - 1);
                if (x + 1 < width) Visit(index + 1);
                if (y > 0) Visit(index - width);
                if (y + 1 < height) Visit(index + width);
            }
        }

        internal static void Dispose()
        {
            foreach (Bitmap image in images.Values)
                image.Dispose();
            images.Clear();
        }
    }
}
