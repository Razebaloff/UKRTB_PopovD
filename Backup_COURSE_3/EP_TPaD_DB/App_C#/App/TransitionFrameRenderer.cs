using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace LP_PopovD_App
{
    // Во время быстрого движения камера работает в ограниченном буфере.
    // Неподвижные экраны и крайние кадры сохраняют исходное разрешение.
    internal sealed class TransitionFrameRenderer : IDisposable
    {
        private readonly Size displaySize, renderSize;
        private readonly Image originalFrom, originalTo;
        private readonly Bitmap from, to, buffer;
        private readonly Graphics graphics;
        private readonly PointF origin;
        private readonly bool revealOnly;
        private readonly Rectangle revealBounds;

        internal TransitionFrameRenderer(Size size, Image before, Image after, PointF origin, bool revealOnly = false,
            Rectangle? revealBounds = null)
        {
            displaySize = size;
            int width = Math.Min(size.Width, size.Width >= 2560 ? 960 : 1280);
            renderSize = new Size(width, Math.Max(1, (int)Math.Round(size.Height * width / (float)size.Width)));
            originalFrom = before; originalTo = after;
            from = Reduced(before, renderSize);
            to = Reduced(after, renderSize);
            buffer = new Bitmap(renderSize.Width, renderSize.Height, PixelFormat.Format32bppPArgb);
            buffer.SetResolution(96, 96);
            graphics = Graphics.FromImage(buffer);
            this.origin = new PointF(origin.X * renderSize.Width / size.Width, origin.Y * renderSize.Height / size.Height);
            this.revealOnly = revealOnly;
            this.revealBounds = Rectangle.Intersect(new Rectangle(Point.Empty, size), revealBounds ?? new Rectangle(Point.Empty, size));
        }

        private static Bitmap Reduced(Image source, Size size)
        {
            Bitmap result = new(size.Width, size.Height, PixelFormat.Format32bppPArgb);
            result.SetResolution(96, 96);
            using Graphics g = Graphics.FromImage(result);
            g.CompositingMode = CompositingMode.SourceCopy;
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.DrawImage(source, new Rectangle(Point.Empty, size), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel);
            return result;
        }

        internal void Draw(Graphics output, float progress)
        {
            GraphicsState saved = output.Save();
            CompositingMode mode = output.CompositingMode;
            output.CompositingMode = CompositingMode.SourceCopy;
            output.InterpolationMode = InterpolationMode.Bilinear;
            output.PixelOffsetMode = PixelOffsetMode.Half;
            Rectangle destination = new(Point.Empty, displaySize);
            if (progress <= 0 || progress >= 1)
            {
                Image image = progress <= 0 ? originalFrom : originalTo;
                if (image.Size == displaySize) output.DrawImageUnscaled(image, 0, 0);
                else output.DrawImage(image, destination, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel);
            }
            else if (revealOnly)
            {
                output.DrawImage(originalFrom, destination, 0, 0, originalFrom.Width, originalFrom.Height, GraphicsUnit.Pixel);
                output.CompositingMode = CompositingMode.SourceOver;
                using ImageAttributes alpha = new();
                alpha.SetColorMatrix(new ColorMatrix { Matrix33 = SnowDrawing.Smooth(progress) });
                Rectangle region = revealBounds;
                output.DrawImage(originalTo, region, region.X * originalTo.Width / (float)displaySize.Width,
                    region.Y * originalTo.Height / (float)displaySize.Height,
                    region.Width * originalTo.Width / (float)displaySize.Width,
                    region.Height * originalTo.Height / (float)displaySize.Height, GraphicsUnit.Pixel, alpha);
            }
            else
            {
                TransitionDrawing.Draw(graphics, renderSize, from, to, origin, progress, revealOnly);
                output.DrawImage(buffer, destination, 0, 0, buffer.Width, buffer.Height, GraphicsUnit.Pixel);
            }
            output.Restore(saved);
            output.CompositingMode = mode;
        }

        public void Dispose()
        {
            graphics.Dispose(); buffer.Dispose(); from.Dispose(); to.Dispose();
        }
    }
}
