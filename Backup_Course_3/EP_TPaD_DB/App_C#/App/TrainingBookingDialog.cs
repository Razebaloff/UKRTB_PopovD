namespace LP_PopovD_App
{
    internal sealed class TrainingBookingDialog : Form
    {
        private sealed class DialogButton : Button
        {
            protected override bool ShowFocusCues => false;
            protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); BackColor = Color.FromArgb(35, 121, 165); }
            protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); BackColor = Color.FromArgb(26, 87, 123); }
        }
        private readonly TextBox tenant = new();
        internal string Tenant => tenant.Text.Trim();

        internal TrainingBookingDialog(DateTime date, TrainingSlot slot, int zones, string suggestedTenant)
        {
            Text = "Бронирование зон";
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(520, 340);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(12, 34, 57);
            ForeColor = Color.FromArgb(239, 249, 255);
            Font = new Font(RinkTypography.FamilyName, 11, FontStyle.Regular);

            Label heading = new() { Text = "ПРОВЕРЬТЕ БРОНЬ", AutoSize = false,
                Bounds = new Rectangle(28, 24, 460, 32),
                Font = new Font(RinkTypography.FamilyName, 16, FontStyle.Bold) };
            Label details = new() { AutoSize = false, Bounds = new Rectangle(28, 68, 460, 58),
                Text = $"{date:dd.MM.yyyy}  ·  {slot.Label}\n" +
                    (zones == 3 ? "Обе зоны: зона 1 и зона 2" : $"Зона {(zones == 1 ? 1 : 2)}") };
            Label name = new() { Text = "Арендатор / название команды", AutoSize = true,
                Location = new Point(28, 140) };
            tenant.Bounds = new Rectangle(28, 169, 460, 30);
            tenant.MaxLength = 80;
            tenant.Text = suggestedTenant;
            tenant.AccessibleName = "Арендатор или название команды";
            Label note = new() { AutoSize = false, Bounds = new Rectangle(28, 214, 460, 42),
                ForeColor = Color.FromArgb(174, 210, 231),
                Text = "Бронь сохранится только на этом устройстве.\nЗаявка в организацию и оплата не отправляются." };
            Button confirm = new DialogButton { Text = "СОХРАНИТЬ БРОНЬ", Bounds = new Rectangle(248, 275, 240, 40),
                FlatStyle = FlatStyle.Flat, UseVisualStyleBackColor = false, BackColor = Color.FromArgb(26, 108, 153) };
            confirm.FlatAppearance.BorderSize = 0;
            Button cancel = new DialogButton { Text = "ОТМЕНА", Bounds = new Rectangle(28, 275, 195, 40),
                DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false, BackColor = Color.FromArgb(26, 87, 123) };
            confirm.Click += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(tenant.Text))
                {
                    tenant.Focus();
                    tenant.PlaceholderText = "Укажите арендатора";
                    return;
                }
                DialogResult = DialogResult.OK;
            };
            Controls.AddRange(new Control[] { heading, details, name, tenant, note, cancel, confirm });
            AcceptButton = confirm;
            CancelButton = cancel;
            Shown += (_, _) => tenant.Focus();
        }
    }
}
