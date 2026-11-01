namespace LP_PopovD_App
{
    internal static class JerseyFieldDrawing
    {
        internal static readonly Color Ink = Color.FromArgb(235, 241, 245);
        internal static readonly Color CueInk = Color.FromArgb(204, 220, 231);
        internal static readonly Color FallbackFill = Color.FromArgb(24, 46, 70);

        private static float Ripple(float x, RectangleF bounds)
            => .55f * MathF.Sin((x - bounds.X) * .047f + bounds.Y * .031f)
                + .25f * MathF.Sin((x - bounds.X) * .113f + bounds.Y * .019f);

        // Плоскость вставки использует свет и складки той же ткани, что и спинка.
        internal static void Shade(float x, float y, float scale, RectangleF bounds, float alpha,
            ref float red, ref float green, ref float blue)
        {
            if (x < bounds.Left - 4 || x > bounds.Right + 4 || y < bounds.Top - 5 || y > bounds.Bottom + 5) return;
            float radius = bounds.Height / 2;
            float py = y - (bounds.Top + radius) + Ripple(x, bounds);
            float px = Math.Max(0, Math.Abs(x - (bounds.Left + bounds.Width / 2)) - (bounds.Width / 2 - radius));
            float distance = MathF.Sqrt(px * px + py * py) - radius;
            if (distance > 3) return;
            float normalY = Math.Clamp(py / radius, -1, 1);
            float coverage = Math.Clamp(.5f - distance * scale, 0, 1);
            float clothAlpha = alpha / 255;
            float light = Math.Clamp(.52f + (red * .2126f + green * .7152f + blue * .0722f) / Math.Max(1, clothAlpha) / 34,
                .58f, 1.65f);
            float upper = (1 - normalY) / 2;
            float insetShadow = Math.Max(0, 1 + distance / 5) * (.11f + .28f * upper);
            float lowerLip = Math.Max(0, 1 - Math.Abs(distance + 1.4f) / 1.1f) * Math.Max(0, normalY) * .12f;
            float lighting = light * (1 - insetShadow) + lowerLip;
            red += (26 * lighting * clothAlpha - red) * coverage;
            green += (48 * lighting * clothAlpha - green) * coverage;
            blue += (73 * lighting * clothAlpha - blue) * coverage;
            if (distance > 0)
            {
                float contact = 1 - (1 - distance / 3) * .18f * (1 - coverage);
                red *= contact; green *= contact; blue *= contact;
            }
        }

        internal static void DrawCue(Graphics g, RectangleF bounds, string text)
        {
            using Font font = new(RinkTypography.FamilyName, 21, FontStyle.Bold, GraphicsUnit.Pixel);
            using StringFormat centered = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using SolidBrush shadow = new(Color.FromArgb(115, 3, 10, 22));
            using SolidBrush ink = new(CueInk);
            RectangleF offset = bounds; offset.Offset(0, .7f);
            g.DrawString(text, font, shadow, offset, centered);
            g.DrawString(text, font, ink, bounds, centered);
        }
    }
}
