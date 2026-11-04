using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace LP_PopovD_App
{
    // Лёд трескается ВОКРУГ овальной кнопки, не на поверхности кнопки.
    // Рисуется на родительской форме, иначе WinForms обрежет края HWND.
    internal static class CapsuleCrackDrawing
    {
        internal static Rectangle Bounds(Rectangle capsule)
        {
            int margin = Math.Max(12, (int)(capsule.Height * .95f));
            return Rectangle.Inflate(capsule, margin, margin);
        }

        internal static Bitmap Create(Size size, int seed)
        {
            Rectangle original = new(0, 0, size.Width, size.Height);
            Rectangle bounds = Bounds(original);
            Bitmap layer = new(Math.Max(1, bounds.Width), Math.Max(1, bounds.Height),
                PixelFormat.Format32bppPArgb);
            layer.SetResolution(96, 96);
            if (size.Width < 10 || size.Height < 10) return layer;

            using Graphics g = Graphics.FromImage(layer);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Random random = new(seed);
            float offset = -bounds.Left;
            float cy = size.Height / 2f - bounds.Top;
            float rad = size.Height / 2f;
            float longPart = Math.Max(0, size.Width - size.Height);

            float scale = Math.Max(.75f, size.Height / 52f);
            using Pen shadow = new(Color.FromArgb(226, 18, 55, 78), 2)
                { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            using Pen highlight = new(Color.FromArgb(244, 242, 253, 255), .9f)
                { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using Pen soft = new(Color.FromArgb(35, 143, 211, 235), 5)
                { StartCap = LineCap.Round, EndCap = LineCap.Round };
            for (int i = 0; i < 31; i++)
            {
                float x, y, nx, ny;
                if (i % 3 == 0)
                {
                    bool left = random.Next(2) == 0;
                    float angle = -MathF.PI / 2 + random.NextSingle() * MathF.PI;
                    nx = (left ? -1 : 1) * MathF.Cos(angle); ny = MathF.Sin(angle);
                    x = offset + (left ? rad : rad + longPart) + nx * rad;
                    y = cy + ny * rad;
                }
                else
                {
                    bool top = random.Next(2) == 0;
                    x = offset + rad + random.NextSingle() * longPart;
                    y = cy + (top ? -rad : rad);
                    nx = (random.NextSingle() - .5f) * .45f; ny = top ? -1 : 1;
                }
                float tx = -ny, ty = nx, length = rad * (.55f + random.NextSingle() * .88f);
                PointF[] nodes = new PointF[6];
                for (int j = 0; j < nodes.Length; j++)
                {
                    float t = j / 5f, bend = j == 0 ? 0 : (random.NextSingle() - .5f) * length * .29f;
                    nodes[j] = new(x + nx * length * t + tx * bend, y + ny * length * t + ty * bend);
                }
                float width = (1.7f + random.NextSingle() * 1.2f) * scale;
                for (int j = 0; j < nodes.Length - 1; j++)
                {
                    shadow.Width = width * (1 - .15f * j);
                    highlight.Width = Math.Max(.5f * scale, shadow.Width * .42f);
                    soft.Width = shadow.Width * 2;
                    g.DrawLine(soft, nodes[j], nodes[j + 1]);
                    g.DrawLine(shadow, nodes[j], nodes[j + 1]);
                    g.DrawLine(highlight, nodes[j].X + .65f * scale, nodes[j].Y - .7f * scale,
                        nodes[j + 1].X + .65f * scale, nodes[j + 1].Y - .7f * scale);
                }
                PointF origin = nodes[1 + random.Next(3)];
                float side = random.Next(2) == 0 ? -1 : 1;
                PointF end = new(origin.X + nx * length * .26f + tx * length * .35f * side,
                    origin.Y + ny * length * .26f + ty * length * .35f * side);
                shadow.Width = .9f * scale;
                g.DrawLine(shadow, origin, end);
                g.DrawLine(highlight, origin.X + .6f * scale, origin.Y - .6f * scale,
                    end.X + .6f * scale, end.Y - .6f * scale);
            }
            return layer;
        }

        internal static void Draw(Graphics g, Bitmap layer, Rectangle capsule, float amount)
        {
            if (amount < .003f || capsule.Width < 10 || capsule.Height < 10) return;
            float p = SnowDrawing.Smooth(Math.Clamp(amount, 0, 1));
            Rectangle dest = Bounds(capsule);
            GraphicsState original = g.Save();
            // Сначала трещины короткие; затем постепенно расходятся
            // наружу от периметра капсулы.
            using GraphicsPath reveal = new(FillMode.Alternate);
            float outerPad = capsule.Height * (.08f + .78f * p);
            using GraphicsPath outer = JerseyDrawing.Capsule(
                RectangleF.Inflate(capsule, outerPad, outerPad));
            using GraphicsPath inner = JerseyDrawing.Capsule(
                RectangleF.Inflate(capsule, -Math.Max(1, capsule.Height * .04f),
                    -Math.Max(1, capsule.Height * .04f)));
            reveal.AddPath(outer, false);
            reveal.AddPath(inner, false);
            g.SetClip(reveal, CombineMode.Intersect);
            using ImageAttributes alpha = new();
            alpha.SetColorMatrix(new ColorMatrix { Matrix33 = p });
            g.InterpolationMode = InterpolationMode.Bilinear;
            g.DrawImage(layer, dest, 0, 0, layer.Width, layer.Height,
                GraphicsUnit.Pixel, alpha);
            g.Restore(original);
        }
    }
}
