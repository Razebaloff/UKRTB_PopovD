using System.Numerics;

namespace Verdant.Core;

public sealed class Enemy
{
    public int Id { get; init; }
    public EnemyKind Kind { get; init; }
    public EnemySpec Spec => Content.Enemies[(int)Kind];
    public int RouteIndex { get; init; }
    public float Distance { get; set; }
    public Vector2 Position { get; set; }
    public float Health { get; set; }
    public float MaxHealth { get; init; }
    public float SlowTime { get; set; }
    public float SlowFactor { get; set; } = 1;
    public float PoisonTime { get; set; }
    public float PoisonDps { get; set; }
    public float AttackTimer { get; set; }
    public float AbilityTimer { get; set; } = 3;
    public bool Removed { get; set; }
    public bool Alive => !Removed && Health > 0;
}

public sealed class Tower
{
    public int Site { get; init; }
    public TowerKind Kind { get; init; }
    public Vector2 Position { get; init; }
    public Vector2 Rally { get; set; }
    public int Level { get; set; } = 1;
    public int Branch { get; set; } = -1;
    public int Skill { get; set; }
    public int Spent { get; set; }
    public float Cooldown { get; set; }
    public float Respawn { get; set; }
    public float Range => (Kind switch { TowerKind.Ranger => 142, TowerKind.Rune => 130, TowerKind.Grove => 120, _ => 110 }) + (Level - 1) * 12 + (Kind == TowerKind.Ranger && Branch == 1 ? 90 : 0);
    public float Damage => (Kind switch { TowerKind.Ranger => 14, TowerKind.Rune => 30, TowerKind.Grove => 43, _ => 12 }) * MathF.Pow(1.52f, Level - 1) * (Kind == TowerKind.Ranger && Branch == 1 ? 2 : 1);
    public float Interval => Kind switch { TowerKind.Ranger => Branch == 1 ? 1.35f : .65f, TowerKind.Rune => 1.25f, TowerKind.Grove => 2.1f, _ => .9f };
    public int UpgradeCost => 65 + (int)Kind * 12 + Level * 55;
    public int SkillCost => 120 + Skill * 85;
    public int Refund => (int)(Spent * .7f);
    public string Name => Branch < 0 ? Content.TowerNames[(int)Kind] : Content.Branches[(int)Kind][Branch];
}

public sealed class Ally
{
    public Vector2 Position { get; set; }
    public Vector2 Rally { get; set; }
    public Tower? Home { get; init; }
    public bool Hero { get; init; }
    public int Archetype { get; init; }
    public int Level { get; set; } = 1;
    public float Health { get; set; }
    public float MaxHealth { get; set; }
    public float Cooldown { get; set; }
    public float Life { get; set; } = float.PositiveInfinity;
    public float Stun { get; set; }
    public int? TargetId { get; set; }
    public bool Alive => Health > 0 && Life > 0;
    public float Range => Hero ? Archetype switch { 0 => 130, 2 => 115, _ => 34 } : 28;
    public float Damage => Hero ? (Archetype == 1 ? 24 : 21) + Level * 5 : (Home?.Damage ?? 16) * (Home?.Branch == 1 ? 1.5f : 1);
    public float Armor => Hero && Archetype == 1 ? .35f : Home?.Branch == 0 ? .45f : 0;
}

public sealed class Effect
{
    public EffectKind Kind { get; init; }
    public Vector2 From { get; init; }
    public Vector2 To { get; init; }
    public float Radius { get; init; }
    public string Text { get; init; } = "";
    public float Time { get; set; }
    public float Duration { get; init; }
}

public sealed record SpawnOrder(float Time, EnemyKind Kind, int Route);
