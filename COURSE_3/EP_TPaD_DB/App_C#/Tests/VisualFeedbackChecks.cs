using System.Drawing.Imaging;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using LP_PopovD_App;

internal static class VisualFeedbackChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    internal static int Run()
    {
        Assembly app = typeof(Form1).Assembly;
        int checks = 0;
        void Check(bool ok, string reason) { if (!ok) throw new InvalidOperationException(reason); checks++; }
        Type frostType = app.GetType("LP_PopovD_App.FrostHoverAnimation", true)!;
        int updates = 0;
        using (IDisposable frost = (IDisposable)Activator.CreateInstance(frostType, Hidden, null,
            new object[] { (Action)(() => updates++), 900 }, CultureInfo.InvariantCulture)!)
        {
            frostType.GetMethod("SetVisible", Hidden)!.Invoke(frost, new object[] { true });
            frostType.GetProperty("Amount", Hidden)!.SetValue(frost, 1f);
            frostType.GetField("lastAmbientFrame", Hidden)!.SetValue(frost, -1);
            updates = 0;
            frostType.GetMethod("TickAll", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null);
            Check(updates > 0, "Иней продолжает обновляться после полного наведения.");
        }

        Type boardType = app.GetType("LP_PopovD_App.ScheduleBoard", true)!;
        using Control board = (Control)Activator.CreateInstance(boardType, nonPublic: true)!;
        board.Size = new Size(960, 540);
        MethodInfo paint = boardType.GetMethod("OnPaint", Hidden)!;
        PropertyInfo enabled = boardType.GetProperty("BookingEnabled", Hidden)!;
        Check(!(bool)enabled.GetValue(board)!, "Расписание по умолчанию доступно только для просмотра.");
        using Bitmap viewer = Paint(board, paint);
        object firstCache = boardType.GetField("contentCache", Hidden)!.GetValue(board)!;
        boardType.GetMethod("OnMouseMove", Hidden)!.Invoke(board,
            new object[] { new MouseEventArgs(MouseButtons.None, 0, 570, 100, 0) });
        using Bitmap hovered = Paint(board, paint);
        Check(ReferenceEquals(firstCache, boardType.GetField("contentCache", Hidden)!.GetValue(board)),
            "Наведение не растеризует текст и всю таблицу повторно.");
        boardType.GetMethod("OnMouseDown", Hidden)!.Invoke(board,
            new object[] { new MouseEventArgs(MouseButtons.Left, 1, 200, 210, 0) });
        using Bitmap selection = Paint(board, paint);
        Check(ReferenceEquals(firstCache, boardType.GetField("contentCache", Hidden)!.GetValue(board)),
            "Выбор дня и часа не перестраивает большой кэш расписания.");
        Check(boardType.GetField("footerCache", Hidden)!.GetValue(board) is Bitmap,
            "Нижняя панель выбора кэшируется со сглаживанием.");
        enabled.SetValue(board, true);
        using Bitmap planner = Paint(board, paint);
        Check(viewer.GetPixel(800, 489) != planner.GetPixel(800, 489), "Кнопка брони появляется только в режиме аренды.");
        enabled.SetValue(board, false);
        using Bitmap viewerAgain = Paint(board, paint);
        Check(viewer.GetPixel(800, 489) == viewerAgain.GetPixel(800, 489), "После возврата в просмотр кнопка брони исчезает.");

        Type glowType = app.GetType("LP_PopovD_App.TitleGlowDrawing", true)!;
        foreach (bool menu in new[] { false, true })
        {
            using IDisposable glow = (IDisposable)Activator.CreateInstance(glowType, Hidden, null,
                new object[] { new Size(960, 540), menu }, CultureInfo.InvariantCulture)!;
            MethodInfo draw = glowType.GetMethod("Draw", Hidden)!;
            object halo = glowType.GetField("halo", Hidden)!.GetValue(glow)!;
            byte[] Frame(float seconds)
            {
                using Bitmap image = new(960, 540, PixelFormat.Format32bppArgb);
                image.SetResolution(96, 96);
                using (Graphics g = Graphics.FromImage(image)) draw.Invoke(glow, new object[] { g, seconds });
                BitmapData data = image.LockBits(new Rectangle(Point.Empty, image.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    byte[] pixels = new byte[data.Stride * data.Height];
                    Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                    return pixels;
                }
                finally { image.UnlockBits(data); }
            }
            byte[] first = Frame(1.75f), later = Frame(5.25f);
            Check(!first.AsSpan().SequenceEqual(later), "Свечение плавно меняет общую яркость.");
            int peakFirst = 0, peakLater = 0;
            for (int i = 3; i < first.Length; i += 4)
            {
                peakFirst = Math.Max(peakFirst, first[i]);
                peakLater = Math.Max(peakLater, later[i]);
            }
            int iceFace = 0, pureWhite = 0;
            for (int i = 0; i < first.Length; i += 4)
            {
                if (first[i + 3] < 195) continue;
                if (first[i] > first[i + 1] && first[i + 1] > first[i + 2] && first[i + 2] > 190) iceFace++;
                if (first[i] >= 245 && first[i + 1] >= 245 && first[i + 2] >= 245) pureWhite++;
            }
            Check(iceFace > 100 && pureWhite == 0, "Даже в максимуме буквы сохраняют ледяной оттенок.");
            Check(peakFirst <= 230 && peakFirst - peakLater is >= 20 and <= 40,
                "Плавная пульсация сохраняет видимость фоновой краски.");
            bool uniform = peakFirst > 0 && peakLater > 0;
            for (int i = 3; i < first.Length && uniform; i += 4)
                uniform = Math.Abs(first[i] * peakLater - later[i] * peakFirst) <=
                    2 * (peakFirst + peakLater);
            Check(uniform, "Все участки ореола меняют яркость вместе, без движущегося светового пятна.");
            Check(ReferenceEquals(halo, glowType.GetField("halo", Hidden)!.GetValue(glow)),
                "Ореол не создаётся повторно на каждом кадре.");
        }

        Type success = app.GetType("LP_PopovD_App.LoginSuccessDrawing", true)!;
        Bitmap LightFrame(string login, bool registration)
        {
            Size size = new(640, 360);
            using IDisposable effect = (IDisposable)Activator.CreateInstance(success, Hidden, null,
                new object[] { size, new Bitmap(size.Width, size.Height), new Bitmap(size.Width, size.Height),
                    login, registration }, CultureInfo.InvariantCulture)!;
            return (Bitmap)success.GetMethod("Render", Hidden)!.Invoke(effect, new object[] { 1350f })!;
        }
        foreach (bool registration in new[] { false, true })
        {
            using Bitmap shortName = LightFrame("Я", registration);
            using Bitmap longName = LightFrame("Хоккеист_с_длинным_логином", registration);
            bool sameRays = true, differentLetters = false;
            for (int y = 0; y < shortName.Height; y += 3)
                for (int x = 0; x < shortName.Width; x += 3)
                {
                    if (shortName.GetPixel(x, y) == longName.GetPixel(x, y)) continue;
                    if (x < 210 || x > 430) sameRays = false;
                    else differentLetters = true;
                }
            Check(sameRays && differentLetters, "За пределами логина свет не зависит от ширины и контуров букв.");
        }

        using Control info = (Control)Activator.CreateInstance(app.GetType("LP_PopovD_App.InformationPanel", true)!, nonPublic: true)!;
        foreach (int width in new[] { 658, 1316, 658 })
        {
            info.GetType().GetMethod("SetSceneBounds", Hidden)!.Invoke(info,
                new object[] { new Rectangle(0, 0, width, width * 385 / 1232) });
            info.PerformLayout();
            foreach (Label field in info.Controls.OfType<Label>())
            {
                Size measured = TextRenderer.MeasureText(field.Text, field.Font);
                Check(field.Width >= measured.Width && field.Height >= measured.Height,
                    $"Текст раздела «Информация» не помещается при ширине панели {width}: {field.Text}");
            }
        }
        return checks;
    }

    private static Bitmap Paint(Control control, MethodInfo paint)
    {
        Bitmap image = new(control.Width, control.Height, PixelFormat.Format32bppArgb);
        image.SetResolution(96, 96);
        using Graphics g = Graphics.FromImage(image);
        using PaintEventArgs args = new(g, control.ClientRectangle);
        paint.Invoke(control, new object[] { args });
        return image;
    }
}
