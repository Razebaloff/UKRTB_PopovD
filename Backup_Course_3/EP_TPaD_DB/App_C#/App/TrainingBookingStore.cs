using System.Text.Json;

namespace LP_PopovD_App
{
    internal sealed record TrainingBooking(Guid Id, DateTime Date, int Slot, int Zones,
        string Tenant, DateTime CreatedUtc);

    // Локальный журнал. Одна запись может атомарно занимать обе зоны.
    // Общее серверное расписание не имитируется заранее придуманными бронями.
    internal sealed class TrainingBookingStore
    {
        private readonly string path;
        internal TrainingBookingStore(string? path = null) => this.path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TerritoriyaHokkeya", "training-bookings.json");

        internal IReadOnlyList<TrainingBooking> Load()
        {
            if (!File.Exists(path)) return Array.Empty<TrainingBooking>();
            List<TrainingBooking> rows = JsonSerializer.Deserialize<List<TrainingBooking>>(
                File.ReadAllText(path)) ?? throw new InvalidDataException("Пустой журнал аренды.");
            HashSet<Guid> ids = new();
            HashSet<(DateTime Date, int Slot, int Zone)> occupied = new();
            foreach (TrainingBooking row in rows)
            {
                if (row is null || row.Id == Guid.Empty || row.Slot < 0 ||
                    row.Slot >= TrainingSchedule.Slots.Count || row.Zones is < 1 or > 3 ||
                    string.IsNullOrWhiteSpace(row.Tenant) || row.Tenant.Length > 80 ||
                    row.Date != row.Date.Date || !ids.Add(row.Id))
                    throw new InvalidDataException("В журнале аренды найдены некорректные или пересекающиеся брони.");
                foreach (int zone in new[] { 1, 2 })
                    if ((row.Zones & zone) != 0 && !occupied.Add((row.Date, row.Slot, zone)))
                        throw new InvalidDataException("В журнале аренды найдены пересекающиеся брони.");
            }
            return rows.AsReadOnly();
        }

        internal TrainingBooking Book(DateTime date, int slot, int zones, string tenant)
        {
            tenant = tenant.Trim();
            if (slot < 0 || slot >= TrainingSchedule.Slots.Count || zones is < 1 or > 3)
                throw new ArgumentException("Выберите время и одну или две зоны.");
            if (tenant.Length is < 1 or > 80)
                throw new ArgumentException("Укажите арендатора: от 1 до 80 символов.");
            date = date.Date;
            if (date.Add(TrainingSchedule.Slots[slot].Start.ToTimeSpan()) <= DateTime.Now)
                throw new InvalidOperationException("Нельзя бронировать уже начавшийся сеанс.");
            using FileStream lease = Lock();
            List<TrainingBooking> rows = Load().ToList();
            if (rows.Any(row => row.Date == date && row.Slot == slot && (row.Zones & zones) != 0))
                throw new InvalidOperationException("Одна из выбранных зон уже занята. Обновите выбор.");
            TrainingBooking booking = new(Guid.NewGuid(), date, slot, zones, tenant, DateTime.UtcNow);
            rows.Add(booking);
            Save(rows);
            return booking;
        }

        internal void Cancel(Guid id)
        {
            using FileStream lease = Lock();
            List<TrainingBooking> rows = Load().ToList();
            if (rows.RemoveAll(row => row.Id == id) == 0)
                throw new InvalidOperationException("Эта бронь уже снята. Обновите расписание.");
            Save(rows);
        }

        private FileStream Lock()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            return new FileStream(path + ".lock", FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None);
        }

        private void Save(List<TrainingBooking> rows)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonSerializer.Serialize(rows));
                File.Move(temporary, path, overwrite: true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
