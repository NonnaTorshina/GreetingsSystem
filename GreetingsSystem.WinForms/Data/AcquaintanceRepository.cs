using System;
using System.Collections.Generic;

namespace GreetingsSystem.WinForms.Data
{
    public class AcquaintanceRepository
    {
        public List<long> GetKnownIds(long personId)
        {
            var ids = new List<long>();

            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT PersonBId
                    FROM Acquaintances
                    WHERE PersonAId = $personId

                    UNION

                    SELECT PersonAId
                    FROM Acquaintances
                    WHERE PersonBId = $personId;
                ";

                command.Parameters.AddWithValue(
                    "$personId", personId);

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

        public void Save(long personId, List<long> knownIds)
        {
            if (personId <= 0)
                throw new ArgumentException("Не указан человек.");

            if (knownIds == null)
                throw new ArgumentNullException(nameof(knownIds));

            var uniqueIds = new HashSet<long>(knownIds);

            if (uniqueIds.Contains(personId))
            {
                throw new ArgumentException(
                    "Человек не может быть знаком сам с собой.");
            }

            using (var connection = Database.OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                // Проверяем, что человек ещё существует.
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;

                    command.CommandText = @"
                        SELECT COUNT(*)
                        FROM People
                        WHERE Id = $personId;
                    ";

                    command.Parameters.AddWithValue(
                        "$personId", personId);

                    long count = Convert.ToInt64(
                        command.ExecuteScalar());

                    if (count != 1)
                    {
                        throw new InvalidOperationException(
                            "Человек не найден. Обновите таблицу.");
                    }
                }

                // Удаляем только связи выбранного человека.
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;

                    command.CommandText = @"
                        DELETE FROM Acquaintances
                        WHERE PersonAId = $personId
                           OR PersonBId = $personId;
                    ";

                    command.Parameters.AddWithValue(
                        "$personId", personId);

                    command.ExecuteNonQuery();
                }

                // Сохраняем новый набор связей.
                foreach (long knownId in uniqueIds)
                {
                    long firstId = Math.Min(personId, knownId);
                    long secondId = Math.Max(personId, knownId);

                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;

                        command.CommandText = @"
                            INSERT INTO Acquaintances
                            (
                                PersonAId,
                                PersonBId
                            )
                            VALUES
                            (
                                $firstId,
                                $secondId
                            );
                        ";

                        command.Parameters.AddWithValue(
                            "$firstId", firstId);

                        command.Parameters.AddWithValue(
                            "$secondId", secondId);

                        command.ExecuteNonQuery();
                    }
                }

                transaction.Commit();
            }
        }
    }
}