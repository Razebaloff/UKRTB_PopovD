using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Numerics;
using Verdant.Core;

namespace Verdant.Game;

internal sealed partial class GameWindow : Form
{
    private enum Screen { Title, Campaign, Upgrades, Codex, Achievements, Help, Settings, Battle }
    private sealed record Hotspot(RectangleF Rect, Action Action, string Tip);
    private readonly List<Hotspot> hotspots = [];
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 16 };
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly Audio audio = new();
    private readonly Profile profile;
    private Screen screen;
    private Battle? battle;
    private int selectedLevel, selectedSite = -1, spell = -1, heroSelection, mode, codexPage;
    private bool paused, recorded, rallyMode, fullScreen;
    private int speed = 1;
    private float scale = 1, offsetX, offsetY, accumulator, uiTime, toastTime;
    private double lastTime;
    private Vector2 mouse = new(-1);
    private string toast = "";
    private Rectangle oldBounds;
    private FormWindowState oldWindowState;

    public GameWindow()
    {
        Text = "Стражи рассвета • Verdant Guard";
        ClientSize = new Size(1280, 720); MinimumSize = new Size(960, 580);
        StartPosition = FormStartPosition.CenterScreen; BackColor = Art.Ink; KeyPreview = true;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        profile = ProfileStore.Load(ProfileStore.DefaultPath, out string warning);
        audio.Enabled = profile.Sound; audio.MusicEnabled = profile.Music;
        if (warning.Length > 0) Notify(warning);
        timer.Tick += Frame; timer.Start();
        Deactivate += (_, _) => { if (screen == Screen.Battle) { paused = true; accumulator = 0; } };
        FormClosing += (_, _) => { RecordBattle(); Save(); };
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { timer.Dispose(); audio.Dispose(); terrain?.Dispose(); }
        base.Dispose(disposing);
    }
    private void Frame(object? sender, EventArgs e)
    {
        double now = clock.Elapsed.TotalSeconds;
        float dt = (float)Math.Clamp(now - lastTime, 0, .12); lastTime = now; uiTime += dt;
        toastTime = Math.Max(0, toastTime - dt);
        if (screen == Screen.Battle && battle is { State: BattleState.Playing } b && !paused)
        {
            accumulator += dt * speed;
            while (accumulator >= Battle.Step) { b.Tick(Battle.Step); accumulator -= Battle.Step; }
            if (b.State != BattleState.Playing) { spell = -1; heroSelection = 0; RecordBattle(); audio.Cue(b.State == BattleState.Victory); }
        }
        else accumulator = 0;
        Invalidate();
    }
    private void Notify(string message) { toast = message; toastTime = 6; }
    private void Save()
    {
        if (!ProfileStore.Save(profile, ProfileStore.DefaultPath, out string warning)) Notify(warning);
    }
    private void RecordBattle()
    {
        if (battle is null || recorded) return;
        profile.Record(battle); recorded = true; Save();
    }
    private void StartBattle()
    {
        var chosenMode = (BattleMode)mode;
        if (!profile.Unlocked(selectedLevel) || (chosenMode is BattleMode.Heroic or BattleMode.Iron && !profile.ChallengeUnlocked(selectedLevel))) return;
        RecordBattle();
        battle = new Battle(Content.Levels[selectedLevel], chosenMode, profile.Difficulty, profile.Hero, profile.Perks);
        battle.Sound += audio.Play;
        screen = Screen.Battle; paused = false; recorded = false; speed = 1; selectedSite = -1; spell = -1; heroSelection = 0; rallyMode = false;
        MakeTerrain(battle.Map);
    }
    private void LeaveBattle()
    {
        RecordBattle(); screen = Screen.Campaign; battle = null; selectedSite = -1; spell = -1; paused = false;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    { base.OnMouseMove(e); mouse = new((e.X - offsetX) / scale, (e.Y - offsetY) / scale); Cursor = hotspots.Any(h => h.Rect.Contains(Art.P(mouse))) || spell >= 0 || heroSelection > 0 || rallyMode ? Cursors.Hand : Cursors.Default; }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); mouse = new(-1); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        mouse = new((e.X - offsetX) / scale, (e.Y - offsetY) / scale);
        if (e.Button == MouseButtons.Right) { spell = -1; heroSelection = 0; rallyMode = false; selectedSite = -1; return; }
        if (e.Button != MouseButtons.Left) return;
        var hit = hotspots.LastOrDefault(h => h.Rect.Contains(Art.P(mouse)));
        if (hit is not null) { hit.Action(); Invalidate(); return; }
        if (screen != Screen.Battle || battle is null || paused || battle.State != BattleState.Playing || mouse.X >= 960 || mouse.Y is < 75 or > 625) return;
        if (spell >= 0) { if (battle.Cast(spell, mouse)) spell = -1; return; }
        if (rallyMode)
        {
            var t = battle.Towers.Find(t => t.Site == selectedSite);
            if (t is not null && battle.SetRally(t, mouse)) rallyMode = false;
            else Notify("Точка сбора должна находиться у дороги в пределах радиуса казармы.");
            return;
        }
        if (heroSelection > 0) { battle.MoveHero(mouse); heroSelection = 0; return; }
        if (Vector2.Distance(mouse, battle.Hero.Position) < 22 && battle.Hero.Alive) { heroSelection = 1; selectedSite = -1; return; }
        selectedSite = Array.FindIndex(battle.Map.Sites, p => Vector2.Distance(p, mouse) < 33);
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.F11) { ToggleFullscreen(); e.Handled = true; return; }
        if (e.KeyCode == Keys.M) { profile.Sound = !profile.Sound; audio.Enabled = profile.Sound; Save(); return; }
        if (screen != Screen.Battle || battle is null) { if (e.KeyCode == Keys.Escape) screen = Screen.Title; return; }
        if (e.KeyCode == Keys.Escape)
        {
            if (spell >= 0 || heroSelection > 0 || rallyMode) { spell = -1; heroSelection = 0; rallyMode = false; }
            else paused = !paused;
            return;
        }
        if (e.KeyCode is Keys.P or Keys.Space) { paused = !paused; e.Handled = true; return; }
        if (paused || battle.State != BattleState.Playing) return;
        if (e.KeyCode == Keys.Enter) battle.CallWave();
        if (e.KeyCode == Keys.Tab) { speed = speed == 1 ? 2 : 1; e.Handled = true; }
        if (e.KeyCode == Keys.H) { heroSelection = 1; spell = -1; rallyMode = false; }
        if (e.KeyCode is Keys.Q or Keys.W or Keys.E or Keys.R)
        {
            int index = e.KeyCode switch { Keys.Q => 0, Keys.W => 1, Keys.E => 2, _ => 3 };
            SelectSpell(index);
        }
        if (e.KeyCode is >= Keys.D1 and <= Keys.D4 && selectedSite >= 0) battle.Build(selectedSite, (TowerKind)(e.KeyCode - Keys.D1));
    }
    private void SelectSpell(int i)
    {
        if (battle is null || paused || battle.State != BattleState.Playing || battle.SpellCooldown[i] > 0) return;
        if (i == 3) { battle.Cast(3, battle.Hero.Position); return; }
        spell = spell == i ? -1 : i; heroSelection = 0; rallyMode = false;
    }
    private void ToggleFullscreen()
    {
        if (!fullScreen)
        {
            oldBounds = Bounds; oldWindowState = WindowState; WindowState = FormWindowState.Normal;
            FormBorderStyle = FormBorderStyle.None; Bounds = System.Windows.Forms.Screen.FromControl(this).Bounds;
        }
        else { FormBorderStyle = FormBorderStyle.Sizable; Bounds = oldBounds; WindowState = oldWindowState; }
        fullScreen = !fullScreen;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); var g = e.Graphics; g.Clear(Art.Ink);
        scale = Math.Min(ClientSize.Width / 1280f, ClientSize.Height / 720f);
        offsetX = (ClientSize.Width - 1280 * scale) / 2; offsetY = (ClientSize.Height - 720 * scale) / 2;
        g.TranslateTransform(offsetX, offsetY); g.ScaleTransform(scale, scale);
        g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        hotspots.Clear();
        if (screen == Screen.Battle) DrawBattle(g);
        else
        {
            DrawBackdrop(g);
            switch (screen)
            {
                case Screen.Title: DrawTitle(g); break;
                case Screen.Campaign: DrawCampaign(g); break;
                case Screen.Upgrades: DrawUpgrades(g); break;
                case Screen.Codex: DrawCodex(g); break;
                case Screen.Achievements: DrawAchievements(g); break;
                case Screen.Help: DrawHelp(g); break;
                case Screen.Settings: DrawSettings(g); break;
            }
        }
        DrawTooltip(g);
        if (toastTime > 0)
        {
            Art.Box(g, new(225, 9, 830, 48), Color.FromArgb(245, 38, 53, 48), Art.Gold);
            Art.Text(g, toast, 240, 20, 14, Art.Gold, width: 800, height: 33, center: true);
        }
    }
    private void Button(Graphics g, RectangleF rect, string text, Action action, bool enabled = true, bool active = false, string tip = "", float textSize = 15)
    {
        bool hover = enabled && rect.Contains(Art.P(mouse));
        Art.Box(g, rect, enabled ? active ? Color.FromArgb(57, 94, 78) : hover ? Color.FromArgb(48, 73, 66) : Art.Panel : Color.FromArgb(28, 39, 43), active || hover ? Art.Gold : Color.FromArgb(63, 83, 77), 8);
        Art.Text(g, text, rect.X + 8, rect.Y + (rect.Height - textSize * 1.4f) / 2, textSize, enabled ? active ? Art.Gold : Art.Cream : Color.FromArgb(98, 117, 112), true, rect.Width - 16, rect.Height - 5, true);
        if (enabled) hotspots.Add(new(rect, action, tip));
        else if (tip.Length > 0) hotspots.Add(new(rect, () => { }, tip));
    }
    private void Header(Graphics g, string title, string subtitle)
    {
        Art.Text(g, "VERDANT GUARD / СТРАЖИ РАССВЕТА", 48, 24, 11, Art.Mint, true);
        Art.Text(g, title, 48, 51, 35, bold: true); Art.Text(g, subtitle, 50, 101, 15, Art.Muted, width: 1050);
        Button(g, new(1104, 35, 128, 42), "← Назад", () => screen = screen == Screen.Campaign ? Screen.Title : Screen.Campaign);
    }
    private void DrawBackdrop(Graphics g)
    {
        using var gradient = new LinearGradientBrush(new Rectangle(0, 0, 1280, 720), Color.FromArgb(18, 39, 48), Color.FromArgb(31, 65, 55), 85);
        g.FillRectangle(gradient, 0, 0, 1280, 720);
        Art.Ellipse(g, Color.FromArgb(15, 186, 219, 174), 790, -200, 650, 650);
        for (int i = 0; i < 22; i++) Art.Tree(g, i * 67 - 30, 700 + i % 3 * 15, 1.4f + i % 4 * .2f, Color.FromArgb(12 + i % 3 * 3, 37 + i % 3 * 5, 39 + i % 3 * 4));
        for (int i = 0; i < 35; i++)
        {
            float x = (i * 173 + MathF.Sin(uiTime * .3f + i) * 12) % 1280, y = 120 + i * 97 % 540;
            Art.Ellipse(g, Art.Alpha(Art.Gold, .2f + .16f * MathF.Sin(uiTime + i)), x, y, 3, 3);
        }
        Art.Text(g, "Оригинальная tower defense на C#  •  F11 — полный экран", 48, 691, 11, Art.Muted);
    }
    private void DrawTitle(Graphics g)
    {
        Art.Text(g, "VERDANT GUARD", 90, 107, 15, Art.Mint, true);
        Art.Text(g, "СТРАЖИ\nРАССВЕТА", 84, 142, 74, bold: true, width: 730, height: 195);
        Art.Line(g, Art.Gold, 3, new(92, 347), new(196, 347));
        Art.Text(g, "Пепел наступает. Лес ещё помнит свет.\nВозглавьте стражу и удержите двенадцать рубежей.", 92, 373, 18, Art.Muted, width: 590, height: 75);
        Button(g, new(92, 475, 315, 61), "НАЧАТЬ КАМПАНИЮ  →", () => screen = Screen.Campaign, active: true, textSize: 18);
        Button(g, new(92, 551, 150, 45), "Как играть", () => screen = Screen.Help);
        Button(g, new(257, 551, 150, 45), "Настройки", () => screen = Screen.Settings);
        Button(g, new(92, 611, 315, 42), "Выйти", Close);
        var state = g.Save(); g.TranslateTransform(956, 382); g.ScaleTransform(3.1f, 3.1f);
        Art.Tower(g, Vector2.Zero, 1, 4, 0); g.Restore(state);
        Art.Text(g, $"12 рубежей  /  3 героя  /  8 специализаций\nПрогресс: {profile.TotalStars} из 60 звёзд", 740, 555, 16, Art.Gold, width: 430, height: 65, center: true);
    }
    private void DrawCampaign(Graphics g)
    {
        Header(g, "Карта рубежей", $"Звёзды: {profile.TotalStars}/60   •   Доступно для улучшений: {profile.AvailableStars}");
        var positions = new Vector2[12];
        for (int i = 0; i < 12; i++) { int row = i / 4, col = row % 2 == 0 ? i % 4 : 3 - i % 4; positions[i] = new(130 + col * 194, 225 + row * 153); }
        Art.Line(g, Color.FromArgb(66, 108, 94), 7, positions.Select(Art.P).ToArray());
        using (var dash = new Pen(Art.Alpha(Art.Gold, .4f), 1) { DashStyle = DashStyle.Dash }) g.DrawLines(dash, positions.Select(Art.P).ToArray());
        for (int i = 0; i < 12; i++)
        {
            int index = i; var p = positions[i]; bool unlocked = profile.Unlocked(i), selected = selectedLevel == i;
            Art.Ellipse(g, Color.FromArgb(45, 0, 0, 0), p.X - 35, p.Y - 30, 76, 76);
            Art.Ellipse(g, selected ? Art.Gold : unlocked ? Color.FromArgb(83, 126, 96) : Color.FromArgb(49, 66, 65), p.X - 30, p.Y - 30, 60, 60);
            Art.Ellipse(g, Art.Panel, p.X - 25, p.Y - 25, 50, 50);
            Art.Text(g, unlocked ? (i + 1).ToString("00") : "×", p.X - 26, p.Y - 17, 24, unlocked ? Art.Cream : Art.Muted, true, 52, 40, true);
            Art.Text(g, Content.Levels[i].Name, p.X - 88, p.Y + 44, 13, unlocked ? Art.Cream : Art.Muted, true, 176, 38, true);
            for (int j = 0; j < 3; j++) Art.Star(g, new(p.X - 17 + j * 17, p.Y + 33), 7, profile.Stars[i * 3] > j ? Art.Gold : Color.FromArgb(69, 90, 79));
            if (profile.Stars[i * 3 + 1] > 0) Art.Ellipse(g, Art.Mint, p.X - 32, p.Y - 29, 9, 9);
            if (profile.Stars[i * 3 + 2] > 0) Art.Ellipse(g, Color.LightSteelBlue, p.X + 23, p.Y - 29, 9, 9);
            if (unlocked) hotspots.Add(new(new(p.X - 65, p.Y - 38, 130, 115), () => { selectedLevel = index; mode = 0; }, Content.Levels[i].Story));
        }
        Art.Box(g, new(855, 156, 377, 464), Art.Panel, Color.FromArgb(65, 91, 78));
        var level = Content.Levels[selectedLevel];
        Art.Text(g, $"РУБЕЖ {selectedLevel + 1:00}", 878, 179, 11, Art.Mint, true);
        Art.Text(g, level.Name, 878, 205, 25, bold: true, width: 330);
        Art.Text(g, level.Story, 878, 254, 16, Art.Muted, width: 328, height: 83);
        string[] labels = ["Кампания", "Героич.", "Железо", "∞"];
        for (int i = 0; i < 4; i++)
        {
            int m = i; bool allowed = i is 0 or 3 || profile.ChallengeUnlocked(selectedLevel);
            Button(g, new(877 + i * 83, 348, 78, 38), labels[i], () => mode = m, allowed, mode == i, allowed ? "" : "Для испытаний получите 3 звезды кампании на этом уровне.", 12);
        }
        Art.Text(g, mode switch { 1 => "6 усиленных волн • 1 жизнь", 2 => "Одна длинная волна • 1 жизнь\nДоступны только руны и стража", 3 => "Бесконечные волны. Сложность растёт.", _ => $"{level.Waves} волн • 20 жизней • до 3 звёзд" }, 880, 404, 14, Art.Gold, width: 325, height: 52);
        Button(g, new(878, 468, 331, 42), Content.HeroNames[profile.Hero], () => { profile.Hero = (profile.Hero + 1) % 3; Save(); }, tip: Content.HeroTips[profile.Hero]);
        Button(g, new(878, 528, 331, 60), "ЗАЩИТИТЬ РУБЕЖ  →", StartBattle, active: true, textSize: 17);
        Button(g, new(48, 631, 180, 40), "Улучшения", () => screen = Screen.Upgrades);
        Button(g, new(244, 631, 180, 40), "Бестиарий", () => screen = Screen.Codex);
        Button(g, new(440, 631, 180, 40), "Достижения", () => screen = Screen.Achievements);
        Button(g, new(636, 631, 180, 40), "Как играть", () => screen = Screen.Help);
    }
    private void DrawUpgrades(Graphics g)
    {
        Header(g, "Совет хранителей", $"Свободные звёзды: {profile.AvailableStars}. Улучшения действуют во всех новых боях.");
        for (int i = 0; i < 4; i++)
        {
            int index = i; float x = 48 + i * 300;
            Art.Box(g, new(x, 188, 282, 333), Art.Panel, Color.FromArgb(64, 92, 79));
            Art.Star(g, new(x + 140, 241), 26, Art.Gold);
            Art.Text(g, Content.PerkNames[i], x + 16, 291, 22, bold: true, width: 250, center: true);
            Art.Text(g, Content.PerkTips[i], x + 22, 338, 16, Art.Muted, width: 238, height: 58, center: true);
            Art.Text(g, $"Ранг {profile.Perks[i]} / 5", x + 22, 407, 17, Art.Mint, true, 238, center: true);
            Button(g, new(x + 21, 455, 240, 44), profile.Perks[i] == 5 ? "Максимум" : $"Изучить: {profile.Perks[i] + 1} ★", () => { if (profile.BuyPerk(index)) Save(); }, profile.Perks[i] < 5 && profile.AvailableStars >= profile.Perks[i] + 1);
        }
        Button(g, new(440, 565, 400, 47), "Вернуть звёзды и сбросить улучшения", () => { profile.ResetPerks(); Save(); });
    }
    private void DrawCodex(Graphics g)
    {
        Header(g, "Полевой бестиарий", "Знание врага помогает выбрать башни. Все записи доступны сразу.");
        for (int j = 0; j < 6; j++)
        {
            int i = codexPage * 6 + j; var spec = Content.Enemies[i]; float x = 48 + j % 3 * 403, y = 167 + j / 3 * 215;
            Art.Box(g, new(x, y, 385, 196), Art.Panel, Color.FromArgb(64, 92, 79));
            DrawEnemy(g, (EnemyKind)i, new(x + 37, y + 52), 1, 1, false, false);
            Art.Text(g, spec.Name, x + 71, y + 20, 18, Art.Gold, true, 300, 47);
            Art.Text(g, $"ОЗ {spec.Health}   Скорость {spec.Speed}   Золото {spec.Bounty}\nБроня {spec.Armor:P0}   Защита от магии {spec.Resist:P0}", x + 20, y + 78, 13, Art.Muted, width: 345, height: 48);
            Art.Text(g, spec.Tip, x + 20, y + 131, 14, width: 345, height: 60);
        }
        Button(g, new(440, 630, 190, 43), "← Предыдущие", () => codexPage = 0, codexPage > 0);
        Button(g, new(650, 630, 190, 43), "Следующие →", () => codexPage = 1, codexPage < 1);
    }
    private void DrawAchievements(Graphics g)
    {
        Header(g, "Хроника рассвета", $"Получено: {profile.Achievements.Count(a => a.Done)}/12   •   Врагов: {profile.TotalKills}   •   Башен: {profile.TotalBuilt}   •   Рекорд волн: {profile.EndlessBest}");
        var achievements = profile.Achievements;
        for (int i = 0; i < achievements.Length; i++)
        {
            var a = achievements[i]; float x = 48 + i % 3 * 403, y = 169 + i / 3 * 117;
            Art.Box(g, new(x, y, 385, 101), a.Done ? Color.FromArgb(41, 71, 56) : Art.Panel, a.Done ? Art.Gold : Color.FromArgb(64, 83, 79));
            Art.Star(g, new(x + 31, y + 33), 14, a.Done ? Art.Gold : Art.Muted);
            Art.Text(g, a.Name, x + 60, y + 17, 18, a.Done ? Art.Gold : Art.Cream, true, 310);
            Art.Text(g, a.Description, x + 20, y + 57, 14, Art.Muted, width: 350, height: 40);
        }
    }
    private void DrawHelp(Graphics g)
    {
        Header(g, "Памятка командира", "Цель: не дать врагам пройти к правому краю. Сначала постройте башни, затем вызовите волну.");
        string[] titles = ["ПОСТРОЙКА И УЛУЧШЕНИЯ", "ПОЛЕ БОЯ", "ЗАКЛИНАНИЯ", "КАМПАНИЯ"];
        string[] bodies = ["Нажмите на круглую площадку, затем выберите башню справа.\n1–4 — построить выбранный тип. Улучшайте башни до IV уровня, выбирайте одну из двух специализаций и изучайте её усиления. Продажа возвращает 70% затрат.", "ЛКМ по герою или H → ЛКМ на дороге: перемещение. Стража задерживает наземных врагов; точку сбора меняют кнопкой справа. Летающих врагов атакуют стрелы и руны. Герой возрождается через 15 секунд.", "Q — метеор, чистый урон по площади.\nW — мороз, замедляет в том числе летающих.\nE — подкрепление на 22 секунды. R — умение героя.\nПосле Q/W/E укажите цель. ПКМ отменяет выбор.", "Enter — вызвать волну; ранний вызов даёт золото.\nПробел / P / Esc — пауза. Tab — скорость ×1/×2.\n18–20 жизней = 3 звезды; 10–17 = 2; 1–9 = 1.\n3 звезды открывают два испытания. Звёзды расходуются на постоянные улучшения. Сохранение — автоматически."];
        for (int i = 0; i < 4; i++)
        {
            float x = 48 + i % 2 * 605, y = 168 + i / 2 * 236;
            Art.Box(g, new(x, y, 582, 216), Art.Panel, Color.FromArgb(64, 92, 79));
            Art.Text(g, titles[i], x + 23, y + 23, 14, Art.Gold, true);
            Art.Text(g, bodies[i], x + 23, y + 59, 16, width: 536, height: 153);
        }
    }
    private void DrawSettings(Graphics g)
    {
        Header(g, "Настройки", "Выбранная сложность применяется к следующему бою. Игра работает без интернета.");
        Art.Box(g, new(330, 170, 620, 439), Art.Panel, Color.FromArgb(64, 92, 79));
        Art.Text(g, "Сложность", 370, 201, 20, bold: true);
        string[] names = ["Лёгкая", "Обычная", "Ветеран"];
        for (int i = 0; i < 3; i++) { int d = i; Button(g, new(370 + i * 183, 245, 174, 46), names[i], () => { profile.Difficulty = d; Save(); }, active: profile.Difficulty == i); }
        Button(g, new(370, 320, 540, 48), $"Звуковые эффекты: {(profile.Sound ? "включены" : "выключены")}", () => { profile.Sound = !profile.Sound; audio.Enabled = profile.Sound; Save(); });
        Button(g, new(370, 387, 540, 48), $"Музыка: {(profile.Music ? "включена" : "выключена")}", () => { profile.Music = !profile.Music; audio.MusicEnabled = profile.Music; Save(); });
        Button(g, new(370, 454, 540, 48), "Полный экран / окно • F11", ToggleFullscreen);
        Art.Text(g, "M — включить / выключить эффекты. При потере фокуса бой ставится на паузу. Музыка и звуки синтезируются самой игрой.", 372, 528, 14, Art.Muted, width: 535, height: 68);
    }
    private void DrawTooltip(Graphics g)
    {
        if (paused && screen == Screen.Battle) return;
        string? tip = hotspots.LastOrDefault(h => h.Rect.Contains(Art.P(mouse)))?.Tip;
        if (string.IsNullOrWhiteSpace(tip)) return;
        float x = Math.Clamp(mouse.X + 15, 10, 890), y = Math.Clamp(mouse.Y + 22, 10, 625);
        Art.Box(g, new(x, y, 365, 73), Color.FromArgb(249, 16, 31, 37), Color.FromArgb(98, 125, 102));
        Art.Text(g, tip, x + 13, y + 10, 13, Art.Cream, width: 339, height: 55);
    }
}
