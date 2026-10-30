using System.Drawing.Drawing2D;
using System.Numerics;
using Verdant.Core;

namespace Verdant.Game;

internal sealed partial class GameWindow
{
    private Bitmap? terrain;
    private void MakeTerrain(Level map)
    {
        terrain?.Dispose(); terrain = new Bitmap(960, 640);
        using var g = Graphics.FromImage(terrain); g.SmoothingMode = SmoothingMode.AntiAlias;
        Color[] ground = [Color.FromArgb(64, 101, 65), Color.FromArgb(54, 89, 79), Color.FromArgb(64, 78, 101), Color.FromArgb(83, 67, 64)];
        Color[] dark = [Color.FromArgb(37, 68, 48), Color.FromArgb(32, 57, 59), Color.FromArgb(40, 47, 71), Color.FromArgb(47, 40, 45)];
        using var gradient = new LinearGradientBrush(new Rectangle(0, 0, 960, 640), ground[map.Theme], dark[map.Theme], 70);
        g.FillRectangle(gradient, 0, 0, 960, 640);
        var random = new Random(9481 + map.Id);
        for (int i = 0; i < 1900; i++)
        {
            float x = random.Next(960), y = random.Next(640), r = random.Next(1, 4);
            Art.Ellipse(g, Color.FromArgb(random.Next(8, 27), random.Next(2) == 0 ? Art.Cream : Color.Black), x, y, r, r);
        }
        // Pools only occupy unbuildable ground, never a road or building site.
        for (int i = 0; i < 20; i++)
        {
            var p = new Vector2(random.Next(50, 900), random.Next(110, 570));
            if (map.Routes.Min(r => Vector2.Distance(p, r.Nearest(p))) < 110 || map.Sites.Any(s => Vector2.Distance(p, s) < 80)) continue;
            Art.Ellipse(g, Color.FromArgb(85, 22, 54, 61), p.X - 63, p.Y - 30, 126, 60);
            Art.Ellipse(g, Color.FromArgb(65, 48, 113, 119), p.X - 55, p.Y - 25, 110, 49);
            for (int j = 0; j < 4; j++) Art.Line(g, Color.FromArgb(36, 172, 221, 208), 1, new(p.X - 25 + j * 7, p.Y - 13 + j * 10), new(p.X + 22 + j * 5, p.Y - 13 + j * 10));
        }
        foreach (var route in map.Routes)
        {
            var points = route.Points.Select(Art.P).ToArray();
            Art.Line(g, Color.FromArgb(72, 10, 18, 20), 64, points);
            Art.Line(g, Color.FromArgb(92, 95, 70), 52, points);
            Art.Line(g, map.Theme == 3 ? Color.FromArgb(157, 119, 92) : Color.FromArgb(171, 156, 110), 44, points);
            Art.Line(g, Color.FromArgb(40, 231, 213, 156), 24, points);
            for (float d = 15; d < route.Length; d += 24)
            {
                var p = route.At(d) + new Vector2(random.Next(-15, 16), random.Next(-15, 16));
                Art.Ellipse(g, Color.FromArgb(40, 93, 71, 49), p.X, p.Y, random.Next(2, 5), 2);
            }
        }
        for (int i = 0; i < 95; i++)
        {
            var p = new Vector2(random.Next(15, 945), random.Next(82, 640));
            if (map.Routes.Min(r => Vector2.Distance(p, r.Nearest(p))) < 65 || map.Sites.Any(s => Vector2.Distance(p, s) < 55)) continue;
            if (map.Theme == 2 && i % 3 == 0)
            {
                Color crystal = Color.FromArgb(113, 156, 186);
                Art.Polygon(g, crystal, new(p.X - 9, p.Y), new(p.X - 6, p.Y - 27), new(p.X + 2, p.Y - 39), new(p.X + 11, p.Y - 17), new(p.X + 7, p.Y));
                Art.Line(g, Color.FromArgb(180, 195, 234, 239), 1, new(p.X + 2, p.Y - 36), new(p.X + 2, p.Y - 4));
            }
            else if (map.Theme == 3 && i % 3 == 0)
            {
                Art.Polygon(g, Color.FromArgb(47, 43, 49), new(p.X - 18, p.Y), new(p.X - 9, p.Y - 20), new(p.X + 9, p.Y - 14), new(p.X + 18, p.Y + 3));
                Art.Line(g, Color.FromArgb(201, 104, 65), 2, new(p.X - 5, p.Y - 15), new(p.X, p.Y - 7), new(p.X + 9, p.Y - 3));
            }
            else Art.Tree(g, p.X, p.Y, .45f + (float)random.NextDouble() * .48f, map.Theme == 3 ? Color.FromArgb(44, 53, 49) : map.Theme == 2 ? Color.FromArgb(40, 76, 81) : Color.FromArgb(30 + random.Next(16), 71 + random.Next(26), 57 + random.Next(15)));
        }
        foreach (var p in map.Sites)
        {
            Art.Ellipse(g, Color.FromArgb(65, 0, 0, 0), p.X - 30, p.Y - 19, 61, 44);
            Art.Ellipse(g, Color.FromArgb(108, 111, 84), p.X - 26, p.Y - 22, 52, 38);
            Art.Ellipse(g, Color.FromArgb(66, 86, 65), p.X - 22, p.Y - 18, 44, 30);
            using var pen = new Pen(Color.FromArgb(130, 208, 196, 138), 1) { DashStyle = DashStyle.Dash };
            g.DrawEllipse(pen, p.X - 23, p.Y - 20, 46, 34);
            Art.Text(g, "+", p.X - 15, p.Y - 20, 26, Art.Alpha(Art.Cream, .7f), false, 30, 40, true);
        }
        foreach (var route in map.Routes)
        {
            var p = route.At(50);
            Art.Line(g, Art.Gold, 3, new(p.X, p.Y - 35), new(p.X, p.Y - 4));
            Art.Polygon(g, Color.FromArgb(188, 88, 68), new(p.X + 1, p.Y - 35), new(p.X + 23, p.Y - 28), new(p.X + 1, p.Y - 20));
        }
        var exit = map.Routes[0].At(map.Routes[0].Length - 55);
        Art.Fill(g, Color.FromArgb(152, 163, 138), exit.X - 24, exit.Y - 58, 11, 60);
        Art.Fill(g, Color.FromArgb(132, 147, 128), exit.X + 19, exit.Y - 58, 11, 60);
        Art.Fill(g, Color.FromArgb(171, 180, 144), exit.X - 24, exit.Y - 62, 54, 12);
        Art.Polygon(g, Art.Mint, new(exit.X + 4, exit.Y - 68), new(exit.X + 13, exit.Y - 53), new(exit.X + 4, exit.Y - 40), new(exit.X - 5, exit.Y - 53));
    }
    private void DrawBattle(Graphics g)
    {
        if (battle is not { } b) return;
        if (terrain is not null) g.DrawImageUnscaled(terrain, 0, 0);
        var world = g.Save(); g.SetClip(new RectangleF(0, 75, 960, 550));
        var tower = b.Towers.Find(t => t.Site == selectedSite);
        if (tower is not null)
        {
            Art.Ellipse(g, Color.FromArgb(20, 188, 235, 178), tower.Position.X - tower.Range, tower.Position.Y - tower.Range, tower.Range * 2, tower.Range * 2);
            using var pen = new Pen(Color.FromArgb(100, 193, 224, 161), 1) { DashStyle = DashStyle.Dash };
            g.DrawEllipse(pen, tower.Position.X - tower.Range, tower.Position.Y - tower.Range, tower.Range * 2, tower.Range * 2);
            if (tower.Kind == TowerKind.Guard) Art.Line(g, Art.Alpha(Art.Gold, .55f), 1, Art.P(tower.Position), Art.P(tower.Rally));
        }
        if (selectedSite >= 0)
        {
            var p = b.Map.Sites[selectedSite];
            using var pen = new Pen(Art.Gold, 2); g.DrawEllipse(pen, p.X - 31, p.Y - 27, 62, 47);
        }
        var objects = new List<(float Y, Action Draw)>();
        foreach (var t in b.Towers) objects.Add((t.Position.Y, () => Art.Tower(g, t.Position, (int)t.Kind, t.Level, t.Branch)));
        foreach (var enemy in b.Enemies.Where(e => e.Alive)) objects.Add((enemy.Position.Y, () => DrawEnemy(g, enemy.Kind, enemy.Position, enemy.Health, enemy.MaxHealth, enemy.PoisonTime > 0, enemy.SlowTime > 0)));
        foreach (var ally in b.Allies.Where(a => a.Alive)) objects.Add((ally.Position.Y, () => DrawAlly(g, ally)));
        foreach (var obj in objects.OrderBy(o => o.Y)) obj.Draw();
        foreach (var fx in b.Effects) DrawEffect(g, fx);
        for (int i = 0; i < 18; i++)
        {
            float x = (i * 127 + uiTime * (b.Map.Theme == 3 ? 16 : 5)) % 960, y = 100 + (i * 79 + MathF.Sin(uiTime + i) * 7) % 500;
            Art.Ellipse(g, Art.Alpha(b.Map.Theme == 3 ? Color.Orange : Art.Gold, .4f), x, y, 2, 2);
        }
        if (spell >= 0 && mouse.X < 960 && mouse.Y is > 75 and < 625)
        {
            float radius = spell == 0 ? 100 : spell == 1 ? 120 : 45;
            Art.Ellipse(g, Color.FromArgb(45, spell == 1 ? Color.LightBlue : Art.Gold), mouse.X - radius, mouse.Y - radius, radius * 2, radius * 2);
            using var p = new Pen(spell == 1 ? Color.LightBlue : Art.Gold, 2); g.DrawEllipse(p, mouse.X - radius, mouse.Y - radius, radius * 2, radius * 2);
        }
        g.Restore(world);
        DrawBattleHeader(g, b);
        DrawBattleSidebar(g, b, tower);
        DrawBattleBottom(g, b);
        var boss = b.Enemies.FirstOrDefault(e => e.Alive && e.Spec.Boss);
        if (boss is not null)
        {
            Art.Box(g, new(250, 82, 460, 47), Color.FromArgb(230, 30, 31, 36), Color.FromArgb(148, 82, 70), 6);
            Art.Text(g, boss.Spec.Name, 268, 86, 13, Art.Gold, true, 424, 24, true);
            Art.Health(g, new(480, 116), boss.Health, boss.MaxHealth, 415, Color.FromArgb(222, 106, 80));
        }
        if (b.State != BattleState.Playing) DrawResult(g, b);
        else if (paused) DrawPause(g);
    }
    private void DrawBattleHeader(Graphics g, Battle b)
    {
        Art.Fill(g, Art.Ink, 0, 0, 1280, 75);
        Art.Line(g, Color.FromArgb(75, 97, 75), 1, new(0, 74), new(1280, 74));
        Art.Text(g, $"РУБЕЖ {b.Map.Id + 1:00} / {b.Map.Name}", 24, 13, 18, bold: true, width: 350, height: 30);
        Art.Text(g, $"{new[] { "Лёгкая", "Обычная", "Ветеран" }[b.Difficulty]}  •  {new[] { "Кампания", "Героический", "Железный", "Бесконечный" }[(int)b.Mode]}", 25, 44, 12, Art.Muted);
        Art.Text(g, $"♥ {b.Lives}", 407, 18, 25, Color.FromArgb(234, 143, 118), true);
        Art.Text(g, $"● {b.Gold}", 508, 18, 25, Art.Gold, true);
        Art.Text(g, $"ВОЛНА {b.Wave} / {(b.Mode == BattleMode.Endless ? "∞" : b.WaveLimit)}", 661, 17, 20, bold: true, width: 215);
        Art.Text(g, $"Врагов: {b.Enemies.Count}   В пути: {b.PendingSpawns}", 664, 45, 12, Art.Muted);
        Button(g, new(897, 16, 74, 42), $"×{speed}", () => speed = speed == 1 ? 2 : 1, tip: "Скорость симуляции. Tab — переключить.");
        Button(g, new(983, 16, 119, 42), paused ? "Продолжить" : "Пауза", () => paused = !paused, b.State == BattleState.Playing, textSize: 13);
        Button(g, new(1113, 16, 142, 42), "Меню", () => paused = true, b.State == BattleState.Playing);
    }
    private void DrawBattleSidebar(Graphics g, Battle b, Tower? tower)
    {
        Art.Fill(g, Art.Panel, 960, 75, 320, 645);
        Art.Line(g, Color.FromArgb(76, 96, 76), 1, new(960, 75), new(960, 720));
        if (selectedSite >= 0)
        {
            Art.Text(g, tower is null ? "ПОСТРОИТЬ БАШНЮ" : tower.Name, 984, 102, 21, Art.Gold, true, 275, 56);
            if (tower is null)
            {
                Art.Text(g, "Выберите защитников для этой площадки.", 984, 158, 14, Art.Muted, width: 264, height: 48);
                for (int i = 0; i < 4; i++)
                {
                    int kind = i; bool allowed = b.Mode != BattleMode.Iron || i is 1 or 3;
                    Button(g, new(982, 227 + i * 74, 275, 59), $"{i + 1}. {Content.TowerNames[i]}   {Content.Costs[i]} ●", () => b.Build(selectedSite, (TowerKind)kind), b.Gold >= Content.Costs[i] && allowed && !paused && b.State == BattleState.Playing, tip: allowed ? Content.TowerTips[i] : "Железное испытание: этот тип недоступен.", textSize: 14);
                }
            }
            else
            {
                var state = g.Save(); g.TranslateTransform(1115, 215); g.ScaleTransform(1.65f, 1.65f); Art.Tower(g, Vector2.Zero, (int)tower.Kind, tower.Level, tower.Branch); g.Restore(state);
                Art.Text(g, $"Уровень {tower.Level}/4    Урон {tower.Damage:0}\nДальность {tower.Range:0}    Интервал {tower.Interval:0.00} с", 985, 249, 14, Art.Muted, width: 275, height: 55);
                if (tower.Level < 3)
                    Button(g, new(983, 324, 273, 49), $"Улучшить: {tower.UpgradeCost} ●", () => b.Upgrade(tower), b.Gold >= tower.UpgradeCost && !paused, tip: "Увеличить урон и дальность; бойцам — здоровье.");
                else if (tower.Level == 3)
                {
                    Art.Text(g, "Выберите специализацию", 985, 314, 13, Art.Mint);
                    for (int i = 0; i < 2; i++)
                    {
                        int branch = i;
                        Button(g, new(983, 344 + i * 61, 273, 51), $"{Content.Branches[(int)tower.Kind][i]}\n{tower.UpgradeCost} ●", () => b.Upgrade(tower, branch), b.Gold >= tower.UpgradeCost && !paused, tip: Content.BranchTips[(int)tower.Kind][i], textSize: 13);
                    }
                }
                else
                {
                    Art.Text(g, Content.BranchTips[(int)tower.Kind][tower.Branch], 985, 312, 14, Art.Mint, width: 270, height: 53);
                    Button(g, new(983, 385, 273, 49), tower.Skill == 3 ? "Усиление: 3/3" : $"Усиление {tower.Skill + 1}/3: {tower.SkillCost} ●", () => b.ImproveSkill(tower), tower.Skill < 3 && b.Gold >= tower.SkillCost && !paused, tip: "Ускоряет стрельбу и усиливает эффект специализации; стража быстрее возрождается.", textSize: 13);
                }
                if (tower.Kind == TowerKind.Guard) Button(g, new(983, 471, 273, 44), "Указать точку сбора", () => { rallyMode = !rallyMode; spell = -1; heroSelection = 0; }, !paused, rallyMode, "Нажмите на дорогу в пределах радиуса казармы.");
                Button(g, new(983, 533, 273, 44), $"Продать: +{tower.Refund} ●", () => { b.Sell(tower); selectedSite = -1; }, !paused, tip: "Возврат 70% всех вложений. Бойцы этой башни исчезнут.");
            }
            Button(g, new(983, 599, 273, 39), "Закрыть выбор", () => { selectedSite = -1; rallyMode = false; });
        }
        else
        {
            Art.Text(g, "ПРИКАЗЫ КОМАНДИРА", 983, 104, 17, Art.Gold, true);
            Art.Text(g, "Стройте на круглых площадках.\nЗадерживайте врагов стражей.\nСочетайте стрелы, руны и взрывы.", 983, 151, 15, Art.Muted, width: 271, height: 93);
            Art.Line(g, Color.FromArgb(64, 86, 76), 1, new(983, 261), new(1256, 261));
            Art.Text(g, "СЛЕДУЮЩАЯ ВОЛНА", 983, 283, 12, Art.Mint, true);
            if (b.CanCallWave)
            {
                var plan = b.PlanWave(b.Wave + 1);
                string roster = string.Join("\n", plan.GroupBy(s => s.Kind).Select(gr => $"{Content.Enemies[(int)gr.Key].Name} ×{gr.Count()}"));
                Art.Text(g, roster, 983, 318, 14, width: 272, height: 176);
                Button(g, new(983, 510, 273, 58), b.Wave == 0 ? "НАЧАТЬ ВОЛНУ  [Enter]" : $"ВЫЗВАТЬ РАНЬШЕ  +{(int)b.NextWave + 8} ●", () => b.CallWave(), !paused, active: true, tip: "Ранний вызов приносит золото, но враги предыдущей волны могут оставаться на поле.", textSize: 13);
                if (b.NextWave > 0) Art.Text(g, $"Автоматический вызов через {b.NextWave:0} с", 984, 583, 13, Art.Muted, width: 273);
            }
            else Art.Text(g, b.Wave >= b.WaveLimit && b.Mode != BattleMode.Endless ? "Последняя волна.\nУдержите рубеж!" : "Волна идёт. Подготовьте оборону!", 984, 325, 18, width: 267, height: 80);
        }
        Art.Text(g, "Enter — волна   •   Tab — скорость\nПКМ — отмена   •   Пробел — пауза", 982, 665, 11, Art.Muted, width: 284, height: 40);
    }
    private void DrawBattleBottom(Graphics g, Battle b)
    {
        Art.Fill(g, Art.Ink, 0, 625, 960, 95);
        Art.Line(g, Color.FromArgb(76, 96, 76), 1, new(0, 625), new(960, 625));
        Button(g, new(14, 638, 209, 67), b.Hero.Alive ? $"{Content.HeroNames[profile.Hero].Split('•')[0]}  •  ур. {b.Hero.Level}\nH — переместить" : $"Герой возродится\nчерез {b.HeroRespawn:0} с", () => { heroSelection = heroSelection == 0 ? 1 : 0; spell = -1; rallyMode = false; }, b.Hero.Alive && !paused && b.State == BattleState.Playing, heroSelection > 0, Content.HeroTips[profile.Hero], 13);
        Art.Health(g, new(119, 699), b.Hero.Health, b.Hero.MaxHealth, 173, Art.Mint);
        string[] names = ["Q  Метеор", "W  Мороз", "E  Подкрепление", "R  Умение героя"];
        for (int i = 0; i < 4; i++)
        {
            int s = i; float cd = b.SpellCooldown[i]; bool ready = cd <= 0 && (i != 3 || b.Hero.Alive);
            Button(g, new(240 + i * 177, 638, 165, 67), names[i] + (cd > 0 ? $"\n{cd:0.0} с" : "\nГОТОВО"), () => SelectSpell(s), ready && !paused && b.State == BattleState.Playing, spell == i, i == 3 ? Content.HeroTips[profile.Hero] : new[] { "Чистый урон 210 в радиусе 100. Откат 32 с.", "Замедление на 85% на 4 с (боссы — 45%). Откат 24 с.", "Три бойца на 22 с. Откат 18 с." }[i], 13);
        }
        string instruction = spell >= 0 ? "Укажите цель заклинания • ПКМ — отмена" : rallyMode ? "Укажите точку сбора на дороге" : heroSelection > 0 ? "Нажмите на дорогу, чтобы переместить героя" : b.Wave == 0 ? "Подготовьте оборону, затем нажмите Enter" : "";
        if (instruction.Length > 0)
        {
            Art.Box(g, new(200, 583, 560, 32), Color.FromArgb(225, 23, 43, 41), Art.Alpha(Art.Gold, .4f), 6);
            Art.Text(g, instruction, 210, 590, 13, Art.Gold, width: 540, height: 22, center: true);
        }
    }
    private void DrawPause(Graphics g)
    {
        hotspots.Clear(); Art.Fill(g, Color.FromArgb(175, 7, 17, 24), 0, 0, 1280, 720);
        Art.Box(g, new(421, 151, 438, 416), Art.Panel, Art.Gold);
        Art.Text(g, "ПАУЗА", 441, 188, 35, Art.Gold, true, 398, 55, true);
        Art.Text(g, "Рубеж ждёт вашего приказа", 441, 248, 15, Art.Muted, width: 398, center: true);
        Button(g, new(465, 302, 350, 52), "Продолжить", () => paused = false, active: true);
        Button(g, new(465, 376, 350, 52), "Начать уровень заново", StartBattle);
        Button(g, new(465, 450, 350, 52), "Вернуться на карту", LeaveBattle);
    }
    private void DrawResult(Graphics g, Battle b)
    {
        hotspots.Clear(); bool won = b.State == BattleState.Victory;
        Art.Fill(g, Color.FromArgb(180, 7, 17, 24), 0, 0, 1280, 720);
        Art.Box(g, new(380, 106, 520, 512), Art.Panel, won ? Art.Gold : Color.FromArgb(195, 115, 93));
        Art.Text(g, won ? "РУБЕЖ УДЕРЖАН" : "РУБЕЖ ПОТЕРЯН", 400, 147, 33, won ? Art.Gold : Color.FromArgb(227, 148, 120), true, 480, 60, true);
        if (won)
            for (int i = 0; i < (b.Mode == BattleMode.Campaign ? 3 : 1); i++) Art.Star(g, new(b.Mode == BattleMode.Campaign ? 580 + i * 60 : 640, 254), 24, i < b.Stars ? Art.Gold : Color.FromArgb(67, 83, 69));
        else Art.Text(g, "Пересмотрите расстановку и попробуйте снова.", 413, 238, 16, Art.Muted, width: 454, height: 58, center: true);
        Art.Text(g, $"Побеждено врагов: {b.Kills}   •   Волна: {b.Wave}\nПостроено башен: {b.Built}   •   Заклинаний: {b.Casts}\nВремя боя: {(int)b.Time / 60}:{(int)b.Time % 60:00}", 413, 307, 17, width: 454, height: 100, center: true);
        if (won && b.Map.Id == 11 && b.Mode == BattleMode.Campaign)
            Art.Text(g, "Пепел осел. Над лесом снова поднимается солнце.", 404, 410, 14, Art.Mint, width: 472, center: true);
        bool next = won && b.Mode == BattleMode.Campaign && selectedLevel < 11;
        Button(g, new(435, 461, 410, 49), next ? "Следующий рубеж →" : "Попробовать ещё раз", () => { if (next) { selectedLevel++; mode = 0; } StartBattle(); }, active: true);
        Button(g, new(435, 534, 410, 44), "На карту кампании", LeaveBattle);
    }
    private static void DrawEnemy(Graphics g, EnemyKind kind, Vector2 p, float hp, float maxHp, bool poisoned, bool chilled)
    {
        var state = g.Save(); g.TranslateTransform(p.X, p.Y);
        bool boss = (int)kind >= 9; float s = boss ? 1.8f : kind is EnemyKind.Brute or EnemyKind.Brood ? 1.24f : 1;
        g.ScaleTransform(s, s);
        Color body = kind switch { EnemyKind.Shield or EnemyKind.Beetle => Color.FromArgb(102, 113, 126), EnemyKind.Shaman => Color.FromArgb(154, 104, 164), EnemyKind.Brute => Color.FromArgb(143, 135, 85), EnemyKind.Wraith => Color.FromArgb(124, 161, 180), EnemyKind.Brood => Color.FromArgb(135, 90, 116), EnemyKind.Colossus => Color.FromArgb(128, 131, 113), EnemyKind.Oracle => Color.FromArgb(173, 131, 196), EnemyKind.Sovereign => Color.FromArgb(189, 110, 76), _ => Color.FromArgb(172, 104, 80) };
        if (chilled) body = Color.FromArgb(134, 198, 217);
        Art.Ellipse(g, Color.FromArgb(60, 0, 0, 0), -12, -1, 27, 9);
        if (kind == EnemyKind.Wisp)
        {
            Art.Ellipse(g, Color.FromArgb(50, 141, 210, 224), -17, -31, 34, 35);
            Art.Polygon(g, Color.FromArgb(174, 225, 225), new(-10, -18), new(0, -33), new(10, -18), new(0, -4));
            Art.Ellipse(g, Art.Cream, -3, -23, 6, 7);
            Art.Line(g, Color.LightSteelBlue, 2, new(-4, -9), new(-12, -2), new(-4, 2));
        }
        else if (kind == EnemyKind.Hound)
        {
            Art.Ellipse(g, body, -15, -19, 29, 15);
            Art.Polygon(g, body, new(8, -19), new(19, -25), new(23, -13), new(8, -9));
            Art.Line(g, body, 3, new(-9, -8), new(-13, 1)); Art.Line(g, body, 3, new(9, -8), new(14, 1));
            Art.Polygon(g, Art.Gold, new(-10, -17), new(-5, -27), new(0, -18));
            Art.Ellipse(g, Color.FromArgb(255, 221, 133), 15, -21, 3, 3);
        }
        else if (kind is EnemyKind.Beetle or EnemyKind.Brood)
        {
            for (int i = 0; i < 3; i++)
            {
                Art.Line(g, Art.Ink, 2, new(-7, -18 + i * 7), new(-18, -24 + i * 9), new(-20, -16 + i * 9));
                Art.Line(g, Art.Ink, 2, new(7, -18 + i * 7), new(18, -24 + i * 9), new(20, -16 + i * 9));
            }
            Art.Ellipse(g, body, -12, -28, 24, 29); Art.Line(g, Art.Gold, 1.5f, new(0, -26), new(0, -3));
            Art.Ellipse(g, Color.FromArgb(205, 168, 110), -5, -29, 4, 4); Art.Ellipse(g, Color.FromArgb(205, 168, 110), 3, -29, 4, 4);
        }
        else
        {
            Art.Line(g, Art.Ink, 4, new(-5, -7), new(-7, 1)); Art.Line(g, Art.Ink, 4, new(5, -7), new(7, 1));
            Art.Polygon(g, body, new(-9, -24), new(8, -24), new(12, -5), new(-12, -5));
            Art.Ellipse(g, Color.FromArgb(186, 157, 117), -7, -35, 14, 15);
            Art.Polygon(g, body, new(-9, -29), new(-7, -38), new(7, -38), new(9, -29));
            Art.Ellipse(g, Color.FromArgb(247, 215, 133), -4, -29, 2, 2); Art.Ellipse(g, Color.FromArgb(247, 215, 133), 3, -29, 2, 2);
            if (kind == EnemyKind.Shield || boss)
            {
                Art.Polygon(g, Color.FromArgb(77, 84, 95), new(-17, -26), new(-5, -25), new(-5, -9), new(-11, -3), new(-18, -11));
                Art.Line(g, Art.Gold, 1.2f, new(-12, -24), new(-12, -8));
            }
            if (kind is EnemyKind.Shaman or EnemyKind.Oracle)
            {
                Art.Line(g, Art.Gold, 2, new(15, 0), new(15, -39)); Art.Ellipse(g, Color.Plum, 10, -43, 10, 10);
            }
            else Art.Line(g, Color.FromArgb(202, 198, 169), 2.6f, new(11, -14), new(23, -31));
            if (boss)
            {
                Art.Polygon(g, Art.Gold, new(-10, -36), new(-12, -45), new(-3, -40), new(0, -49), new(4, -40), new(12, -45), new(10, -36));
            }
        }
        if (poisoned) { Art.Ellipse(g, Color.FromArgb(173, 147, 214, 84), -17, -12, 5, 5); Art.Ellipse(g, Color.FromArgb(173, 147, 214, 84), 14, -19, 4, 4); }
        g.Restore(state);
        if (hp < maxHp || boss) Art.Health(g, p + new Vector2(0, -42 * s), hp, maxHp, boss ? 48 : 27, Color.FromArgb(222, 121, 93));
    }
    private void DrawAlly(Graphics g, Ally a)
    {
        var p = a.Position; float bob = a.TargetId is not null ? MathF.Sin(uiTime * 12) * 1.5f : 0;
        Color cape = a.Hero ? a.Archetype switch { 0 => Art.Mint, 1 => Art.Gold, _ => Color.FromArgb(175, 162, 238) } : a.Home?.Branch == 1 ? Color.FromArgb(157, 157, 220) : Color.FromArgb(111, 175, 198);
        if (a.Hero)
        {
            using var pen = new Pen(heroSelection > 0 ? Art.Gold : Art.Alpha(Art.Mint, .6f), 2);
            g.DrawEllipse(pen, p.X - 17, p.Y - 7, 34, 15);
        }
        Art.Ellipse(g, Color.FromArgb(60, 0, 0, 0), p.X - 11, p.Y - 1, 24, 8);
        Art.Line(g, Art.Ink, 4, new(p.X - 4, p.Y - 9), new(p.X - 6, p.Y + 1));
        Art.Line(g, Art.Ink, 4, new(p.X + 4, p.Y - 9), new(p.X + 6, p.Y + 1));
        Art.Polygon(g, cape, new(p.X - 7, p.Y - 25 + bob), new(p.X + 7, p.Y - 25 + bob), new(p.X + 11, p.Y - 6), new(p.X - 11, p.Y - 6));
        Art.Ellipse(g, Color.FromArgb(217, 184, 134), p.X - 6, p.Y - 35 + bob, 12, 13);
        Art.Polygon(g, a.Hero ? cape : Color.FromArgb(164, 184, 176), new(p.X - 8, p.Y - 28 + bob), new(p.X - 5, p.Y - 38 + bob), new(p.X + 6, p.Y - 37 + bob), new(p.X + 8, p.Y - 28 + bob));
        Art.Line(g, Art.Gold, 2, new(p.X + 9, p.Y - 14), new(p.X + 16, p.Y - 32));
        if (a.Hero && a.Archetype == 2) Art.Ellipse(g, Color.FromArgb(188, 169, 249), p.X + 11, p.Y - 38, 10, 10);
        if (a.Stun > 0) Art.Star(g, p + new Vector2(0, -45), 6, Art.Gold);
        Art.Health(g, p + new Vector2(0, -43), a.Health, a.MaxHealth, a.Hero ? 35 : 24, Art.Mint);
    }
    private static void DrawEffect(Graphics g, Effect fx)
    {
        float progress = 1 - fx.Time / fx.Duration;
        Color color = fx.Kind switch { EffectKind.Magic => Color.FromArgb(173, 173, 255), EffectKind.Frost => Color.LightBlue, EffectKind.Heal => Art.Mint, EffectKind.Leak => Color.IndianRed, _ => Art.Gold };
        if (fx.Kind == EffectKind.Gold)
            Art.Text(g, fx.Text, fx.To.X - 50, fx.To.Y - 30 - progress * 30, 13, Art.Alpha(Art.Gold, 1 - progress), true, 100, 35, true);
        else if (fx.Kind is EffectKind.Arrow or EffectKind.Magic or EffectKind.Rock)
        {
            var to = Vector2.Lerp(fx.From - new Vector2(0, 35), fx.To - new Vector2(0, 13), progress);
            if (fx.Kind == EffectKind.Rock) { to.Y -= MathF.Sin(progress * MathF.PI) * 45; Art.Ellipse(g, Color.FromArgb(185, 167, 126), to.X - 5, to.Y - 5, 11, 11); }
            else Art.Line(g, Art.Alpha(color, .9f), fx.Kind == EffectKind.Arrow ? 2 : 3, Art.P(Vector2.Lerp(fx.From - new Vector2(0, 35), to, .7f)), Art.P(to));
        }
        else
        {
            float radius = Math.Max(12, fx.Radius) * (.35f + progress * .65f);
            using var pen = new Pen(Art.Alpha(color, (1 - progress) * .8f), 2.5f);
            g.DrawEllipse(pen, fx.To.X - radius, fx.To.Y - radius * .6f, radius * 2, radius * 1.2f);
            Art.Ellipse(g, Art.Alpha(color, (1 - progress) * .14f), fx.To.X - radius, fx.To.Y - radius * .6f, radius * 2, radius * 1.2f);
            if (fx.Kind == EffectKind.Burst && progress < .4f)
            {
                var head = Vector2.Lerp(fx.From, fx.To, Math.Min(1, progress * 3));
                Art.Line(g, Color.FromArgb(230, 236, 144, 84), 7 * (1 - progress), Art.P(head - new Vector2(15, 36)), Art.P(head));
            }
            if (fx.Text.Length > 0) Art.Text(g, fx.Text, fx.To.X - 70, fx.To.Y - 65, 14, color, true, 140, 45, true);
        }
    }
}
