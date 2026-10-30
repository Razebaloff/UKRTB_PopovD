using System.Numerics;
using Verdant.Core;

/// <summary>Deterministic player using only legal gold, cooldowns and affordable campaign perks.</summary>
internal static class CampaignSmoke
{
    public static void Run()
    {
        foreach (var map in Content.Levels)
        {
            int rank = Math.Min(3, map.Id / 3);
            var battle = new Battle(map, difficulty: 1, hero: 2, upgrades: [rank, rank, rank, rank]);
            var sites = map.Sites.Select((p, i) => new
            {
                Index = i,
                Coverage = map.Routes.Sum(r => Enumerable.Range(0, 50).Count(n => Vector2.Distance(p, r.At(r.Length * (.08f + n * .016f))) < 140))
            }).OrderByDescending(s => s.Coverage).Select(s => s.Index).ToArray();

            void Spend()
            {
                if (battle.Towers.Count < Math.Min(8, map.Sites.Length))
                {
                    int site = sites.First(i => battle.Towers.All(t => t.Site != i));
                    var kind = (battle.Towers.Count % 6) switch
                    {
                        0 or 4 => TowerKind.Ranger, 1 => TowerKind.Guard, 3 => TowerKind.Grove, _ => TowerKind.Rune
                    };
                    battle.Build(site, kind);
                }
                else
                {
                    var tower = battle.Towers.Where(t => t.Level < 4).OrderBy(t => t.Level).ThenBy(t => t.Kind == TowerKind.Guard ? 1 : 0).FirstOrDefault();
                    if (tower is not null) battle.Upgrade(tower, tower.Kind == TowerKind.Ranger ? 0 : 1);
                    else foreach (var t in battle.Towers) battle.ImproveSkill(t);
                }
            }

            for (int i = 0; i < 8; i++) Spend();
            battle.CallWave();
            for (int frame = 0; frame < 60 * 900 && battle.State == BattleState.Playing; frame++)
            {
                if (frame % 30 == 0)
                {
                    Spend();
                    var enemy = battle.Enemies.Where(e => e.Alive).MaxBy(e => e.Distance / map.Routes[e.RouteIndex].Length);
                    if (enemy is not null)
                    {
                        if (enemy.Spec.Boss || battle.Enemies.Count(e => e.Alive && Vector2.Distance(e.Position, enemy.Position) < 100) >= 3)
                        { battle.Cast(0, enemy.Position); battle.Cast(1, enemy.Position); }
                        battle.Cast(2, enemy.Position); battle.MoveHero(enemy.Position); battle.Cast(3, battle.Hero.Position);
                    }
                }
                battle.Tick(Battle.Step);
            }
            Console.WriteLine($"  Map {map.Id + 1:00}: {battle.State}, lives {battle.Lives}, kills {battle.Kills}, time {battle.Time:0}s");
            if (battle.State != BattleState.Victory) throw new InvalidOperationException("Campaign bot could not win map " + map.Name);
        }
    }
}
