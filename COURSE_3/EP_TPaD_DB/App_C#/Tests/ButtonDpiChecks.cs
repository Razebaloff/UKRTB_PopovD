using System.Drawing.Imaging;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using LP_PopovD_App;

// Проверяем настоящий OnPaint, а не только формулу центрирования.
// Размер окна и DPI независимы: один и тот же кэш текста должен давать
// те же пиксели на экранах 100/125/150/200% и после переноса обратно.
internal static class ButtonDpiChecks
{
    internal static int Run()
    {
        int checks = 0;
        foreach (string kind in new[] { "PuckButton", "RinkMenuButton", "CapsuleButton" })
        {
            Type type = typeof(Form1).Assembly.GetType("LP_PopovD_App." + kind, true)!;
            using Control button = (Control)(kind == "CapsuleButton"
                ? Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new object[] { false }, CultureInfo.InvariantCulture)!
                : Activator.CreateInstance(type)!);
            // Останавливаем зависящий от времени иней; OnPaint вызываем явно.
            button.Visible = false;
            MethodInfo paint = type.GetMethod("OnPaint", BindingFlags.Instance | BindingFlags.NonPublic)!;
            string[] captions = kind switch
            {
                "PuckButton" => new[] { "ВОЙТИ", "РАСПИСАНИЕ", "ЗАРЕГИСТРИРОВАТЬСЯ" },
                "RinkMenuButton" => new[] { "ЗАКАЗАТЬ\nАРЕНДУ", "ПРОФИЛЬ", "ИНФОРМАЦИЯ", "РАСПИСАНИЕ\nЗАНЯТИЙ" },
                _ => new[] { "ПРИСЛАТЬ КОД НА ЭЛ. ПОЧТУ", "ЗАРЕГИСТРИРОВАТЬСЯ", "ВОЙТИ", "НАЗАД" }
            };
            foreach (float scale in new[] { .8f, 1.068f, 1.5f })
            {
                button.Size = kind == "CapsuleButton"
                    ? new Size((int)(448 * scale), (int)(64 * scale))
                    : new Size((int)(330 * scale), (int)(330 * scale));
                type.GetMethod("SetScale", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(button, new object[] { scale });
                foreach (string text in captions)
                {
                    button.Text = text;
                    byte[] expected = Paint(button, paint, 96);
                    foreach (float dpi in new[] { 120f, 144f, 192f, 144f, 96f })
                    {
                        byte[] actual = Paint(button, paint, dpi);
                        if (!expected.AsSpan().SequenceEqual(actual))
                            throw new InvalidOperationException(
                                $"{kind}: надпись '{text}' смещается при {dpi} DPI, масштаб сцены {scale}.");
                        checks++;
                    }
                }
            }
        }
        Console.WriteLine($"Passed: {checks} button DPI comparisons (125/150/200% and return to 100%).");
        return checks;
    }

    private static byte[] Paint(Control button, MethodInfo paint, float dpi)
    {
        using Bitmap image = new(button.Width, button.Height, PixelFormat.Format32bppArgb);
        image.SetResolution(dpi, dpi);
        using (Graphics graphics = Graphics.FromImage(image))
        {
            graphics.PageUnit = GraphicsUnit.Pixel;
            if (button.Region != null) graphics.SetClip(button.Region);
            using PaintEventArgs args = new(graphics, button.ClientRectangle);
            paint.Invoke(button, new object[] { args });
        }
        BitmapData data = image.LockBits(button.ClientRectangle, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            byte[] pixels = new byte[data.Stride * data.Height];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            return pixels;
        }
        finally { image.UnlockBits(data); }
    }
}
