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
            => Math.Max(1, (int)MathF.Ceiling(Math.Max(puck.Width, puck.Height) * 1.65f));

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

            float scale = Math.Max(.7f, Math.Min(puck.Width, puck.Height) / 330f);
            using Pen shadow = new(Color.FromArgb(226, 13, 48, 72), 2)
                { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            using Pen glint = new(Color.FromArgb(239, 244, 253, 255), 1)
                { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            using Pen diffuse = new(Color.FromArgb(38, 141, 207, 234), 5)
                { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using SolidBrush chip = new(Color.FromArgb(145, 232, 247, 252));
            int rays = 18 + random.Next(8);
            for (int i = 0; i < rays; i++)
            {
                if (i % 7 == 2) continue;
                float angle = (i + random.NextSingle() * .85f) * MathF.Tau / rays;
                float start = rPuck * (.97f + random.NextSingle() * .025f);
                float end = rPuck * (1.23f + random.NextSingle() * .30f);
                int count = 5 + random.Next(3);
                PointF[] nodes = new PointF[count + 1];
                for (int j = 0; j <= count; j++)
                {
                    float t = j / (float)count;
                    float radius = start + (end - start) * t;
                    float bend = j == 0 ? 0 : (random.NextSingle() - .5f) * .10f;
                    nodes[j] = new(center + MathF.Cos(angle + bend) * radius,
                        center + MathF.Sin(angle + bend) * radius);
                }
                float width = (2.2f + random.NextSingle() * 1.5f) * scale;
                for (int j = 0; j < count; j++)
                {
                    shadow.Width = Math.Max(.55f * scale, width * (1 - .78f * j / count));
                    glint.Width = Math.Max(.45f * scale, shadow.Width * .43f);
                    diffuse.Width = shadow.Width * 2.1f;
                    g.DrawLine(diffuse, nodes[j], nodes[j + 1]);
                    g.DrawLine(shadow, nodes[j], nodes[j + 1]);
                    g.DrawLine(glint, nodes[j].X + .8f * scale, nodes[j].Y - .8f * scale,
                        nodes[j + 1].X + .8f * scale, nodes[j + 1].Y - .8f * scale);
                }
                for (int branch = 0; branch < 1 + i % 3; branch++)
                {
                    PointF origin = nodes[1 + random.Next(count - 1)];
                    float direction = angle + (random.Next(2) == 0 ? -1 : 1) * (.35f + random.NextSingle() * .75f);
                    float length = rPuck * (.04f + random.NextSingle() * .11f);
                    PointF middle = new(origin.X + MathF.Cos(direction) * length * .54f,
                        origin.Y + MathF.Sin(direction) * length * .54f);
                    PointF tip = new(origin.X + MathF.Cos(direction + .12f) * length,
                        origin.Y + MathF.Sin(direction + .12f) * length);
                    shadow.Width = (1.1f + random.NextSingle() * .6f) * scale;
                    glint.Width = .55f * scale;
                    g.DrawLines(shadow, new[] { origin, middle, tip });
                    g.DrawLine(glint, middle.X + .6f * scale, middle.Y - .6f * scale,
                        tip.X + .6f * scale, tip.Y - .6f * scale);
                    if (branch == 1)
                        g.DrawLine(glint, middle, new PointF(middle.X + MathF.Cos(direction - .6f) * length * .35f,
                            middle.Y + MathF.Sin(direction - .6f) * length * .35f));
                }
                if (i % 4 == 0)
                {
                    PointF point = nodes[^2];
                    float shard = (1.4f + random.NextSingle() * 2) * scale;
                    g.FillPolygon(chip, new[] { new PointF(point.X - shard, point.Y),
                        new PointF(point.X + shard * .7f, point.Y - shard * .55f),
                        new PointF(point.X + shard * .3f, point.Y + shard) });
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
            float outer = puckRadius * (.99f + .60f * p);
            reveal.AddEllipse(cx - outer, cy - outer, outer * 2, outer * 2);
            float inner = puckRadius * .98f;
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
