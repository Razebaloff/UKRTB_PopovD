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
            byte[] first = Frame(0), later = Frame(3);
            Check(!first.AsSpan().SequenceEqual(later), "Свечение плавно меняет общую яркость.");
            int peakFirst = 0, peakLater = 0;
            for (int i = 3; i < first.Length; i += 4)
            {
                peakFirst = Math.Max(peakFirst, first[i]);
                peakLater = Math.Max(peakLater, later[i]);
            }
            bool uniform = peakFirst > 0 && peakLater > 0;
            for (int i = 3; i < first.Length && uniform; i += 4)
                uniform = Math.Abs(first[i] * peakLater - later[i] * peakFirst) <=
                    2 * (peakFirst + peakLater);
            Check(uniform, "Все участки ореола меняют яркость вместе, без движущегося светового пятна.");
            Check(ReferenceEquals(halo, glowType.GetField("halo", Hidden)!.GetValue(glow)),
                "Ореол не создаётся повторно на каждом кадре.");
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
