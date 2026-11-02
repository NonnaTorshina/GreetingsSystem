using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using GreetingsSystem.WinForms.Data;
using GreetingsSystem.WinForms.Models;

namespace GreetingsSystem.WinForms.Services
{
    public class GreetingService
    {
        public GreetingResult Generate(long eventId)
        {
            using (var connection = Database.OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                // Получаем событие и данные человека.
                DataTable events = Read(
                    connection,
                    transaction,
                    @"
                        SELECT
                            e.PersonId,
                            e.EventTypeId,
                            e.EventDate,
                            p.FirstName,
                            p.Gender,
                            p.BirthDate
                        FROM Events e
                        JOIN People p ON p.Id = e.PersonId
                        WHERE e.Id = $p0;
                    ",
                    eventId);

                if (events.Rows.Count != 1)
                {
                    throw new InvalidOperationException(
                        "Событие не найдено.");
                }

                // Не создаём второй активный вариант для события.
                DataTable existing = Read(
                    connection,
                    transaction,
                    @"
                        SELECT Id, Text
                        FROM GreetingHistory
                        WHERE EventId = $p0
                          AND Status <> 'Отменено'
                        ORDER BY Id DESC
                        LIMIT 1;
                    ",
                    eventId);

                if (existing.Rows.Count > 0)
                {
                    var result = new GreetingResult
                    {
                        Id = Convert.ToInt64(
                            existing.Rows[0]["Id"]),

                        Text = Convert.ToString(
                            existing.Rows[0]["Text"]),

                        AlreadyExists = true
                    };

                    transaction.Commit();
                    return result;
                }

                DataRow personEvent = events.Rows[0];

                long personId = Convert.ToInt64(
                    personEvent["PersonId"]);

                long eventTypeId = Convert.ToInt64(
                    personEvent["EventTypeId"]);

                string firstName = Convert.ToString(
                    personEvent["FirstName"]);

                string gender = Convert.ToString(
                    personEvent["Gender"]);

                DateTime birthDate = ParseDate(
                    Convert.ToString(personEvent["BirthDate"]));

                DateTime eventDate = ParseDate(
                    Convert.ToString(personEvent["EventDate"]));

                int age = CalculateAge(birthDate, eventDate);

                // Отбираем шаблоны по поводу, полу,
                // возрастной группе и увлечениям.
                DataTable templates = Read(
                    connection,
                    transaction,
                    @"
                        SELECT t.Id, t.Body
                        FROM GreetingTemplates t
                        LEFT JOIN AgeGroups a
                            ON a.Id = t.AgeGroupId
                        WHERE t.IsActive = 1
                          AND t.EventTypeId = $p0

                          AND (
                              t.Gender = 'Любой'
                              OR t.Gender = $p1
                          )

                          AND (
                              t.AgeGroupId IS NULL
                              OR $p2 BETWEEN a.MinAge AND a.MaxAge
                          )

                          AND (
                              NOT EXISTS (
                                  SELECT 1
                                  FROM TemplateHobbies th
                                  WHERE th.TemplateId = t.Id
                              )

                              OR EXISTS (
                                  SELECT 1
                                  FROM TemplateHobbies th
                                  JOIN PersonHobbies ph
                                      ON ph.HobbyId = th.HobbyId
                                  WHERE th.TemplateId = t.Id
                                    AND ph.PersonId = $p3
                              )
                          )

                        ORDER BY
                            (t.AgeGroupId IS NOT NULL) DESC,
                            (t.Gender <> 'Любой') DESC,
                            t.Id;
                    ",
                    eventTypeId,
                    gender,
                    age,
                    personId);

                if (templates.Rows.Count == 0)
                {
                    throw new InvalidOperationException(
                        "Нет подходящих шаблонов для этого " +
                        "повода, пола и возраста.");
                }

                DataTable personHobbies = Read(
                    connection,
                    transaction,
                    @"
                        SELECT HobbyId
                        FROM PersonHobbies
                        WHERE PersonId = $p0;
                    ",
                    personId);

                DataTable hobbyWishes = Read(
                    connection,
                    transaction,
                    @"
                        SELECT w.Body
                        FROM HobbyWishes w
                        JOIN PersonHobbies ph
                            ON ph.HobbyId = w.HobbyId
                        WHERE ph.PersonId = $p0
                        ORDER BY w.Id;
                    ",
                    personId);

                var wishes = new List<string>();

                foreach (DataRow row in hobbyWishes.Rows)
                {
                    wishes.Add(Convert.ToString(row["Body"]));
                }

                if (personHobbies.Rows.Count > 0
                    && wishes.Count == 0)
                {
                    throw new InvalidOperationException(
                        "У человека указаны увлечения, " +
                        "но для них нет тематических пожеланий.");
                }

                // Без увлечений используем основной текст.
                if (wishes.Count == 0)
                    wishes.Add("");

                // Читаем уже зарезервированное содержание.
                DataTable history = Read(
                    connection,
                    transaction,
                    @"
                        SELECT ContentKey, Text
                        FROM GreetingHistory
                        WHERE Status <> 'Отменено';
                    ");

                var usedKeys = new HashSet<string>();
                var usedTexts = new HashSet<string>();

                foreach (DataRow row in history.Rows)
                {
                    usedKeys.Add(
                        Convert.ToString(row["ContentKey"]));

                    usedTexts.Add(
                        Normalize(Convert.ToString(row["Text"])));
                }

                foreach (DataRow template in templates.Rows)
                {
                    string body = Convert.ToString(
                        template["Body"]);

                    foreach (string wish in wishes)
                    {
                        // Ключ строим ДО подстановки имени и возраста.
                        // Их замена не делает содержание уникальным.
                        string contentKey = Normalize(
                            body + " " + wish);

                        if (usedKeys.Contains(contentKey))
                            continue;

                        string text = body
                            .Replace("{Имя}", firstName)
                            .Replace(
                                "{Возраст}",
                                age.ToString(
                                    CultureInfo.InvariantCulture));

                        // Для универсального шаблона с именем
                        // добавляем грамматически подходящее обращение.
                        if (body.TrimStart().StartsWith(
                            "{Имя}",
                            StringComparison.Ordinal))
                        {
                            if (gender == "Мужской")
                                text = "Дорогой " + text;
                            else if (gender == "Женский")
                                text = "Дорогая " + text;
                        }

                        if (!string.IsNullOrWhiteSpace(wish))
                        {
                            text += " " + wish.Trim();
                        }

                        text = text.Trim();

                        if (usedTexts.Contains(Normalize(text)))
                            continue;

                        long historyId;

                        using (var command =
                            connection.CreateCommand())
                        {
                            command.Transaction = transaction;

                            command.CommandText = @"
                                INSERT INTO GreetingHistory
                                (
                                    EventId,
                                    TemplateId,
                                    Text,
                                    ContentKey,
                                    Status
                                )
                                VALUES
                                (
                                    $eventId,
                                    $templateId,
                                    $text,
                                    $contentKey,
                                    'Сформировано'
                                );
                            ";

                            command.Parameters.AddWithValue(
                                "$eventId", eventId);

                            command.Parameters.AddWithValue(
                                "$templateId",
                                Convert.ToInt64(template["Id"]));

                            command.Parameters.AddWithValue(
                                "$text", text);

                            command.Parameters.AddWithValue(
                                "$contentKey", contentKey);

                            command.ExecuteNonQuery();
                        }

                        using (var command =
                            connection.CreateCommand())
                        {
                            command.Transaction = transaction;

                            command.CommandText =
                                "SELECT last_insert_rowid();";

                            historyId = Convert.ToInt64(
                                command.ExecuteScalar());
                        }

                        transaction.Commit();

                        return new GreetingResult
                        {
                            Id = historyId,
                            Text = text,
                            AlreadyExists = false
                        };
                    }
                }

                throw new InvalidOperationException(
                    "Подходящие уникальные варианты закончились. " +
                    "Нужно расширить набор шаблонов или пожеланий.");
            }
        }

        private static DataTable Read(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string sql,
            params object[] values)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = sql;

                for (int i = 0; i < values.Length; i++)
                {
                    command.Parameters.AddWithValue(
                        "$p" + i,
                        values[i] ?? DBNull.Value);
                }

                using (var reader = command.ExecuteReader())
                {
                    var table = new DataTable();
                    table.Load(reader);
                    return table;
                }
            }
        }

        private static DateTime ParseDate(string value)
        {
            return DateTime.ParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture);
        }

        private static int CalculateAge(
            DateTime birthDate,
            DateTime eventDate)
        {
            if (eventDate.Date < birthDate.Date)
            {
                throw new ArgumentException(
                    "Событие не может быть раньше даты рождения.");
            }

            int age = eventDate.Year - birthDate.Year;

            if (birthDate.AddYears(age).Date > eventDate.Date)
                age--;

            return age;
        }

        private static string Normalize(string value)
        {
            return Regex.Replace(
                (value ?? "").Trim().ToLowerInvariant(),
                @"\s+",
                " ");
        }
    }
}