using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using LP_PopovD_App;

internal static class WinFormsChecks
{
    [STAThread]
    private static int Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        int checks = 0, drawingExceptions = 0;
        void Observe(object? sender, FirstChanceExceptionEventArgs e)
        {
            if (e.Exception is ArgumentException or ObjectDisposedException) drawingExceptions++;
        }
        AppDomain.CurrentDomain.FirstChanceException += Observe;
        try
        {
            using Form host = new() { AutoScaleMode = AutoScaleMode.None, ClientSize = new Size(800, 180), ShowInTaskbar = false };
            float[] scales = { 1.068f, 1.068f, 1.068f, .8f, .8f, 1, 1, 1.25f, 1.25f, 1.5f, 2, 2, 1.068f };
            foreach (string kind in new[] { "RememberCheckBox", "CapsuleButton", "CapsuleInput" })
            {
                Type type = typeof(Form1).Assembly.GetType("LP_PopovD_App." + kind, throwOnError: true)!;
                object? instance = kind == "CapsuleInput"
                    ? Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null,
                        new object[] { "Логин", "ВВЕДИТЕ ЛОГИН", false, 64 }, CultureInfo.InvariantCulture)
                    : Activator.CreateInstance(type, nonPublic: true);
                using Control control = (Control)instance!;
                control.Bounds = kind == "RememberCheckBox" ? new Rectangle(0, 0, 359, 30) : new Rectangle(0, 0, 480, 64);
                control.Text = kind == "CapsuleButton" ? "ВОЙТИ" : control.Text;
                host.Controls.Add(control);
                host.CreateControl();
                control.CreateControl();
                MethodInfo setScale = type.GetMethod("SetScale", BindingFlags.Instance | BindingFlags.NonPublic)!;
                MethodInfo paint = type.GetMethod("OnPaint", BindingFlags.Instance | BindingFlags.NonPublic)!;
                for (int round = 0; round < 5; round++)
                    foreach (float scale in scales)
                    {
                        setScale.Invoke(control, new object[] { scale });
                        Font first = control.Font;
                        setScale.Invoke(control, new object[] { scale });
                        if (!ReferenceEquals(first, control.Font))
                            throw new InvalidOperationException(kind + ": повторный масштаб заменил шрифт.");
                        using Bitmap image = new(control.Width, control.Height);
                        control.DrawToBitmap(image, control.ClientRectangle);
                        using Graphics g = Graphics.FromImage(image);
                        // Прямой вызов гарантирует проверку OnPaint даже у скрытого окна теста.
                        using PaintEventArgs args = new(g, control.ClientRectangle);
                        paint.Invoke(control, new object[] { args });
                        foreach (Control text in Descendants(control))
                            g.MeasureString("ПРОВЕРКА", text.Font);
                        if (control is CheckBox check)
                        {
                            check.Checked = !check.Checked;
                            paint.Invoke(control, new object[] { args });
                        }
                        checks++;
                    }
                host.Controls.Remove(control);
            }
            if (drawingExceptions != 0)
                throw new InvalidOperationException("Исключения отрисовки: " + drawingExceptions);
            Console.WriteLine($"Passed: {checks} native WinForms paint cycles; drawing exceptions: 0.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.ToString());
            return 1;
        }
        finally { AppDomain.CurrentDomain.FirstChanceException -= Observe; }
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        yield return root;
        foreach (Control child in root.Controls)
            foreach (Control item in Descendants(child)) yield return item;
    }
}
