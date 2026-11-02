using System;
using System.Drawing;
using System.Windows.Forms;
using GreetingsSystem.WinForms.Models;

namespace GreetingsSystem.WinForms
{
    public class GreetingResultForm : Form
    {
        public GreetingResultForm(GreetingResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            Text = "Поздравление №" + result.Id;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(700, 400);
            MinimumSize = new Size(500, 300);
            Font = new Font("Segoe UI", 11);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(12)
            };

            layout.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100));

            layout.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 45));

            layout.RowStyles.Add(
                new RowStyle(SizeType.Percent, 100));

            layout.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 50));

            var information = new Label
            {
                Dock = DockStyle.Fill,

                Text = result.AlreadyExists
                    ? "Показан ранее сохранённый вариант."
                    : "Поздравление сформировано и сохранено в истории."
            };

            var textBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Text = result.Text
            };

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 8, 0, 0)
            };

            var copyButton = new Button
            {
                Text = "Скопировать",
                AutoSize = true
            };

            copyButton.Click += delegate
            {
                try
                {
                    Clipboard.SetText(textBox.Text);

                    MessageBox.Show(
                        this,
                        "Текст скопирован.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        this,
                        "Не удалось скопировать текст:\n" +
                        ex.Message);
                }
            };

            var closeButton = new Button
            {
                Text = "Закрыть",
                AutoSize = true,
                DialogResult = DialogResult.Cancel
            };

            buttons.Controls.Add(copyButton);
            buttons.Controls.Add(closeButton);

            layout.Controls.Add(information, 0, 0);
            layout.Controls.Add(textBox, 0, 1);
            layout.Controls.Add(buttons, 0, 2);

            Controls.Add(layout);

            CancelButton = closeButton;
        }
    }
}