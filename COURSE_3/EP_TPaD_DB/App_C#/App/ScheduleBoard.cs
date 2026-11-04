using System.Drawing.Drawing2D;
using System.Globalization;

namespace LP_PopovD_App
{
    internal sealed class ScheduleBoard : Control
    {
        private const float LogicalWidth = 1600, LogicalHeight = 900;
        private const float TableLeft = 48, DaysLeft = 188, DayWidth = 194;
        private const float DayHeaderTop = 202, RowTop = 274, RowHeight = 44;
        private const float TableWidth = 1498;
        private static readonly RectangleF PreviousWeek = new(1360, 151, 72, 40);
        private static readonly RectangleF NextWeek = new(1450, 151, 72, 40);
        private static readonly RectangleF TodayButton = new(920, 151, 110, 40);
        private static readonly RectangleF[] ZoneChoices =
            { new(770, 800, 126, 45), new(906, 800, 126, 45), new(1042, 800, 156, 45) };
        private static readonly RectangleF BookButton = new(1220, 795, 326, 55);
        private readonly CultureInfo russian = CultureInfo.GetCultureInfo("ru-RU");
        private readonly TrainingBookingStore bookings = new();
        private readonly ToolTip tip = new();
        private IReadOnlyList<TrainingBooking> rows = Array.Empty<TrainingBooking>();
        private DateTime monday = TrainingSchedule.MondayOf(DateTime.Today);
        private int selectedDay = ((int)DateTime.Today.DayOfWeek + 6) % 7, selectedSlot, selectedZones = 1;
        private PointF hover = new(-1, -1);
        private string message = "", dataError = "";
        internal string SuggestedTenant = "";
        private DateTime SelectedDate => monday.AddDays(selectedDay);

        internal ScheduleBoard()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.Opaque | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            AccessibleName = "Расписание двух тренировочных зон. Стрелки выбирают день и время, пробел переключает зоны, Enter открывает бронь.";
            AccessibleRole = AccessibleRole.Client;
        }

        internal void Reload()
        {
            try { rows = bookings.Load(); dataError = ""; }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            { dataError = "Журнал аренды недоступен. Бронирование приостановлено."; }
            Invalidate();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible) Reload();
        }

        internal static void DrawBackdrop(Graphics g, Size size)
        {
            if (size.Width < 1 || size.Height < 1) return;
            GraphicsState saved = g.Save();
            using LinearGradientBrush background = new(new Rectangle(Point.Empty, size),
                Color.FromArgb(9, 30, 54), Color.FromArgb(4, 14, 30), 85f);
            g.FillRectangle(background, new Rectangle(Point.Empty, size));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using SolidBrush upper = new(Color.FromArgb(18, 97, 166, 214));
            using SolidBrush lower = new(Color.FromArgb(9, 83, 160, 205));
            g.FillEllipse(upper, -size.Width * .2f, -size.Height * .62f, size.Width * 1.25f, size.Height * .93f);
            g.FillEllipse(lower, size.Width * .45f, size.Height * .61f, size.Width * .88f, size.Height * .66f);
            g.Restore(saved);
        }

        private static GraphicsPath Rounded(RectangleF r, float radius = 8)
        {
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            GraphicsPath path = new();
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static void Label(Graphics g, string text, Font font, Brush ink, RectangleF r,
            StringAlignment alignment = StringAlignment.Center)
        {
            using StringFormat format = new() { Alignment = alignment, LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            g.DrawString(text, font, ink, r, format);
        }

        private TrainingBooking? At(DateTime date, int slot, int zone) =>
            dataError.Length > 0 ? null : rows.FirstOrDefault(row => row.Date == date.Date && row.Slot == slot && (row.Zones & zone) != 0);
        private bool Started(DateTime date, int slot) =>
            date.Date.Add(TrainingSchedule.Slots[slot].Start.ToTimeSpan()) <= DateTime.Now;
        private bool Available(int zones) => dataError.Length == 0 && !Started(SelectedDate, selectedSlot) &&
            !rows.Any(row => row.Date == SelectedDate && row.Slot == selectedSlot && (row.Zones & zones) != 0);
        private TrainingBooking? SelectedBooking
        {
            get
            {
                if (selectedZones != 3) return At(SelectedDate, selectedSlot, selectedZones);
                TrainingBooking? first = At(SelectedDate, selectedSlot, 1);
                return first != null && At(SelectedDate, selectedSlot, 2)?.Id == first.Id ? first : null;
            }
        }

        private string OccupancySummary()
        {
            if (dataError.Length > 0) return "Данные о занятости недоступны.";
            TrainingBooking? first = At(SelectedDate, selectedSlot, 1), second = At(SelectedDate, selectedSlot, 2);
            if (first != null && first.Id == second?.Id) return "Обе зоны: " + first.Tenant;
            return "Зона 1: " + (first?.Tenant ?? "свободна") + "   ·   Зона 2: " + (second?.Tenant ?? "свободна");
        }

        protected override void OnPaintBackground(PaintEventArgs e) => DrawBackdrop(e.Graphics, ClientSize);
        protected override void OnPaint(PaintEventArgs e)
        {
            OnPaintBackground(e);
            if (Width < 40 || Height < 30) return;
            Graphics g = e.Graphics;
            GraphicsState saved = g.Save();
            try
            {
                g.ScaleTransform(Width / LogicalWidth, Height / LogicalHeight);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                using Font title = new(RinkTypography.FamilyName, 38, FontStyle.Bold, GraphicsUnit.Pixel);
                using Font medium = new(RinkTypography.FamilyName, 20, FontStyle.Bold, GraphicsUnit.Pixel);
                using Font normal = new(RinkTypography.FamilyName, 16, FontStyle.Regular, GraphicsUnit.Pixel);
                using Font small = new(RinkTypography.FamilyName, 13, FontStyle.Bold, GraphicsUnit.Pixel);
                using SolidBrush white = new(Color.FromArgb(244, 251, 255));
                using SolidBrush pale = new(Color.FromArgb(176, 214, 235));
                using SolidBrush muted = new(Color.FromArgb(119, 161, 186));
                using SolidBrush freeInk = new(Color.FromArgb(133, 237, 211));
                using SolidBrush busyInk = new(Color.FromArgb(255, 180, 174));
                using SolidBrush selected = new(Color.FromArgb(65, 116, 181, 219));
                using SolidBrush rowFill = new(Color.FromArgb(29, 70, 112, 147));
                using Pen grid = new(Color.FromArgb(42, 143, 190, 218), 1);

                Label(g, "РАСПИСАНИЕ И АРЕНДА", title, white, new(252, 38, 970, 61), StringAlignment.Near);
                Label(g, "ДВЕ ТРЕНИРОВОЧНЫЕ ЗОНЫ  ·  СЕАНСЫ ПО 60 МИНУТ", normal, pale,
                    new(254, 105, 900, 30), StringAlignment.Near);
                Label(g, "● СВОБОДНО", small, freeInk, new(52, 151, 160, 40), StringAlignment.Near);
                Label(g, "● ЗАНЯТО", small, busyInk, new(227, 151, 140, 40), StringAlignment.Near);
                Label(g, "Выберите ячейку зоны", normal, pale, new(397, 151, 460, 40), StringAlignment.Near);
                ActionButton(g, TodayButton, "СЕГОДНЯ", normal, pale, TodayButton.Contains(hover));
                Label(g, $"{monday:dd.MM} — {monday.AddDays(6):dd.MM.yyyy}", medium, white, new(1044, 151, 302, 40));
                ActionButton(g, PreviousWeek, "‹", medium, white, PreviousWeek.Contains(hover));
                ActionButton(g, NextWeek, "›", medium, white, NextWeek.Contains(hover));

                using SolidBrush header = new(Color.FromArgb(100, 34, 91, 129));
                g.FillRectangle(header, TableLeft, DayHeaderTop, TableWidth, RowTop - DayHeaderTop);
                Label(g, "ВРЕМЯ", medium, white, new(TableLeft, DayHeaderTop, DaysLeft - TableLeft, 72));
                for (int day = 0; day < 7; day++)
                {
                    float x = DaysLeft + day * DayWidth;
                    DateTime date = monday.AddDays(day);
                    if (day == selectedDay) g.FillRectangle(selected, x + 1, DayHeaderTop, DayWidth - 2, 70);
                    Label(g, date.ToString("ddd", russian).ToUpper(russian) + $"  {date:dd.MM}", medium,
                        date == DateTime.Today ? freeInk : white, new(x, DayHeaderTop, DayWidth, 42));
                    Label(g, "ЗОНА 1", small, pale, new(x, DayHeaderTop + 42, DayWidth / 2, 28));
                    Label(g, "ЗОНА 2", small, pale, new(x + DayWidth / 2, DayHeaderTop + 42, DayWidth / 2, 28));
                }
                for (int slot = 0; slot < TrainingSchedule.Slots.Count; slot++)
                {
                    float y = RowTop + slot * RowHeight;
                    if (slot % 2 == 0) g.FillRectangle(rowFill, TableLeft, y, TableWidth, RowHeight);
                    Label(g, TrainingSchedule.Slots[slot].Label, normal, white, new(TableLeft, y, DaysLeft - TableLeft, RowHeight));
                    for (int day = 0; day < 7; day++)
                        for (int zone = 0; zone < 2; zone++)
                        {
                            RectangleF cell = new(DaysLeft + day * DayWidth + zone * DayWidth / 2 + 4,
                                y + 5, DayWidth / 2 - 8, RowHeight - 10);
                            TrainingBooking? booking = At(monday.AddDays(day), slot, 1 << zone);
                            bool past = Started(monday.AddDays(day), slot);
                            bool chosen = day == selectedDay && slot == selectedSlot && (selectedZones & (1 << zone)) != 0;
                            using GraphicsPath pill = Rounded(cell, 6);
                            using SolidBrush fill = new(booking != null ? Color.FromArgb(74, 132, 57, 70)
                                : past ? Color.FromArgb(28, 90, 113, 135) : Color.FromArgb(38, 41, 156, 145));
                            g.FillPath(fill, pill);
                            if (chosen)
                            {
                                using Pen choice = new(Color.FromArgb(215, 126, 218, 255), Focused ? 2 : 1.25f);
                                g.DrawPath(choice, pill);
                            }
                            Label(g, booking != null ? "ЗАНЯТО" : past ? "ПРОШЛО" : dataError.Length > 0 ? "ОШИБКА" : "СВОБОДНО",
                                small, booking != null ? busyInk : past || dataError.Length > 0 ? muted : freeInk, cell);
                        }
                    g.DrawLine(grid, TableLeft, y + RowHeight, TableLeft + TableWidth, y + RowHeight);
                }
                for (int day = 0; day <= 7; day++)
                    g.DrawLine(grid, DaysLeft + day * DayWidth, DayHeaderTop,
                        DaysLeft + day * DayWidth, RowTop + TrainingSchedule.Slots.Count * RowHeight);

                Label(g, SelectedDate.ToString("dddd, dd MMMM", russian).ToUpper(russian) +
                    "  ·  " + TrainingSchedule.Slots[selectedSlot].Label, medium, white,
                    new(52, 778, 700, 34), StringAlignment.Near);
                Label(g, OccupancySummary(), normal, pale, new(52, 817, 700, 36), StringAlignment.Near);
                for (int i = 0; i < ZoneChoices.Length; i++)
                    ActionButton(g, ZoneChoices[i], i == 2 ? "ОБЕ ЗОНЫ" : $"ЗОНА {i + 1}", small,
                        selectedZones == i + 1 ? white : pale, selectedZones == i + 1 || ZoneChoices[i].Contains(hover));
                bool available = Available(selectedZones);
                ActionButton(g, BookButton, available ? "ЗАБРОНИРОВАТЬ" : SelectedBooking != null ? "ПОСМОТРЕТЬ БРОНЬ" : "НЕДОСТУПНО",
                    medium, available || SelectedBooking != null ? white : muted, BookButton.Contains(hover), available);
                Label(g, dataError.Length > 0 ? dataError : message.Length > 0 ? message :
                    "ЛОКАЛЬНЫЙ ЖУРНАЛ: БРОНИ СОХРАНЯЮТСЯ НА ЭТОМ УСТРОЙСТВЕ.",
                    normal, dataError.Length > 0 ? busyInk : pale, new(52, 861, 1490, 27), StringAlignment.Near);
            }
            finally { g.Restore(saved); }
        }

        private static void ActionButton(Graphics g, RectangleF r, string text, Font font, Brush ink,
            bool highlighted, bool primary = false)
        {
            using GraphicsPath shape = Rounded(r, 10);
            using SolidBrush fill = new(primary ? Color.FromArgb(highlighted ? 235 : 205, 28, 118, 153)
                : Color.FromArgb(highlighted ? 140 : 68, 59, 132, 177));
            using Pen edge = new(Color.FromArgb(highlighted ? 200 : 80, 132, 201, 235), 1.2f);
            g.FillPath(fill, shape);
            g.DrawPath(edge, shape);
            Label(g, text, font, ink, r);
        }

        private PointF Logical(Point point) => new(point.X * LogicalWidth / Math.Max(1, Width), point.Y * LogicalHeight / Math.Max(1, Height));
        private bool CellAt(PointF p, out int day, out int slot, out int zone)
        {
            day = slot = zone = 0;
            if (p.X < DaysLeft || p.X >= DaysLeft + DayWidth * 7 || p.Y < RowTop ||
                p.Y >= RowTop + RowHeight * TrainingSchedule.Slots.Count) return false;
            day = (int)((p.X - DaysLeft) / DayWidth);
            slot = (int)((p.Y - RowTop) / RowHeight);
            zone = (p.X - DaysLeft - day * DayWidth) < DayWidth / 2 ? 1 : 2;
            return true;
        }

        private void ActivateBooking()
        {
            Reload(); // Повторно читаем перед выбором, а запись проверяет конфликт под блокировкой.
            TrainingBooking? occupied = SelectedBooking;
            if (occupied != null)
            {
                string zones = occupied.Zones == 3 ? "Обе зоны" : occupied.Zones == 1 ? "Зона 1" : "Зона 2";
                if (MessageBox.Show(this, $"{occupied.Date:dd.MM.yyyy} · {TrainingSchedule.Slots[occupied.Slot].Label}\n" +
                    $"{zones}\nАрендатор: {occupied.Tenant}\n\nСнять эту локальную бронь?",
                    "Локальная бронь", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                {
                    try { bookings.Cancel(occupied.Id); message = "Локальная бронь снята."; }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                        InvalidOperationException or System.Text.Json.JsonException) { message = "Не удалось снять бронь: " + error.Message; }
                    Reload();
                }
                return;
            }
            if (!Available(selectedZones)) return;
            using TrainingBookingDialog dialog = new(SelectedDate, TrainingSchedule.Slots[selectedSlot], selectedZones, SuggestedTenant);
            if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
            try
            {
                bookings.Book(SelectedDate, selectedSlot, selectedZones, dialog.Tenant);
                SuggestedTenant = dialog.Tenant;
                message = "Бронь сохранена на этом устройстве.";
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                InvalidOperationException or ArgumentException or System.Text.Json.JsonException)
            { message = "Бронь не сохранена: " + error.Message; }
            Reload();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Focus();
            PointF p = Logical(e.Location);
            message = "";
            if (PreviousWeek.Contains(p)) monday = monday.AddDays(-7);
            else if (NextWeek.Contains(p)) monday = monday.AddDays(7);
            else if (TodayButton.Contains(p)) { monday = TrainingSchedule.MondayOf(DateTime.Today); selectedDay = ((int)DateTime.Today.DayOfWeek + 6) % 7; }
            else if (CellAt(p, out int day, out int slot, out int zone))
            { selectedDay = day; selectedSlot = slot; selectedZones = zone; }
            else if (p.Y >= DayHeaderTop && p.Y < RowTop && p.X >= DaysLeft && p.X < DaysLeft + DayWidth * 7)
            {
                selectedDay = (int)((p.X - DaysLeft) / DayWidth);
                if (p.Y > DayHeaderTop + 42)
                    selectedZones = p.X - DaysLeft - selectedDay * DayWidth < DayWidth / 2 ? 1 : 2;
            }
            else if (BookButton.Contains(p)) ActivateBooking();
            else for (int i = 0; i < ZoneChoices.Length; i++) if (ZoneChoices[i].Contains(p)) selectedZones = i + 1;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            PointF p = Logical(e.Location);
            bool hand = PreviousWeek.Contains(p) || NextWeek.Contains(p) || TodayButton.Contains(p) ||
                ZoneChoices.Any(r => r.Contains(p)) || BookButton.Contains(p);
            string help = "";
            if (CellAt(p, out int day, out int slot, out int zone))
            {
                hand = true;
                TrainingBooking? booking = At(monday.AddDays(day), slot, zone);
                help = $"{monday.AddDays(day):dd.MM} · {TrainingSchedule.Slots[slot].Label} · Зона {(zone == 1 ? 1 : 2)}\n" +
                    (dataError.Length > 0 ? "Данные о занятости недоступны" : booking == null ? Started(monday.AddDays(day), slot)
                        ? "Сеанс уже начался или завершён" : "Свободна в локальном журнале" : "Занята: " + booking.Tenant);
            }
            if (tip.GetToolTip(this) != help) tip.SetToolTip(this, help);
            bool changed = PreviousWeek.Contains(hover) != PreviousWeek.Contains(p) ||
                NextWeek.Contains(hover) != NextWeek.Contains(p) || TodayButton.Contains(hover) != TodayButton.Contains(p) ||
                BookButton.Contains(hover) != BookButton.Contains(p) ||
                ZoneChoices.Any(r => r.Contains(hover) != r.Contains(p));
            hover = p;
            if (changed) Invalidate();
            Cursor = hand ? Cursors.Hand : Cursors.Default;
        }
        protected override void OnMouseLeave(EventArgs e)
        { base.OnMouseLeave(e); hover = new(-1, -1); tip.SetToolTip(this, ""); Cursor = Cursors.Default; Invalidate(); }

        protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is
            Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.Enter or Keys.Space or Keys.F5 || base.IsInputKey(keyData);
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.KeyCode)
            {
                case Keys.Left: if (selectedDay-- == 0) { selectedDay = 6; monday = monday.AddDays(-7); } break;
                case Keys.Right: if (selectedDay++ == 6) { selectedDay = 0; monday = monday.AddDays(7); } break;
                case Keys.Up: selectedSlot = Math.Max(0, selectedSlot - 1); break;
                case Keys.Down: selectedSlot = Math.Min(TrainingSchedule.Slots.Count - 1, selectedSlot + 1); break;
                case Keys.PageUp: monday = monday.AddDays(-7); break;
                case Keys.PageDown: monday = monday.AddDays(7); break;
                case Keys.Home: monday = TrainingSchedule.MondayOf(DateTime.Today); selectedDay = ((int)DateTime.Today.DayOfWeek + 6) % 7; break;
                case Keys.Space: selectedZones = selectedZones % 3 + 1; break;
                case Keys.Enter: ActivateBooking(); break;
                case Keys.F5: Reload(); break;
                default: return;
            }
            e.Handled = e.SuppressKeyPress = true;
            Invalidate();
        }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void Dispose(bool disposing) { if (disposing) tip.Dispose(); base.Dispose(disposing); }
    }
}
