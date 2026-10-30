using System.Drawing.Imaging;

namespace LP_PopovD_App
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();
            if (args.Length >= 2 && (args[0] == "--export-entry" || args[0] == "--export-menu"))
            {
                // Экспорт использует ту же отрисовку, что и окно, сразу в целевом разрешении.
                int width = args.Length >= 3 ? int.Parse(args[2]) : 15360;
                if (width < 960 || width > 15360)
                    throw new ArgumentOutOfRangeException(nameof(width), "Ширина: от 960 до 15360 пикселей.");
                using Bitmap artwork = SceneDrawing.Render(new Size(width, width * 9 / 16),
                    ArtworkResources.Get("IceFrame"), args[0] == "--export-menu");
                if (string.Equals(Path.GetExtension(args[1]), ".png", StringComparison.OrdinalIgnoreCase))
                {
                    artwork.Save(Path.GetFullPath(args[1]), ImageFormat.Png);
                    ArtworkResources.Dispose();
                    return;
                }
                ImageCodecInfo jpeg = ImageCodecInfo.GetImageEncoders().First(c => c.MimeType == "image/jpeg");
                using EncoderParameters settings = new(1);
                settings.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 100L);
                artwork.Save(Path.GetFullPath(args[1]), jpeg, settings);
                ArtworkResources.Dispose();
                return;
            }
            Application.Run(new Form1());
            ArtworkResources.Dispose();
        }
    }
}
