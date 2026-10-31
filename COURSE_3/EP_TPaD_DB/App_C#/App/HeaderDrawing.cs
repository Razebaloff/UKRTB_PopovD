using System.Drawing.Drawing2D;

namespace LP_PopovD_App
{
    internal static class HeaderDrawing
    {
        internal static readonly string[] LeftClubs = { "Salavat", "Tolpar", "Agidel", "Astra", "Azamatov" };
        internal static readonly string[] RightClubs = { "Cska", "Ska", "Spartak", "Avangard", "AkBars" };

        internal static void Draw(Graphics g)
        {
            // Эмблемы и флаги лежат на одной горизонтальной оси (Y = 60).
            for (int i = 0; i < LeftClubs.Length; i++)
            {
                DrawClub(g, LeftClubs[i], 112 + i * 110);
                DrawClub(g, RightClubs[i], 1048 + i * 110);
            }
            DrawFlag(g, new RectangleF(632, 28, 106, 64), true);
            DrawFlag(g, new RectangleF(862, 28, 106, 64), false);
            DrawSticks(g);
            using Font title = new(RinkTypography.FamilyName, 42, FontStyle.Bold, GraphicsUnit.Pixel);
            using StringFormat format = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using SolidBrush halo = new(Color.FromArgb(200, 239, 250, 255));
            using SolidBrush ink = new(RinkDrawing.Blue);
            RectangleF bounds = new(120, 112, 1360, 65);
            g.DrawString("ТЕРРИТОРИЯ ХОККЕЯ", title, halo,
                new RectangleF(bounds.X + 1, bounds.Y + 2, bounds.Width, bounds.Height), format);
            g.DrawString("ТЕРРИТОРИЯ ХОККЕЯ", title, ink, bounds, format);
        }

        private static void DrawFlag(Graphics g, RectangleF r, bool bashkortostan)
        {
            Color[] colors = bashkortostan
                ? new[] { Color.FromArgb(0, 96, 174), Color.White, Color.FromArgb(0, 148, 74) }
                : new[] { Color.White, Color.FromArgb(0, 57, 166), Color.FromArgb(213, 43, 30) };
            using SolidBrush shadow = new(Color.FromArgb(32, 11, 42, 64));
            g.FillRectangle(shadow, r.X + 2, r.Y + 3, r.Width, r.Height);
            for (int i = 0; i < 3; i++)
            {
                using SolidBrush stripe = new(colors[i]);
                g.FillRectangle(stripe, r.X, r.Y + r.Height * i / 3, r.Width, r.Height / 3 + 0.2f);
            }
            if (bashkortostan)
            {
                float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
                using Pen gold = new(Color.FromArgb(211, 166, 40), 0.85f);
                using SolidBrush flower = new(gold.Color);
                g.DrawEllipse(gold, cx - 8, cy - 8, 16, 16);
                g.DrawLine(gold, cx, cy + 6, cx, cy - 4);
                for (int i = 0; i < 7; i++)
                {
                    float angle = (205 + i * 21.6667f) * MathF.PI / 180;
                    float x = cx + MathF.Cos(angle) * 5.4f;
                    float y = cy + MathF.Sin(angle) * 5.4f;
                    g.DrawLine(gold, cx, cy + 4, x, y);
                    g.FillEllipse(flower, x - 1.1f, y - 1.1f, 2.2f, 2.2f);
                }
            }
            using LinearGradientBrush satin = new(r, Color.FromArgb(36, 255, 255, 255),
                Color.FromArgb(15, 0, 15, 30), 25f);
            g.FillRectangle(satin, r);
            using Pen border = new(Color.FromArgb(105, 67, 98, 119), 0.8f);
            g.DrawRectangle(border, r.X, r.Y, r.Width, r.Height);
        }

        private static void DrawClub(Graphics g, string name, float centerX)
        {
            // Только сама эмблема: без подложки, круглой маски и пустых полей источника.
            DrawImageFit(g, ArtworkResources.GetEmblem(name), new RectangleF(centerX - 44, 20, 88, 80));
        }

        private static void DrawSticks(Graphics g)
        {
            // Готовая фотография настоящей клюшки. Отражённая пара соприкасается перьями снизу.
            Image stick = ArtworkResources.Get("HockeyStick");
            RectangleF bounds = new(750, 8, 100, 100);
            DrawImageFit(g, stick, bounds);
            GraphicsState state = g.Save();
            g.TranslateTransform(1600, 0);
            g.ScaleTransform(-1, 1);
            DrawImageFit(g, stick, bounds);
            g.Restore(state);
        }

        private static void DrawImageFit(Graphics g, Image image, RectangleF bounds)
        {
            float scale = Math.Min(bounds.Width / image.Width, bounds.Height / image.Height);
            float width = image.Width * scale, height = image.Height * scale;
            RectangleF destination = new(bounds.X + (bounds.Width - width) / 2,
                bounds.Y + (bounds.Height - height) / 2, width, height);
            g.DrawImage(image, destination, new RectangleF(0, 0, image.Width, image.Height), GraphicsUnit.Pixel);
        }
    }
}
