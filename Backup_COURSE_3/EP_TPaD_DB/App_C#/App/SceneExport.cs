using System.Drawing.Imaging;

namespace LP_PopovD_App
{
    internal static class SceneExport
    {
        internal const int Width16K = 15360, Height16K = 8640;
        internal const int Width32K = 30720, Height32K = 17280;
        internal const int BandHeight = 256;

        internal static string Argument(AppScreen screen) => screen switch
        {
            AppScreen.Entry => "--export-entry",
            AppScreen.Login => "--export-login",
            AppScreen.Registration => "--export-registration",
            AppScreen.Menu => "--export-menu",
            _ => throw new ArgumentOutOfRangeException(nameof(screen))
        };

        internal static bool TryGetScreen(string argument, out AppScreen screen)
        {
            foreach (AppScreen candidate in Enum.GetValues<AppScreen>())
                if (string.Equals(argument, Argument(candidate), StringComparison.Ordinal))
                {
                    screen = candidate;
                    return true;
                }
            screen = default;
            return false;
        }

        private static Size ExportSize(int width)
        {
            if (width < 960 || width > Width32K)
                throw new ArgumentOutOfRangeException(nameof(width), "Ширина: от 960 до 30720 пикселей.");
            return new Size(width, width * 9 / 16);
        }

        internal static void Save(string path, AppScreen screen, int width = Width32K,
            Action<int, int>? progress = null)
        {
            Size size = ExportSize(width);
            string extension = Path.GetExtension(path);
            if (string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase))
            {
                SavePng(new[] { path }, new[] { screen }, size, progress);
                return;
            }
            if (!string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Формат экспорта: PNG или JPEG.", nameof(path));
            if (width > Width16K)
                throw new ArgumentException("Для экспорта больше 16K используйте PNG без потерь.", nameof(path));
            using Bitmap artwork = SceneDrawing.Render(size, screen);
            using FileStream output = File.Create(Path.GetFullPath(path));
            ImageCodecInfo jpeg = ImageCodecInfo.GetImageEncoders().First(c => c.MimeType == "image/jpeg");
            using EncoderParameters settings = new(1);
            settings.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 100L);
            artwork.Save(output, jpeg, settings);
        }

        internal static void SaveAll(string directory, int width = Width32K, Action<int, int>? progress = null)
        {
            Size size = ExportSize(width);
            string folder = Path.GetFullPath(directory);
            Directory.CreateDirectory(folder);
            AppScreen[] screens = { AppScreen.Entry, AppScreen.Login, AppScreen.Registration, AppScreen.Menu };
            string suffix = width == Width32K ? "32K" : width == Width16K ? "16K" : $"{size.Width}x{size.Height}";
            string[] paths = screens.Select(s => Path.Combine(folder, $"{s}_{suffix}.png")).ToArray();
            SavePng(paths, screens, size, progress);
        }

        private static void SavePng(string[] paths, AppScreen[] screens, Size size, Action<int, int>? progress)
        {
            List<PngRowWriter> writers = new();
            try
            {
                foreach (string path in paths) writers.Add(new PngRowWriter(path, size));
                for (int y = 0; y < size.Height; y += BandHeight)
                {
                    Rectangle region = new(0, y, size.Width, Math.Min(BandHeight, size.Height - y));
                    // Общая полоса льда считается один раз для всех четырёх экранов.
                    using Bitmap ice = NativeIceDrawing.RenderRegion(size, region);
                    for (int i = 0; i < screens.Length; i++)
                    {
                        using Bitmap band = SceneDrawing.RenderBand(size, region, screens[i], ice);
                        writers[i].Append(band);
                    }
                    progress?.Invoke(region.Bottom, size.Height);
                }
                foreach (PngRowWriter writer in writers) writer.Complete();
            }
            finally
            {
                foreach (PngRowWriter writer in writers) writer.Dispose();
            }
        }
    }
}
