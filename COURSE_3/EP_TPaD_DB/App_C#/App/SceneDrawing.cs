using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace LP_PopovD_App
{
    internal static class SceneDrawing
    {
        internal static Bitmap Render(Size size, AppScreen screen)
        {
            Bitmap result = new(size.Width, size.Height, PixelFormat.Format24bppRgb);
            result.SetResolution(96, 96);
            try
            {
                using Graphics output = Graphics.FromImage(result);
                output.InterpolationMode = InterpolationMode.NearestNeighbor;
                output.PixelOffsetMode = PixelOffsetMode.None;
                // Небольшие плитки ограничивают временные буферы при большом экспорте.
                const int tileSize = 1536;
                for (int y = 0; y < size.Height; y += tileSize)
                    for (int x = 0; x < size.Width; x += tileSize)
                    {
                        using Bitmap tile = new(Math.Min(tileSize, size.Width - x),
                            Math.Min(tileSize, size.Height - y), PixelFormat.Format24bppRgb);
                        tile.SetResolution(96, 96);
                        using (Graphics g = Graphics.FromImage(tile))
                        {
                            g.TranslateTransform(-x, -y);
                            Draw(g, size, screen, includeControls: true);
                        }
                        output.DrawImage(tile, new Rectangle(x, y, tile.Width, tile.Height),
                            0, 0, tile.Width, tile.Height, GraphicsUnit.Pixel);
                    }
                return result;
            }
            catch { result.Dispose(); throw; }
        }

        internal static void Draw(Graphics g, Size size, AppScreen screen, bool includeControls = false, string status = "")
        {
            GraphicsState saved = g.Save();
            g.Clear(Color.FromArgb(137, 177, 202));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            NativeIceDrawing.Draw(g, size);
            RectangleF canvas = RinkDrawing.GetCanvasBounds(size);
            g.TranslateTransform(canvas.X, canvas.Y);
            g.ScaleTransform(canvas.Width / RinkDrawing.Width, canvas.Height / RinkDrawing.Height);
            switch (screen)
            {
                case AppScreen.Entry:
                    EntryDrawing.Draw(g);
                    if (includeControls)
                        for (int i = 0; i < EntryDrawing.Captions.Length; i++)
                        {
                            PointF c = EntryDrawing.PuckCenters[i];
                            EntryDrawing.DrawPuck(g, Rectangle.Round(new RectangleF(c.X - 165, c.Y - 165, 330, 330)),
                                EntryDrawing.Captions[i], false, false);
                        }
                    break;
                case AppScreen.Login:
                case AppScreen.Registration:
                    JerseyDrawing.Draw(g, screen == AppScreen.Registration, includeControls, status);
                    break;
                case AppScreen.Menu:
                    RinkDrawing.Draw(g);
                    if (includeControls)
                    {
                        string[] labels = { "ЗАКАЗАТЬ\nАРЕНДУ", "ПРОФИЛЬ", "ИНФОРМАЦИЯ", "РАСПИСАНИЕ\nЗАНЯТИЙ" };
                        for (int i = 0; i < labels.Length; i++)
                        {
                            PointF c = RinkDrawing.MenuCenters[i];
                            float d = RinkDrawing.ButtonDiameter;
                            RinkTypography.Draw(g, labels[i], new RectangleF(c.X - d / 2, c.Y - d / 2, d, d), RinkDrawing.Blue);
                        }
                        JerseyDrawing.DrawCapsule(g, JerseyDrawing.BackBounds, "НАЗАД", false);
                    }
                    break;
            }
            g.Restore(saved);
        }
    }
}
