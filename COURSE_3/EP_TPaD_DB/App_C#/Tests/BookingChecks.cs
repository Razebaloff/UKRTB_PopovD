using LP_PopovD_App;

internal static class BookingChecks
{
    internal static int Run()
    {
        int count = 0;
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); count++; }
        string folder = Path.Combine(Path.GetTempPath(), "hockey-bookings-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "bookings.json");
        TrainingBookingStore store = new(path);
        DateTime date = DateTime.Today.AddDays(7);
        try
        {
            Check(store.Load().Count == 0, "Новый журнал не содержит выдуманных арендаторов.");
            TrainingBooking a = store.Book(date, 0, 1, "Команда А");
            TrainingBooking b = store.Book(date, 0, 2, "Команда Б");
            Check(a.Zones == 1 && b.Zones == 2 && a.Tenant != b.Tenant,
                "В один час разные арендаторы могут занять разные зоны.");
            bool conflict = false;
            try { store.Book(date, 0, 1, "Третий арендатор"); } catch (InvalidOperationException) { conflict = true; }
            Check(conflict && store.Load().Count == 2, "Повторная аренда занятой зоны отклоняется.");
            store.Cancel(a.Id);
            conflict = false;
            try { store.Book(date, 0, 3, "Обе зоны"); } catch (InvalidOperationException) { conflict = true; }
            Check(conflict && store.Load().All(row => row.Zones == 2),
                "Бронь двух зон атомарна: свободная зона не занята при конфликте второй.");
            store.Cancel(b.Id);
            TrainingBooking both = store.Book(date, 0, 3, "Общая аренда");
            Check(new TrainingBookingStore(path).Load().Single().Id == both.Id,
                "Бронь обеих зон сохраняется после нового запуска.");
            store.Book(date, 1, 1, "Следующий час");
            store.Book(date.AddDays(1), 0, 1, "Следующий день");
            Check(store.Load().Count == 3, "Даты и соседние сеансы независимы.");
            store.Cancel(both.Id);
            Check(!store.Load().Any(row => row.Date == date && row.Slot == 0),
                "Отмена общей брони освобождает сразу обе зоны.");
            bool past = false;
            try { store.Book(DateTime.Today.AddDays(-1), 0, 1, "Прошлое"); } catch (InvalidOperationException) { past = true; }
            Check(past, "Прошедшие сеансы недоступны для новой аренды.");
            using (FileStream lease = new(path + ".lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                bool locked = false;
                try { store.Book(date, 2, 1, "Конкурентная запись"); } catch (IOException) { locked = true; }
                Check(locked, "Параллельная запись не обходит блокировку журнала.");
            }
            File.WriteAllText(path, "broken");
            bool invalid = false;
            try { store.Book(date, 2, 1, "Повреждённые данные"); }
            catch (System.Text.Json.JsonException) { invalid = true; }
            Check(invalid && File.ReadAllText(path) == "broken", "Повреждённый журнал не перезаписывается пустыми данными.");
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        return count;
    }
}
