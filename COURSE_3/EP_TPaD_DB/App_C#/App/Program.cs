namespace LP_PopovD_App
{
    internal static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            ApplicationConfiguration.Initialize();
            try
            {
                if (args.Length > 0)
                {
                    try
                    {
                        if (args.Length is < 2 or > 3 || !SceneExport.TryGetScreen(args[0], out AppScreen screen))
                            throw new ArgumentException("Экспорт: --export-entry|--export-login|--export-registration|--export-menu файл.png [ширина].");
                        int width = args.Length == 3 ? int.Parse(args[2]) : SceneExport.Width16K;
                        SceneExport.Save(args[1], screen, width);
                        return 0;
                    }
                    catch (Exception error)
                    {
                        Console.Error.WriteLine($"Не удалось экспортировать экран: {error.Message}");
                        return 1;
                    }
                }
                Application.Run(new Form1());
                return 0;
            }
            finally
            {
                ArtworkResources.Dispose();
                NativeIceDrawing.Dispose();
            }
        }
    }
}
