using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace LP_PopovD_App
{
    internal static class EntryDrawing
    {
        internal const float PuckDiameter = 330;
        internal static readonly PointF[] PuckCenters = { new(400, 350), new(1200, 350), new(800, 605) };
        internal static readonly string[] Captions = { "ВОЙТИ", "ЗАРЕГИСТРИРОВАТЬСЯ", "РАСПИСАНИЕ" };

        internal static void Draw(Graphics g, RectangleF? viewport = null, bool includeSticks = true)
        {
            HeaderDrawing.Draw(g, viewport, includeSticks);
            foreach (PointF center in PuckCenters)
            {
                if (viewport.HasValue && !viewport.Value.IntersectsWith(new RectangleF(center.X - 190, center.Y - 190, 380, 400))) continue;
                for (int i = 5; i >= 0; i--)
                {
                    float spread = 3 + i * 4;
                    using SolidBrush shadow = new(Color.FromArgb(9, 10, 28, 44));
                    g.FillEllipse(shadow, center.X - 165 - spread, center.Y - 165 + 11 - spread / 2,
                        330 + spread * 2, 330 + spread);
                }
            }
        }

        private static PointF Polar(float angle, float radius)
            => new(165 + MathF.Cos(angle) * radius, 165 + MathF.Sin(angle) * radius);

        internal static void DrawPuck(Graphics g, Rectangle bounds, string text, bool highlighted, bool pressed)
        {
            GraphicsState saved = g.Save();
            float diameter = Math.Min(bounds.Width, bounds.Height);
            g.TranslateTransform(bounds.X + (bounds.Width - diameter) / 2, bounds.Y + (bounds.Height - diameter) / 2);
            g.ScaleTransform(diameter / 330, diameter / 330);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            int index = Array.IndexOf(Captions, text);
            if (index < 0) index = 0;
            RectangleF body = new(3, 3, 324, 324);
            using GraphicsPath outline = new();
            outline.AddEllipse(body);
            using Region silhouette = new(outline);
            Random random = new(9214 + index * 787);
            // Малые сколы распределены по всей окружности, а не по одной стороне.
            for (int i = 0; i < 12; i++)
            {
                float angle = (i + 0.2f + random.NextSingle() * 0.5f) * MathF.Tau / 12;
                using GraphicsPath chip = new();
                chip.AddPolygon(new[] { Polar(angle - 0.009f, 164),
                    Polar(angle, 159.2f + random.NextSingle() * 1.3f), Polar(angle + 0.012f, 164) });
                silhouette.Exclude(chip);
            }
            g.SetClip(silhouette, CombineMode.Intersect);
            using LinearGradientBrush rim = new(body, Color.FromArgb(69, 74, 79), Color.FromArgb(7, 9, 11), 62);
            g.FillEllipse(rim, body);
            GraphicsState faceState = g.Save();
            using GraphicsPath face = new();
            face.AddEllipse(7, 7, 316, 316);
            g.SetClip(face, CombineMode.Intersect);
            // Фотографическая фактура содержит поры, потёртости и надрезы по всей площади.
            // Повороты дают каждой шайбе свой рисунок, не меняя направление общего света.
            g.TranslateTransform(165, 165);
            g.RotateTransform(index * 120);
            Image material = ArtworkResources.Get("PuckSurface");
            float side = Math.Min(material.Width, material.Height);
            using ImageAttributes finish = new();
            finish.SetGamma(highlighted ? 1.02f : 1.15f);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(material, new Rectangle(-158, -158, 316, 316),
                (material.Width - side) / 2, (material.Height - side) / 2, side, side, GraphicsUnit.Pixel, finish);
            g.Restore(faceState);
            using LinearGradientBrush light = new(new RectangleF(7, 7, 316, 316),
                Color.FromArgb(8, 207, 218, 225), Color.FromArgb(38, 0, 0, 0), 58);
            g.FillEllipse(light, 7, 7, 316, 316);
            if (pressed)
            {
                using SolidBrush shade = new(Color.FromArgb(35, 0, 0, 0));
                g.FillEllipse(shade, body);
            }
            RinkTypography.DrawPuckCaption(g, text, new RectangleF(10, 10 + (pressed ? 2 : 0), 310, 310));
            g.Restore(saved);
        }
    }
}
