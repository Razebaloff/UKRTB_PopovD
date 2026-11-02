using LP_PopovD_App;

int checks = 0;
void Check(bool value, string message)
{
    if (!value) throw new InvalidOperationException(message);
    checks++;
}

LoginFlowState login = new();
Check(!login.TryConfirm("123"), "Нельзя подтвердить код до запроса.");
Check(AuthInputRules.Validate(false, " ", "secret", "").Field == 0, "Нужен непустой логин.");
Check(AuthInputRules.Validate(false, "Player", "\t", "").Field == 1, "Нужен непустой пароль.");
Check(AuthInputRules.Validate(false, "Игрок", "secret", "").Field == -1, "Вход не требует отдельного поля email.");
Check(AuthInputRules.Validate(true, "Player", "secret", "invalid").Field == 2, "Проверка email регистрации сохранена.");
login.RequestCode();
foreach (string code in new[] { "", " ", "\t\r\n" })
    Check(!login.TryConfirm(code) && !login.Animating, "Пустой код не запускает вход.");
Check(login.TryConfirm("любой текст ✦") && login.Animating, "Демонстрация принимает любой непустой код.");
Check(!login.TryConfirm("again"), "Повторное подтверждение заблокировано.");
login.CancelAnimation();
Check(login.CodeRequested && !login.Animating, "Отмена оставляет возможность повторить подтверждение.");
Check(login.TryConfirm("retry"), "После отмены можно повторить вход.");
login.Reset();
Check(!login.CodeRequested && !login.TryConfirm("old"), "Смена данных/экрана отменяет прежний запрос кода.");

StickAnimationState sticks = new();
int actions = 0;
Check(sticks.TryBegin(() => actions++), "Клюшки запускаются.");
Check(!sticks.TryBegin(() => actions += 100), "Двойной клик не подменяет действие.");
Check(!sticks.TryFinish(5000, out _), "Действие ждёт первого нарисованного кадра.");
Check(sticks.Progress(5000) == 0, "Долгая подготовка не съедает начало анимации.");
Check(!sticks.TryFinish(5000 + StickAnimationState.DurationMilliseconds - 1, out _), "Раннего перехода нет.");
Check(sticks.TryFinish(5000 + StickAnimationState.DurationMilliseconds, out Action? action), "Полный оборот завершён.");
action?.Invoke();
Check(actions == 1 && !sticks.Active && !sticks.TryFinish(9999, out _), "Действие выполняется ровно один раз.");
sticks.TryBegin(() => actions++);
sticks.Progress(10000);
sticks.Cancel();
Check(!sticks.TryFinish(20000, out _) && actions == 1, "Esc отменяет ожидающий переход.");

string folder = Path.Combine(Path.GetTempPath(), "hockey-state-checks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);
try
{
    string path = Path.Combine(folder, "login.json");
    RememberedLoginStore store = new(path);
    Check(store.Load() == "", "Отсутствующие настройки допустимы.");
    Check(await store.SaveAsync("Игрок_42", true), "Логин сохраняется.");
    Check(new RememberedLoginStore(path).Load() == "Игрок_42", "Логин восстанавливается после нового запуска.");
    using (var data = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path)))
    {
        Check(data.RootElement.EnumerateObject().Count() == 2, "В настройках только Login и Remember.");
        Check(data.RootElement.GetProperty("Remember").GetBoolean(), "Флажок сохранён.");
    }
    Check(await store.SaveAsync("ignored", false) && !File.Exists(path), "Снятый флажок удаляет сохранённый логин.");
    Task<bool> remember = store.SaveAsync("Player", true);
    Task<bool> forget = store.SaveAsync("", false);
    await Task.WhenAll(remember, forget);
    Check(!File.Exists(path), "Быстрое снятие флажка побеждает незавершённую запись.");
    File.WriteAllText(path, "broken json");
    Check(store.Load() == "", "Повреждённый файл не мешает запуску.");
    File.WriteAllText(path, new string('x', 4097));
    Check(store.Load() == "", "Слишком большой файл настроек отклоняется.");
    Directory.CreateDirectory(Path.Combine(folder, "blocked"));
    Check(!await new RememberedLoginStore(Path.Combine(folder, "blocked")).SaveAsync("Player", true),
        "Ошибка записи не прерывает вход.");
}
finally { Directory.Delete(folder, recursive: true); }

Console.WriteLine($"Passed: {checks} behavior checks.");
