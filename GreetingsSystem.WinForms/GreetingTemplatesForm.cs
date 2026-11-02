using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using GreetingsSystem.WinForms.Data;
using GreetingsSystem.WinForms.Models;

namespace GreetingsSystem.WinForms
{
    public class GreetingTemplatesForm : Form
    {
        private readonly GreetingTemplateRepository repository =
            new GreetingTemplateRepository();

        private DataGridView templatesGrid;
        private TextBox nameBox;
        private ComboBox eventTypeBox;
        private ComboBox genderBox;
        private ComboBox ageGroupBox;
        private TextBox bodyBox;
        private CheckedListBox hobbiesList;
        private CheckBox activeBox;
        private Label modeLabel;

        private long editingId = 0;

        public GreetingTemplatesForm()
        {
            CreateInterface();

            Shown += delegate
            {
                RunAction(delegate
                {
                    LoadLookups();
                    LoadTemplates();
                    StartNew();
                });
            };
        }

        private void CreateInterface()
        {
            Text = "Шаблоны поздравлений";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1000, 820);
            MinimumSize = new Size(800, 650);
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
                new RowStyle(SizeType.Absolute, 45));

            layout.RowStyles.Add(
                new RowStyle(SizeType.Percent, 100));

            layout.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 30));

            layout.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 430));

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill
            };

            AddButton(toolbar, "Новый шаблон", StartNew);
            AddButton(toolbar, "Изменить выбранный", EditSelected);
            AddButton(toolbar, "Обновить таблицу", LoadTemplates);

            templatesGrid = new DataGridView
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

            AddColumn("Id", "№", 30);
            AddColumn("Name", "Название", 140);
            AddColumn("EventName", "Повод", 100);
            AddColumn("Gender", "Пол", 70);
            AddColumn("AgeGroupName", "Возрастная группа", 100);
            AddColumn("IsActive", "Активен: 1/0", 65);

            modeLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var fields = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 8
            };

            fields.ColumnStyles.Add(
                new ColumnStyle(SizeType.Absolute, 180));

            fields.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100));

            for (int i = 0; i < 4; i++)
            {
                fields.RowStyles.Add(
                    new RowStyle(SizeType.Absolute, 36));
            }

            fields.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 100));

            fields.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 105));

            fields.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 32));

            fields.RowStyles.Add(
                new RowStyle(SizeType.Percent, 100));

            nameBox = new TextBox
            {
                Dock = DockStyle.Fill,
                MaxLength = 150
            };

            eventTypeBox = CreateCombo();
            genderBox = CreateCombo();
            ageGroupBox = CreateCombo();

            genderBox.Items.AddRange(new object[]
            {
                "Любой",
                "Мужской",
                "Женский",
                "Не указан"
            });

            bodyBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                MaxLength = 10000
            };

            hobbiesList = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                IntegralHeight = false
            };

            activeBox = new CheckBox
            {
                Text = "Использовать при формировании",
                Dock = DockStyle.Fill
            };

            AddField(fields, 0, "Название *", nameBox);
            AddField(fields, 1, "Повод *", eventTypeBox);
            AddField(fields, 2, "Пол", genderBox);
            AddField(fields, 3, "Возрастная группа", ageGroupBox);

            AddField(
                fields,
                4,
                "Текст *\n{Имя}, {Возраст}",
                bodyBox);

            AddField(
                fields,
                5,
                "Увлечения\nПусто — любые",
                hobbiesList);

            fields.Controls.Add(activeBox, 1, 6);

            var saveButton = new Button
            {
                Text = "Сохранить шаблон",
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };

            saveButton.Click += delegate
            {
                RunAction(SaveTemplate);
            };

            fields.Controls.Add(saveButton, 1, 7);

            layout.Controls.Add(toolbar, 0, 0);
            layout.Controls.Add(templatesGrid, 0, 1);
            layout.Controls.Add(modeLabel, 0, 2);
            layout.Controls.Add(fields, 0, 3);

            Controls.Add(layout);
        }

        private ComboBox CreateCombo()
        {
            return new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
        }

        private void AddButton(
            FlowLayoutPanel toolbar,
            string text,
            Action action)
        {
            var button = new Button
            {
                Text = text,
                AutoSize = true
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
            templatesGrid.Columns.Add(
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
            Control input)
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

            table.Controls.Add(input, 1, row);
        }

        private void LoadLookups()
        {
            eventTypeBox.DisplayMember = "Name";
            eventTypeBox.ValueMember = "Id";
            eventTypeBox.DataSource =
                new EventRepository().GetTypes();

            ageGroupBox.DisplayMember = "Name";
            ageGroupBox.ValueMember = "Id";
            ageGroupBox.DataSource = repository.GetAgeGroups();

            hobbiesList.Items.Clear();

            foreach (Hobby hobby in new HobbyRepository().GetAll())
            {
                hobbiesList.Items.Add(hobby);
            }
        }

        private void LoadTemplates()
        {
            templatesGrid.DataSource = repository.GetAll();
        }

        private void StartNew()
        {
            editingId = 0;
            modeLabel.Text = "Добавление нового шаблона";

            nameBox.Clear();
            eventTypeBox.SelectedIndex = -1;
            genderBox.SelectedItem = "Любой";
            ageGroupBox.SelectedValue = 0L;
            bodyBox.Text = "{Имя}, поздравляю!";
            activeBox.Checked = true;

            for (int i = 0; i < hobbiesList.Items.Count; i++)
            {
                hobbiesList.SetItemChecked(i, false);
            }
        }

        private void EditSelected()
        {
            if (templatesGrid.CurrentRow == null)
            {
                throw new InvalidOperationException(
                    "Сначала выберите шаблон в таблице.");
            }

            DataRow row = ((DataRowView)
                templatesGrid.CurrentRow.DataBoundItem).Row;

            editingId = Convert.ToInt64(row["Id"]);

            nameBox.Text = Convert.ToString(row["Name"]);

            eventTypeBox.SelectedValue =
                Convert.ToInt64(row["EventTypeId"]);

            genderBox.SelectedItem =
                Convert.ToString(row["Gender"]);

            ageGroupBox.SelectedValue =
                row.IsNull("AgeGroupId")
                    ? 0L
                    : Convert.ToInt64(row["AgeGroupId"]);

            bodyBox.Text = Convert.ToString(row["Body"]);

            activeBox.Checked =
                Convert.ToInt32(row["IsActive"]) == 1;

            List<long> selectedHobbies =
                repository.GetHobbyIds(editingId);

            for (int i = 0; i < hobbiesList.Items.Count; i++)
            {
                Hobby hobby = (Hobby)hobbiesList.Items[i];

                hobbiesList.SetItemChecked(
                    i,
                    selectedHobbies.Contains(hobby.Id));
            }

            modeLabel.Text =
                "Редактирование шаблона №" + editingId;
        }

        private void SaveTemplate()
        {
            var eventType = eventTypeBox.SelectedItem as EventType;

            if (eventType == null)
                throw new ArgumentException("Выберите повод.");

            if (genderBox.SelectedItem == null)
                throw new ArgumentException("Выберите пол.");

            if (ageGroupBox.SelectedValue == null)
                throw new ArgumentException("Выберите возрастную группу.");

            var hobbyIds = new List<long>();

            foreach (object item in hobbiesList.CheckedItems)
            {
                hobbyIds.Add(((Hobby)item).Id);
            }

            repository.Save(
                editingId,
                nameBox.Text,
                eventType.Id,
                genderBox.SelectedItem.ToString(),
                Convert.ToInt64(ageGroupBox.SelectedValue),
                bodyBox.Text,
                activeBox.Checked,
                hobbyIds);

            LoadTemplates();
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
    }
}