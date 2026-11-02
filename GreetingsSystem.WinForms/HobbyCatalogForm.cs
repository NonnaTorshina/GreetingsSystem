using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using GreetingsSystem.WinForms.Data;
using GreetingsSystem.WinForms.Models;

namespace GreetingsSystem.WinForms
{
    public class HobbyCatalogForm : Form
    {
        private readonly HobbyRepository hobbyRepository =
            new HobbyRepository();

        private readonly HobbyCatalogRepository catalogRepository =
            new HobbyCatalogRepository();

        private ListBox hobbiesList;
        private TextBox hobbyNameBox;

        private DataGridView wishesGrid;
        private TextBox wishBox;
        private Label wishModeLabel;

        private long editingWishId = 0;
        private bool loading = false;

        public HobbyCatalogForm()
        {
            CreateInterface();

            Shown += delegate
            {
                RunAction(delegate { LoadHobbies(0); });
            };
        }

        private void CreateInterface()
        {
            Text = "Увлечения и тематические пожелания";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1100, 650);
            MinimumSize = new Size(900, 550);
            Font = new Font("Segoe UI", 10);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(12)
            };

            layout.ColumnStyles.Add(
                new ColumnStyle(SizeType.Absolute, 320));

            layout.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100));

            var left = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4
            };

            left.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100));

            left.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 30));

            left.RowStyles.Add(
                new RowStyle(SizeType.Percent, 100));

            left.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 38));

            left.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 90));

            hobbiesList = new ListBox
            {
                Dock = DockStyle.Fill,
                DisplayMember = "Name"
            };

            hobbiesList.SelectedIndexChanged += delegate
            {
                if (!loading)
                    RunAction(LoadSelectedHobby);
            };

            hobbyNameBox = new TextBox
            {
                Dock = DockStyle.Fill,
                MaxLength = 100
            };

            var hobbyButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = true
            };

            AddButton(hobbyButtons, "Новое", StartNewHobby);
            AddButton(hobbyButtons, "Сохранить", SaveHobby);
            AddButton(hobbyButtons, "Удалить", DeleteHobby);

            left.Controls.Add(
                new Label
                {
                    Text = "Увлечения",
                    Dock = DockStyle.Fill
                },
                0,
                0);

            left.Controls.Add(hobbiesList, 0, 1);
            left.Controls.Add(hobbyNameBox, 0, 2);
            left.Controls.Add(hobbyButtons, 0, 3);

            var right = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(10, 0, 0, 0)
            };

            right.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100));

            right.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 30));

            right.RowStyles.Add(
                new RowStyle(SizeType.Percent, 100));

            right.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 70));

            right.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 30));

            right.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 120));

            wishesGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                MultiSelect = false,
                SelectionMode =
                    DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                BackgroundColor = Color.White
            };

            wishesGrid.Columns.Add(
                new DataGridViewTextBoxColumn
                {
                    DataPropertyName = "Id",
                    HeaderText = "№",
                    Width = 55
                });

            wishesGrid.Columns.Add(
                new DataGridViewTextBoxColumn
                {
                    DataPropertyName = "Body",
                    HeaderText = "Пожелание",
                    AutoSizeMode =
                        DataGridViewAutoSizeColumnMode.Fill
                });

            var wishButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = true
            };

            AddButton(wishButtons, "Новое пожелание", StartNewWish);

            AddButton(
                wishButtons,
                "Изменить выбранное",
                EditSelectedWish);

            AddButton(wishButtons, "Сохранить", SaveWish);
            AddButton(wishButtons, "Удалить", DeleteWish);

            wishModeLabel = new Label
            {
                Dock = DockStyle.Fill
            };

            wishBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                MaxLength = 2000
            };

            right.Controls.Add(
                new Label
                {
                    Text = "Пожелания для выбранного увлечения",
                    Dock = DockStyle.Fill
                },
                0,
                0);

            right.Controls.Add(wishesGrid, 0, 1);
            right.Controls.Add(wishButtons, 0, 2);
            right.Controls.Add(wishModeLabel, 0, 3);
            right.Controls.Add(wishBox, 0, 4);

            layout.Controls.Add(left, 0, 0);
            layout.Controls.Add(right, 1, 0);

            Controls.Add(layout);
        }

        private void AddButton(
            FlowLayoutPanel panel,
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

            panel.Controls.Add(button);
        }

        private Hobby SelectedHobby()
        {
            return hobbiesList.SelectedItem as Hobby;
        }

        private long RequireHobbyId()
        {
            Hobby hobby = SelectedHobby();

            if (hobby == null)
            {
                throw new InvalidOperationException(
                    "Сначала выберите сохранённое увлечение слева.");
            }

            return hobby.Id;
        }

        private void LoadHobbies(long selectedId)
        {
            loading = true;

            try
            {
                hobbiesList.Items.Clear();

                foreach (Hobby hobby in hobbyRepository.GetAll())
                {
                    int index = hobbiesList.Items.Add(hobby);

                    if (hobby.Id == selectedId)
                        hobbiesList.SelectedIndex = index;
                }
            }
            finally
            {
                loading = false;
            }

            LoadSelectedHobby();
        }

        private void LoadSelectedHobby()
        {
            Hobby hobby = SelectedHobby();

            hobbyNameBox.Text =
                hobby == null ? "" : hobby.Name;

            wishesGrid.DataSource =
                catalogRepository.GetWishes(
                    hobby == null ? 0 : hobby.Id);

            StartNewWish();
        }

        private void StartNewHobby()
        {
            hobbiesList.SelectedIndex = -1;
            LoadSelectedHobby();
            hobbyNameBox.Focus();
        }

        private void SaveHobby()
        {
            Hobby hobby = SelectedHobby();

            long savedId = catalogRepository.SaveHobby(
                hobby == null ? 0 : hobby.Id,
                hobbyNameBox.Text);

            LoadHobbies(savedId);
        }

        private void DeleteHobby()
        {
            long id = RequireHobbyId();

            if (!Confirm(
                "Удалить увлечение и все его тематические пожелания?"))
            {
                return;
            }

            catalogRepository.DeleteHobby(id);
            LoadHobbies(0);
        }

        private void StartNewWish()
        {
            editingWishId = 0;
            wishBox.Clear();
            wishModeLabel.Text = "Новое пожелание";
        }

        private DataRow SelectedWish()
        {
            if (wishesGrid.CurrentRow == null)
            {
                throw new InvalidOperationException(
                    "Сначала выберите пожелание в таблице.");
            }

            return ((DataRowView)
                wishesGrid.CurrentRow.DataBoundItem).Row;
        }

        private void EditSelectedWish()
        {
            RequireHobbyId();

            DataRow row = SelectedWish();

            editingWishId = Convert.ToInt64(row["Id"]);
            wishBox.Text = Convert.ToString(row["Body"]);

            wishModeLabel.Text =
                "Редактирование пожелания №" + editingWishId;
        }

        private void SaveWish()
        {
            long hobbyId = RequireHobbyId();

            catalogRepository.SaveWish(
                editingWishId,
                hobbyId,
                wishBox.Text);

            wishesGrid.DataSource =
                catalogRepository.GetWishes(hobbyId);

            StartNewWish();
        }

        private void DeleteWish()
        {
            long hobbyId = RequireHobbyId();
            long id = Convert.ToInt64(SelectedWish()["Id"]);

            if (!Confirm("Удалить выбранное пожелание?"))
                return;

            catalogRepository.DeleteWish(id, hobbyId);

            wishesGrid.DataSource =
                catalogRepository.GetWishes(hobbyId);

            StartNewWish();
        }

        private bool Confirm(string message)
        {
            return MessageBox.Show(
                this,
                message,
                "Подтверждение",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2)
                == DialogResult.Yes;
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