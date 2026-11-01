using System.Drawing.Drawing2D;

namespace LP_PopovD_App
{
    internal readonly record struct SnowSeed(float X, float Y, float Radius, float Phase, float Speed, float Angle);

    internal static class SnowDrawing
    {
        internal static SnowSeed[] Seeds(int count, int seed)
        {
            Random random = new(seed);
            SnowSeed[] result = new SnowSeed[count];
            for (int i = 0; i < count; i++)
                result[i] = new(random.NextSingle(), random.NextSingle(), .35f + random.NextSingle() * 1.35f,
                    random.NextSingle(), .5f + random.NextSingle(), random.NextSingle() * MathF.Tau);
            return result;
        }

        internal static float Smooth(float t)
        {
            t = Math.Clamp(t, 0, 1);
            return t * t * t * (t * (t * 6 - 15) + 10);
        }

        internal static void Grain(Graphics g, SolidBrush brush, Pen streak, float x, float y,
            float radius, float dx, float dy, float opacity, bool crystal = false)
        {
            int alpha = (int)Math.Clamp(opacity * 255, 0, 255);
            if (alpha < 2) return;
            brush.Color = Color.FromArgb(alpha, 245, 252, 255);
            streak.Color = Color.FromArgb(alpha / 3, 228, 246, 255);
            streak.Width = Math.Max(.4f, radius * .65f);
            float length = MathF.Sqrt(dx * dx + dy * dy);
            if (length > .7f) g.DrawLine(streak, x - dx, y - dy, x, y);
            g.FillEllipse(brush, x - radius, y - radius, radius * 2, radius * 2);
            if (crystal && radius > 1.15f)
            {
                streak.Color = Color.FromArgb(alpha / 2, 249, 254, 255);
                g.DrawLine(streak, x - radius * 1.5f, y, x + radius * 1.5f, y);
                g.DrawLine(streak, x, y - radius * 1.5f, x, y + radius * 1.5f);
            }
        }
    }
}
