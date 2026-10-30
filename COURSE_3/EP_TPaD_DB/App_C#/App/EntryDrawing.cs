using System.Drawing.Drawing2D;

namespace LP_PopovD_App
{
    internal static class EntryDrawing
    {
        internal const float PuckDiameter = 330;
        internal static readonly PointF[] PuckCenters =
        {
            new(400, 350), new(1200, 350), new(800, 605)
        };
        internal static readonly string[] Captions = { "ВОЙТИ", "ЗАРЕГИСТРИРОВАТЬСЯ", "РАСПИСАНИЕ" };

        internal static void Draw(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            HeaderDrawing.Draw(g);
            foreach (PointF center in PuckCenters)
            {
                for (int i = 5; i >= 0; i--)
                {
                    float spread = 3 + i * 4;
                    using SolidBrush shadow = new(Color.FromArgb(10, 12, 32, 48));
                    g.FillEllipse(shadow, center.X - PuckDiameter / 2 - spread,
                        center.Y - PuckDiameter / 2 + 12 - spread / 2,
                        PuckDiameter + spread * 2, PuckDiameter + spread);
                }
            }
        }

        private static bool InsideFace(float x, float y, float radius = 156)
        {
            float dx = x - 165, dy = y - 165;
            return dx * dx + dy * dy < radius * radius;
        }

        private static PointF Polar(float angle, float radius)
            => new(165 + MathF.Cos(angle) * radius, 165 + MathF.Sin(angle) * radius);

        private static int WearSeed(string text)
        {
            int seed = 1703;
            foreach (char letter in text)
                seed = unchecked(seed * 31 + letter);
            return seed;
        }

        private static Region PuckSilhouette(RectangleF body, Random random)
        {
            using GraphicsPath outline = new();
            outline.AddEllipse(body);
            Region shape = new(outline);
            // Небольшие реальные выемки на кромке. Общий силуэт остаётся круглым.
            for (int i = 0; i < 9; i++)
            {
                float angle = i * MathF.Tau / 9 + random.NextSingle() * 0.13f;
                float width = 0.008f + random.NextSingle() * 0.014f;
                using GraphicsPath chip = new();
                chip.AddPolygon(new[] {
                    Polar(angle - width, 164), Polar(angle, 157.5f + random.NextSingle() * 2),
                    Polar(angle + width, 164) });
                shape.Exclude(chip);
            }
            return shape;
        }

        internal static void DrawPuck(Graphics g, Rectangle bounds, string text, bool highlighted, bool pressed)
        {
            GraphicsState state = g.Save();
            float diameter = Math.Min(bounds.Width, bounds.Height);
            float scale = diameter / 330f;
            g.TranslateTransform(bounds.X + (bounds.Width - diameter) / 2,
                bounds.Y + (bounds.Height - diameter) / 2);
            g.ScaleTransform(scale, scale);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            Random random = new(WearSeed(text));
            RectangleF body = new(3, 3, 324, 324);
            using Region silhouette = PuckSilhouette(body, random);
            g.SetClip(silhouette, CombineMode.Intersect);
            using LinearGradientBrush rim = new(body, Color.FromArgb(72, 78, 84),
                Color.FromArgb(8, 10, 12), 65f);
            g.FillEllipse(rim, body);

            // Матовая рабочая поверхность и тонкая кромка имеют один центр.
            RectangleF face = new(7, 7, 316, 316);
            using LinearGradientBrush rubber = new(face,
                highlighted ? Color.FromArgb(52, 56, 60) : Color.FromArgb(42, 46, 50),
                Color.FromArgb(7, 9, 11), 58f);
            g.FillEllipse(rubber, face);

            // Широкий рассеянный отблеск не образует светлого кольца.
            using GraphicsPath reflection = new();
            reflection.AddEllipse(18, 14, 238, 215);
            using PathGradientBrush matteLight = new(reflection)
            {
                CenterPoint = new PointF(93, 72),
                CenterColor = Color.FromArgb(25, 144, 154, 162),
                SurroundColors = new[] { Color.FromArgb(0, 144, 154, 162) }
            };
            g.FillPath(matteLight, reflection);
            DrawGrain(g, random);
            DrawWear(g, random);
            DrawEdgeWear(g, random);
            if (pressed)
            {
                using SolidBrush shade = new(Color.FromArgb(38, 0, 0, 0));
                g.FillEllipse(shade, face);
            }
            // Единая строка занимает почти всю ширину лица шайбы.
            RinkTypography.Draw(g, text, new RectangleF(10, 10 + (pressed ? 2 : 0), 310, 310),
                Color.White, outline: false);
            g.Restore(state);
        }

        private static void DrawGrain(Graphics g, Random random)
        {
            using SolidBrush light = new(Color.FromArgb(52, 128, 135, 140));
            using SolidBrush dark = new(Color.FromArgb(70, 0, 0, 0));
            for (int i = 0; i < 10500; i++)
            {
                float x = random.NextSingle() * 330, y = random.NextSingle() * 330;
                float dot = 0.22f + random.NextSingle() * 0.95f;
                if (InsideFace(x, y))
                    g.FillEllipse(i % 3 == 0 ? dark : light, x, y, dot, dot * 0.85f);
            }
            using SolidBrush pore = new(Color.FromArgb(95, 2, 3, 4));
            using Pen lip = new(Color.FromArgb(28, 157, 164, 169), 0.3f);
            for (int i = 0; i < 240; i++)
            {
                float x = 12 + random.NextSingle() * 304, y = 12 + random.NextSingle() * 304;
                float size = 0.8f + random.NextSingle() * 1.6f;
                if (InsideFace(x, y, 154))
                {
                    g.FillEllipse(pore, x, y, size, size * 0.7f);
                    g.DrawArc(lip, x, y, size, size * 0.7f, 205, 100);
                }
            }
        }

        private static void DrawWear(Graphics g, Random random)
        {
            using Pen abrasion = new(Color.FromArgb(52, 129, 135, 139), 0.45f)
                { StartCap = LineCap.Round, EndCap = LineCap.Round };
            // Короткие неодинаковые потёртости, сгруппированные в несколько участков.
            for (int patch = 0; patch < 7; patch++)
            {
                PointF center = Polar(random.NextSingle() * MathF.Tau, 72 + random.NextSingle() * 63);
                for (int i = 0; i < 12; i++)
                {
                    float x = center.X + random.NextSingle() * 24 - 12;
                    float y = center.Y + random.NextSingle() * 16 - 8;
                    float length = 2 + random.NextSingle() * 11;
                    if (InsideFace(x, y) && InsideFace(x + length, y - length * 0.22f))
                        g.DrawLine(abrasion, x, y, x + length, y - length * 0.22f);
                }
            }

            // Тёмный канал и светлая грань дают надрезу глубину, а не белую полоску.
            using Pen channel = new(Color.FromArgb(180, 1, 3, 5), 1.15f)
                { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using Pen cutEdge = new(Color.FromArgb(100, 137, 145, 151), 0.45f)
                { StartCap = LineCap.Round, EndCap = LineCap.Round };
            for (int i = 0; i < 19; i++)
            {
                PointF start = Polar(random.NextSingle() * MathF.Tau, 50 + random.NextSingle() * 91);
                float angle = -1.1f + random.NextSingle() * 1.6f;
                float length = (i < 5 ? 27 : 8) + random.NextSingle() * (i < 5 ? 37 : 16);
                PointF end = new(start.X + MathF.Cos(angle) * length,
                    start.Y + MathF.Sin(angle) * length);
                if (!InsideFace(end.X, end.Y, 153))
                    continue;
                using GraphicsPath cut = new();
                cut.AddBezier(start, new PointF(start.X + (end.X - start.X) * 0.35f,
                    start.Y + (end.Y - start.Y) * 0.35f + 0.7f),
                    new PointF(start.X + (end.X - start.X) * 0.7f,
                    start.Y + (end.Y - start.Y) * 0.7f + 0.5f), end);
                g.DrawPath(channel, cut);
                using Matrix offset = new();
                offset.Translate(-0.15f, -0.55f);
                cut.Transform(offset);
                g.DrawPath(cutEdge, cut);
            }
        }

        private static void DrawEdgeWear(Graphics g, Random random)
        {
            using Pen groove = new(Color.FromArgb(110, 2, 4, 6), 0.65f);
            for (int i = 0; i < 72; i++)
            {
                float angle = (12 + i * 2.2f) * MathF.PI / 180;
                g.DrawLine(groove, Polar(angle, 159), Polar(angle, 161.6f));
            }
            using Pen wornRubber = new(Color.FromArgb(88, 126, 134, 140), 0.75f);
            for (int i = 0; i < 34; i++)
            {
                float angle = random.NextSingle() * MathF.Tau;
                float radius = 157.7f + random.NextSingle() * 3;
                g.DrawLine(wornRubber, Polar(angle, radius),
                    Polar(angle + 0.006f + random.NextSingle() * 0.014f, radius - 0.25f));
            }
        }
    }
}
