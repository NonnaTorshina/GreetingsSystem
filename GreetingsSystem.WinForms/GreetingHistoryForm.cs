using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using GreetingsSystem.WinForms.Data;
using GreetingsSystem.WinForms.Models;

namespace GreetingsSystem.WinForms
{
    public class GreetingHistoryForm : Form
    {
        private readonly GreetingHistoryRepository repository =
            new GreetingHistoryRepository();

        private DataGridView historyGrid;
        private Label countLabel;

        public GreetingHistoryForm()
        {
            CreateInterface();

            Shown += delegate
            {
                RunAction(LoadHistory);
            };
        }

        private void CreateInterface()
        {
            Text = "История поздравлений";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1100, 600);
            MinimumSize = new Size(850, 450);
            Font = new Font("Segoe UI", 10);

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
                new RowStyle(SizeType.Absolute, 90));

            layout.RowStyles.Add(
                new RowStyle(SizeType.Percent, 100));

            layout.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 32));

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = true
            };

            AddButton(toolbar, "Обновить", LoadHistory);
            AddButton(toolbar, "Открыть текст", OpenText);

            AddButton(
                toolbar,
                "Подтвердить",
                delegate { SetStatus("Подтверждено"); });

            AddButton(
                toolbar,
                "Отметить отправленным",
                delegate { SetStatus("Отправлено"); });

            AddButton(
                toolbar,
                "Отменить вариант",
                delegate { SetStatus("Отменено"); });

            historyGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                MultiSelect = false,
                SelectionMode =
                    DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode =
                    DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                BackgroundColor = Color.White
            };

            AddColumn("Id", "№", 35);
            AddColumn("PersonName", "Получатель", 130);
            AddColumn("EventName", "Повод", 100);
            AddColumn("EventDate", "Дата события", 80);
            AddColumn("Status", "Состояние", 100);
            AddColumn("CreatedAt", "Создано, UTC", 130);

            countLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };

            layout.Controls.Add(toolbar, 0, 0);
            layout.Controls.Add(historyGrid, 0, 1);
            layout.Controls.Add(countLabel, 0, 2);

            Controls.Add(layout);
        }

        private void AddButton(
            FlowLayoutPanel toolbar,
            string text,
            Action action)
        {
            var button = new Button
            {
                Text = text,
                AutoSize = true,
                Height = 35
            };

            button.Click += delegate
            {
                RunAction(action);
            };

            toolbar.Controls.Add(button);
        }

        private void AddColumn(
            string property,
            string title,
            float width)
        {
            historyGrid.Columns.Add(
                new DataGridViewTextBoxColumn
                {
                    DataPropertyName = property,
                    HeaderText = title,
                    FillWeight = width
                });
        }

        private void LoadHistory()
        {
            DataTable table = repository.GetAll();

            historyGrid.DataSource = table;
            countLabel.Text = "Записей: " + table.Rows.Count;
        }

        private DataRow GetSelectedRow()
        {
            if (historyGrid.CurrentRow == null)
            {
                throw new InvalidOperationException(
                    "Сначала выберите поздравление в таблице.");
            }

            return ((DataRowView)
                historyGrid.CurrentRow.DataBoundItem).Row;
        }

        private void OpenText()
        {
            DataRow row = GetSelectedRow();

            var result = new GreetingResult
            {
                Id = Convert.ToInt64(row["Id"]),
                Text = Convert.ToString(row["Text"]),
                AlreadyExists = true
            };

            using (var form = new GreetingResultForm(result))
            {
                form.ShowDialog(this);
            }
        }

        private void SetStatus(string newStatus)
        {
            DataRow row = GetSelectedRow();

            long id = Convert.ToInt64(row["Id"]);

            string message;

            if (newStatus == "Отменено")
            {
                message =
                    "Отменить этот вариант?\n\n" +
                    "Он останется в истории, но его содержание " +
                    "снова станет доступно для формирования.";
            }
            else if (newStatus == "Отправлено")
            {
                message =
                    "Подтвердить, что вы уже отправили " +
                    "поздравление самостоятельно?\n\n" +
                    "Программа не выполняет отправку.";
            }
            else
            {
                message =
                    "Подтвердить использование этого поздравления?";
            }

            DialogResult answer = MessageBox.Show(
                this,
                message,
                "Изменение состояния",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);

            if (answer != DialogResult.Yes)
                return;

            repository.ChangeStatus(id, newStatus);
            LoadHistory();
        }

        private void RunAction(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Не удалось выполнить действие",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
    }
}