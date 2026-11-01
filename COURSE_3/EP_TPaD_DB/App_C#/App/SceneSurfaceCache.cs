using System.Drawing.Imaging;

namespace LP_PopovD_App
{
    internal readonly record struct SceneSurfaceKey(AppScreen Screen, bool CodeRequested = false, bool CleanJersey = false);

    // Растеризация материалов идёт последовательно вне UI. В кадре используются готовые PArgb-изображения.
    internal sealed class SceneSurfaceCache : IDisposable
    {
        private static readonly object renderer = new();
        private readonly Dictionary<SceneSurfaceKey, Task<Bitmap>> surfaces = new();
        internal Size Size { get; }
        private volatile bool disposed;

        internal SceneSurfaceCache(Size size) => Size = size;

        internal static void DisposeRendererResources()
        {
            lock (renderer)
            {
                ArtworkResources.Dispose();
                NativeIceDrawing.Dispose();
            }
        }

        internal Task<Bitmap> GetAsync(SceneSurfaceKey key)
        {
            if (disposed) throw new ObjectDisposedException(nameof(SceneSurfaceCache));
            if (surfaces.TryGetValue(key, out Task<Bitmap>? task)) return task;
            task = Task.Run(() =>
            {
                lock (renderer)
                {
                    if (disposed) throw new OperationCanceledException();
                    Bitmap image = new(Size.Width, Size.Height, PixelFormat.Format32bppPArgb);
                    image.SetResolution(96, 96);
                    try
                    {
                        using Graphics g = Graphics.FromImage(image);
                        SceneDrawing.Draw(g, Size, key.Screen, includeSticks: false,
                            codeRequested: key.CodeRequested, authPanels: !key.CleanJersey);
                        return image;
                    }
                    catch { image.Dispose(); throw; }
                }
            });
            surfaces.Add(key, task);
            return task;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (Task<Bitmap> task in surfaces.Values)
                _ = task.ContinueWith(done => { if (done.Status == TaskStatus.RanToCompletion) done.Result.Dispose(); },
                    CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            surfaces.Clear();
        }
    }
}
