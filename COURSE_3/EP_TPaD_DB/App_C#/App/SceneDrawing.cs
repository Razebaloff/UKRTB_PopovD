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
                        Rectangle region = new(x, y, Math.Min(tileSize, size.Width - x), Math.Min(tileSize, size.Height - y));
                        using Bitmap ice = NativeIceDrawing.RenderRegion(size, region);
                        using Bitmap tile = RenderBand(size, region, screen, ice);
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
            DrawContents(g, size, screen, includeControls, status);
            g.Restore(saved);
        }

        internal static Bitmap RenderBand(Size size, Rectangle region, AppScreen screen, Bitmap ice)
        {
            if (ice.Size != region.Size) throw new ArgumentException("Размер фона не совпадает с полосой.", nameof(ice));
            Bitmap band = new(region.Width, region.Height, PixelFormat.Format24bppRgb);
            band.SetResolution(96, 96);
            try
            {
                using Graphics g = Graphics.FromImage(band);
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.None;
                g.DrawImage(ice, new Rectangle(Point.Empty, region.Size), 0, 0, ice.Width, ice.Height, GraphicsUnit.Pixel);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.TranslateTransform(-region.X, -region.Y);
                DrawContents(g, size, screen, true, "", region);
                return band;
            }
            catch { band.Dispose(); throw; }
        }

        private static void DrawContents(Graphics g, Size size, AppScreen screen, bool includeControls,
            string status, Rectangle? pixelViewport = null)
        {
            GraphicsState saved = g.Save();
            RectangleF canvas = RinkDrawing.GetCanvasBounds(size);
            float scale = canvas.Width / RinkDrawing.Width;
            RectangleF? viewport = pixelViewport is Rectangle view
                ? new RectangleF((view.X - canvas.X) / scale, (view.Y - canvas.Y) / scale, view.Width / scale, view.Height / scale)
                : null;
            g.TranslateTransform(canvas.X, canvas.Y);
            g.ScaleTransform(canvas.Width / RinkDrawing.Width, canvas.Height / RinkDrawing.Height);
            switch (screen)
            {
                case AppScreen.Entry:
                    EntryDrawing.Draw(g, viewport);
                    if (includeControls)
                        for (int i = 0; i < EntryDrawing.Captions.Length; i++)
                        {
                            PointF c = EntryDrawing.PuckCenters[i];
                            if (viewport.HasValue && !viewport.Value.IntersectsWith(new RectangleF(c.X - 165, c.Y - 165, 330, 330))) continue;
                            EntryDrawing.DrawPuck(g, Rectangle.Round(new RectangleF(c.X - 165, c.Y - 165, 330, 330)),
                                EntryDrawing.Captions[i], false, false);
                        }
                    break;
                case AppScreen.Login:
                case AppScreen.Registration:
                    JerseyDrawing.Draw(g, screen == AppScreen.Registration, includeControls, status, viewport);
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
