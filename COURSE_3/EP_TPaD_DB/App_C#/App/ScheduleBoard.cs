using System.Drawing.Drawing2D;
using System.Globalization;

namespace LP_PopovD_App
{
    // Недельная сетка расписания. Показывает только запланированные
    // интервалы — наличие конкретных тренировок/броней не выдумывает.
    // Одна отрисовка на изменение выбранной даты/слота, не постоянный таймер.
    internal sealed class ScheduleBoard : Control
    {
        private const float LogicalWidth = 1380;
        private const float LogicalHeight = 600;
        private const float TableLeft = 40;
        private const float DaysLeft = 216;
        private const float DayWidth = 162;
        private const float DayHeaderTop = 131;
        private const float RowTop = 181;
        private const float RowHeight = 34;
        private const float TableWidth = 1310;
        private readonly CultureInfo russian = CultureInfo.GetCultureInfo("ru-RU");
        private DateTime monday = TrainingSchedule.MondayOf(DateTime.Today);
        private int selectedDay = ((int)DateTime.Today.DayOfWeek + 6) % 7;
        private int selectedSlot;
        private bool hoveredLeft, hoveredRight;

        internal ScheduleBoard()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.Opaque |
                ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            Cursor = Cursors.Default;
            AccessibleName = "Расписание тренировок на неделю. Стрелки меняют день и занятие.";
            AccessibleRole = AccessibleRole.Client;
        }

        private static GraphicsPath Rounded(RectangleF rect, float radius)
        {
            float d = radius * 2;
            GraphicsPath path = new();
            path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static void Label(Graphics g, string text, Font font, Brush ink,
            RectangleF area, StringAlignment horizontal = StringAlignment.Center)
        {
            using StringFormat alignment = new()
            {
                Alignment = horizontal,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            };
            g.DrawString(text, font, ink, area, alignment);
        }

        private void ShiftWeek(int delta)
        {
            monday = monday.AddDays(delta * 7);
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (Parent is Form1 form)
                form.PaintScene(e.Graphics, Bounds);
            else
                e.Graphics.Clear(Color.FromArgb(140, 189, 216));
        }

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

                using GraphicsPath surface = Rounded(new RectangleF(12, 12, 1356, 577), 27);
                using LinearGradientBrush background = new(new RectangleF(12, 12, 1356, 577),
                    Color.FromArgb(250, 11, 38, 65), Color.FromArgb(252, 5, 21, 43), 80f);
                using Pen rim = new(Color.FromArgb(Focused ? 223 : 116, 109, 195, 234),
                    Focused ? 2.7f : 1.7f);
                g.FillPath(background, surface);
                g.DrawPath(rim, surface);

                using Font title = new("Segoe UI", 30, FontStyle.Bold, GraphicsUnit.Pixel);
                using Font subtitle = new("Segoe UI", 17, FontStyle.Regular, GraphicsUnit.Pixel);
                using Font weekFont = new("Segoe UI", 20, FontStyle.Bold, GraphicsUnit.Pixel);
                using Font heading = new("Segoe UI", 16.5f, FontStyle.Bold, GraphicsUnit.Pixel);
                using Font rowFont = new("Segoe UI", 17.5f, FontStyle.Bold, GraphicsUnit.Pixel);
                using Font dashFont = new("Segoe UI", 18, FontStyle.Regular, GraphicsUnit.Pixel);
                using SolidBrush white = new(Color.FromArgb(249, 253, 255));
                using SolidBrush pale = new(Color.FromArgb(182, 220, 240));
                using SolidBrush muted = new(Color.FromArgb(125, 178, 204));
                using SolidBrush highlight = new(Color.FromArgb(50, 94, 165, 210));
                using SolidBrush selected = new(Color.FromArgb(96, 43, 111, 158));
                using Pen grid = new(Color.FromArgb(45, 150, 193, 220), 1f);

                Label(g, "РАСПИСАНИЕ ЗАНЯТИЙ", title, white,
                    new RectangleF(46, 27, 720, 52), StringAlignment.Near);
                Label(g, "ЛЕДОВАЯ АРЕНА  /  ЕЖЕДНЕВНЫЕ ЧАСОВЫЕ СЕАНСЫ", subtitle, pale,
                    new RectangleF(48, 78, 695, 28), StringAlignment.Near);

                string weekRange = $"{monday:dd.MM} — {monday.AddDays(6):dd.MM.yyyy}";
                Label(g, weekRange, weekFont, white,
                    new RectangleF(837, 51, 292, 40));
                DrawWeekArrow(g, new RectangleF(1160, 52, 64, 40), "‹", hoveredLeft, white);
                DrawWeekArrow(g, new RectangleF(1240, 52, 64, 40), "›", hoveredRight, white);

                using SolidBrush headerBackground = new(Color.FromArgb(111, 34, 91, 129));
                g.FillRectangle(headerBackground, TableLeft, DayHeaderTop, TableWidth, 45);
                Label(g, "ВРЕМЯ", heading, white,
                    new RectangleF(TableLeft + 8, DayHeaderTop, 160, 45));
                for (int day = 0; day < 7; day++)
                {
                    DateTime date = monday.AddDays(day);
                    float x = DaysLeft + day * DayWidth;
                    if (day == selectedDay)
                        g.FillRectangle(selected, x + 1, DayHeaderTop + 1, DayWidth - 2, 43);
                    string label = date.ToString("ddd", russian).ToUpper(russian);
                    string text = $"{label}  {date:dd.MM}";
                    Label(g, text, heading, white,
                        new RectangleF(x, DayHeaderTop, DayWidth, 45));
                }

                int count = TrainingSchedule.Slots.Count;
                for (int index = 0; index < count; index++)
                {
                    float y = RowTop + index * RowHeight;
                    if (index % 2 == 0)
                        g.FillRectangle(highlight, TableLeft, y, TableWidth, RowHeight - 1);
                    g.DrawLine(grid, TableLeft, y + RowHeight,
                        TableLeft + TableWidth, y + RowHeight);

                    TrainingSlot slot = TrainingSchedule.Slots[index];
                    Label(g, slot.Label, rowFont, white,
                        new RectangleF(TableLeft + 8, y, 160, RowHeight));
                    for (int day = 0; day < 7; day++)
                    {
                        float x = DaysLeft + day * DayWidth;
                        if (day == selectedDay && index == selectedSlot)
                        {
                            using GraphicsPath choice = Rounded(
                                new RectangleF(x + 4, y + 2, DayWidth - 8, RowHeight - 4), 8);
                            g.FillPath(selected, choice);
                        }
                        Label(g, "—", dashFont, day == selectedDay ? pale : muted,
                            new RectangleF(x, y, DayWidth, RowHeight));
                    }
                }
                for (int day = 0; day <= 7; day++)
                {
                    float x = DaysLeft + day * DayWidth;
                    g.DrawLine(grid, x, DayHeaderTop, x, RowTop + count * RowHeight);
                }

                Label(g, "СЕТКА ВРЕМЕНИ ДЛЯ ПЛАНИРОВАНИЯ. ЗАНЯТИЯ ПОКА НЕ НАЗНАЧЕНЫ.",
                    subtitle, pale, new RectangleF(42, 562, 1110, 25), StringAlignment.Near);
                Label(g, "← →  ДНИ     ↑ ↓  ВРЕМЯ", subtitle, muted,
                    new RectangleF(1112, 562, 221, 25));
            }
            finally { g.Restore(saved); }
        }

        private static void DrawWeekArrow(Graphics g, RectangleF region, string sign,
            bool hovered, Brush textInk)
        {
            using GraphicsPath shape = Rounded(region, 13);
            using SolidBrush fill = new(Color.FromArgb(hovered ? 130 : 69, 59, 132, 177));
            using Pen edge = new(Color.FromArgb(145, 134, 201, 239), 1.4f);
            using Font arrow = new("Segoe UI", 27, FontStyle.Bold, GraphicsUnit.Pixel);
            g.FillPath(fill, shape);
            g.DrawPath(edge, shape);
            Label(g, sign, arrow, textInk, region);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (Width < 10 || Height < 10) return;
            float x = e.X * LogicalWidth / Width;
            float y = e.Y * LogicalHeight / Height;
            if (y >= 52 && y <= 96 && x >= 1160 && x <= 1224)
                ShiftWeek(-1);
            else if (y >= 52 && y <= 96 && x >= 1240 && x <= 1304)
                ShiftWeek(1);
            else if (y >= DayHeaderTop && y <= DayHeaderTop + 45 &&
                x >= DaysLeft && x < DaysLeft + 7 * DayWidth)
            {
                selectedDay = Math.Clamp((int)((x - DaysLeft) / DayWidth), 0, 6);
                Invalidate();
            }
            else if (y >= RowTop && y < RowTop + RowHeight * TrainingSchedule.Slots.Count &&
                x >= TableLeft && x < TableLeft + TableWidth)
            {
                selectedSlot = Math.Clamp((int)((y - RowTop) / RowHeight),
                    0, TrainingSchedule.Slots.Count - 1);
                if (x >= DaysLeft)
                    selectedDay = Math.Clamp((int)((x - DaysLeft) / DayWidth), 0, 6);
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            float x = Width > 0 ? e.X * LogicalWidth / Width : 0;
            float y = Height > 0 ? e.Y * LogicalHeight / Height : 0;
            bool left = y >= 52 && y <= 96 && x >= 1160 && x <= 1224;
            bool right = y >= 52 && y <= 96 && x >= 1240 && x <= 1304;
            if (left != hoveredLeft || right != hoveredRight)
            {
                hoveredLeft = left;
                hoveredRight = right;
                Cursor = left || right ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hoveredLeft = hoveredRight = false;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down
                or Keys.PageUp or Keys.PageDown or Keys.Home)
                return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.KeyCode)
            {
                case Keys.Left:
                    if (selectedDay == 0) { selectedDay = 6; ShiftWeek(-1); }
                    else selectedDay--;
                    break;
                case Keys.Right:
                    if (selectedDay == 6) { selectedDay = 0; ShiftWeek(1); }
                    else selectedDay++;
                    break;
                case Keys.Up:
                    selectedSlot = Math.Max(0, selectedSlot - 1);
                    break;
                case Keys.Down:
                    selectedSlot = Math.Min(TrainingSchedule.Slots.Count - 1, selectedSlot + 1);
                    break;
                case Keys.PageUp:
                    ShiftWeek(-1);
                    break;
                case Keys.PageDown:
                    ShiftWeek(1);
                    break;
                case Keys.Home:
                    monday = TrainingSchedule.MondayOf(DateTime.Today);
                    selectedDay = ((int)DateTime.Today.DayOfWeek + 6) % 7;
                    selectedSlot = 0;
                    break;
                default:
                    return;
            }
            e.Handled = e.SuppressKeyPress = true;
            Invalidate();
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }
    }
}
