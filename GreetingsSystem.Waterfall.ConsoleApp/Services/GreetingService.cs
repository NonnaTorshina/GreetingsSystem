using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using GreetingsSystem.Waterfall.ConsoleApp.Data;

namespace GreetingsSystem.Waterfall.ConsoleApp.Services
{
    public class GreetingService
    {
        public static string Normalize(string value)
        {
            return Regex.Replace(
                (value ?? "").Trim().ToLowerInvariant(),
                @"\s+",
                " ");
        }

        public static DateTime ParseDate(string value)
        {
            return DateTime.ParseExact(
                value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        public static string FormatDate(DateTime value)
        {
            return value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        public static int Age(DateTime birth, DateTime date)
        {
            if (date.Date < birth.Date)
                throw new ArgumentException("Событие раньше даты рождения.");

            int age = date.Year - birth.Year;

            if (birth.AddYears(age).Date > date.Date)
                age--;

            return age;
        }

        public string Generate(long eventId)
        {
            string result = null;

            Database.InTransaction((connection, transaction) =>
            {
                DataTable events = Database.Read(
                    connection, transaction,
                    @"SELECT e.*,p.FirstName,p.Gender,p.BirthDate
                      FROM Events e
                      JOIN People p ON p.Id=e.PersonId
                      WHERE e.Id=$p0;",
                    eventId);

                if (events.Rows.Count != 1)
                    throw new Exception("Событие не найдено.");

                DataTable existing = Database.Read(
                    connection, transaction,
                    @"SELECT Id,Text FROM History
                      WHERE EventId=$p0 AND Status<>'Отменено';",
                    eventId);

                if (existing.Rows.Count > 0)
                {
                    result = "Ранее сохранённое поздравление №"
                        + existing.Rows[0]["Id"]
                        + Environment.NewLine
                        + existing.Rows[0]["Text"];

                    return;
                }

                DataRow personEvent = events.Rows[0];

                int age = Age(
                    ParseDate((string)personEvent["BirthDate"]),
                    ParseDate((string)personEvent["EventDate"]));

                DataTable templates = Database.Read(
                    connection, transaction,
                    @"SELECT t.* FROM Templates t
                      WHERE t.IsActive=1
                        AND t.Occasion=$p0 COLLATE NOCASE
                        AND (t.Gender='Любой' OR t.Gender=$p1)
                        AND $p2 BETWEEN t.MinAge AND t.MaxAge
                        AND (
                            NOT EXISTS(
                                SELECT 1 FROM TemplateHobbies th
                                WHERE th.TemplateId=t.Id
                            )
                            OR EXISTS(
                                SELECT 1 FROM TemplateHobbies th
                                JOIN PersonHobbies ph ON ph.HobbyId=th.HobbyId
                                WHERE th.TemplateId=t.Id AND ph.PersonId=$p3
                            )
                        )
                      ORDER BY (t.MaxAge-t.MinAge),
                               (t.Gender<>'Любой') DESC,t.Id;",
                    personEvent["Occasion"],
                    personEvent["Gender"],
                    age,
                    personEvent["PersonId"]);

                if (templates.Rows.Count == 0)
                    throw new Exception("Подходящих шаблонов нет.");

                DataTable hobbies = Database.Read(
                    connection, transaction,
                    "SELECT HobbyId FROM PersonHobbies WHERE PersonId=$p0;",
                    personEvent["PersonId"]);

                DataTable wishesTable = Database.Read(
                    connection, transaction,
                    @"SELECT w.Body FROM Wishes w
                      JOIN PersonHobbies ph ON ph.HobbyId=w.HobbyId
                      WHERE ph.PersonId=$p0 ORDER BY w.Id;",
                    personEvent["PersonId"]);

                var wishes = new List<string>();

                foreach (DataRow row in wishesTable.Rows)
                    wishes.Add((string)row["Body"]);

                if (hobbies.Rows.Count > 0 && wishes.Count == 0)
                    throw new Exception("Добавьте пожелания для увлечений человека.");

                if (wishes.Count == 0)
                    wishes.Add("");

                var usedKeys = new HashSet<string>();
                var usedTexts = new HashSet<string>();

                DataTable history = Database.Read(
                    connection, transaction,
                    "SELECT ContentKey,Text FROM History WHERE Status<>'Отменено';");

                foreach (DataRow row in history.Rows)
                {
                    usedKeys.Add((string)row["ContentKey"]);
                    usedTexts.Add(Normalize((string)row["Text"]));
                }

                foreach (DataRow template in templates.Rows)
                {
                    foreach (string wish in wishes)
                    {
                        string body = (string)template["Body"];

                        // Сравниваем содержание ДО подстановки имени и возраста.
                        string key = Normalize(body + " " + wish);

                        if (usedKeys.Contains(key))
                            continue;

                        string text = body
                            .Replace("{Имя}", (string)personEvent["FirstName"])
                            .Replace("{Возраст}", age.ToString());

                        if (body.TrimStart().StartsWith(
                            "{Имя}", StringComparison.Ordinal))
                        {
                            string gender = (string)personEvent["Gender"];

                            if (gender == "Мужской")
                                text = "Дорогой " + text;
                            else if (gender == "Женский")
                                text = "Дорогая " + text;
                        }

                        text = (text + " " + wish).Trim();

                        if (usedTexts.Contains(Normalize(text)))
                            continue;

                        using (var command = Database.Command(
                            connection, transaction,
                            @"INSERT INTO History
                                (EventId,TemplateId,Text,ContentKey)
                              VALUES($p0,$p1,$p2,$p3);",
                            eventId, template["Id"], text, key))
                        {
                            command.ExecuteNonQuery();
                        }

                        result = text;
                        return;
                    }
                }

                throw new Exception(
                    "Уникальные варианты закончились. " +
                    "Добавьте новые шаблоны или пожелания.");
            });

            return result;
        }

        public void ChangeStatus(long id, string newStatus)
        {
            Database.InTransaction((connection, transaction) =>
            {
                DataTable table = Database.Read(
                    connection, transaction,
                    "SELECT Status FROM History WHERE Id=$p0;", id);

                if (table.Rows.Count != 1)
                    throw new Exception("Запись истории не найдена.");

                string oldStatus = (string)table.Rows[0]["Status"];

                bool allowed =
                    (oldStatus == "Сформировано"
                        && newStatus == "Подтверждено")
                    || (oldStatus == "Подтверждено"
                        && newStatus == "Отправлено")
                    || (newStatus == "Отменено"
                        && (oldStatus == "Сформировано"
                            || oldStatus == "Подтверждено"));

                if (!allowed)
                    throw new Exception("Такое изменение состояния запрещено.");

                using (var command = Database.Command(
                    connection, transaction,
                    @"UPDATE History SET Status=$p0
                      WHERE Id=$p1 AND Status=$p2;",
                    newStatus, id, oldStatus))
                {
                    if (command.ExecuteNonQuery() != 1)
                        throw new Exception("Запись изменилась. Обновите список.");
                }
            });
        }
    }
}