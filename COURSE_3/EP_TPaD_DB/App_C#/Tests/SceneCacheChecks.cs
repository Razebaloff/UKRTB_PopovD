using System.Drawing;
using System.Reflection;
using System.Runtime.ExceptionServices;
using LP_PopovD_App;

AppContext.SetSwitch("System.Drawing.EnableUnixSupport", true);
int checks = 0;
int unexpectedLifecycleExceptions = 0;
List<string> failures = new();
void Check(bool value, string description)
{
    checks++;
    if (!value) failures.Add(description);
}
void Observe(object? sender, FirstChanceExceptionEventArgs e)
{
    if (e.Exception is OperationCanceledException or ObjectDisposedException)
        Interlocked.Increment(ref unexpectedLifecycleExceptions);
}
object renderGate = typeof(SceneSurfaceCache).GetField("renderer", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
AppDomain.CurrentDomain.FirstChanceException += Observe;
try
{
    // Воспроизводит пересоздание окна, пока старая задача ждёт общий отрисовщик.
    for (int round = 0; round < 20; round++)
    {
        SceneSurfaceCache cache = new(new Size(160, 90));
        List<Task<Bitmap?>> pending = new();
        lock (renderGate)
        {
            foreach (AppScreen screen in new[] { AppScreen.Entry, AppScreen.Login, AppScreen.Registration, AppScreen.Menu })
                pending.Add(cache.GetAsync(new(screen)));
            cache.Dispose();
            cache.Dispose();
        }
        foreach (Task<Bitmap?> task in pending)
        {
            try
            {
                Bitmap? result = await task;
                Check(result == null && task.IsCompletedSuccessfully, "Отменённая подготовка должна закончиться без исключения.");
            }
            catch (Exception error) when (error is OperationCanceledException or ObjectDisposedException)
            { Check(false, "При смене размера возникло " + error.GetType().Name); }
        }
        try
        {
            Task<Bitmap?> ignored = cache.GetAsync(new(AppScreen.Entry));
            Check(ignored.IsCompletedSuccessfully && await ignored == null, "Запрос к старому кэшу должен безопасно игнорироваться.");
        }
        catch (ObjectDisposedException) { Check(false, "Запрос к старому кэшу бросает ObjectDisposedException."); }
    }
    using (SceneSurfaceCache live = new(new Size(160, 90)))
    {
        Task<Bitmap?> first = live.GetAsync(new(AppScreen.Entry));
        Task<Bitmap?> second = live.GetAsync(new(AppScreen.Entry));
        Check(ReferenceEquals(first, second), "Один ключ должен использовать одну задачу.");
        Bitmap? image = await first;
        Check(image?.Size == new Size(160, 90), "Действующий кэш должен продолжать рисовать кадры.");
    }
    using (SceneSurfaceCache invalid = new(Size.Empty))
    {
        Task<Bitmap?> failed = invalid.GetAsync(new(AppScreen.Entry));
        bool preserved = false;
        try { await failed; }
        catch (ArgumentException) { preserved = true; }
        Check(preserved && failed.IsFaulted, "Настоящая ошибка отрисовки не должна превращаться в штатную отмену.");
    }
    Check(unexpectedLifecycleExceptions == 0, "Отладчик всё ещё увидит исключения штатного жизненного цикла: " + unexpectedLifecycleExceptions);
}
finally
{
    AppDomain.CurrentDomain.FirstChanceException -= Observe;
    SceneSurfaceCache.DisposeRendererResources();
}
if (failures.Count > 0)
{
    Console.Error.WriteLine($"Failed: {failures.Count}/{checks}; lifecycle exceptions: {unexpectedLifecycleExceptions}");
    foreach (string failure in failures.Distinct()) Console.Error.WriteLine(failure);
    return 1;
}
Console.WriteLine($"Passed: {checks} scene-cache checks; lifecycle exceptions: 0.");
return 0;
