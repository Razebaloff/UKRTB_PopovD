using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace LP_PopovD_App
{
    // Только надпись на ткани. У области ввода нет отдельной поверхности или контура.
    internal static class JerseyFieldDrawing
    {
        internal static readonly Color Ink = Color.FromArgb(235, 241, 245);
        internal static readonly Color FallbackFill = Color.FromArgb(9, 26, 51);

        internal static void DrawCue(Graphics g, RectangleF bounds, string text, RectangleF? viewport = null)
        {
            if (string.IsNullOrEmpty(text)) return;
            RectangleF visible = viewport.HasValue ? RectangleF.Intersect(bounds, viewport.Value) : bounds;
            if (visible.IsEmpty) return;
            using Matrix transform = g.Transform;
            float[] m = transform.Elements;
            float sx = m[0], sy = m[3], dx = m[4], dy = m[5];
            if (sx <= 0 || sy <= 0 || Math.Abs(m[1]) > .001f || Math.Abs(m[2]) > .001f)
                throw new NotSupportedException("Для надписи нужен прямой масштаб без поворота.");
            int left = (int)MathF.Floor(visible.Left * sx + dx), top = (int)MathF.Floor(visible.Top * sy + dy);
            int width = (int)MathF.Ceiling(visible.Right * sx + dx) - left;
            int height = (int)MathF.Ceiling(visible.Bottom * sy + dy) - top;
            if (width < 1 || height < 1) return;

            using FontFamily family = new(RinkTypography.FamilyName);
            using StringFormat centered = new()
            {
                Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap
            };
            using GraphicsPath letters = new();
            letters.AddString(text, family, (int)FontStyle.Bold, 22, bounds, centered);
            if (letters.PointCount == 0) return;
            PointF[] points = letters.PathPoints;
            float centerY = bounds.Top + bounds.Height / 2;
            float centerLight = JerseyFabricDrawing.InkLight(bounds.Left + bounds.Width / 2, centerY);
            for (int i = 0; i < points.Length; i++)
            {
                points[i].Y += Math.Clamp((JerseyFabricDrawing.InkLight(points[i].X, centerY) - centerLight) * 2, -.5f, .5f);
                // Масштабируем координаты один раз: смена полосы не меняет сглаживание контура.
                points[i].X = (float)((double)points[i].X * sx + dx - left);
                points[i].Y = (float)((double)points[i].Y * sy + dy - top);
            }
            using GraphicsPath conformed = new(points, letters.PathTypes, letters.FillMode);

            // Маска ограничена видимой полосой, в том числе при экспорте 32K.
            using Bitmap print = new(width, height, PixelFormat.Format32bppPArgb);
            print.SetResolution(96, 96);
            using (Graphics mask = Graphics.FromImage(print))
            {
                mask.SmoothingMode = SmoothingMode.AntiAlias;
                mask.PixelOffsetMode = PixelOffsetMode.HighQuality;
                mask.FillPath(Brushes.White, conformed);
            }
            BitmapData data = print.LockBits(new Rectangle(Point.Empty, print.Size),
                ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb);
            try
            {
                int[] row = new int[width];
                for (int y = 0; y < height; y++)
                {
                    IntPtr address = IntPtr.Add(data.Scan0, y * data.Stride);
                    Marshal.Copy(address, row, 0, width);
                    float wy = (top + y + .5f - dy) / sy;
                    for (int x = 0; x < width; x++)
                    {
                        int alpha = (int)((uint)row[x] >> 24);
                        if (alpha == 0) continue;
                        float wx = (left + x + .5f - dx) / sx;
                        float light = JerseyFabricDrawing.InkLight(wx, wy);
                        int r = (int)Math.Clamp(Ink.R * light, 0, 255) * alpha / 255;
                        int green = (int)Math.Clamp(Ink.G * light, 0, 255) * alpha / 255;
                        int b = (int)Math.Clamp(Ink.B * light, 0, 255) * alpha / 255;
                        row[x] = alpha << 24 | r << 16 | green << 8 | b;
                    }
                    Marshal.Copy(row, 0, address, width);
                }
            }
            finally { print.UnlockBits(data); }
            GraphicsState state = g.Save();
            g.ResetTransform();
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.None;
            g.DrawImageUnscaled(print, left, top);
            g.Restore(state);
        }
    }
}
