using System.Numerics;

namespace Verdant.Core;

public enum TowerKind { Ranger, Rune, Grove, Guard }
public enum EnemyKind { Raider, Hound, Shield, Wisp, Shaman, Beetle, Brute, Wraith, Brood, Colossus, Oracle, Sovereign }
public enum BattleMode { Campaign, Heroic, Iron, Endless }
public enum BattleState { Playing, Victory, Defeat }
public enum DamageKind { Physical, Magic, Pure }
public enum EffectKind { Arrow, Magic, Rock, Slash, Burst, Frost, Heal, Gold, Spawn, Leak }

public sealed class Route
{
    public Vector2[] Points { get; }
    public float Length { get; }
    public Route(params Vector2[] points)
    {
        if (points.Length < 2) throw new ArgumentException("Route needs two points.");
        Points = points;
        for (int i = 1; i < points.Length; i++) Length += Vector2.Distance(points[i - 1], points[i]);
    }
    public Vector2 At(float distance)
    {
        if (distance <= 0) return Points[0];
        if (distance >= Length) return Points[^1];
        for (int i = 1; i < Points.Length; i++)
        {
            float segment = Vector2.Distance(Points[i - 1], Points[i]);
            if (distance <= segment) return Vector2.Lerp(Points[i - 1], Points[i], segment == 0 ? 0 : distance / segment);
            distance -= segment;
        }
        return Points[^1];
    }
    public Vector2 Nearest(Vector2 p)
    {
        var best = Points[0]; float min = float.MaxValue;
        for (int i = 1; i < Points.Length; i++)
        {
            var a = Points[i - 1]; var d = Points[i] - a;
            var q = a + d * Math.Clamp(Vector2.Dot(p - a, d) / Math.Max(.01f, d.LengthSquared()), 0, 1);
            float dist = Vector2.DistanceSquared(p, q);
            if (dist < min) { min = dist; best = q; }
        }
        return best;
    }
}

public sealed record Level(int Id, string Name, string Story, int Theme, Route[] Routes, Vector2[] Sites, int Waves, int Gold, EnemyKind? Boss);
public sealed record EnemySpec(string Name, float Health, float Speed, int Bounty, float Armor, float Resist, bool Flying, float Attack, string Tip)
{
    public bool Boss => Health >= 2000;
}

public static class Content
{
    public static readonly string[] TowerNames = ["Следопыты", "Рунный шпиль", "Каменный круг", "Стража"];
    public static readonly string[] TowerTips = ["Быстрые стрелы. Атакуют воздух. Броня снижает урон.", "Магический урон против брони. Атакует воздух.", "Урон по площади. Только наземные цели.", "Три бойца задерживают наземных врагов. Меняйте точку сбора."];
    public static readonly int[] Costs = [85, 110, 135, 90];
    public static readonly string[][] Branches = [["Ядовитые стрелы", "Дальний выстрел"], ["Цепная молния", "Ледяные руны"], ["Вулканический круг", "Корни земли"], ["Рыцари рассвета", "Лунные клинки"]];
    public static readonly string[][] BranchTips = [["Яд: 10 ед./с, игнорирует броню.", "+90 к дальности, двойной урон."], ["Цепь поражает ещё две цели.", "Замедление на 60% на 2 секунды."], ["Взрыв поджигает врагов.", "Большая площадь и замедление."], ["Больше здоровья и броня бойцов.", "Быстрые атаки и уклонение бойцов."]];
    public static readonly string[] HeroNames = ["Эйра • следопыт", "Бран • хранитель", "Лиора • чародей"];
    public static readonly string[] HeroTips = ["Дальний бой. Умение: град из 7 стрел.", "Ближний бой, много здоровья. Умение: исцеление союзников и удар по земле.", "Магия и атака воздуха. Умение: огненный круг с чистым уроном."];
    public static readonly string[] PerkNames = ["Острые клинки", "Запасы золота", "Живой источник", "Знание рун"];
    public static readonly string[] PerkTips = ["+5% к урону башен за ранг", "+30 золота в начале боя за ранг", "+12% к здоровью союзников за ранг", "−5% к откату заклинаний за ранг"];
    public static readonly EnemySpec[] Enemies =
    [
        new("Пепельный налётчик", 64, 40, 9, 0, 0, false, 10, "Обычный пехотинец. Стража удерживает его под огнём."),
        new("Шипастая гончая", 45, 77, 8, 0, 0, false, 8, "Быстрая. Ставьте бойцов и замедляющие башни."),
        new("Щитоносец", 160, 29, 15, .65f, 0, false, 17, "Броня 65%. Используйте рунные шпили."),
        new("Болотный огонёк", 76, 49, 11, 0, .2f, true, 0, "Летает: бойцы и каменные круги его не атакуют."),
        new("Пепельный шаман", 140, 31, 20, 0, .5f, false, 13, "Раз в 3 секунды лечит соседей. Магическая защита 50%."),
        new("Обсидиановый жук", 280, 24, 23, .5f, .3f, false, 22, "Высокая живучесть. Яд и чистый урон особенно полезны."),
        new("Лесной громила", 450, 27, 30, .15f, 0, false, 35, "Восстанавливает здоровье. Нужен сосредоточенный огонь."),
        new("Зеркальная тень", 120, 56, 18, 0, .7f, false, 17, "Каждые 5 секунд прыгает вперёд. Магическая защита 70%."),
        new("Матка роя", 350, 25, 32, .2f, .2f, false, 26, "После гибели оставляет трёх гончих."),
        new("Каменный исполин", 2600, 18, 180, .35f, .15f, false, 90, "Босс. Топот ранит ближайших бойцов. Пропуск = поражение."),
        new("Слепой оракул", 3200, 19, 220, .1f, .55f, false, 70, "Босс. Призывает огоньки. Пропуск = поражение."),
        new("Владыка пепла", 5200, 16, 350, .35f, .35f, false, 110, "Последний босс. Призывает щитоносцев и оглушает бойцов.")
    ];
    public static readonly Level[] Levels = CreateLevels();

    private static Route R(params float[] xy) => new(Enumerable.Range(0, xy.Length / 2).Select(i => new Vector2(xy[i * 2], xy[i * 2 + 1])).ToArray());
    private static Level[] CreateLevels()
    {
        Route[][] routes =
        [
            [R(-30,210, 170,210, 270,340, 520,340, 640,470, 990,470)],
            [R(-30,440, 190,440, 190,210, 430,210, 570,400, 790,400, 990,240)],
            [R(-30,190, 230,190, 370,320, 560,320, 700,490, 990,490), R(-30,510, 220,510, 370,320, 560,320, 700,490, 990,490)],
            [R(-30,330, 150,330, 270,160, 480,160, 480,490, 740,490, 850,320, 990,320)],
            [R(160,70, 160,260, 350,260, 350,470, 650,470, 650,230, 990,230), R(-30,500, 160,500, 160,260, 350,260, 350,470, 650,470, 650,230, 990,230)],
            [R(-30,170, 300,170, 300,400, 570,400, 570,230, 790,230, 790,490, 990,490)],
            [R(-30,490, 230,490, 230,180, 480,180, 620,350, 830,350, 990,200)],
            [R(-30,180, 210,180, 360,330, 600,330, 760,510, 990,510), R(-30,520, 210,520, 360,330, 600,330, 760,510, 990,510)],
            [R(200,70, 200,220, 420,220, 420,460, 690,460, 820,300, 990,300), R(-30,460, 200,460, 200,220, 420,220, 420,460, 690,460, 820,300, 990,300)],
            [R(-30,300, 120,300, 240,160, 440,160, 440,510, 670,510, 800,330, 990,330)],
            [R(-30,180, 200,180, 200,420, 430,420, 580,230, 800,230, 990,400), R(-30,520, 200,520, 200,420, 430,420, 580,230, 800,230, 990,400)],
            [R(-30,180, 190,180, 320,310, 510,310, 640,490, 840,490, 990,350), R(-30,520, 190,520, 320,310, 510,310, 640,490, 840,490, 990,350)]
        ];
        string[] names = ["Тропа росы", "Старый брод", "Развилка ветров", "Корни великана", "Туманные чаши", "Храм отголосков", "Хрустальный склон", "Осколки луны", "Обсерватория", "Угольный перевал", "Последний рубеж", "Сердце пепла"];
        string[] stories = ["Дозор заметил первых налётчиков. Защитите дорогу к роще.", "Враг обошёл заставу через старую переправу.", "Две тропы ведут к воротам. Следите за обоими входами.", "Древний исполин пробудился под корнями. Не дайте ему пройти.", "Над топями появились огоньки, а шаманы лечат раненых.", "В тесных коридорах храма решает правильная расстановка.", "Зеркальные тени скрываются среди кристаллов.", "Рой идёт с двух сторон. Его матки оставляют потомство.", "Слепой оракул ведёт духов к обсерватории.", "Дорога к вулкану занята бронированными отрядами.", "Вся армия пепла собралась у последней заставы.", "Остановите Владыку пепла и верните лесу рассвет."];
        return Enumerable.Range(0, 12).Select(i => new Level(i, names[i], stories[i], i / 3, routes[i], Sites(routes[i], i), 6 + i / 2 + (i == 11 ? 1 : 0), 390 + i * 35, i switch { 3 => EnemyKind.Colossus, 8 => EnemyKind.Oracle, 11 => EnemyKind.Sovereign, _ => null })).ToArray();
    }
    private static Vector2[] Sites(Route[] routes, int seed)
    {
        var points = new List<Vector2>();
        // Deterministic building plots, always on solid ground near a road.
        for (int y = 135; y <= 565; y += 86)
        for (int x = 95; x <= 880; x += 96)
        {
            var p = new Vector2(x + (seed % 2) * 12, y);
            float d = routes.Min(r => Vector2.Distance(p, r.Nearest(p)));
            if (d is >= 49 and <= 119 && points.All(q => Vector2.Distance(p, q) >= 85)) points.Add(p);
        }
        return points.Take(17).ToArray();
    }
}
