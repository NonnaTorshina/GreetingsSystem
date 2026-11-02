using System;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using GreetingsSystem.WinForms.Data;
using GreetingsSystem.WinForms.Models;
using GreetingsSystem.WinForms.Services;

namespace GreetingsSystem.WinForms
{
    public class EventsForm : Form
    {
        private readonly Person person;

        private readonly EventRepository repository =
            new EventRepository();

        private DataGridView eventsGrid;
        private ComboBox typeBox;
        private DateTimePicker datePicker;
        private TextBox notesBox;
        private Label modeLabel;
        private readonly GreetingService greetingService = new GreetingService();

        // 0 — добавление; положительный номер — редактирование.
        private long editingId = 0;

        public EventsForm(Person selectedPerson)
        {
            if (selectedPerson == null)
                throw new ArgumentNullException(nameof(selectedPerson));

            person = selectedPerson;

            CreateInterface();
            LoadTypes();
            LoadEvents();
            StartNew();
        }

        private void CreateInterface()
        {
            Text = "События: " +
                person.LastName + " " + person.FirstName;

            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(850, 600);
            MinimumSize = new Size(700, 500);
            Font = new Font("Segoe UI", 10);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(12)
            };

            layout.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100));

            layout.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 90));

            layout.RowStyles.Add(
                new RowStyle(SizeType.Percent, 100));

            layout.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 30));

            layout.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 160));

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill
            };

            AddButton(toolbar, "Новое", StartNew);
            AddButton(toolbar, "Изменить выбранное", EditSelected);
            AddButton(toolbar, "Удалить", DeleteSelected);
            AddButton(toolbar, "Обновить", LoadEvents);
            AddButton(toolbar,"Сформировать поздравление",GenerateGreeting);

            eventsGrid = new DataGridView
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
            AddColumn("EventName", "Повод", 100);
            AddColumn("EventDate", "Дата", 75);
            AddColumn("Notes", "Примечание", 140);

            modeLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var fields = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4
            };

            fields.ColumnStyles.Add(
                new ColumnStyle(SizeType.Absolute, 110));

            fields.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100));

            fields.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 36));

            fields.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 36));

            fields.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 40));

            fields.RowStyles.Add(
                new RowStyle(SizeType.Percent, 100));

            typeBox = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            datePicker = new DateTimePicker
            {
                Dock = DockStyle.Fill,
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "dd.MM.yyyy"
            };

            notesBox = new TextBox
            {
                Dock = DockStyle.Fill,
                MaxLength = 1000
            };

            AddField(fields, 0, "Повод *", typeBox);
            AddField(fields, 1, "Дата *", datePicker);
            AddField(fields, 2, "Примечание", notesBox);

            var saveButton = new Button
            {
                Text = "Сохранить событие",
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };

            saveButton.Click += delegate
            {
                RunAction(SaveEvent);
            };

            fields.Controls.Add(saveButton, 1, 3);

            layout.Controls.Add(toolbar, 0, 0);
            layout.Controls.Add(eventsGrid, 0, 1);
            layout.Controls.Add(modeLabel, 0, 2);
            layout.Controls.Add(fields, 0, 3);

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
            eventsGrid.Columns.Add(
                new DataGridViewTextBoxColumn
                {
                    DataPropertyName = property,
                    HeaderText = title,
                    FillWeight = width
                });
        }

        private void AddField(
            TableLayoutPanel table,
            int row,
            string title,
            Control control)
        {
            table.Controls.Add(
                new Label
                {
                    Text = title,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft
                },
                0,
                row);

            table.Controls.Add(control, 1, row);
        }

        private void LoadTypes()
        {
            typeBox.DisplayMember = "Name";
            typeBox.ValueMember = "Id";
            typeBox.DataSource = repository.GetTypes();
        }

        private void LoadEvents()
        {
            eventsGrid.DataSource =
                repository.GetForPerson(person.Id);
        }

        private void StartNew()
        {
            editingId = 0;

            modeLabel.Text = "Добавление нового события";
            typeBox.SelectedIndex = -1;
            datePicker.Value = DateTime.Today;
            notesBox.Clear();
        }

        private DataRow GetSelectedRow()
        {
            if (eventsGrid.CurrentRow == null)
            {
                throw new InvalidOperationException(
                    "Сначала выберите событие в таблице.");
            }

            return ((DataRowView)
                eventsGrid.CurrentRow.DataBoundItem).Row;
        }

        private void EditSelected()
        {
            DataRow row = GetSelectedRow();

            editingId = Convert.ToInt64(row["Id"]);

            typeBox.SelectedValue =
                Convert.ToInt64(row["EventTypeId"]);

            datePicker.Value = DateTime.ParseExact(
                Convert.ToString(row["EventDate"]),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture);

            notesBox.Text = Convert.ToString(row["Notes"]);

            modeLabel.Text =
                "Редактирование события №" + editingId;
        }

        private void SaveEvent()
        {
            var eventType = typeBox.SelectedItem as EventType;

            if (eventType == null)
            {
                throw new ArgumentException(
                    "Выберите повод из списка.");
            }

            repository.Save(
                editingId,
                person.Id,
                eventType.Id,
                datePicker.Value.Date,
                notesBox.Text);

            LoadEvents();
            StartNew();
        }

        private void DeleteSelected()
        {
            DataRow row = GetSelectedRow();
            long id = Convert.ToInt64(row["Id"]);

            DialogResult answer = MessageBox.Show(
                this,
                "Удалить выбранное событие?",
                "Подтверждение",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);

            if (answer != DialogResult.Yes)
                return;

            repository.Delete(id, person.Id);

            LoadEvents();
            StartNew();
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
        private void GenerateGreeting()
        {
            DataRow row = GetSelectedRow();

            long eventId = Convert.ToInt64(row["Id"]);

            GreetingResult result =
                greetingService.Generate(eventId);

            using (var form = new GreetingResultForm(result))
            {
                form.ShowDialog(this);
            }
        }
    }
}