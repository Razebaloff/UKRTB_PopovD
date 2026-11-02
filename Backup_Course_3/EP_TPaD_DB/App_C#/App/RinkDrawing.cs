using System.Drawing.Drawing2D;

namespace LP_PopovD_App
{
    internal static class RinkDrawing
    {
        internal const float Width = 1600;
        internal const float Height = 800;
        internal const float FaceoffRadius = 120;
        internal const float ButtonDiameter = 232;
        internal static readonly Color Blue = Color.FromArgb(19, 65, 118);
        private static readonly Color Red = Color.FromArgb(220, 185, 37, 54);

        internal static readonly PointF[] MenuCenters =
        {
            new(350, 230), new(350, 570), new(1250, 230), new(1250, 570)
        };

        internal static RectangleF GetCanvasBounds(Size clientSize)
        {
            float horizontal = Math.Max(clientSize.Width * 0.055f, 20);
            float vertical = Math.Max(clientSize.Height * 0.065f, 20);
            float scale = Math.Min(Math.Max(1, clientSize.Width - horizontal * 2) / Width,
                Math.Max(1, clientSize.Height - vertical * 2) / Height);
            float width = Width * scale;
            float height = Height * scale;
            return new RectangleF((clientSize.Width - width) / 2,
                (clientSize.Height - height) / 2, width, height);
        }

        private static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
        {
            float diameter = radius * 2;
            GraphicsPath path = new();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        internal static void Draw(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using Pen red = new(Red, 4.2f);
            using Pen blueLine = new(Color.FromArgb(215, 30, 105, 177), 12);
            using SolidBrush ice = new(Color.FromArgb(38, 225, 243, 252));
            using GraphicsPath rink = RoundedRectangle(new RectangleF(28, 28, 1544, 744), 128);
            g.FillPath(ice, rink);
            // Следы лезвий и снежная крошка уже входят в фактуру фонового изображения.

            g.DrawLine(blueLine, 600, 28, 600, 772);
            g.DrawLine(blueLine, 1000, 28, 1000, 772);
            using (Pen centerLine = new(Red, 8) { DashStyle = DashStyle.Solid })
            {
                // Линия доходит до внешнего края синей окружности и не входит в круг.
                float centerX = Width / 2;
                float centerY = Height / 2;
                float gap = FaceoffRadius + red.Width / 2;
                g.DrawLine(centerLine, centerX, 28, centerX, centerY - gap);
                g.DrawLine(centerLine, centerX, centerY + gap, centerX, 772);
            }

            using (SolidBrush crease = new(Color.FromArgb(135, 166, 208, 239)))
            {
                g.FillPie(crease, 102, 352, 96, 96, 270, 180);
                g.DrawArc(red, 102, 352, 96, 96, 270, 180);
                g.FillPie(crease, 1402, 352, 96, 96, 90, 180);
                g.DrawArc(red, 1402, 352, 96, 96, 90, 180);
            }
            g.DrawLine(red, 150, 28, 150, 772);
            g.DrawLine(red, 1450, 28, 1450, 772);
            DrawGoalkeeperTrapezoid(g, red, true);
            DrawGoalkeeperTrapezoid(g, red, false);
            DrawGoal(g, true);
            DrawGoal(g, false);

            foreach (PointF center in MenuCenters)
            {
                g.DrawEllipse(red, center.X - FaceoffRadius, center.Y - FaceoffRadius,
                    FaceoffRadius * 2, FaceoffRadius * 2);
                foreach (int side in new[] { -1, 1 })
                {
                    foreach (int offset in new[] { -24, 24 })
                    {
                        // Начало точно на окружности: штрих примыкает к ней без зазора.
                        float start = MathF.Sqrt(FaceoffRadius * FaceoffRadius - offset * offset);
                        float end = start + 20;
                        g.DrawLine(red, center.X + offset, center.Y + side * start,
                            center.X + offset, center.Y + side * end);
                        g.DrawLine(red, center.X + side * start, center.Y + offset,
                            center.X + side * end, center.Y + offset);
                    }
                }
            }

            using SolidBrush dot = new(Red);
            foreach (float x in new[] { 650f, 950f })
                foreach (float y in new[] { 230f, 570f })
                    g.FillEllipse(dot, x - 5, y - 5, 10, 10);

            // Внутри центрального круга остаются лёд и надпись, без красной линии.
            using Pen centerCircle = new(Blue, red.Width);
            g.DrawEllipse(centerCircle, 680, 280, 240, 240);
            RinkTypography.Draw(g, "ТЕРРИТОРИЯ\nХОККЕЯ",
                new RectangleF(684, 284, 232, 232), Blue);

            DrawIceWear(g);
            using Pen frostEdge = new(Color.FromArgb(150, 244, 253, 255), 7);
            using Pen board = new(Color.FromArgb(135, 42, 95, 132), 2.5f);
            g.DrawPath(frostEdge, rink);
            g.DrawPath(board, rink);
        }

        private static bool InsideRink(float x, float y)
        {
            if (x < 28 || x > 1572 || y < 28 || y > 772)
                return false;
            float dx = x - Math.Clamp(x, 156, 1444);
            float dy = y - Math.Clamp(y, 156, 644);
            return dx * dx + dy * dy <= 128 * 128;
        }

        // Тонкие прорези поверх краски создают впечатление разметки под слоем льда.
        private static void DrawIceWear(Graphics g)
        {
            Random random = new(81021);
            using Pen scratch = new(Color.FromArgb(42, 239, 250, 255), 0.65f);
            for (int i = 0; i < 1100; i++)
            {
                float x = 30 + random.NextSingle() * 1540;
                float y = 30 + random.NextSingle() * 740;
                float length = 3 + random.NextSingle() * 23;
                if (InsideRink(x, y) && InsideRink(x + length, y - length * 0.38f))
                    g.DrawLine(scratch, x, y, x + length, y - length * 0.38f);
            }
            using Pen skate = new(Color.FromArgb(26, 245, 253, 255), 0.9f);
            for (int i = 0; i < 38; i++)
                g.DrawArc(skate, random.Next(160, 1120), random.Next(160, 460),
                    random.Next(90, 310), random.Next(60, 180), random.Next(360), random.Next(30, 100));
        }

        private static void DrawGoalkeeperTrapezoid(Graphics g, Pen red, bool left)
        {
            // IIHF 2026/27, 1.8: 6,80 м на линии ворот и 8,60 м у заднего борта.
            float unitsPerMetre = FaceoffRadius / 4.5f;
            float near = 6.80f / 2 * unitsPerMetre;
            float far = 8.60f / 2 * unitsPerMetre;
            float goalX = left ? 150 : 1450;
            float boardX = left ? 28 : Width - 28;
            foreach (int side in new[] { -1, 1 })
                g.DrawLine(red, goalX, Height / 2 + side * near,
                    boardX, Height / 2 + side * far);
        }

        private static void DrawGoal(Graphics g, bool left)
        {
            GraphicsState state = g.Save();
            if (!left)
            {
                g.TranslateTransform(Width, 0);
                g.ScaleTransform(-1, 1);
            }

            // Вид сверху: открытая сторона на линии ворот, округлая сетка позади.
            using GraphicsPath net = new();
            net.AddLine(150, 375, 132, 378);
            net.AddBezier(132, 378, 118, 380, 118, 388, 118, 400);
            net.AddBezier(118, 400, 118, 412, 118, 420, 132, 422);
            net.AddLine(132, 422, 150, 425);
            net.CloseFigure();

            using GraphicsPath shadow = (GraphicsPath)net.Clone();
            using (Matrix offset = new())
            {
                offset.Translate(2, 3);
                shadow.Transform(offset);
            }
            using SolidBrush shade = new(Color.FromArgb(45, 21, 59, 87));
            using SolidBrush netFloor = new(Color.FromArgb(195, 249, 253, 255));
            g.FillPath(shade, shadow);
            g.FillPath(netFloor, net);

            GraphicsState meshClip = g.Save();
            g.SetClip(net, CombineMode.Intersect);
            using Pen mesh = new(Color.FromArgb(150, 117, 149, 167), 0.65f);
            for (int x = 65; x < 180; x += 5)
            {
                g.DrawLine(mesh, x, 374, x + 52, 426);
                g.DrawLine(mesh, x, 426, x + 52, 374);
            }
            g.Restore(meshClip);

            using Pen frameShadow = new(Color.FromArgb(137, 24, 42), 4.2f) { LineJoin = LineJoin.Round };
            using Pen frame = new(Color.FromArgb(225, 52, 64), 2.6f) { LineJoin = LineJoin.Round };
            using Pen highlight = new(Color.FromArgb(230, 255, 166, 166), 0.85f);
            g.DrawPath(frameShadow, net);
            g.DrawPath(frame, net);
            g.DrawLine(highlight, 149.5f, 377, 149.5f, 423);
            g.Restore(state);
        }
    }
}
