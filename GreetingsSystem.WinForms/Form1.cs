using System;
using System.Drawing;
using System.Windows.Forms;
using GreetingsSystem.WinForms.Data;
using GreetingsSystem.WinForms.Models;
using System.Collections.Generic;
using System.Linq;

namespace GreetingsSystem.WinForms
{
    public partial class Form1 : Form
    {
        private readonly PersonRepository personRepository =
            new PersonRepository();

        private DataGridView peopleGrid;
        private Label countLabel;
        private TextBox searchBox;
        private List<Person> allPeople = new List<Person>();

        private void Form1_Load(object sender, EventArgs e)
        {
        }

        public Form1()
        {
            InitializeComponent();
            CreateInterface();

            // Загружаем данные после открытия окна.
            Shown += delegate { LoadPeople(); };
        }

        private void CreateInterface()
        {
            Text = "Система поздравления друзей и коллег";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1050, 600);
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
                new RowStyle(SizeType.Absolute, 100));

            layout.RowStyles.Add(
                new RowStyle(SizeType.Percent, 100));

            layout.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 32));

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = true
            };

            var refreshButton = new Button
            {
                Text = "Обновить",
                AutoSize = true,
                Height = 35
            };

            refreshButton.Click += delegate { LoadPeople(); };

            var addButton = new Button
            {
                Text = "Добавить человека",
                AutoSize = true,
                Height = 35
            };

            addButton.Click += AddButton_Click;

            var editButton = new Button
            {
                Text = "Изменить",
                AutoSize = true,
                Height = 35
            };

            editButton.Click += EditButton_Click;

            var deleteButton = new Button
            {
                Text = "Удалить",
                AutoSize = true,
                Height = 35
            };

            deleteButton.Click += DeleteButton_Click;

            var acquaintancesButton = new Button
            {
                Text = "Знакомства",
                AutoSize = true,
                Height = 35
            };

            acquaintancesButton.Click += AcquaintancesButton_Click;

            toolbar.Controls.Add(addButton);
            toolbar.Controls.Add(editButton);
            toolbar.Controls.Add(deleteButton);
            var eventsButton = new Button
            {
                Text = "События",
                AutoSize = true,
                Height = 35
            };

            eventsButton.Click += EventsButton_Click;

            toolbar.Controls.Add(acquaintancesButton);
            var historyButton = new Button
            {
                Text = "История",
                AutoSize = true,
                Height = 35
            };

            historyButton.Click += HistoryButton_Click;

            toolbar.Controls.Add(eventsButton);
            var templatesButton = new Button
            {
                Text = "Шаблоны",
                AutoSize = true,
                Height = 35
            };

            templatesButton.Click += TemplatesButton_Click;

            toolbar.Controls.Add(historyButton);
            var hobbyCatalogButton = new Button
            {
                Text = "Увлечения и пожелания",
                AutoSize = true,
                Height = 35
            };

            hobbyCatalogButton.Click += HobbyCatalogButton_Click;

            toolbar.Controls.Add(templatesButton);
            toolbar.Controls.Add(hobbyCatalogButton);
            toolbar.Controls.Add(refreshButton);

            var searchLabel = new Label
            {
                Text = "Поиск:",
                AutoSize = true,
                Margin = new Padding(12, 8, 3, 0)
            };

            searchBox = new TextBox
            {
                Width = 190
            };

            searchBox.TextChanged += delegate
            {
                ApplyFilter();
            };

            toolbar.Controls.Add(searchLabel);
            toolbar.Controls.Add(searchBox);

            peopleGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                MultiSelect = false,
                SelectionMode =
                    DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode =
                    DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                RowHeadersVisible = false
            };

            AddColumn("Id", "№", 40);
            AddColumn("LastName", "Фамилия", 110);
            AddColumn("FirstName", "Имя", 100);
            AddColumn("MiddleName", "Отчество", 110);
            AddColumn("Gender", "Пол", 90);

            var birthDateColumn = new DataGridViewTextBoxColumn
            {
                DataPropertyName = "BirthDate",
                HeaderText = "Дата рождения",
                FillWeight = 100,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "dd.MM.yyyy"
                }
            };

            peopleGrid.Columns.Add(birthDateColumn);

            AddColumn("Category", "Категория", 90);

            countLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "Загрузка..."
            };

            layout.Controls.Add(toolbar, 0, 0);
            layout.Controls.Add(peopleGrid, 0, 1);
            layout.Controls.Add(countLabel, 0, 2);

            Controls.Add(layout);
        }

        private void AddColumn(
            string propertyName,
            string header,
            float width)
        {
            peopleGrid.Columns.Add(
                new DataGridViewTextBoxColumn
                {
                    DataPropertyName = propertyName,
                    HeaderText = header,
                    FillWeight = width
                });
        }
        private void AddButton_Click(object sender, EventArgs e)
        {
            using (var form = new PersonEditForm())
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    LoadPeople();
                }
            }
        }
        private void EditButton_Click(object sender, EventArgs e)
        {
            if (peopleGrid.CurrentRow == null)
            {
                MessageBox.Show(
                    this,
                    "Сначала выберите человека в таблице.",
                    "Редактирование",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return;
            }

            Person selectedPerson =
                peopleGrid.CurrentRow.DataBoundItem as Person;

            if (selectedPerson == null)
                return;

            using (var form = new PersonEditForm(selectedPerson))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    LoadPeople();
                }
            }
        }
        private void DeleteButton_Click(object sender, EventArgs e)
        {
            if (peopleGrid.CurrentRow == null)
            {
                MessageBox.Show(
                    this,
                    "Сначала выберите человека в таблице.",
                    "Удаление",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return;
            }

            Person selectedPerson =
                peopleGrid.CurrentRow.DataBoundItem as Person;

            if (selectedPerson == null)
                return;

            string fullName =
                selectedPerson.LastName + " " + selectedPerson.FirstName;

            DialogResult answer = MessageBox.Show(
                this,
                "Удалить человека «" + fullName + "»?\n" +
                "Его связи с увлечениями и знакомыми тоже будут удалены.",
                "Подтверждение удаления",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);

            if (answer != DialogResult.Yes)
                return;

            try
            {
                personRepository.Delete(selectedPerson.Id);
                LoadPeople();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Не удалось удалить человека",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
        private void LoadPeople()
        {
            try
            {
                allPeople = personRepository.GetAll();
                ApplyFilter();
            }
            catch (Exception ex)
            {
                countLabel.Text = "Не удалось загрузить данные.";

                MessageBox.Show(
                    this,
                    ex.Message,
                    "Ошибка чтения базы",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        private void ApplyFilter()
        {
            string query = searchBox == null
                ? ""
                : searchBox.Text.Trim();

            var filteredPeople = allPeople
                .Where(person =>
                {
                    string searchableText =
                        person.LastName + " " +
                        person.FirstName + " " +
                        person.MiddleName + " " +
                        person.Gender + " " +
                        person.Category;

                    return searchableText.IndexOf(
                        query,
                        StringComparison.OrdinalIgnoreCase) >= 0;
                })
                .ToList();

            peopleGrid.DataSource = filteredPeople;

            countLabel.Text =
                "Показано: " + filteredPeople.Count +
                " из " + allPeople.Count;
        }
        private void AcquaintancesButton_Click(
    object sender,
    EventArgs e)
        {
            if (peopleGrid.CurrentRow == null)
            {
                MessageBox.Show(
                    this,
                    "Сначала выберите человека в таблице.",
                    "Знакомства",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return;
            }

            Person selectedPerson =
                peopleGrid.CurrentRow.DataBoundItem as Person;

            if (selectedPerson == null)
                return;

            try
            {
                using (var form = new AcquaintancesForm(selectedPerson))
                {
                    form.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "Не удалось открыть знакомства:\n" + ex.Message,
                    "Ошибка",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        private void EventsButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (peopleGrid.CurrentRow == null)
                {
                    MessageBox.Show(
                        this,
                        "Сначала выберите человека в таблице.",
                        "События");

                    return;
                }

                object boundItem = peopleGrid.CurrentRow.DataBoundItem;

                Person selectedPerson = boundItem as Person;

                if (selectedPerson == null)
                {
                    string actualType = boundItem == null
                        ? "Нет связанного объекта"
                        : boundItem.GetType().FullName;

                    MessageBox.Show(
                        this,
                        "Не удалось получить человека из строки.\n\n" +
                        "Тип данных: " + actualType,
                        "Проверка данных");

                    return;
                }

                using (var form = new EventsForm(selectedPerson))
                {
                    form.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    ex.ToString(),
                    "Ошибка открытия событий",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        private void HistoryButton_Click(object sender, EventArgs e)
        {
            try
            {
                using (var form = new GreetingHistoryForm())
                {
                    form.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Не удалось открыть историю",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        private void TemplatesButton_Click(object sender, EventArgs e)
        {
            try
            {
                using (var form = new GreetingTemplatesForm())
                {
                    form.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Не удалось открыть шаблоны",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        private void HobbyCatalogButton_Click(object sender, EventArgs e)
        {
            try
            {
                using (var form = new HobbyCatalogForm())
                {
                    form.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Не удалось открыть справочник",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}