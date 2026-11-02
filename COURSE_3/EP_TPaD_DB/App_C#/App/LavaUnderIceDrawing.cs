using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace LP_PopovD_App
{
    // Медленная лава под полупрозрачным льдом на каждом экране.
    // Светящиеся русла подготовлены один раз, по кадрам меняется лишь
    // смещение слоя; материалы арены не растеризуются повторно.
    internal static class LavaUnderIceDrawing
    {
        private const int Tile = 512;
        private static Bitmap? lava;

        private static Bitmap MakeTexture()
        {
            Bitmap texture = new(Tile, Tile, PixelFormat.Format32bppPArgb);
            texture.SetResolution(96, 96);
            using Graphics g = Graphics.FromImage(texture);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Random random = new(219742);
            using Pen glow = new(Color.White, 1)
                { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            using Pen vein = new(Color.White, 1)
                { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            using SolidBrush ember = new(Color.White);

            for (int channel = 0; channel < 10; channel++)
            {
                float y0 = 19 + channel * 52f;
                float phase = random.NextSingle() * MathF.Tau;
                float phase2 = random.NextSingle() * MathF.Tau;
                float amplitude = 12 + random.NextSingle() * 13;
                using GraphicsPath line = new();
                PointF[] points = new PointF[65];
                for (int n = 0; n < points.Length; n++)
                {
                    float x = n * 8;
                    float angle = x / Tile * MathF.Tau;
                    float y = y0 + amplitude * MathF.Sin(angle + phase) +
                        7 * MathF.Sin(2 * angle + phase2);
                    points[n] = new PointF(x, y);
                }
                line.AddLines(points);

                glow.Color = Color.FromArgb(15, 179, 31, 12);
                glow.Width = 54;
                g.DrawPath(glow, line);
                glow.Color = Color.FromArgb(28, 245, 66, 20);
                glow.Width = 27;
                g.DrawPath(glow, line);
                vein.Color = Color.FromArgb(57, 255, 106, 34);
                vein.Width = 10;
                g.DrawPath(vein, line);
                vein.Color = Color.FromArgb(78, 255, 187, 67);
                vein.Width = 2.8f;
                g.DrawPath(vein, line);

                // Всполохи расплавленного материала следуют вдоль русла.
                for (int i = 0; i < 15; i++)
                {
                    float x = random.NextSingle() * Tile;
                    float a = x / Tile * MathF.Tau;
                    float y = y0 + amplitude * MathF.Sin(a + phase) +
                        7 * MathF.Sin(2 * a + phase2);
                    float radius = 1.5f + random.NextSingle() * 4;
                    ember.Color = Color.FromArgb(54 + random.Next(60), 255, 183, 65);
                    g.FillEllipse(ember, x - radius, y - radius, radius * 2, radius * 2);
                }
            }
            return texture;
        }

        private static GraphicsPath RinkShape()
        {
            GraphicsPath path = new();
            RectangleF r = new(28, 28, 1544, 744);
            const float d = 256;
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        internal static void Draw(Graphics g, Size size, AppScreen screen, double seconds)
        {
            if (size.Width < 2 || size.Height < 2) return;
            Bitmap sheet = lava ??= MakeTexture();
            GraphicsState saved = g.Save();
            RectangleF canvas = RinkDrawing.GetCanvasBounds(size);
            g.TranslateTransform(canvas.X, canvas.Y);
            g.ScaleTransform(canvas.Width / 1600f, canvas.Height / 800f);
            using GraphicsPath rink = RinkShape();
            g.SetClip(rink, CombineMode.Intersect);
            // Удерживаем лаву физически ПОД майкой, а не поверх полей и текста.
            if (screen is AppScreen.Login or AppScreen.Registration)
                g.ExcludeClip(Rectangle.Round(JerseyFabricDrawing.PaintBounds));
            if (screen == AppScreen.Menu)
                g.ExcludeClip(new Rectangle(660, 275, 280, 250));

            g.InterpolationMode = InterpolationMode.Bilinear;
            g.PixelOffsetMode = PixelOffsetMode.None;
            float drift = (float)((seconds * 26) % Tile);
            float vertical = (float)((seconds * 6.5) % Tile);
            for (int y = -Tile; y < 900; y += Tile)
                for (int x = -Tile; x < 1700; x += Tile)
                    g.DrawImage(sheet, new RectangleF(x + drift, y + vertical, Tile, Tile));

            // Лёд поверх горячих трещин: приглушение света голубоватой
            // матовой плёнкой создаёт ощущение глубины, а не огня на льду.
            using SolidBrush iceVeil = new(Color.FromArgb(23, 226, 243, 252));
            g.FillPath(iceVeil, rink);
            g.Restore(saved);
        }

        internal static void Dispose()
        {
            lava?.Dispose();
            lava = null;
        }
    }
}
