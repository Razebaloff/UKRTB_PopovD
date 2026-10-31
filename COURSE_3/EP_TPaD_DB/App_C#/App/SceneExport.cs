using System.Drawing.Imaging;

namespace LP_PopovD_App
{
    internal static class SceneExport
    {
        internal const int Width16K = 15360;
        internal const int Height16K = 8640;

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

        internal static void Save(string path, AppScreen screen, int width = Width16K)
        {
            if (width < 960 || width > Width16K)
                throw new ArgumentOutOfRangeException(nameof(width), "Ширина: от 960 до 15360 пикселей.");
            string extension = Path.GetExtension(path);
            bool png = string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase);
            if (!png && !string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Формат экспорта: PNG или JPEG.", nameof(path));

            // Фон и геометрия создаются сразу в целевом размере, без увеличения превью.
            using Bitmap artwork = SceneDrawing.Render(new Size(width, width * 9 / 16), screen);
            using FileStream output = File.Create(Path.GetFullPath(path));
            if (png) artwork.Save(output, ImageFormat.Png);
            else
            {
                ImageCodecInfo jpeg = ImageCodecInfo.GetImageEncoders().First(c => c.MimeType == "image/jpeg");
                using EncoderParameters settings = new(1);
                settings.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 100L);
                artwork.Save(output, jpeg, settings);
            }
        }
    }
}
