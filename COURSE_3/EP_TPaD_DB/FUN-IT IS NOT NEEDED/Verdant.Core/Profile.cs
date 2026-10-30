using System.Text.Json;

namespace Verdant.Core;

public sealed class Profile
{
    public int Version { get; set; } = 1;
    public int[] Stars { get; set; } = new int[36];
    public int[] Perks { get; set; } = new int[4];
    public int Hero { get; set; }
    public int Difficulty { get; set; } = 1;
    public bool Sound { get; set; } = true;
    public bool Music { get; set; }
    public int TotalKills { get; set; }
    public int TotalBuilt { get; set; }
    public int TotalCasts { get; set; }
    public int EndlessBest { get; set; }
    public int TotalStars => Stars.Sum();
    public int AvailableStars => TotalStars - Perks.Sum(rank => rank * (rank + 1) / 2);
    public bool Unlocked(int level) => level >= 0 && level < 12 && (level == 0 || Stars[(level - 1) * 3] > 0);
    public bool ChallengeUnlocked(int level) => level >= 0 && level < 12 && Stars[level * 3] == 3;
    public bool BuyPerk(int index)
    {
        if (index < 0 || index >= 4 || Perks[index] >= 5 || AvailableStars < Perks[index] + 1) return false;
        Perks[index]++; return true;
    }
    public void ResetPerks() => Array.Clear(Perks);
    public void Record(Battle battle)
    {
        TotalKills += battle.Kills; TotalBuilt += battle.Built; TotalCasts += battle.Casts;
        if (battle.Mode == BattleMode.Endless) EndlessBest = Math.Max(EndlessBest, battle.Wave);
        else if (battle.State == BattleState.Victory)
        {
            int i = battle.Map.Id * 3 + (int)battle.Mode;
            Stars[i] = Math.Max(Stars[i], battle.Stars);
        }
    }
    public (string Name, string Description, bool Done)[] Achievements =>
    [
        ("Первый рассвет", "Победить на первом уровне", Stars[0] > 0),
        ("Без единой потери", "Получить три звезды на любом уровне", Stars.Where((_, i) => i % 3 == 0).Any(s => s == 3)),
        ("Архитектор", "Построить 50 башен", TotalBuilt >= 50),
        ("Хранитель леса", "Победить 500 врагов", TotalKills >= 500),
        ("Знаток рун", "Применить 100 заклинаний", TotalCasts >= 100),
        ("Победитель великана", "Пройти Корни великана", Stars[9] > 0),
        ("Изгнание оракула", "Пройти Обсерваторию", Stars[24] > 0),
        ("Новый рассвет", "Победить Владыку пепла", Stars[33] > 0),
        ("Созвездие", "Набрать 36 звёзд кампании", Stars.Where((_, i) => i % 3 == 0).Sum() == 36),
        ("Вечный дозор", "Дойти до 20-й бесконечной волны", EndlessBest >= 20),
        ("Испытанный", "Победить в героическом испытании", Stars.Where((_, i) => i % 3 == 1).Any(s => s > 0)),
        ("Железная воля", "Победить в железном испытании", Stars.Where((_, i) => i % 3 == 2).Any(s => s > 0))
    ];
    internal void Normalize()
    {
        Stars = Enumerable.Range(0, 36).Select(i => Math.Clamp(Stars is not null && i < Stars.Length ? Stars[i] : 0, 0, i % 3 == 0 ? 3 : 1)).ToArray();
        Perks = Enumerable.Range(0, 4).Select(i => Math.Clamp(Perks is not null && i < Perks.Length ? Perks[i] : 0, 0, 5)).ToArray();
        if (AvailableStars < 0) ResetPerks();
        Hero = Math.Clamp(Hero, 0, 2); Difficulty = Math.Clamp(Difficulty, 0, 2);
        TotalKills = Math.Max(0, TotalKills); TotalBuilt = Math.Max(0, TotalBuilt); TotalCasts = Math.Max(0, TotalCasts); EndlessBest = Math.Max(0, EndlessBest);
    }
}

public static class ProfileStore
{
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VerdantGuard", "profile.json");
    public static Profile Load(string path, out string warning)
    {
        warning = "";
        if (!File.Exists(path) && !File.Exists(path + ".bak")) return new();
        foreach (var candidate in new[] { path, path + ".bak" })
        {
            try
            {
                var profile = JsonSerializer.Deserialize<Profile>(File.ReadAllText(candidate)) ?? throw new JsonException();
                profile.Normalize();
                if (candidate != path) warning = "Основное сохранение повреждено. Восстановлена резервная копия.";
                return profile;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or NotSupportedException) { }
        }
        warning = "Не удалось прочитать сохранение. Начат новый профиль; старые файлы оставлены на месте.";
        return new();
    }
    public static bool Save(Profile profile, string path, out string warning)
    {
        warning = "";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true }));
            if (File.Exists(path))
            {
                // A corrupt primary must never overwrite the last valid backup.
                try { _ = JsonSerializer.Deserialize<Profile>(File.ReadAllText(path)) ?? throw new JsonException(); File.Copy(path, path + ".bak", true); }
                catch (JsonException) { }
            }
            File.Move(temp, path, true); return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        { warning = "Прогресс не сохранён: " + e.Message; return false; }
    }
}
