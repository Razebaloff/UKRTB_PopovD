namespace LP_PopovD_App
{
    internal sealed class InformationPanel : TableLayoutPanel
    {
        internal static readonly RectangleF SceneBounds = new(184, 203, 1232, 385);
        internal static readonly RectangleF MapBounds = new(345, 627, 438, 60);
        internal static readonly RectangleF SiteBounds = new(817, 627, 438, 60);
        internal const string MapUrl = "https://yandex.ru/profile/143450221364";
        internal const string SiteUrl = "https://territoryhockeyufa.wixsite.com/site/nashi-kontakty";
        private readonly Label title, trainingHeading, contactHeading;
        private Font? titleFont, headingFont, bodyFont;
        private float currentScale;

        internal InformationPanel()
        {
            DoubleBuffered = true;
            TabStop = false;
            AccessibleRole = AccessibleRole.Grouping;
            AccessibleName = "Информация о тренировочном центре «Территория хоккея»";
            BackColor = Color.FromArgb(19, 45, 65);
            ForeColor = Color.FromArgb(231, 240, 245);
            ColumnCount = 2;
            RowCount = 6;
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 49));
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 51));
            foreach (int percent in new[] { 26, 22, 13, 13, 13, 13 })
                RowStyles.Add(new RowStyle(SizeType.Percent, percent));

            title = AddLabel("Тренировочный центр в Уфе", 0, 0);
            SetColumnSpan(title, 2);
            trainingHeading = AddLabel("Занятия", 0, 1);
            contactHeading = AddLabel("Адрес и запись", 1, 1);
            trainingHeading.ForeColor = contactHeading.ForeColor = Color.FromArgb(161, 204, 223);

            AddLabel("Техника катания", 0, 2);
            AddLabel("Владение клюшкой и броски", 0, 3);
            AddLabel("Общая физическая подготовка", 0, 4);
            AddLabel("Мастер-классы", 0, 5);
            AddLabel("Уфа, ул. Авроры, 17/2", 1, 2);
            AddLabel("Ежедневно с 09:00 до 23:00, по записи", 1, 3);
            AddLabel("+7 (917) 364-89-99", 1, 4);
            AddLabel("territorihockey@mail.ru", 1, 5);
        }

        private Label AddLabel(string text, int column, int row)
        {
            Label label = new() { Text = text, Dock = DockStyle.Fill, Margin = Padding.Empty,
                TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false };
            Controls.Add(label, column, row);
            return label;
        }

        internal void SetSceneBounds(Rectangle bounds)
        {
            float scale = Math.Max(.25f, bounds.Width / SceneBounds.Width);
            Bounds = bounds;
            if (Math.Abs(scale - currentScale) < .001f) return;
            currentScale = scale;
            SuspendLayout();
            try
            {
                int horizontal = (int)(52 * scale), vertical = (int)(28 * scale);
                Padding = new Padding(horizontal, vertical, horizontal, vertical);
                Font? oldBody = bodyFont, oldTitle = titleFont, oldHeading = headingFont;
                Font = bodyFont = new Font(RinkTypography.FamilyName, 25 * scale, GraphicsUnit.Pixel);
                title.Font = titleFont = new Font(RinkTypography.FamilyName, 44 * scale, FontStyle.Bold, GraphicsUnit.Pixel);
                trainingHeading.Font = contactHeading.Font = headingFont =
                    new Font(RinkTypography.FamilyName, 28 * scale, FontStyle.Bold, GraphicsUnit.Pixel);
                oldTitle?.Dispose();
                oldHeading?.Dispose();
                oldBody?.Dispose();
            }
            finally { ResumeLayout(true); }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) { titleFont?.Dispose(); headingFont?.Dispose(); bodyFont?.Dispose(); }
        }
    }
}
