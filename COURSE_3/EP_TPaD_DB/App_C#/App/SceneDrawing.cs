using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace LP_PopovD_App
{
    internal static class SceneDrawing
    {
        internal static Bitmap Render(Size size, Image ice, bool menuOpened)
        {
            // Небольшие плитки ограничивают временные буферы GDI+ при экспорте в 16K.
            Bitmap result = new(size.Width, size.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            try
            {
                using Graphics output = Graphics.FromImage(result);
                const int tileSize = 1536;
                for (int y = 0; y < size.Height; y += tileSize)
                    for (int x = 0; x < size.Width; x += tileSize)
                    {
                        using Bitmap tile = new(Math.Min(tileSize, size.Width - x),
                            Math.Min(tileSize, size.Height - y), System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                        using (Graphics g = Graphics.FromImage(tile))
                        {
                            g.TranslateTransform(-x, -y);
                            Draw(g, size, ice, menuOpened, includeButtons: true);
                        }
                        output.DrawImageUnscaled(tile, x, y);
                    }
                return result;
            }
            catch
            {
                result.Dispose();
                throw;
            }
        }

        internal static void Draw(Graphics g, Size size, Image? ice, bool menuOpened, bool includeButtons = false)
        {
            GraphicsState state = g.Save();
            g.Clear(Color.FromArgb(225, 241, 250));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            if (ice != null)
            {
                // Средние тона приподняты: новая синяя фактура темнее прежней, но не скрывает эмблемы.
                using ImageAttributes palette = new();
                palette.SetGamma(0.55f, ColorAdjustType.Bitmap);
                g.DrawImage(ice, new Rectangle(Point.Empty, size),
                    0, 0, ice.Width, ice.Height, GraphicsUnit.Pixel, palette);
            }
            RectangleF canvas = RinkDrawing.GetCanvasBounds(size);
            g.TranslateTransform(canvas.X, canvas.Y);
            g.ScaleTransform(canvas.Width / RinkDrawing.Width, canvas.Height / RinkDrawing.Height);
            if (menuOpened)
                RinkDrawing.Draw(g);
            else
                EntryDrawing.Draw(g);
            if (includeButtons)
            {
                if (menuOpened)
                {
                    string[] captions = { "ЗАКАЗАТЬ\nАРЕНДУ", "ПРОФИЛЬ", "ИНФОРМАЦИЯ", "РАСПИСАНИЕ\nЗАНЯТИЙ" };
                    for (int i = 0; i < captions.Length; i++)
                    {
                        PointF c = RinkDrawing.MenuCenters[i];
                        float d = RinkDrawing.ButtonDiameter;
                        RinkTypography.Draw(g, captions[i], new RectangleF(c.X - d / 2, c.Y - d / 2, d, d), RinkDrawing.Blue);
                    }
                }
                else
                {
                    for (int i = 0; i < EntryDrawing.Captions.Length; i++)
                    {
                        PointF c = EntryDrawing.PuckCenters[i];
                        float d = EntryDrawing.PuckDiameter;
                        EntryDrawing.DrawPuck(g, Rectangle.Round(new RectangleF(c.X - d / 2, c.Y - d / 2, d, d)),
                            EntryDrawing.Captions[i], false, false);
                    }
                }
            }
            g.Restore(state);
        }
    }
}
