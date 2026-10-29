using System.Drawing.Drawing2D;
using System.Numerics;

namespace Verdant.Game;

internal static class Art
{
    public static readonly Color Ink = Color.FromArgb(15, 29, 36);
    public static readonly Color Panel = Color.FromArgb(22, 42, 46);
    public static readonly Color Gold = Color.FromArgb(237, 194, 114);
    public static readonly Color Cream = Color.FromArgb(235, 231, 204);
    public static readonly Color Muted = Color.FromArgb(151, 178, 165);
    public static readonly Color Mint = Color.FromArgb(115, 213, 163);
    public static Color Alpha(Color c, float opacity) => Color.FromArgb(Math.Clamp((int)(255 * opacity), 0, 255), c);
    public static PointF P(Vector2 p) => new(p.X, p.Y);
    public static void Fill(Graphics g, Color c, float x, float y, float w, float h)
    { using var b = new SolidBrush(c); g.FillRectangle(b, x, y, w, h); }
    public static void Ellipse(Graphics g, Color c, float x, float y, float w, float h)
    { using var b = new SolidBrush(c); g.FillEllipse(b, x, y, w, h); }
    public static void Line(Graphics g, Color c, float width, params PointF[] pts)
    { using var p = new Pen(c, width) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round }; if (pts.Length > 1) g.DrawLines(p, pts); }
    public static void Polygon(Graphics g, Color c, params PointF[] pts)
    { using var b = new SolidBrush(c); g.FillPolygon(b, pts); }
    public static GraphicsPath Round(RectangleF r, float radius = 12)
    {
        var p = new GraphicsPath(); float d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
    }
    public static void Box(Graphics g, RectangleF r, Color c, Color? border = null, float radius = 12)
    {
        using var shape = Round(r, radius); using var b = new SolidBrush(c); g.FillPath(b, shape);
        if (border is { } color) { using var pen = new Pen(color, 1.3f); g.DrawPath(pen, shape); }
    }
    public static void Text(Graphics g, string s, float x, float y, float size = 14, Color? color = null, bool bold = false, float width = 1000, float height = 70, bool center = false)
    {
        using var font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
        using var b = new SolidBrush(color ?? Cream);
        using var format = new StringFormat { Alignment = center ? StringAlignment.Center : StringAlignment.Near, LineAlignment = StringAlignment.Near, Trimming = StringTrimming.EllipsisWord };
        g.DrawString(s, font, b, new RectangleF(x, y, width, height), format);
    }
    public static void Star(Graphics g, Vector2 at, float size, Color color)
    {
        Polygon(g, color, Enumerable.Range(0, 10).Select(i => new PointF(at.X + MathF.Sin(i * MathF.PI / 5) * (i % 2 == 0 ? size : size * .43f), at.Y - MathF.Cos(i * MathF.PI / 5) * (i % 2 == 0 ? size : size * .43f))).ToArray());
    }
    public static void Tree(Graphics g, float x, float y, float scale, Color foliage)
    {
        Ellipse(g, Color.FromArgb(35, 0, 0, 0), x - 24 * scale, y - 8 * scale, 55 * scale, 16 * scale);
        Fill(g, Color.FromArgb(72, 61, 48), x - 4 * scale, y - 38 * scale, 8 * scale, 40 * scale);
        for (int i = 0; i < 3; i++)
        {
            float top = y - (90 - i * 20) * scale, half = (19 + i * 7) * scale;
            Polygon(g, foliage, new(x, top), new(x - half, top + 43 * scale), new(x + half, top + 43 * scale));
            Line(g, Alpha(Cream, .08f), 1, new(x, top + 3), new(x - half + 5, top + 40 * scale));
        }
    }
    public static void Health(Graphics g, Vector2 p, float hp, float max, float width, Color c)
    {
        Fill(g, Color.FromArgb(200, 12, 23, 28), p.X - width / 2 - 1, p.Y, width + 2, 5);
        Fill(g, c, p.X - width / 2, p.Y + 1, width * Math.Clamp(hp / Math.Max(1, max), 0, 1), 3);
    }
    public static void Tower(Graphics g, Vector2 p, int kind, int level, int branch = -1)
    {
        float x = p.X, y = p.Y;
        Ellipse(g, Color.FromArgb(65, 0, 0, 0), x - 27, y - 9, 57, 24);
        Ellipse(g, Color.FromArgb(88, 88, 68), x - 26, y - 15, 52, 22);
        Ellipse(g, Color.FromArgb(146, 139, 101), x - 23, y - 18, 46, 18);
        Color accent = kind switch { 0 => Mint, 1 => Color.FromArgb(152, 160, 247), 2 => Color.FromArgb(232, 162, 100), _ => Color.FromArgb(99, 181, 219) };
        float h = 29 + level * 5;
        if (kind == 0)
        {
            Fill(g, Color.FromArgb(97, 74, 49), x - 16, y - h, 32, h - 5);
            Line(g, Color.FromArgb(63, 58, 43), 5, new(x - 18, y - h + 3), new(x - 18, y - 1));
            Line(g, Color.FromArgb(177, 142, 85), 3, new(x + 15, y - h + 5), new(x + 15, y - 2));
            Polygon(g, accent, new(x - 28, y - h + 2), new(x, y - h - 24), new(x + 28, y - h + 2));
            Ellipse(g, Ink, x - 6, y - h + 10, 12, 15);
            Line(g, Gold, 2, new(x - 12, y - h + 8), new(x - 12, y - h + 22));
        }
        else if (kind == 1)
        {
            Polygon(g, Color.FromArgb(85, 92, 120), new(x - 18, y - 8), new(x - 12, y - h), new(x + 12, y - h), new(x + 18, y - 8));
            Line(g, accent, 3, new(x, y - 14), new(x, y - h + 5));
            Ellipse(g, Alpha(accent, .16f), x - 25, y - h - 31, 50, 50);
            Polygon(g, accent, new(x, y - h - 29), new(x + 12, y - h - 10), new(x, y - h + 3), new(x - 12, y - h - 10));
            Line(g, Cream, 2, new(x, y - h - 23), new(x + 6, y - h - 12));
        }
        else if (kind == 2)
        {
            for (int i = 0; i < 5; i++)
            {
                float angle = i * MathF.Tau / 5, sx = x + MathF.Cos(angle) * 21, sy = y + MathF.Sin(angle) * 9;
                Polygon(g, Color.FromArgb(121 + i * 8, 120 + i * 6, 97), new(sx - 7, sy), new(sx - 6, sy - 22 - level * 2), new(sx + 5, sy - 28 - level * 2), new(sx + 8, sy));
            }
            Ellipse(g, accent, x - 9, y - 22, 18, 14);
            Line(g, Gold, 2, new(x - 4, y - 19), new(x + 2, y - 28));
        }
        else
        {
            Fill(g, Color.FromArgb(123, 141, 138), x - 21, y - h + 6, 42, h - 9);
            Fill(g, Color.FromArgb(77, 98, 101), x - 24, y - h, 48, 12);
            for (int i = -1; i <= 1; i++) Fill(g, Color.FromArgb(147, 164, 153), x + i * 17 - 5, y - h - 8, 10, 13);
            Box(g, new(x - 7, y - 22, 14, 20), Ink, null, 5);
            Line(g, Gold, 2, new(x + 28, y - 4), new(x + 28, y - h - 12));
            Polygon(g, accent, new(x + 29, y - h - 12), new(x + 47, y - h - 7), new(x + 29, y - h + 3));
        }
        for (int i = 0; i < level; i++) Ellipse(g, Gold, x - level * 4 + i * 8, y + 8, 5, 5);
        if (branch >= 0) Star(g, new(x + 23, y - 42), 7, branch == 0 ? Gold : Color.FromArgb(167, 190, 255));
    }
}
