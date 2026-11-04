using System.Drawing.Imaging;
using System.Globalization;
using System.Reflection;
using LP_PopovD_App;

internal static class VerificationRevealChecks
{
    internal static int Run()
    {
        Assembly app = typeof(Form1).Assembly;
        Type fieldType = app.GetType("LP_PopovD_App.VerificationMovingField", true)!;
        Type animationType = app.GetType("LP_PopovD_App.VerificationRevealDrawing", true)!;
        Bitmap Flat(int width, int height, Color color)
        {
            Bitmap image = new(width, height, PixelFormat.Format32bppPArgb);
            image.SetResolution(96, 96);
            using Graphics g = Graphics.FromImage(image);
            g.Clear(color);
            return image;
        }
        Color backgroundColor = Color.FromArgb(12, 34, 57);
        Bitmap background = Flat(640, 400, backgroundColor), before = Flat(120, 32, Color.Red),
            after = Flat(120, 26, Color.Blue), code = Flat(120, 26, Color.White);
        Array fields = Array.CreateInstance(fieldType, 1);
        fields.SetValue(Activator.CreateInstance(fieldType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, new object[] { before, after, new Rectangle(200, 180, 120, 32), new Rectangle(200, 120, 120, 26) },
            CultureInfo.InvariantCulture), 0);
        using IDisposable animation = (IDisposable)Activator.CreateInstance(animationType,
            BindingFlags.Instance | BindingFlags.NonPublic, null,
            new object[] { background, fields, code, new Rectangle(200, 230, 120, 26) }, CultureInfo.InvariantCulture)!;
        MethodInfo draw = animationType.GetMethod("Draw", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Bitmap Frame(float time, float dpi)
        {
            Bitmap result = Flat(640, 400, backgroundColor);
            result.SetResolution(dpi, dpi);
            using Graphics g = Graphics.FromImage(result);
            draw.Invoke(animation, new object[] { g, time });
            return result;
        }
        int checks = 0;
        void Check(bool ok, string why) { if (!ok) throw new InvalidOperationException(why); checks++; }
        using Bitmap initial = Frame(0, 96), final = Frame(1600, 96), highDpi = Frame(1600, 144);
        Check(initial.GetPixel(210, 185).ToArgb() == Color.Red.ToArgb(), "Начало раскрытия сохраняет прежнее положение поля.");
        Check(initial.GetPixel(210, 235).ToArgb() == backgroundColor.ToArgb(), "Поле кода не возникает целиком на первом кадре.");
        Check(final.GetPixel(210, 125).ToArgb() == Color.Blue.ToArgb(), "Поле поднято в конечное положение.");
        Check(final.GetPixel(210, 185).ToArgb() == backgroundColor.ToArgb(), "На прежнем месте поля не остаётся шлейфа или прямоугольника.");
        Check(final.GetPixel(210, 235).ToArgb() == Color.White.ToArgb(), "К финалу поле кода полностью материализовано.");
        Check(highDpi.GetPixel(210, 235).ToArgb() == Color.White.ToArgb() &&
            highDpi.GetPixel(400, 320).ToArgb() == backgroundColor.ToArgb(), "Другой DPI не увеличивает спрайты повторно.");
        return checks;
    }
}
