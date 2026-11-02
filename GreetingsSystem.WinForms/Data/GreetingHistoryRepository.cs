using System;
using System.Data;

namespace GreetingsSystem.WinForms.Data
{
    public class GreetingHistoryRepository
    {
        public DataTable GetAll()
        {
            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT
                        h.Id,
                        p.LastName || ' ' || p.FirstName ||
                            ' ' || p.MiddleName AS PersonName,
                        t.Name AS EventName,
                        e.EventDate,
                        h.Text,
                        h.Status,
                        h.CreatedAt
                    FROM GreetingHistory h
                    JOIN Events e ON e.Id = h.EventId
                    JOIN People p ON p.Id = e.PersonId
                    JOIN EventTypes t ON t.Id = e.EventTypeId
                    ORDER BY h.Id DESC;
                ";

                using (var reader = command.ExecuteReader())
                {
                    var table = new DataTable();
                    table.Load(reader);
                    return table;
                }
            }
        }

        public void ChangeStatus(long id, string newStatus)
        {
            using (var connection = Database.OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                string oldStatus;

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;

                    command.CommandText = @"
                        SELECT Status
                        FROM GreetingHistory
                        WHERE Id = $id;
                    ";

                    command.Parameters.AddWithValue("$id", id);

                    object value = command.ExecuteScalar();

                    if (value == null)
                    {
                        throw new InvalidOperationException(
                            "Поздравление не найдено.");
                    }

                    oldStatus = Convert.ToString(value);
                }

                bool allowed =
                    (
                        oldStatus == "Сформировано"
                        && newStatus == "Подтверждено"
                    )
                    ||
                    (
                        oldStatus == "Подтверждено"
                        && newStatus == "Отправлено"
                    )
                    ||
                    (
                        newStatus == "Отменено"
                        && (
                            oldStatus == "Сформировано"
                            || oldStatus == "Подтверждено"
                        )
                    );

                if (!allowed)
                {
                    throw new InvalidOperationException(
                        "Такое изменение состояния недоступно.\n\n" +
                        "Порядок: Сформировано → Подтверждено → " +
                        "Отправлено.\n" +
                        "Отменить можно только неотправленный вариант.");
                }

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;

                    command.CommandText = @"
                        UPDATE GreetingHistory
                        SET Status = $newStatus
                        WHERE Id = $id
                          AND Status = $oldStatus;
                    ";

                    command.Parameters.AddWithValue("$id", id);

                    command.Parameters.AddWithValue(
                        "$newStatus", newStatus);

                    command.Parameters.AddWithValue(
                        "$oldStatus", oldStatus);

                    if (command.ExecuteNonQuery() != 1)
                    {
                        throw new InvalidOperationException(
                            "Запись изменилась. Обновите историю.");
                    }
                }

                transaction.Commit();
            }
        }
    }
}