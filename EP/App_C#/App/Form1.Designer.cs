namespace LP_PopovD_App
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                components?.Dispose();
                DisposeArtwork();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            rentButton = new RinkMenuButton();
            profileButton = new RinkMenuButton();
            informationButton = new RinkMenuButton();
            scheduleButton = new RinkMenuButton();
            entryButton = new PuckButton();
            SuspendLayout();
            //
            // rentButton
            //
            rentButton.AccessibleName = "Заказать аренду";
            rentButton.Location = new Point(296, 195);
            rentButton.Name = "rentButton";
            rentButton.Size = new Size(207, 207);
            rentButton.TabIndex = 0;
            rentButton.Text = "ЗАКАЗАТЬ\nАРЕНДУ";
            rentButton.Visible = false;
            //
            // profileButton
            //
            profileButton.AccessibleName = "Профиль";
            profileButton.Location = new Point(296, 498);
            profileButton.Name = "profileButton";
            profileButton.Size = new Size(207, 207);
            profileButton.TabIndex = 1;
            profileButton.Text = "ПРОФИЛЬ";
            profileButton.Visible = false;
            //
            // informationButton
            //
            informationButton.AccessibleName = "Информация";
            informationButton.Location = new Point(1097, 195);
            informationButton.Name = "informationButton";
            informationButton.Size = new Size(207, 207);
            informationButton.TabIndex = 2;
            informationButton.Text = "ИНФОРМАЦИЯ";
            informationButton.Visible = false;
            //
            // scheduleButton
            //
            scheduleButton.AccessibleName = "Расписание занятий";
            scheduleButton.Location = new Point(1097, 498);
            scheduleButton.Name = "scheduleButton";
            scheduleButton.Size = new Size(207, 207);
            scheduleButton.TabIndex = 3;
            scheduleButton.Text = "РАСПИСАНИЕ\nЗАНЯТИЙ";
            scheduleButton.Visible = false;
            //
            // entryButton
            //
            entryButton.Location = new Point(570, 220);
            entryButton.Name = "entryButton";
            entryButton.Size = new Size(460, 460);
            entryButton.TabIndex = 0;
            //
            // Form1
            //
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.FromArgb(225, 241, 250);
            ClientSize = new Size(1600, 900);
            Controls.Add(rentButton);
            Controls.Add(profileButton);
            Controls.Add(informationButton);
            Controls.Add(scheduleButton);
            Controls.Add(entryButton);
            MinimumSize = new Size(960, 600);
            Name = "Form1";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "ТЕРРИТОРИЯ ХОККЕЯ";
            ResumeLayout(false);
        }
        #endregion

        private RinkMenuButton rentButton = null!;
        private RinkMenuButton profileButton = null!;
        private RinkMenuButton informationButton = null!;
        private RinkMenuButton scheduleButton = null!;
        private PuckButton entryButton = null!;
    }
}

