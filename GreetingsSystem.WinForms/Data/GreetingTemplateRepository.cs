using System;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;

namespace GreetingsSystem.WinForms.Data
{
    public class GreetingTemplateRepository
    {
        public DataTable GetAll()
        {
            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT
                        t.Id,
                        t.Name,
                        t.EventTypeId,
                        e.Name AS EventName,
                        t.Gender,
                        t.AgeGroupId,
                        a.Name AS AgeGroupName,
                        t.Body,
                        t.IsActive
                    FROM GreetingTemplates t
                    JOIN EventTypes e ON e.Id = t.EventTypeId
                    LEFT JOIN AgeGroups a ON a.Id = t.AgeGroupId
                    ORDER BY t.Id;
                ";

                using (var reader = command.ExecuteReader())
                {
                    var table = new DataTable();
                    table.Load(reader);
                    return table;
                }
            }
        }

        public DataTable GetAgeGroups()
        {
            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT 0 AS Id, 'Любой возраст' AS Name
                    UNION ALL
                    SELECT Id, Name FROM AgeGroups;
                ";

                using (var reader = command.ExecuteReader())
                {
                    var table = new DataTable();
                    table.Load(reader);
                    return table;
                }
            }
        }

        public List<long> GetHobbyIds(long templateId)
        {
            var ids = new List<long>();

            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT HobbyId
                    FROM TemplateHobbies
                    WHERE TemplateId = $id;
                ";

                command.Parameters.AddWithValue("$id", templateId);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        ids.Add(reader.GetInt64(0));
                    }
                }
            }

            return ids;
        }

        public void Save(
            long id,
            string name,
            long eventTypeId,
            string gender,
            long ageGroupId,
            string body,
            bool isActive,
            List<long> hobbyIds)
        {
            name = (name ?? "").Trim();
            body = (body ?? "").Trim();

            if (name.Length == 0 || body.Length == 0)
            {
                throw new ArgumentException(
                    "Введите название и текст шаблона.");
            }

            if (eventTypeId <= 0)
                throw new ArgumentException("Выберите повод.");

            if (gender != "Любой"
                && gender != "Мужской"
                && gender != "Женский"
                && gender != "Не указан")
            {
                throw new ArgumentException("Выберите пол из списка.");
            }

            // Проверяем подстановки в тексте.
            string remainder = body
                .Replace("{Имя}", "")
                .Replace("{Возраст}", "");

            if (remainder.Contains("{") || remainder.Contains("}"))
            {
                throw new ArgumentException(
                    "Разрешены только подстановки " +
                    "{Имя} и {Возраст}.");
            }

            if (hobbyIds == null)
                throw new ArgumentNullException(nameof(hobbyIds));

            using (var connection = Database.OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                // Проверка одинаковых названий с учётом кириллицы.
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;

                    command.CommandText = @"
                        SELECT Name
                        FROM GreetingTemplates
                        WHERE Id <> $id;
                    ";

                    command.Parameters.AddWithValue("$id", id);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (string.Equals(
                                NormalizeName(reader.GetString(0)),
                                NormalizeName(name),
                                StringComparison.OrdinalIgnoreCase))
                            {
                                throw new ArgumentException(
                                    "Шаблон с таким названием уже есть.");
                            }
                        }
                    }
                }

                long savedId = id;

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;

                    if (id == 0)
                    {
                        command.CommandText = @"
                            INSERT INTO GreetingTemplates
                            (
                                Name, EventTypeId, Gender,
                                AgeGroupId, Body, IsActive
                            )
                            VALUES
                            (
                                $name, $type, $gender,
                                $age, $body, $active
                            );
                        ";
                    }
                    else
                    {
                        command.CommandText = @"
                            UPDATE GreetingTemplates
                            SET
                                Name = $name,
                                EventTypeId = $type,
                                Gender = $gender,
                                AgeGroupId = $age,
                                Body = $body,
                                IsActive = $active
                            WHERE Id = $id;
                        ";

                        command.Parameters.AddWithValue("$id", id);
                    }

                    command.Parameters.AddWithValue("$name", name);
                    command.Parameters.AddWithValue("$type", eventTypeId);
                    command.Parameters.AddWithValue("$gender", gender);

                    command.Parameters.AddWithValue(
                        "$age",
                        ageGroupId == 0
                            ? (object)DBNull.Value
                            : ageGroupId);

                    command.Parameters.AddWithValue("$body", body);

                    command.Parameters.AddWithValue(
                        "$active", isActive ? 1 : 0);

                    if (command.ExecuteNonQuery() != 1)
                    {
                        throw new InvalidOperationException(
                            "Шаблон не найден. Обновите таблицу.");
                    }
                }

                if (id == 0)
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText =
                            "SELECT last_insert_rowid();";

                        savedId = Convert.ToInt64(
                            command.ExecuteScalar());
                    }
                }

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;

                    command.CommandText = @"
                        DELETE FROM TemplateHobbies
                        WHERE TemplateId = $id;
                    ";

                    command.Parameters.AddWithValue("$id", savedId);
                    command.ExecuteNonQuery();
                }

                foreach (long hobbyId in new HashSet<long>(hobbyIds))
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;

                        command.CommandText = @"
                            INSERT INTO TemplateHobbies
                                (TemplateId, HobbyId)
                            VALUES ($templateId, $hobbyId);
                        ";

                        command.Parameters.AddWithValue(
                            "$templateId", savedId);

                        command.Parameters.AddWithValue(
                            "$hobbyId", hobbyId);

                        command.ExecuteNonQuery();
                    }
                }

                transaction.Commit();
            }
        }

        private static string NormalizeName(string value)
        {
            return Regex.Replace(value.Trim(), @"\s+", " ");
        }
    }
}