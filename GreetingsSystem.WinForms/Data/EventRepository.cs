using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using GreetingsSystem.WinForms.Models;

namespace GreetingsSystem.WinForms.Data
{
    public class EventRepository
    {
        public List<EventType> GetTypes()
        {
            var types = new List<EventType>();

            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT Id, Name
                    FROM EventTypes
                    ORDER BY Name;
                ";

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        types.Add(new EventType
                        {
                            Id = reader.GetInt64(0),
                            Name = reader.GetString(1)
                        });
                    }
                }
            }

            return types;
        }

        public DataTable GetForPerson(long personId)
        {
            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT
                        e.Id,
                        e.EventTypeId,
                        t.Name AS EventName,
                        e.EventDate,
                        e.Notes
                    FROM Events e
                    JOIN EventTypes t ON t.Id = e.EventTypeId
                    WHERE e.PersonId = $personId
                    ORDER BY e.EventDate, e.Id;
                ";

                command.Parameters.AddWithValue(
                    "$personId", personId);

                using (var reader = command.ExecuteReader())
                {
                    var table = new DataTable();
                    table.Load(reader);
                    return table;
                }
            }
        }

        public void Save(
            long eventId,
            long personId,
            long eventTypeId,
            DateTime date,
            string notes)
        {
            if (personId <= 0 || eventTypeId <= 0)
            {
                throw new ArgumentException(
                    "Выберите человека и повод.");
            }

            using (var connection = Database.OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                // Читаем актуальную дату рождения из базы.
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;

                    command.CommandText = @"
                        SELECT BirthDate
                        FROM People
                        WHERE Id = $personId;
                    ";

                    command.Parameters.AddWithValue(
                        "$personId", personId);

                    object value = command.ExecuteScalar();

                    if (value == null)
                    {
                        throw new InvalidOperationException(
                            "Человек не найден.");
                    }

                    DateTime birthDate = DateTime.ParseExact(
                        Convert.ToString(value),
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture);

                    if (date.Date < birthDate.Date)
                    {
                        throw new ArgumentException(
                            "Дата события не может быть " +
                            "раньше даты рождения.");
                    }
                }

                // Событие с историей изменять запрещено.
                if (eventId > 0)
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;

                        command.CommandText = @"
                            SELECT COUNT(*)
                            FROM GreetingHistory
                            WHERE EventId = $eventId;
                        ";

                        command.Parameters.AddWithValue(
                            "$eventId", eventId);

                        if (Convert.ToInt64(
                            command.ExecuteScalar()) > 0)
                        {
                            throw new InvalidOperationException(
                                "У события уже есть история поздравлений. " +
                                "Создайте новое событие.");
                        }
                    }
                }

                string dateText = date.ToString(
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture);

                // Проверяем повтор события.
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;

                    command.CommandText = @"
                        SELECT COUNT(*)
                        FROM Events
                        WHERE PersonId = $personId
                          AND EventTypeId = $typeId
                          AND EventDate = $date
                          AND Id <> $eventId;
                    ";

                    command.Parameters.AddWithValue(
                        "$personId", personId);

                    command.Parameters.AddWithValue(
                        "$typeId", eventTypeId);

                    command.Parameters.AddWithValue(
                        "$date", dateText);

                    command.Parameters.AddWithValue(
                        "$eventId", eventId);

                    if (Convert.ToInt64(
                        command.ExecuteScalar()) > 0)
                    {
                        throw new ArgumentException(
                            "Такое событие на эту дату уже существует.");
                    }
                }

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;

                    if (eventId == 0)
                    {
                        command.CommandText = @"
                            INSERT INTO Events
                            (
                                PersonId,
                                EventTypeId,
                                EventDate,
                                Notes
                            )
                            VALUES
                            (
                                $personId,
                                $typeId,
                                $date,
                                $notes
                            );
                        ";
                    }
                    else
                    {
                        command.CommandText = @"
                            UPDATE Events
                            SET
                                EventTypeId = $typeId,
                                EventDate = $date,
                                Notes = $notes
                            WHERE Id = $eventId
                              AND PersonId = $personId;
                        ";

                        command.Parameters.AddWithValue(
                            "$eventId", eventId);
                    }

                    command.Parameters.AddWithValue(
                        "$personId", personId);

                    command.Parameters.AddWithValue(
                        "$typeId", eventTypeId);

                    command.Parameters.AddWithValue(
                        "$date", dateText);

                    command.Parameters.AddWithValue(
                        "$notes", (notes ?? "").Trim());

                    if (command.ExecuteNonQuery() != 1)
                    {
                        throw new InvalidOperationException(
                            "Событие не найдено. Обновите список.");
                    }
                }

                transaction.Commit();
            }
        }

        public void Delete(long eventId, long personId)
        {
            using (var connection = Database.OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;

                    command.CommandText = @"
                        SELECT COUNT(*)
                        FROM GreetingHistory
                        WHERE EventId = $eventId;
                    ";

                    command.Parameters.AddWithValue(
                        "$eventId", eventId);

                    if (Convert.ToInt64(
                        command.ExecuteScalar()) > 0)
                    {
                        throw new InvalidOperationException(
                            "Нельзя удалить событие, " +
                            "у которого есть история поздравлений.");
                    }
                }

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;

                    command.CommandText = @"
                        DELETE FROM Events
                        WHERE Id = $eventId
                          AND PersonId = $personId;
                    ";

                    command.Parameters.AddWithValue(
                        "$eventId", eventId);

                    command.Parameters.AddWithValue(
                        "$personId", personId);

                    if (command.ExecuteNonQuery() != 1)
                    {
                        throw new InvalidOperationException(
                            "Событие уже удалено. Обновите список.");
                    }
                }

                transaction.Commit();
            }
        }
    }
}