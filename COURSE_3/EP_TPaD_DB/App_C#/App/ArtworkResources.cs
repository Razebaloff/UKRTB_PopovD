using System.Drawing.Imaging;

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
            image = new Bitmap(original.Width, original.Height, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(image))
                g.DrawImageUnscaled(original, 0, 0);
            images.Add(name, image);
            return image;
        }

        internal static void Dispose()
        {
            foreach (Bitmap image in images.Values)
                image.Dispose();
            images.Clear();
        }
    }
}
