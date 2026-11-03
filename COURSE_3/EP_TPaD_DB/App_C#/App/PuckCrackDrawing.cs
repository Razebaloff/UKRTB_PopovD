using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace LP_PopovD_App
{
    // Ледяные трещины появляются на поверхности арены вокруг шайбы,
    // за пределами её круглого WinForms Region. Поэтому рисовать их
    // необходимо на родительской форме, под самой шайбой.
    internal static class PuckCrackDrawing
    {
        internal static int Side(Size puck)
            => Math.Max(1, (int)MathF.Ceiling(Math.Max(puck.Width, puck.Height) * 1.55f));

        internal static Rectangle Bounds(Rectangle puck)
        {
            int side = Side(puck.Size);
            return new Rectangle(puck.Left + (puck.Width - side) / 2,
                puck.Top + (puck.Height - side) / 2, side, side);
        }

        internal static Bitmap Create(Size puck, int seed)
        {
            int side = Side(puck);
            Bitmap result = new(side, side, PixelFormat.Format32bppPArgb);
            result.SetResolution(96, 96);
            if (puck.Width < 12 || puck.Height < 12) return result;

            using Graphics g = Graphics.FromImage(result);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            Random random = new(seed);
            float center = side / 2f;
            float rPuck = Math.Min(puck.Width, puck.Height) * .5f;

            using Pen shadow = new(Color.FromArgb(155, 16, 58, 83), 2.1f)
            { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            using Pen glint = new(Color.FromArgb(193, 235, 250, 255), .9f)
            { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            using Pen feather = new(Color.FromArgb(42, 196, 240, 255), 6f)
            { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            using SolidBrush chip = new(Color.FromArgb(112, 241, 252, 255));

            int rays = 15 + random.Next(7);
            for (int i = 0; i < rays; i++)
            {
                float a = (i + (random.NextSingle() - .5f) * .48f) * MathF.Tau / rays;
                float r = rPuck * (.93f + random.NextSingle() * .06f);
                float outer = rPuck * (1.18f + random.NextSingle() * .27f);
                int sections = 3 + random.Next(3);
                PointF[] nodes = new PointF[sections + 1];

                for (int j = 0; j <= sections; j++)
                {
                    float t = j / (float)sections;
                    float rr = r + (outer - r) * t;
                    float offset = j == 0 ? 0 : (random.NextSingle() - .5f) *
                        (.055f + .024f * t);
                    float angle = a + offset;
                    nodes[j] = new PointF(center + MathF.Cos(angle) * rr,
                        center + MathF.Sin(angle) * rr);
                }

                shadow.Width = 1.0f + random.NextSingle() * 1.5f;
                glint.Width = Math.Max(.5f, shadow.Width * .55f);
                g.DrawLines(feather, nodes);
                g.DrawLines(shadow, nodes);
                PointF[] shifted = new PointF[nodes.Length];
                for (int j = 0; j < nodes.Length; j++)
                    shifted[j] = new PointF(nodes[j].X + .9f, nodes[j].Y - 1f);
                g.DrawLines(glint, shifted);

                // Редкие разветвления с разной длиной, неровными углами
                // и отдельными сколами. Линии не образуют геометрическую сетку.
                if (i % 3 != 1)
                {
                    int source = 1 + random.Next(sections - 1);
                    float sideAngle = a + (random.Next(2) == 0 ? -1 : 1) *
                        (.34f + random.NextSingle() * .48f);
                    float branch = rPuck * (.035f + random.NextSingle() * .12f);
                    PointF origin = nodes[source];
                    PointF middle = new(origin.X + MathF.Cos(sideAngle) * branch * .48f,
                        origin.Y + MathF.Sin(sideAngle) * branch * .48f);
                    PointF end = new(origin.X + MathF.Cos(sideAngle + .07f) * branch,
                        origin.Y + MathF.Sin(sideAngle - .09f) * branch);
                    shadow.Width *= .58f;
                    glint.Width *= .65f;
                    g.DrawLines(shadow, new[] { origin, middle, end });
                    g.DrawLine(glint, origin.X + .7f, origin.Y - .7f,
                        end.X + .7f, end.Y - .7f);
                }
                if (i % 4 == 0)
                {
                    PointF p = nodes[^1];
                    float size = 1.1f + random.NextSingle() * 2.8f;
                    g.FillEllipse(chip, p.X - size, p.Y - size, size * 2, size * 2);
                }
            }
            return result;
        }

        internal static void Draw(Graphics g, Bitmap cracks, Rectangle puck, float amount)
        {
            if (amount <= .002f || puck.Width < 12 || puck.Height < 12) return;
            float p = SnowDrawing.Smooth(Math.Clamp(amount, 0, 1));
            Rectangle dest = Bounds(puck);
            float cx = puck.Left + puck.Width / 2f;
            float cy = puck.Top + puck.Height / 2f;
            float puckRadius = Math.Min(puck.Width, puck.Height) * .5f;

            GraphicsState saved = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            // Наружная граница растёт по радиусу — трещины визуально
            // распространяются от края шайбы в окружающий лёд.
            using GraphicsPath reveal = new() { FillMode = FillMode.Alternate };
            float outer = puckRadius * (.99f + .46f * p);
            reveal.AddEllipse(cx - outer, cy - outer, outer * 2, outer * 2);
            float inner = puckRadius * .94f;
            reveal.AddEllipse(cx - inner, cy - inner, inner * 2, inner * 2);
            g.SetClip(reveal, CombineMode.Intersect);
            using ImageAttributes alpha = new();
            alpha.SetColorMatrix(new ColorMatrix { Matrix33 = p });
            g.InterpolationMode = InterpolationMode.Bilinear;
            g.DrawImage(cracks, dest, 0, 0, cracks.Width, cracks.Height,
                GraphicsUnit.Pixel, alpha);
            g.Restore(saved);
        }
    }
}
