using System;
using System.IO;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var form = new Form { Text = "ЛР 38–39", Width = 700, Height = 450 };
        var editor = new RichTextBox { Dock = DockStyle.Fill };
        var panel = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        var open = new Button { Text = "Открыть", AutoSize = true };
        var save = new Button { Text = "Сохранить", AutoSize = true, Enabled = false };
        panel.Controls.Add(open);
        panel.Controls.Add(save);
        form.Controls.Add(editor);
        form.Controls.Add(panel);

        string path = "";

        open.Click += (sender, e) =>
        {
            using var dialog = new OpenFileDialog { Filter = "Текстовые файлы (*.txt)|*.txt" };
            if (dialog.ShowDialog(form) != DialogResult.OK) return;

            using StreamReader reader = File.OpenText(dialog.FileName);
            editor.Text = reader.ReadToEnd();
            path = dialog.FileName;
            form.Text = Path.GetFileName(path);
            save.Enabled = true;
        };

        save.Click += (sender, e) =>
        {
            using StreamWriter writer = File.CreateText(path);
            writer.Write(editor.Text);
        };

        Application.Run(form);
    }
}