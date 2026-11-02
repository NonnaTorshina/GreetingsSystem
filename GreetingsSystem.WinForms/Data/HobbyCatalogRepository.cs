using System;
using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace GreetingsSystem.WinForms.Data
{
    public class HobbyCatalogRepository
    {
        public DataTable GetWishes(long hobbyId)
        {
            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT Id, Body
                    FROM HobbyWishes
                    WHERE HobbyId = $hobbyId
                    ORDER BY Id;
                ";

                command.Parameters.AddWithValue(
                    "$hobbyId", hobbyId);

                using (var reader = command.ExecuteReader())
                {
                    var table = new DataTable();
                    table.Load(reader);
                    return table;
                }
            }
        }

        public long SaveHobby(long id, string name)
        {
            name = Clean(name);

            if (name.Length == 0)
                throw new ArgumentException("Введите название увлечения.");

            using (var connection = Database.OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                CheckDuplicate(
                    connection,
                    transaction,
                    "SELECT Name FROM Hobbies WHERE Id <> $id;",
                    id,
                    null,
                    name);

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;

                    command.CommandText = id == 0
                        ? "INSERT INTO Hobbies(Name) VALUES($name);"
                        : "UPDATE Hobbies SET Name = $name WHERE Id = $id;";

                    command.Parameters.AddWithValue("$name", name);

                    if (id != 0)
                        command.Parameters.AddWithValue("$id", id);

                    if (command.ExecuteNonQuery() != 1)
                    {
                        throw new InvalidOperationException(
                            "Увлечение не найдено. Обновите список.");
                    }
                }

                long savedId = id;

                if (id == 0)
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = "SELECT last_insert_rowid();";

                        savedId = Convert.ToInt64(
                            command.ExecuteScalar());
                    }
                }

                transaction.Commit();
                return savedId;
            }
        }

        public void DeleteHobby(long id)
        {
            using (var connection = Database.OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;

                    command.CommandText = @"
                        SELECT
                            (SELECT COUNT(*) FROM PersonHobbies
                             WHERE HobbyId = $id)
                            +
                            (SELECT COUNT(*) FROM TemplateHobbies
                             WHERE HobbyId = $id);
                    ";

                    command.Parameters.AddWithValue("$id", id);

                    if (Convert.ToInt64(command.ExecuteScalar()) > 0)
                    {
                        throw new InvalidOperationException(
                            "Увлечение используется у людей или шаблонов.\n" +
                            "Сначала снимите соответствующие галочки.");
                    }
                }

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;

                    command.CommandText =
                        "DELETE FROM Hobbies WHERE Id = $id;";

                    command.Parameters.AddWithValue("$id", id);

                    if (command.ExecuteNonQuery() != 1)
                    {
                        throw new InvalidOperationException(
                            "Увлечение уже удалено.");
                    }
                }

                // Пожелания удаляются каскадно по настройкам базы.
                transaction.Commit();
            }
        }

        public void SaveWish(
            long id,
            long hobbyId,
            string body)
        {
            body = Clean(body);

            if (hobbyId <= 0)
                throw new ArgumentException("Выберите увлечение.");

            if (body.Length == 0)
                throw new ArgumentException("Введите пожелание.");

            // В пожеланиях используются обычные тексты.
            // Подстановки поддерживаются в основных шаблонах.
            if (body.Contains("{") || body.Contains("}"))
            {
                throw new ArgumentException(
                    "Введите обычный текст без подстановок " +
                    "{Имя} и {Возраст}.");
            }

            using (var connection = Database.OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                CheckDuplicate(
                    connection,
                    transaction,
                    @"
                        SELECT Body
                        FROM HobbyWishes
                        WHERE HobbyId = $hobbyId
                          AND Id <> $id;
                    ",
                    id,
                    hobbyId,
                    body);

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;

                    command.CommandText = id == 0
                        ? @"
                            INSERT INTO HobbyWishes(HobbyId, Body)
                            VALUES($hobbyId, $body);
                          "
                        : @"
                            UPDATE HobbyWishes
                            SET Body = $body
                            WHERE Id = $id
                              AND HobbyId = $hobbyId;
                          ";

                    command.Parameters.AddWithValue(
                        "$hobbyId", hobbyId);

                    command.Parameters.AddWithValue("$body", body);

                    if (id != 0)
                        command.Parameters.AddWithValue("$id", id);

                    if (command.ExecuteNonQuery() != 1)
                    {
                        throw new InvalidOperationException(
                            "Пожелание не найдено. Обновите список.");
                    }
                }

                transaction.Commit();
            }
        }

        public void DeleteWish(long id, long hobbyId)
        {
            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    DELETE FROM HobbyWishes
                    WHERE Id = $id
                      AND HobbyId = $hobbyId;
                ";

                command.Parameters.AddWithValue("$id", id);
                command.Parameters.AddWithValue("$hobbyId", hobbyId);

                if (command.ExecuteNonQuery() != 1)
                {
                    throw new InvalidOperationException(
                        "Пожелание уже удалено.");
                }
            }
        }

        private static void CheckDuplicate(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string sql,
            long id,
            long? hobbyId,
            string value)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = sql;
                command.Parameters.AddWithValue("$id", id);

                if (hobbyId.HasValue)
                {
                    command.Parameters.AddWithValue(
                        "$hobbyId", hobbyId.Value);
                }

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (string.Equals(
                            Clean(reader.GetString(0)),
                            value,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            throw new ArgumentException(
                                "Такая запись уже существует.");
                        }
                    }
                }
            }
        }

        private static string Clean(string value)
        {
            return Regex.Replace(
                (value ?? "").Trim(),
                @"\s+",
                " ");
        }
    }
}