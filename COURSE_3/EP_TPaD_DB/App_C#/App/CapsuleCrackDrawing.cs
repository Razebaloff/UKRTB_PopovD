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
            int margin = Math.Max(12, (int)(capsule.Height * .65f));
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

            using Pen shadow = new(Color.FromArgb(163, 33, 87, 111), 1.6f)
                { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using Pen highlight = new(Color.FromArgb(205, 237, 250, 255), .85f)
                { StartCap = LineCap.Round, EndCap = LineCap.Round };
            for (int i = 0; i < 25; i++)
            {
                // Семена трещин распределены и по прямым длинным краям,
                // и по закруглённым концам капсулы.
                float t = random.NextSingle();
                float x, y, nx, ny;
                if (i % 3 == 0)
                {
                    bool left = random.Next(2) == 0;
                    float a = -MathF.PI / 2 + random.NextSingle() * MathF.PI;
                    nx = (left ? -1 : 1) * MathF.Cos(a);
                    ny = MathF.Sin(a);
                    x = offset + (left ? rad : rad + longPart) + nx * rad;
                    y = cy + ny * rad;
                }
                else
                {
                    bool top = random.Next(2) == 0;
                    x = offset + rad + t * longPart;
                    y = cy + (top ? -rad : rad);
                    nx = (random.NextSingle() - .5f) * .30f;
                    ny = top ? -1 : 1;
                }
                float tangentX = -ny, tangentY = nx;
                float distance = rad * (.20f + random.NextSingle() * .38f);
                PointF p0 = new(x, y);
                PointF p1 = new(x + nx * distance * .45f + tangentX * distance * .10f,
                    y + ny * distance * .45f + tangentY * distance * .10f);
                PointF p2 = new(x + nx * distance + tangentX * (random.NextSingle() - .5f) * distance * .32f,
                    y + ny * distance + tangentY * (random.NextSingle() - .5f) * distance * .32f);
                shadow.Width = .8f + random.NextSingle() * 1.2f;
                g.DrawLines(shadow, new[] { p0, p1, p2 });
                g.DrawLine(highlight, p0.X + .6f, p0.Y - .5f, p1.X + .6f, p1.Y - .5f);
                if (i % 2 == 0)
                {
                    float direction = random.Next(2) == 0 ? -1 : 1;
                    PointF branch = new(p1.X + tangentX * distance * .35f * direction + nx * distance * .20f,
                        p1.Y + tangentY * distance * .35f * direction + ny * distance * .20f);
                    g.DrawLine(highlight, p1, branch);
                }
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
            float outerPad = capsule.Height * (.06f + .52f * p);
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
