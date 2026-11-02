using System;
using System.Drawing;
using System.Windows.Forms;
using GreetingsSystem.WinForms.Data;
using GreetingsSystem.WinForms.Models;

namespace GreetingsSystem.WinForms
{
    public class PersonEditForm : Form
    {
        private readonly PersonRepository personRepository =
            new PersonRepository();

        private TextBox lastNameBox;
        private TextBox firstNameBox;
        private long personId = 0;
        private TextBox middleNameBox;
        private ComboBox genderBox;
        private DateTimePicker birthDatePicker;
        private ComboBox categoryBox;
        private CheckedListBox hobbiesList;
        private readonly HobbyRepository hobbyRepository =
            new HobbyRepository();

        public PersonEditForm()
        {
            CreateInterface();
            LoadHobbies(0);
        }
        public PersonEditForm(Person person) : this()
        {
            if (person == null)
                throw new ArgumentNullException(nameof(person));

            personId = person.Id;

            Text = "Изменить человека";

            lastNameBox.Text = person.LastName;
            firstNameBox.Text = person.FirstName;
            middleNameBox.Text = person.MiddleName;

            genderBox.SelectedItem = person.Gender;
            categoryBox.SelectedItem = person.Category;

            birthDatePicker.Value = person.BirthDate.Date;
            birthDatePicker.Checked = true;

            LoadHobbies(person.Id);
        }

        private void CreateInterface()
        {
            Text = "Добавить человека";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(480, 540);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new Font("Segoe UI", 10);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                ColumnCount = 2,
                RowCount = 8
            };

            layout.ColumnStyles.Add(
                new ColumnStyle(SizeType.Absolute, 145));

            layout.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100));

            for (int i = 0; i < 6; i++)
            {
                layout.RowStyles.Add(
                    new RowStyle(SizeType.Absolute, 44));
            }

            // Отдельная строка для выбора увлечений.
            layout.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 155));

            layout.RowStyles.Add(
                new RowStyle(SizeType.Percent, 100));

            lastNameBox = new TextBox
            {
                Dock = DockStyle.Fill,
                MaxLength = 100
            };

            firstNameBox = new TextBox
            {
                Dock = DockStyle.Fill,
                MaxLength = 100
            };

            middleNameBox = new TextBox
            {
                Dock = DockStyle.Fill,
                MaxLength = 100
            };

            genderBox = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            genderBox.Items.AddRange(
                new object[]
                {
                    "Не указан",
                    "Мужской",
                    "Женский"
                });

            genderBox.SelectedIndex = 0;

            birthDatePicker = new DateTimePicker
            {
                Dock = DockStyle.Fill,
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "dd.MM.yyyy",
                MaxDate = DateTime.Today,
                ShowCheckBox = true,
                Value = DateTime.Today,
                Checked = false
            };

            categoryBox = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            categoryBox.Items.AddRange(
                new object[]
                {
                    "Друг",
                    "Коллега"
                });

            // Категорию пользователь выбирает самостоятельно.
            categoryBox.SelectedIndex = -1;

            AddField(layout, 0, "Фамилия *", lastNameBox);
            AddField(layout, 1, "Имя *", firstNameBox);
            AddField(layout, 2, "Отчество", middleNameBox);
            AddField(layout, 3, "Пол", genderBox);
            AddField(layout, 4, "Дата рождения *", birthDatePicker);
            AddField(layout, 5, "Категория *", categoryBox);

            hobbiesList = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                IntegralHeight = false
            };

            AddField(layout, 6, "Увлечения", hobbiesList);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };

            var cancelButton = new Button
            {
                Text = "Отмена",
                AutoSize = true,
                DialogResult = DialogResult.Cancel
            };

            var saveButton = new Button
            {
                Text = "Сохранить",
                AutoSize = true
            };

            saveButton.Click += SaveButton_Click;

            buttons.Controls.Add(cancelButton);
            buttons.Controls.Add(saveButton);

            layout.Controls.Add(buttons, 0, 7);
            layout.SetColumnSpan(buttons, 2);

            Controls.Add(layout);

            AcceptButton = saveButton;
            CancelButton = cancelButton;
            ActiveControl = lastNameBox;
        }

        private void AddField(
            TableLayoutPanel layout,
            int row,
            string caption,
            Control input)
        {
            var label = new Label
            {
                Text = caption,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };

            layout.Controls.Add(label, 0, row);
            layout.Controls.Add(input, 1, row);
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
           
            if (string.IsNullOrWhiteSpace(lastNameBox.Text))
            {
                ShowValidationError("Введите фамилию.", lastNameBox);
                return;
            }

            if (string.IsNullOrWhiteSpace(firstNameBox.Text))
            {
                ShowValidationError("Введите имя.", firstNameBox);
                return;
            }

            if (!birthDatePicker.Checked)
            {
                ShowValidationError(
                    "Укажите дату рождения и установите галочку рядом с ней.",
                    birthDatePicker);

                return;
            }

            if (categoryBox.SelectedIndex < 0)
            {
                ShowValidationError(
                    "Выберите категорию: друг или коллега.",
                    categoryBox);

                return;
            }

            var person = new Person
            {
                Id = personId,
                LastName = lastNameBox.Text.Trim(),
                FirstName = firstNameBox.Text.Trim(),
                MiddleName = middleNameBox.Text.Trim(),
                Gender = genderBox.SelectedItem.ToString(),
                BirthDate = birthDatePicker.Value.Date,
                Category = categoryBox.SelectedItem.ToString()
            };

            foreach (object item in hobbiesList.CheckedItems)
            {
                Hobby hobby = (Hobby)item;
                person.HobbyIds.Add(hobby.Id);
            }

            try
            {
                if (personId == 0)
                {
                    personRepository.Add(person);
                }
                else
                {
                    personRepository.Update(person);
                }
                // Закрываем окно только после успешного сохранения.
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "Не удалось сохранить человека:\n" + ex.Message,
                    "Ошибка сохранения",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void ShowValidationError(
            string message,
            Control input)
        {
            MessageBox.Show(
                this,
                message,
                "Проверьте данные",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            input.Focus();
        }

        private void LoadHobbies(long selectedPersonId)
        {
            var hobbies = hobbyRepository.GetAll();

            var selectedIds = selectedPersonId == 0
                ? new System.Collections.Generic.List<long>()
                : hobbyRepository.GetForPerson(selectedPersonId);

            hobbiesList.Items.Clear();

            foreach (Hobby hobby in hobbies)
            {
                bool isSelected = selectedIds.Contains(hobby.Id);

                hobbiesList.Items.Add(hobby, isSelected);
            }
        }
    }
}