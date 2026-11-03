namespace LP_PopovD_App
{
    // Чистая модель времени: один сеанс 60 минут, между соседними
    // сеансами 10 минут подготовки. Отдельной строки перерыва нет.
    internal readonly record struct TrainingSlot(TimeOnly Start, TimeOnly End)
    {
        internal string Label => $"{Start:HH:mm}–{End:HH:mm}";
    }

    internal static class TrainingSchedule
    {
        internal static readonly TimeOnly Opening = new(8, 0);
        internal static readonly TimeOnly Closing = new(21, 0);
        internal const int DurationMinutes = 60;
        internal const int PreparationMinutes = 10;

        internal static IReadOnlyList<TrainingSlot> Slots { get; } = BuildSlots();

        private static IReadOnlyList<TrainingSlot> BuildSlots()
        {
            List<TrainingSlot> slots = new();
            TimeOnly start = Opening;
            while (start.AddMinutes(DurationMinutes) <= Closing)
            {
                slots.Add(new TrainingSlot(start, start.AddMinutes(DurationMinutes)));
                start = start.AddMinutes(DurationMinutes + PreparationMinutes);
            }
            return slots.AsReadOnly();
        }

        internal static DateTime MondayOf(DateTime day) =>
            day.Date.AddDays(-(((int)day.DayOfWeek + 6) % 7));
    }
}
