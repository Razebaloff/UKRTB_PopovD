using System.Numerics;
using Verdant.Core;

int passed = 0;
void Check(string name, Action test)
{
    try { test(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception e) { Console.Error.WriteLine("FAIL " + name + ": " + e); Environment.ExitCode = 1; }
}
static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
static Level Arena(int gold = 10000, int waves = 1) => new(0, "Test", "", 0, [new Route(new(-20, 300), new(990, 300))], [new(180, 230), new(340, 230), new(500, 230), new(660, 230)], waves, gold, null);
static void Advance(Battle b, float seconds)
{ for (int i = 0; i < seconds * 60; i++) b.Tick(Battle.Step); }
static Enemy AddEnemy(Battle b, EnemyKind kind, float distance = 200, float? health = null)
{
    var e = new Enemy { Id = 100000 + b.Enemies.Count, Kind = kind, RouteIndex = 0, Distance = distance, Position = b.Map.Routes[0].At(distance), Health = health ?? Content.Enemies[(int)kind].Health, MaxHealth = health ?? Content.Enemies[(int)kind].Health };
    b.Enemies.Add(e); return e;
}

Check("All 12 maps have valid roads and usable plots", () =>
{
    Require(Content.Levels.Length == 12, "campaign size");
    foreach (var map in Content.Levels)
    {
        Require(map.Sites.Length >= 7, map.Name + " has too few plots");
        foreach (var site in map.Sites)
        {
            float d = map.Routes.Min(r => Vector2.Distance(site, r.Nearest(site)));
            Require(d >= 49 && d <= 120, "plot overlaps road or out of reach");
        }
        foreach (var route in map.Routes)
        {
            Require(route.Length > 700, "route too short");
            Require(route.At(-100) == route.Points[0] && route.At(100000) == route.Points[^1], "route clamp");
        }
    }
});
Check("Economy rejects duplicate plots, insufficient gold and invalid kinds", () =>
{
    var b = new Battle(Arena(100));
    Require(!b.Build(0, TowerKind.Rune) && b.Gold == 100, "overspend");
    Require(!b.Build(0, (TowerKind)999), "invalid tower");
    Require(b.Build(0, TowerKind.Ranger) && b.Gold == 15, "cost");
    Require(!b.Build(0, TowerKind.Ranger) && !b.Build(-1, TowerKind.Ranger), "duplicate or invalid");
    var t = b.Towers[0]; Require(b.Sell(t) && b.Gold == 74 && !b.Sell(t), "refund or double sale");
});
Check("Both branches and all skill levels for every tower", () =>
{
    foreach (var kind in Enum.GetValues<TowerKind>())
    for (int branch = 0; branch < 2; branch++)
    {
        var b = new Battle(Arena()); b.Build(0, kind); var t = b.Towers[0];
        Require(b.Upgrade(t) && b.Upgrade(t), "base upgrades");
        Require(!b.Upgrade(t), "missing branch accepted");
        Require(b.Upgrade(t, branch) && t.Level == 4 && t.Branch == branch, "branch");
        Require(!b.Upgrade(t, branch), "level 5");
        Require(b.ImproveSkill(t) && b.ImproveSkill(t) && b.ImproveSkill(t) && !b.ImproveSkill(t), "skill bounds");
        int gold = b.Gold, refund = t.Refund;
        Require(b.Sell(t) && b.Gold == gold + refund, "upgrade refund");
        Require(!b.Upgrade(t), "sold tower usable");
    }
});
Check("Iron rules, no free casting, fixed cooldown", () =>
{
    var b = new Battle(Arena(), BattleMode.Iron);
    Require(!b.Build(0, TowerKind.Ranger) && !b.Build(0, TowerKind.Grove), "iron restriction");
    Require(b.Build(0, TowerKind.Rune), "iron allowed tower");
    Require(!b.Cast(0, new(1200, 300)) && b.Casts == 0, "invalid target spent cast");
    Require(b.Cast(0, new(200, 300)) && !b.Cast(0, new(200, 300)), "cooldown");
    Advance(b, 32.1f); Require(b.Cast(0, new(200, 300)), "cooldown did not expire");
});
Check("Pure spell damage ignores armor and magic resistance", () =>
{
    var b = new Battle(Arena()); var e = AddEnemy(b, EnemyKind.Beetle, 220);
    Require(b.Cast(0, e.Position) && Math.Abs(e.Health - 70) < .01f, "meteor damage");
});
Check("Physical and magic attacks respect different defenses", () =>
{
    foreach (var kind in new[] { TowerKind.Ranger, TowerKind.Rune })
    {
        var b = new Battle(Arena(), hero: 1); b.Hero.Rally = new(900, 300); b.Hero.Position = b.Hero.Rally;
        b.Build(0, kind); var enemy = AddEnemy(b, EnemyKind.Shield, 200, 1000);
        b.Tick(Battle.Step);
        float expected = kind == TowerKind.Ranger ? 1000 - 14 * .35f : 970;
        Require(Math.Abs(enemy.Health - expected) < .01f, "damage reduction " + kind);
    }
});
Check("Ground-only weapons cannot attack flying enemies", () =>
{
    var b = new Battle(Arena(), hero: 1); b.Hero.Rally = new(900, 300); b.Hero.Position = b.Hero.Rally;
    b.Build(0, TowerKind.Grove); var e = AddEnemy(b, EnemyKind.Wisp, 200);
    Advance(b, 1); Require(e.Health == e.MaxHealth, "rock hit flying");
});
Check("Frost expires and reinforcement troops expire", () =>
{
    var b = new Battle(Arena()); var e = AddEnemy(b, EnemyKind.Beetle, 100);
    b.Cast(1, e.Position); Require(e.SlowFactor == .15f, "slow missing");
    Advance(b, 4.1f); Require(e.SlowFactor == 1, "slow did not expire");
    b.Cast(2, new(500, 300)); Require(b.Allies.Count == 4, "reinforcement count");
    Advance(b, 22.1f); Require(b.Allies.Count == 1, "reinforcement lifetime");
});
Check("Barracks rally validation and respawn", () =>
{
    var b = new Battle(Arena()); b.Build(0, TowerKind.Guard); var t = b.Towers[0];
    Require(!b.SetRally(t, new(900, 300)), "rally outside radius");
    Require(b.SetRally(t, new(190, 290)), "valid rally");
    b.Allies.First(a => a.Home == t).Health = 0; Advance(b, 8);
    Require(b.Allies.Count(a => a.Alive && a.Home == t) == 3, "guard not replaced");
});
Check("Brood death spawns three enemies without collection errors", () =>
{
    var b = new Battle(Arena()); var e = AddEnemy(b, EnemyKind.Brood, 300, 100);
    b.Cast(0, e.Position); Require(e.Removed && b.Kills == 1, "brood death");
    Require(b.Enemies.Count(x => x.Alive && x.Kind == EnemyKind.Hound) == 3, "brood spawn");
    b.Tick(Battle.Step);
});
Check("No-defense defeats, terminal state freezes and rejects actions", () =>
{
    var b = new Battle(Arena(), BattleMode.Heroic); b.MoveHero(new(980, 300));
    Require(b.CallWave() && !b.CallWave(), "wave queue");
    Advance(b, 180); Require(b.State == BattleState.Defeat && b.Lives == 0, "defeat");
    float time = b.Time; int gold = b.Gold;
    Advance(b, 2); Require(b.Time == time && !b.Build(0, TowerKind.Ranger) && !b.Cast(0, new(100, 300)) && b.Gold == gold, "terminal mutation");
});
Check("Wave queue, early call and automatic next waves", () =>
{
    var b = new Battle(Arena(waves: 3)); b.CallWave();
    Advance(b, 10); Require(b.Wave == 1 && b.CanCallWave, "spawns not exhausted");
    int gold = b.Gold; Require(b.CallWave() && b.Wave == 2 && b.Gold > gold, "early call bonus");
    Advance(b, 30); Require(b.Wave == 3, "automatic wave missing");
});
Check("Every campaign/mode wave plan is ordered and uses legal entries", () =>
{
    foreach (var map in Content.Levels)
    foreach (var mode in Enum.GetValues<BattleMode>())
    {
        var b = new Battle(map, mode);
        for (int wave = 1; wave <= b.WaveLimit; wave++)
        {
            var plan = b.PlanWave(wave);
            Require(plan.Count > 0 && plan.Zip(plan.Skip(1)).All(pair => pair.First.Time <= pair.Second.Time), "spawn ordering");
            Require(plan.All(s => s.Route >= 0 && s.Route < map.Routes.Length && Enum.IsDefined(s.Kind)), "invalid spawn");
        }
        if (map.Boss is { } boss && mode == BattleMode.Campaign) Require(b.PlanWave(b.WaveLimit).Any(s => s.Kind == boss), "missing boss");
    }
});
Check("Progression star spending and challenge gates", () =>
{
    var p = new Profile(); Require(p.Unlocked(0) && !p.Unlocked(1) && !p.BuyPerk(0), "initial progression");
    p.Stars[0] = 3; Require(p.Unlocked(1) && p.ChallengeUnlocked(0), "unlock");
    Require(p.BuyPerk(0) && p.BuyPerk(0) && !p.BuyPerk(0), "perk pricing");
    p.ResetPerks(); Require(p.AvailableStars == 3, "star refund");
});
Check("Persistence roundtrip, backup recovery and hostile JSON normalization", () =>
{
    string dir = Path.Combine(Path.GetTempPath(), "verdant-check-" + Guid.NewGuid()); Directory.CreateDirectory(dir);
    try
    {
        string path = Path.Combine(dir, "save.json"); var p = new Profile { TotalKills = 91, Hero = 2 }; p.Stars[0] = 3;
        Require(ProfileStore.Save(p, path, out _), "save failed");
        var loaded = ProfileStore.Load(path, out _); Require(loaded.Hero == 2 && loaded.TotalKills == 91 && loaded.Stars[0] == 3, "roundtrip");
        p.TotalKills = 100; ProfileStore.Save(p, path, out _); File.WriteAllText(path, "broken");
        loaded = ProfileStore.Load(path, out string warning); Require(loaded.TotalKills == 91 && warning.Length > 0, "backup recovery");
        ProfileStore.Save(loaded, path, out _); Require(File.ReadAllText(path + ".bak").Contains("91"), "corrupt primary overwrote backup");
        File.WriteAllText(path, "{\"Stars\":null,\"Perks\":[99,99],\"Hero\":999,\"Difficulty\":-9}");
        loaded = ProfileStore.Load(path, out _); Require(loaded.Stars.Length == 36 && loaded.Perks.Length == 4 && loaded.AvailableStars >= 0 && loaded.Hero == 2 && loaded.Difficulty == 0, "normalization");
    }
    finally { Directory.Delete(dir, true); }
});
Check("Invalid time steps leave simulation valid", () =>
{
    var b = new Battle(Arena()); b.Tick(float.NaN); b.Tick(float.PositiveInfinity); b.Tick(-1);
    Require(b.Time == 0, "invalid dt"); b.Tick(50); Require(b.Time == .05f, "large dt clamp");
});
Check("A defended battle reaches victory and records stars without downgrading", () =>
{
    var b = new Battle(Arena(), difficulty: 0);
    for (int i = 0; i < 4; i++)
    {
        b.Build(i, (TowerKind)(i % 3)); var t = b.Towers[^1];
        b.Upgrade(t); b.Upgrade(t); b.Upgrade(t, i % 2); b.ImproveSkill(t);
    }
    b.CallWave(); Advance(b, 120);
    Require(b.State == BattleState.Victory && b.Stars == 3, "victory or stars");
    var profile = new Profile(); profile.Record(b);
    Require(profile.Stars[0] == 3 && profile.Unlocked(1) && profile.TotalKills == b.Kills, "recorded victory");
    Require(!b.CallWave() && !b.Sell(b.Towers[0]), "terminal actions");
});
Check("Hero death schedules a respawn with full health", () =>
{
    var b = new Battle(Arena(), hero: 1); b.Hero.Health = 1;
    var boss = AddEnemy(b, EnemyKind.Colossus, b.Map.Routes[0].Length * .7f); boss.AbilityTimer = 0;
    b.Tick(Battle.Step);
    Require(!b.Hero.Alive && b.HeroRespawn > 14, "hero did not die");
    Advance(b, 15.1f);
    Require(b.Hero.Alive && b.Hero.Health == b.Hero.MaxHealth, "hero did not respawn");
});
Check("Stress: branches, all enemy abilities, every hero, every map", () =>
{
    foreach (var map in Content.Levels)
    {
        var arena = map with { Gold = 100000, Waves = 2, Boss = null };
        var b = new Battle(arena, hero: map.Id % 3, upgrades: [5, 5, 5, 5]);
        for (int i = 0; i < Math.Min(12, map.Sites.Length); i++)
        {
            b.Build(i, (TowerKind)(i % 4)); var tower = b.Towers[^1]; b.Upgrade(tower); b.Upgrade(tower); b.Upgrade(tower, i % 2); b.ImproveSkill(tower);
        }
        foreach (var kind in Enum.GetValues<EnemyKind>()) AddEnemy(b, kind, 100 + (int)kind * 25);
        b.CallWave();
        for (int frame = 0; frame < 90 * 60 && b.State == BattleState.Playing; frame++)
        {
            if (frame % 90 == 0 && b.Enemies.FirstOrDefault(e => e.Alive) is { } target) for (int s = 0; s < 4; s++) b.Cast(s, target.Position);
            b.Tick(Battle.Step);
            Require(b.Enemies.All(e => float.IsFinite(e.Position.X) && float.IsFinite(e.Health)) && b.Gold >= 0, "invalid world state");
        }
    }
});
Check("Normal difficulty campaign: all 12 maps won using legal economy", CampaignSmoke.Run);
Console.WriteLine($"\n{passed} checks passed; exit code {Environment.ExitCode}.");
