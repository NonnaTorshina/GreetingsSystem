using System;
using System.Drawing;
using System.Windows.Forms;
using GreetingsSystem.WinForms.Data;
using GreetingsSystem.WinForms.Models;

namespace GreetingsSystem.WinForms
{
    public class AcquaintancesForm : Form
    {
        private readonly Person selectedPerson;

        private readonly PersonRepository personRepository =
            new PersonRepository();

        private readonly AcquaintanceRepository acquaintanceRepository =
            new AcquaintanceRepository();

        private CheckedListBox peopleList;

        public AcquaintancesForm(Person person)
        {
            if (person == null)
                throw new ArgumentNullException(nameof(person));

            selectedPerson = person;

            CreateInterface();
            LoadPeople();
        }

        private void CreateInterface()
        {
            Text = "Знакомства: " +
                selectedPerson.LastName + " " +
                selectedPerson.FirstName;

            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(580, 450);
            MinimumSize = new Size(450, 350);
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
                new RowStyle(SizeType.Absolute, 55));

            layout.RowStyles.Add(
                new RowStyle(SizeType.Percent, 100));

            layout.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 50));

            var explanation = new Label
            {
                Text = "Отметьте людей, знакомых с выбранным человеком." +
                    "\nСнятие галочки удаляет знакомство.",
                Dock = DockStyle.Fill
            };

            peopleList = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                IntegralHeight = false,
                HorizontalScrollbar = true
            };

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 8, 0, 0)
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

            layout.Controls.Add(explanation, 0, 0);
            layout.Controls.Add(peopleList, 0, 1);
            layout.Controls.Add(buttons, 0, 2);

            Controls.Add(layout);

            CancelButton = cancelButton;
            AcceptButton = saveButton;
        }

        private void LoadPeople()
        {
            var people = personRepository.GetAll();

            var knownIds = acquaintanceRepository.GetKnownIds(
                selectedPerson.Id);

            peopleList.Items.Clear();

            foreach (Person person in people)
            {
                // Самого человека в список не включаем.
                if (person.Id == selectedPerson.Id)
                    continue;

                var item = new PersonListItem
                {
                    Id = person.Id,

                    DisplayName =
                        person.LastName + " " +
                        person.FirstName + " " +
                        person.MiddleName +
                        " (№" + person.Id + ")"
                };

                peopleList.Items.Add(
                    item,
                    knownIds.Contains(person.Id));
            }
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            var knownIds =
                new System.Collections.Generic.List<long>();

            foreach (object item in peopleList.CheckedItems)
            {
                var personItem = (PersonListItem)item;
                knownIds.Add(personItem.Id);
            }

            try
            {
                acquaintanceRepository.Save(
                    selectedPerson.Id,
                    knownIds);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "Не удалось сохранить знакомства:\n" + ex.Message,
                    "Ошибка",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private class PersonListItem
        {
            public long Id { get; set; }

            public string DisplayName { get; set; }

            public override string ToString()
            {
                return DisplayName;
            }
        }
    }
}