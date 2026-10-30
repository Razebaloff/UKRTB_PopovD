using System.Numerics;

namespace Verdant.Core;

/// <summary>Fixed-step simulation. No window, graphics, audio or file access.</summary>
public sealed class Battle
{
    public const float Step = 1f / 60;
    public Level Map { get; }
    public BattleMode Mode { get; }
    public int Difficulty { get; }
    public BattleState State { get; private set; }
    public List<Enemy> Enemies { get; } = [];
    public List<Tower> Towers { get; } = [];
    public List<Ally> Allies { get; } = [];
    public List<Effect> Effects { get; } = [];
    public Ally Hero { get; }
    public int Gold { get; private set; }
    public int Lives { get; private set; }
    public int Wave { get; private set; }
    public int WaveLimit => Mode == BattleMode.Heroic ? 6 : Mode == BattleMode.Iron ? 1 : Map.Waves;
    public int Kills { get; private set; }
    public int Casts { get; private set; }
    public int Experience { get; private set; }
    public int Built { get; private set; }
    public float Time { get; private set; }
    public float NextWave { get; private set; }
    public float HeroRespawn { get; private set; }
    public float[] SpellCooldown { get; } = new float[4];
    public bool CanCallWave => State == BattleState.Playing && spawnQueue.Count == 0 && (Mode == BattleMode.Endless || Wave < WaveLimit);
    public int PendingSpawns => spawnQueue.Count;
    public int Stars => State != BattleState.Victory ? 0 : Mode != BattleMode.Campaign ? 1 : Lives >= 18 ? 3 : Lives >= 10 ? 2 : 1;
    public event Action<EffectKind>? Sound;
    private readonly Queue<SpawnOrder> spawnQueue = new();
    private readonly int[] perks;
    private float waveClock;
    private int nextId;

    public Battle(Level map, BattleMode mode = BattleMode.Campaign, int difficulty = 1, int hero = 0, int[]? upgrades = null)
    {
        Map = map; Mode = mode; Difficulty = Math.Clamp(difficulty, 0, 2);
        perks = Enumerable.Range(0, 4).Select(i => Math.Clamp(upgrades is not null && i < upgrades.Length ? upgrades[i] : 0, 0, 5)).ToArray();
        Lives = mode is BattleMode.Iron or BattleMode.Heroic ? 1 : 20;
        Gold = map.Gold + perks[1] * 30 + (mode == BattleMode.Iron ? 1250 : mode == BattleMode.Heroic ? 260 : 0);
        var rally = map.Routes[0].At(map.Routes[0].Length * .7f);
        hero = Math.Clamp(hero, 0, 2);
        float hp = (hero == 1 ? 450 : 240) * (1 + perks[2] * .12f);
        Hero = new Ally { Hero = true, Archetype = hero, Position = rally, Rally = rally, Health = hp, MaxHealth = hp };
        Allies.Add(Hero);
    }

    public bool Build(int site, TowerKind kind)
    {
        if (State != BattleState.Playing || !Enum.IsDefined(kind) || site < 0 || site >= Map.Sites.Length || Towers.Any(t => t.Site == site)) return false;
        if (Mode == BattleMode.Iron && kind is TowerKind.Ranger or TowerKind.Grove) return false;
        int cost = Content.Costs[(int)kind];
        if (Gold < cost) return false;
        Gold -= cost; Built++;
        var p = Map.Sites[site];
        var t = new Tower { Site = site, Kind = kind, Position = p, Spent = cost, Rally = NearestRoad(p) };
        Towers.Add(t);
        if (kind == TowerKind.Guard) for (int i = 0; i < 3; i++) AddGuard(t, i);
        AddEffect(EffectKind.Spawn, p, p, 45); return true;
    }
    public bool Upgrade(Tower t, int branch = -1)
    {
        if (State != BattleState.Playing || !Towers.Contains(t) || t.Level >= 4 || Gold < t.UpgradeCost || (t.Level == 3 && branch is not (0 or 1))) return false;
        Gold -= t.UpgradeCost; t.Spent += t.UpgradeCost; t.Level++;
        if (t.Level == 4) t.Branch = branch;
        foreach (var a in Allies.Where(a => a.Home == t))
        {
            a.MaxHealth = GuardHealth(t); a.Health = a.MaxHealth;
        }
        AddEffect(EffectKind.Spawn, t.Position, t.Position, 60); return true;
    }
    public bool ImproveSkill(Tower t)
    {
        if (State != BattleState.Playing || !Towers.Contains(t) || t.Level < 4 || t.Skill >= 3 || Gold < t.SkillCost) return false;
        Gold -= t.SkillCost; t.Spent += t.SkillCost; t.Skill++; return true;
    }
    public bool Sell(Tower t)
    {
        if (State != BattleState.Playing || !Towers.Remove(t)) return false;
        Gold += t.Refund; Allies.RemoveAll(a => a.Home == t); return true;
    }
    public Vector2 NearestRoad(Vector2 point) => Map.Routes.Select(r => r.Nearest(point)).MinBy(q => Vector2.DistanceSquared(q, point));
    public bool SetRally(Tower t, Vector2 point)
    {
        var p = NearestRoad(point);
        if (!Towers.Contains(t) || t.Kind != TowerKind.Guard || Vector2.Distance(t.Position, p) > t.Range) return false;
        t.Rally = p;
        int i = 0;
        foreach (var a in Allies.Where(a => a.Home == t)) { a.Rally = p + new Vector2((i++ - 1) * 13, 4); a.TargetId = null; }
        return true;
    }
    public void MoveHero(Vector2 point)
    {
        if (State == BattleState.Playing && Hero.Alive) { Hero.Rally = NearestRoad(point); Hero.TargetId = null; }
    }

    public bool CallWave()
    {
        if (!CanCallWave) return false;
        if (Wave > 0 && NextWave > 0) { int bonus = (int)NextWave + 8; Gold += bonus; AddEffect(EffectKind.Gold, new(470, 160), new(470, 160), 0, $"Ранний вызов +{bonus}"); }
        Wave++; waveClock = 0; NextWave = 0;
        foreach (var s in PlanWave(Wave)) spawnQueue.Enqueue(s);
        Sound?.Invoke(EffectKind.Spawn); return true;
    }
    public IReadOnlyList<SpawnOrder> PlanWave(int wave)
    {
        var list = new List<SpawnOrder>();
        int effective = Mode == BattleMode.Heroic ? wave + 5 : Mode == BattleMode.Iron ? Map.Waves + 2 : wave;
        int count = Mode == BattleMode.Iron ? 65 + Map.Id * 3 : 8 + effective * 2 + Map.Id;
        int tier = Math.Min(8, 1 + Map.Id / 2 + effective / 3);
        for (int i = 0; i < count; i++)
        {
            var kind = (EnemyKind)((i * 7 + effective * 3 + Map.Id) % (tier + 1));
            if (Map.Id == 0 && wave <= 2) kind = i % 3 == 0 ? EnemyKind.Hound : EnemyKind.Raider;
            list.Add(new(i * (Mode == BattleMode.Iron ? .65f : .83f), kind, i % Map.Routes.Length));
        }
        if (Map.Boss is { } boss && Mode == BattleMode.Campaign && wave == WaveLimit) list.Add(new(count * .83f + 1, boss, 0));
        if (Mode == BattleMode.Endless && wave % 10 == 0) list.Add(new(count * .83f + 1, (EnemyKind)(9 + wave / 10 % 3), 0));
        return list.OrderBy(x => x.Time).ToArray();
    }

    public bool Cast(int spell, Vector2 point)
    {
        if (State != BattleState.Playing || spell < 0 || spell > 3 || SpellCooldown[spell] > 0 || (spell == 3 && !Hero.Alive)) return false;
        if (point.X < 0 || point.X > 960 || point.Y < 75 || point.Y > 625) return false;
        if (spell == 0)
        {
            foreach (var e in Enemies.Where(e => e.Alive && Vector2.Distance(e.Position, point) <= 100).ToArray()) Hit(e, 210, DamageKind.Pure);
            AddEffect(EffectKind.Burst, point - new Vector2(40, 250), point, 100);
        }
        else if (spell == 1)
        {
            foreach (var e in Enemies.Where(e => e.Alive && Vector2.Distance(e.Position, point) <= 120)) Slow(e, e.Spec.Boss ? .55f : .15f, 4);
            AddEffect(EffectKind.Frost, point, point, 120);
        }
        else if (spell == 2)
        {
            point = NearestRoad(point);
            for (int i = -1; i <= 1; i++)
            {
                float hp = 140 * (1 + perks[2] * .12f);
                Allies.Add(new Ally { Position = point + new Vector2(i * 14, 0), Rally = point + new Vector2(i * 14, 0), Health = hp, MaxHealth = hp, Life = 22 });
            }
            AddEffect(EffectKind.Spawn, point, point, 55);
        }
        else
        {
            point = Hero.Position;
            if (Hero.Archetype == 1)
                foreach (var a in Allies.Where(a => a.Alive && Vector2.Distance(a.Position, point) < 180)) a.Health = Math.Min(a.MaxHealth, a.Health + 220);
            var targets = Enemies.Where(e => e.Alive && Vector2.Distance(e.Position, point) <= (Hero.Archetype == 0 ? 220 : 130)).OrderBy(e => -e.Distance).ToArray();
            if (Hero.Archetype == 0)
                for (int i = 0; i < 7 && targets.Length > 0; i++) { var e = targets[i % targets.Length]; Hit(e, 60 + Hero.Level * 5, DamageKind.Physical); AddEffect(EffectKind.Arrow, point, e.Position, 0); }
            else foreach (var e in targets) Hit(e, 160 + Hero.Level * 12, DamageKind.Pure);
            AddEffect(Hero.Archetype == 1 ? EffectKind.Heal : EffectKind.Burst, point, point, 130);
        }
        SpellCooldown[spell] = new float[] { 32, 24, 18, 25 }[spell] * (1 - .05f * perks[3]);
        Casts++; Sound?.Invoke(spell == 1 ? EffectKind.Frost : EffectKind.Burst); return true;
    }

    public void Tick(float dt)
    {
        if (State != BattleState.Playing || !float.IsFinite(dt) || dt <= 0) return;
        dt = Math.Min(dt, .05f); Time += dt;
        foreach (var fx in Effects) fx.Time -= dt;
        Effects.RemoveAll(fx => fx.Time <= 0);
        for (int i = 0; i < 4; i++) SpellCooldown[i] = Math.Max(0, SpellCooldown[i] - dt);
        waveClock += dt;
        while (spawnQueue.TryPeek(out var s) && s.Time <= waveClock) { spawnQueue.Dequeue(); Spawn(s.Kind, s.Route); }
        TickAllies(dt);
        TickTowers(dt);
        TickEnemies(dt);
        Enemies.RemoveAll(e => e.Removed);
        Allies.RemoveAll(a => !a.Hero && !a.Alive);
        if (Lives <= 0) { Lives = 0; State = BattleState.Defeat; return; }
        if (Wave > 0 && spawnQueue.Count == 0)
        {
            if (Mode != BattleMode.Endless && Wave >= WaveLimit && Enemies.Count == 0) { State = BattleState.Victory; return; }
            if (CanCallWave)
            {
                if (NextWave == 0) NextWave = 16;
                NextWave -= dt;
                if (NextWave <= 0) { NextWave = 0; CallWave(); }
            }
        }
    }

    private void TickTowers(float dt)
    {
        foreach (var t in Towers)
        {
            if (t.Kind == TowerKind.Guard)
            {
                t.Respawn -= dt;
                int alive = Allies.Count(a => a.Alive && a.Home == t);
                if (alive < 3 && t.Respawn <= 0) { AddGuard(t, alive); t.Respawn = 7 - t.Skill; }
                continue;
            }
            t.Cooldown -= dt;
            if (t.Cooldown > 0) continue;
            var target = Enemies.Where(e => e.Alive && (!e.Spec.Flying || t.Kind != TowerKind.Grove) && Vector2.Distance(e.Position, t.Position) <= t.Range).MaxBy(e => e.Distance / Map.Routes[e.RouteIndex].Length);
            if (target is null) continue;
            t.Cooldown = t.Interval / (1 + t.Skill * .12f);
            float damage = t.Damage * (1 + perks[0] * .05f);
            var kind = t.Kind == TowerKind.Rune ? DamageKind.Magic : DamageKind.Physical;
            if (t.Kind == TowerKind.Grove)
            {
                float radius = t.Branch == 1 ? 80 : 55;
                foreach (var e in Enemies.Where(e => e.Alive && !e.Spec.Flying && Vector2.Distance(e.Position, target.Position) <= radius).ToArray())
                {
                    Hit(e, damage, kind);
                    if (t.Branch == 0) Poison(e, 16 + t.Skill * 8, 3);
                    if (t.Branch == 1) Slow(e, .45f, 2 + t.Skill * .5f);
                }
                AddEffect(EffectKind.Rock, t.Position, target.Position, radius);
            }
            else
            {
                Hit(target, damage, kind);
                AddEffect(t.Kind == TowerKind.Ranger ? EffectKind.Arrow : EffectKind.Magic, t.Position, target.Position, 0);
                if (t.Kind == TowerKind.Ranger && t.Branch == 0) Poison(target, 10 + t.Skill * 7, 4);
                if (t.Kind == TowerKind.Rune && t.Branch == 1) Slow(target, .4f, 2 + t.Skill * .4f);
                if (t.Kind == TowerKind.Rune && t.Branch == 0)
                    foreach (var e in Enemies.Where(e => e.Alive && e != target && Vector2.Distance(e.Position, target.Position) < 100).OrderBy(e => Vector2.DistanceSquared(e.Position, target.Position)).Take(2 + t.Skill).ToArray())
                    { Hit(e, damage * .6f, kind); AddEffect(EffectKind.Magic, target.Position, e.Position, 0); }
            }
        }
    }
    private void TickAllies(float dt)
    {
        if (!Hero.Alive)
        {
            HeroRespawn -= dt;
            if (HeroRespawn <= 0) { Hero.Health = Hero.MaxHealth; Hero.Position = Hero.Rally; Hero.TargetId = null; AddEffect(EffectKind.Heal, Hero.Position, Hero.Position, 60); }
        }
        var occupied = new HashSet<int>();
        foreach (var a in Allies.Where(a => a.Alive).ToArray())
        {
            a.Life -= dt; a.Cooldown -= dt; a.Stun = Math.Max(0, a.Stun - dt);
            if (a.Stun > 0) { a.TargetId = null; continue; }
            bool ranged = a.Hero && a.Archetype != 1;
            var target = Enemies.Where(e => e.Alive && (!e.Spec.Flying || ranged) && (ranged || !occupied.Contains(e.Id)) && Vector2.Distance(e.Position, a.Rally) <= (ranged ? a.Range : 60)).MaxBy(e => e.Distance);
            a.TargetId = target?.Id;
            if (target is not null)
            {
                if (!ranged) occupied.Add(target.Id);
                if (Vector2.Distance(a.Position, target.Position) <= a.Range + 5)
                {
                    if (a.Cooldown <= 0)
                    {
                        a.Cooldown = a.Home?.Branch == 1 ? .55f : .9f;
                        Hit(target, a.Damage, a.Hero && a.Archetype == 2 ? DamageKind.Magic : DamageKind.Physical);
                        AddEffect(ranged ? a.Archetype == 2 ? EffectKind.Magic : EffectKind.Arrow : EffectKind.Slash, a.Position, target.Position, 15);
                    }
                }
                else a.Position = Move(a.Position, ranged ? a.Rally : target.Position, 85 * dt);
            }
            else
            {
                a.Position = Move(a.Position, a.Rally, (a.Hero ? 145 : 90) * dt);
                a.Health = Math.Min(a.MaxHealth, a.Health + dt * (a.Hero ? 9 : 6));
            }
        }
    }
    private void TickEnemies(float dt)
    {
        foreach (var e in Enemies.ToArray())
        {
            if (!e.Alive) continue;
            if (e.PoisonTime > 0) { e.PoisonTime -= dt; Hit(e, e.PoisonDps * dt, DamageKind.Pure); }
            if (!e.Alive) continue;
            e.SlowTime = Math.Max(0, e.SlowTime - dt);
            if (e.SlowTime == 0) e.SlowFactor = 1;
            e.AttackTimer -= dt; e.AbilityTimer -= dt;
            if (e.Kind == EnemyKind.Brute) e.Health = Math.Min(e.MaxHealth, e.Health + 8 * dt);
            if (e.AbilityTimer <= 0)
            {
                e.AbilityTimer = e.Spec.Boss ? 7 : e.Kind == EnemyKind.Shaman ? 3 : 5;
                if (e.Kind == EnemyKind.Shaman)
                {
                    foreach (var other in Enemies.Where(o => o.Alive && Vector2.Distance(o.Position, e.Position) < 95)) other.Health = Math.Min(other.MaxHealth, other.Health + 30);
                    AddEffect(EffectKind.Heal, e.Position, e.Position, 95);
                }
                if (e.Kind == EnemyKind.Wraith) { e.Distance += 65; AddEffect(EffectKind.Magic, e.Position, Map.Routes[e.RouteIndex].At(e.Distance), 20); }
                if (e.Kind is EnemyKind.Oracle or EnemyKind.Sovereign)
                    for (int i = 0; i < 2; i++) Spawn(e.Kind == EnemyKind.Oracle ? EnemyKind.Wisp : EnemyKind.Shield, e.RouteIndex, Math.Max(0, e.Distance - i * 22));
                if (e.Kind is EnemyKind.Colossus or EnemyKind.Sovereign)
                {
                    foreach (var a in Allies.Where(a => a.Alive && Vector2.Distance(a.Position, e.Position) < 90).ToArray()) { HurtAlly(a, 70); a.Stun = 1.5f; }
                    AddEffect(EffectKind.Burst, e.Position, e.Position, 90);
                }
            }
            var blocker = e.Spec.Flying ? null : Allies.FirstOrDefault(a => a.Alive && a.Stun <= 0 && (!a.Hero || a.Archetype == 1) && a.TargetId == e.Id && Vector2.Distance(a.Position, e.Position) < 35);
            if (blocker is not null)
            {
                if (e.AttackTimer <= 0) { e.AttackTimer = 1.1f; HurtAlly(blocker, e.Spec.Attack); }
            }
            else e.Distance += e.Spec.Speed * e.SlowFactor * dt;
            e.Position = Map.Routes[e.RouteIndex].At(e.Distance);
            if (e.Distance >= Map.Routes[e.RouteIndex].Length)
            {
                Lives -= e.Spec.Boss ? 20 : 1; e.Removed = true;
                AddEffect(EffectKind.Leak, e.Position, e.Position, 30); Sound?.Invoke(EffectKind.Leak);
            }
        }
    }
    private void HurtAlly(Ally a, float damage)
    {
        if (a.Home?.Branch == 1) damage *= .8f;
        a.Health -= damage * (1 - a.Armor);
        if (!a.Alive && a.Hero) { HeroRespawn = 15; a.Health = 0; }
    }
    private void Hit(Enemy e, float damage, DamageKind kind)
    {
        if (!e.Alive) return;
        e.Health -= damage * (kind == DamageKind.Physical ? 1 - e.Spec.Armor : kind == DamageKind.Magic ? 1 - e.Spec.Resist : 1);
        if (e.Health > 0) return;
        e.Removed = true; Gold += e.Spec.Bounty; Kills++; Experience += e.Spec.Boss ? 90 : 5;
        AddEffect(EffectKind.Gold, e.Position, e.Position, 0, $"+{e.Spec.Bounty}");
        if (e.Kind == EnemyKind.Brood) for (int i = 0; i < 3; i++) Spawn(EnemyKind.Hound, e.RouteIndex, Math.Max(0, e.Distance - i * 16));
        int level = Math.Min(10, 1 + Experience / 120);
        if (Hero.Level < level)
        {
            Hero.Level = level; Hero.MaxHealth += 30;
            if (Hero.Alive) Hero.Health = Hero.MaxHealth;
            AddEffect(EffectKind.Heal, Hero.Position, Hero.Position, 70, "Уровень героя!");
        }
    }
    private void Spawn(EnemyKind kind, int route, float distance = 0)
    {
        float multiplier = new float[] { .78f, 1, 1.25f }[Difficulty] * (1 + Map.Id * .045f);
        if (Mode is BattleMode.Heroic or BattleMode.Iron) multiplier *= 1.12f;
        if (Mode == BattleMode.Endless) multiplier *= MathF.Pow(1.065f, Math.Max(0, Wave - 1));
        float hp = Content.Enemies[(int)kind].Health * multiplier;
        Enemies.Add(new Enemy { Id = nextId++, Kind = kind, RouteIndex = route, Distance = distance, Position = Map.Routes[route].At(distance), Health = hp, MaxHealth = hp });
    }
    private float GuardHealth(Tower t) => (135 + (t.Level - 1) * 75) * (t.Branch == 0 ? 1.7f : 1) * (1 + t.Skill * .12f) * (1 + perks[2] * .12f);
    private void AddGuard(Tower t, int index)
    {
        var rally = t.Rally + new Vector2((index - 1) * 13, 4);
        Allies.Add(new Ally { Home = t, Position = t.Position, Rally = rally, Health = GuardHealth(t), MaxHealth = GuardHealth(t) });
    }
    private static void Poison(Enemy e, float dps, float duration) { e.PoisonDps = Math.Max(e.PoisonDps, dps); e.PoisonTime = Math.Max(e.PoisonTime, duration); }
    private static void Slow(Enemy e, float factor, float duration) { e.SlowFactor = Math.Min(e.SlowFactor, factor); e.SlowTime = Math.Max(e.SlowTime, duration); }
    private static Vector2 Move(Vector2 from, Vector2 to, float distance) => Vector2.Distance(from, to) <= distance ? to : from + Vector2.Normalize(to - from) * distance;
    private void AddEffect(EffectKind kind, Vector2 from, Vector2 to, float radius, string text = "")
    {
        float duration = kind == EffectKind.Gold ? 1.05f : kind is EffectKind.Burst or EffectKind.Heal or EffectKind.Frost ? .75f : .25f;
        Effects.Add(new Effect { Kind = kind, From = from, To = to, Radius = radius, Text = text, Time = duration, Duration = duration });
        if (Effects.Count > 350) Effects.RemoveAt(0);
        if (kind is EffectKind.Arrow or EffectKind.Magic or EffectKind.Rock) Sound?.Invoke(kind);
    }
}
