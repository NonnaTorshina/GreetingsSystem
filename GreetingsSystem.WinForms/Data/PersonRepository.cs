using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using GreetingsSystem.WinForms.Models;

namespace GreetingsSystem.WinForms.Data
{
    public class PersonRepository
    {
        public List<Person> GetAll()
        {
            var people = new List<Person>();

            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT
                        Id,
                        LastName,
                        FirstName,
                        MiddleName,
                        Gender,
                        BirthDate,
                        Category
                    FROM People
                    ORDER BY LastName, FirstName, Id;
                ";

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        people.Add(new Person
                        {
                            Id = reader.GetInt64(0),
                            LastName = reader.GetString(1),
                            FirstName = reader.GetString(2),
                            MiddleName = reader.GetString(3),
                            Gender = reader.GetString(4),

                            BirthDate = DateTime.ParseExact(
                                reader.GetString(5),
                                "yyyy-MM-dd",
                                CultureInfo.InvariantCulture),

                            Category = reader.GetString(6)
                        });
                    }
                }
            }

            return people;
        }

        public void Add(Person person)
        {
            Save(person, false);
        }

        public void Update(Person person)
        {
            Save(person, true);
        }

        private void Save(Person person, bool isUpdate)
        {
            Validate(person);

            if (isUpdate && person.Id <= 0)
            {
                throw new ArgumentException(
                    "Не указан номер человека.");
            }

            string lastName = CleanSpaces(person.LastName);
            string firstName = CleanSpaces(person.FirstName);
            string middleName = CleanSpaces(person.MiddleName);

            string birthDate = person.BirthDate.ToString(
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture);

            using (var connection = Database.OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                // Ищем совпадения по дате рождения.
                // При редактировании исключаем саму запись.
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;

                    command.CommandText = @"
                        SELECT LastName, FirstName, MiddleName
                        FROM People
                        WHERE BirthDate = $birthDate
                          AND Id <> $id;
                    ";

                    command.Parameters.AddWithValue(
                        "$birthDate", birthDate);

                    command.Parameters.AddWithValue(
                        "$id", isUpdate ? person.Id : 0);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            bool sameLastName = SameName(
                                reader.GetString(0), lastName);

                            bool sameFirstName = SameName(
                                reader.GetString(1), firstName);

                            bool sameMiddleName = SameName(
                                reader.GetString(2), middleName);

                            if (sameLastName
                                && sameFirstName
                                && sameMiddleName)
                            {
                                throw new ArgumentException(
                                    "Человек с такими ФИО и датой " +
                                    "рождения уже существует.");
                            }
                        }
                    }
                }

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;

                    if (isUpdate)
                    {
                        command.CommandText = @"
                            UPDATE People
                            SET
                                LastName = $lastName,
                                FirstName = $firstName,
                                MiddleName = $middleName,
                                Gender = $gender,
                                BirthDate = $birthDate,
                                Category = $category
                            WHERE Id = $id;
                        ";

                        command.Parameters.AddWithValue(
                            "$id", person.Id);
                    }
                    else
                    {
                        command.CommandText = @"
                            INSERT INTO People
                            (
                                LastName,
                                FirstName,
                                MiddleName,
                                Gender,
                                BirthDate,
                                Category
                            )
                            VALUES
                            (
                                $lastName,
                                $firstName,
                                $middleName,
                                $gender,
                                $birthDate,
                                $category
                            );
                        ";
                    }

                    command.Parameters.AddWithValue(
                        "$lastName", lastName);

                    command.Parameters.AddWithValue(
                        "$firstName", firstName);

                    command.Parameters.AddWithValue(
                        "$middleName", middleName);

                    command.Parameters.AddWithValue(
                        "$gender", person.Gender);

                    command.Parameters.AddWithValue(
                        "$birthDate", birthDate);

                    command.Parameters.AddWithValue(
                        "$category", person.Category);

                    int changedRows = command.ExecuteNonQuery();

                    if (changedRows != 1)
                    {
                        throw new InvalidOperationException(
                            "Запись не найдена. Обновите таблицу.");
                    }
                }
                long savedPersonId = person.Id;

                // Для новой записи получаем номер, назначенный SQLite.
                if (!isUpdate)
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = "SELECT last_insert_rowid();";

                        savedPersonId = Convert.ToInt64(
                            command.ExecuteScalar());
                    }
                }

                // Удаляем прежний набор связей.
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;

                    command.CommandText = @"
        DELETE FROM PersonHobbies
        WHERE PersonId = $personId;
    ";

                    command.Parameters.AddWithValue(
                        "$personId", savedPersonId);

                    command.ExecuteNonQuery();
                }

                // Записываем выбранные увлечения без повторов.
                var hobbyIds = new HashSet<long>(
                    person.HobbyIds ?? new List<long>());

                foreach (long hobbyId in hobbyIds)
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;

                        command.CommandText = @"
            INSERT INTO PersonHobbies(PersonId, HobbyId)
            VALUES ($personId, $hobbyId);
        ";

                        command.Parameters.AddWithValue(
                            "$personId", savedPersonId);

                        command.Parameters.AddWithValue(
                            "$hobbyId", hobbyId);

                        command.ExecuteNonQuery();
                    }
                }

                transaction.Commit();
            }
        }

        public void Delete(long id)
        {
            if (id <= 0)
                throw new ArgumentException("Не указан номер человека.");

            using (var connection = Database.OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                // Не удаляем человека, если у него есть события.
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;

                    command.CommandText = @"
                        SELECT COUNT(*)
                        FROM Events
                        WHERE PersonId = $id;
                    ";

                    command.Parameters.AddWithValue("$id", id);

                    long eventCount = Convert.ToInt64(
                        command.ExecuteScalar());

                    if (eventCount > 0)
                    {
                        throw new InvalidOperationException(
                            "Нельзя удалить человека, у которого " +
                            "есть события. Его данные нужны для " +
                            "сохранения связей и истории поздравлений.");
                    }
                }

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;

                    command.CommandText = @"
                        DELETE FROM People
                        WHERE Id = $id;
                    ";

                    command.Parameters.AddWithValue("$id", id);

                    if (command.ExecuteNonQuery() != 1)
                    {
                        throw new InvalidOperationException(
                            "Запись уже удалена. Обновите таблицу.");
                    }
                }

                transaction.Commit();
            }
        }

        private static void Validate(Person person)
        {
            if (person == null)
                throw new ArgumentNullException(nameof(person));

            if (string.IsNullOrWhiteSpace(person.LastName))
                throw new ArgumentException("Введите фамилию.");

            if (string.IsNullOrWhiteSpace(person.FirstName))
                throw new ArgumentException("Введите имя.");

            if (person.BirthDate.Date > DateTime.Today)
            {
                throw new ArgumentException(
                    "Дата рождения не может быть в будущем.");
            }

            if (person.Gender != "Мужской"
                && person.Gender != "Женский"
                && person.Gender != "Не указан")
            {
                throw new ArgumentException(
                    "Выберите пол из списка.");
            }

            if (person.Category != "Друг"
                && person.Category != "Коллега")
            {
                throw new ArgumentException(
                    "Выберите категорию из списка.");
            }
        }

        private static string CleanSpaces(string value)
        {
            return Regex.Replace(
                (value ?? "").Trim(),
                @"\s+",
                " ");
        }

        private static bool SameName(string first, string second)
        {
            return string.Equals(
                CleanSpaces(first),
                CleanSpaces(second),
                StringComparison.OrdinalIgnoreCase);
        }
    }
}