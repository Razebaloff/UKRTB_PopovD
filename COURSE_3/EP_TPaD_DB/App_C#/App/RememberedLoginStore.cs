using System.Text.Json;

namespace LP_PopovD_App
{
    internal sealed class RememberedLoginStore
    {
        private readonly string path;
        private readonly SemaphoreSlim writes = new(1, 1);
        private sealed record Preference(string Login, bool Remember);

        internal RememberedLoginStore(string? filePath = null)
            => path = filePath ?? Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData), "TerritoryHockey", "login.json");

        internal string Load()
        {
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length > 4096) return "";
                Preference? value = JsonSerializer.Deserialize<Preference>(File.ReadAllText(path));
                return value is { Remember: true, Login.Length: > 0 and <= 64 } ? value.Login : "";
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
            { return ""; }
        }

        internal async Task<bool> SaveAsync(string login, bool remember)
        {
            await writes.WaitAsync();
            try { return await SaveCoreAsync(login, remember); }
            finally { writes.Release(); }
        }

        private async Task<bool> SaveCoreAsync(string login, bool remember)
        {
            string temporary = path + ".tmp";
            try
            {
                if (!remember)
                {
                    File.Delete(path);
                    return true;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(new Preference(login, true)));
                File.Move(temporary, path, overwrite: true);
                return true;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { return false; }
            finally
            {
                try { File.Delete(temporary); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
            }
        }
    }
}
