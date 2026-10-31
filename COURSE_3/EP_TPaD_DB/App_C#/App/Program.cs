using System.Drawing.Imaging;

namespace LP_PopovD_App
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();
            Dictionary<string, AppScreen> exports = new()
            {
                ["--export-entry"] = AppScreen.Entry, ["--export-menu"] = AppScreen.Menu,
                ["--export-login"] = AppScreen.Login, ["--export-registration"] = AppScreen.Registration
            };
            try
            {
                if (args.Length >= 2 && exports.TryGetValue(args[0], out AppScreen screen))
                {
                    int width = args.Length >= 3 ? int.Parse(args[2]) : 1920;
                    if (width < 960 || width > 15360)
                        throw new ArgumentOutOfRangeException(nameof(width), "Ширина: от 960 до 15360 пикселей.");
                    using Bitmap artwork = SceneDrawing.Render(new Size(width, width * 9 / 16), screen);
                    using FileStream output = File.Create(Path.GetFullPath(args[1]));
                    if (string.Equals(Path.GetExtension(args[1]), ".png", StringComparison.OrdinalIgnoreCase))
                        artwork.Save(output, ImageFormat.Png);
                    else
                    {
                        ImageCodecInfo jpeg = ImageCodecInfo.GetImageEncoders().First(c => c.MimeType == "image/jpeg");
                        using EncoderParameters settings = new(1);
                        settings.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 100L);
                        artwork.Save(output, jpeg, settings);
                    }
                    return;
                }
                Application.Run(new Form1());
            }
            finally
            {
                ArtworkResources.Dispose();
                NativeIceDrawing.Dispose();
            }
        }
    }
}
