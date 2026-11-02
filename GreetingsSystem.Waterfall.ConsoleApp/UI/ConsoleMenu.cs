using System;
using System.Collections.Generic;
using System.Data;
using GreetingsSystem.Waterfall.ConsoleApp.Data;
using GreetingsSystem.Waterfall.ConsoleApp.Services;

namespace GreetingsSystem.Waterfall.ConsoleApp.UI
{
    public class ConsoleMenu
    {
        private readonly GreetingService greetingService =
            new GreetingService();

        public void Run()
        {
            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("===== СИСТЕМА ПОЗДРАВЛЕНИЙ =====");
                Console.WriteLine("1. Люди");
                Console.WriteLine("2. Увлечения и пожелания");
                Console.WriteLine("3. Знакомства");
                Console.WriteLine("4. События");
                Console.WriteLine("5. Шаблоны");
                Console.WriteLine("6. Сформировать поздравление");
                Console.WriteLine("7. История");
                Console.WriteLine("0. Выход");

                int choice = (int)Input.Number("Раздел", 0, 7);

                if (choice == 0)
                    return;

                try
                {
                    switch (choice)
                    {
                        case 1: People(); break;
                        case 2: Hobbies(); break;
                        case 3: Acquaintances(); break;
                        case 4: Events(); break;
                        case 5: Templates(); break;
                        case 6:
                            ShowEvents();
                            Console.WriteLine(greetingService.Generate(
                                Input.Number("Номер события", 1)));
                            break;
                        case 7: History(); break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Ошибка: " + ex.Message);
                }
            }
        }

        private void ShowPeople()
        {
            Input.Show(Database.Query(
                @"SELECT Id,LastName,FirstName,MiddleName,
                         Gender,BirthDate,Category
                  FROM People ORDER BY LastName,FirstName;"));
        }

        private void People()
        {
            ShowPeople();

            string action = Input.Pick(
                "Люди",
                "Добавить", "Изменить", "Удалить",
                "Поиск", "Увлечения человека", "Назад");

            if (action == "Назад")
                return;

            if (action == "Поиск")
            {
                string search = Input.Text("Часть ФИО");
                DataTable table = Database.Query("SELECT * FROM People;");
                DataTable result = table.Clone();

                foreach (DataRow row in table.Rows)
                {
                    string name = row["LastName"] + " "
                        + row["FirstName"] + " " + row["MiddleName"];

                    if (name.IndexOf(
                        search, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        result.ImportRow(row);
                    }
                }

                Input.Show(result);
                return;
            }

            if (action == "Увлечения человека")
            {
                long personId = Input.Number("Номер человека", 1);
                RequirePerson(personId);

                Input.Show(Database.Query("SELECT * FROM Hobbies;"));

                Input.Show(Database.Query(
                    @"SELECT h.Id,h.Name FROM Hobbies h
                      JOIN PersonHobbies ph ON ph.HobbyId=h.Id
                      WHERE ph.PersonId=$p0;", personId));

                List<long> ids = Input.Ids("Новый полный список увлечений");

                Database.InTransaction((connection, transaction) =>
                {
                    using (var command = Database.Command(
                        connection, transaction,
                        "DELETE FROM PersonHobbies WHERE PersonId=$p0;",
                        personId))
                    {
                        command.ExecuteNonQuery();
                    }

                    foreach (long id in ids)
                    {
                        using (var command = Database.Command(
                            connection, transaction,
                            "INSERT INTO PersonHobbies VALUES($p0,$p1);",
                            personId, id))
                        {
                            command.ExecuteNonQuery();
                        }
                    }
                });

                Console.WriteLine("Увлечения сохранены.");
                return;
            }

            long person = action == "Добавить"
                ? 0 : Input.Number("Номер человека", 1);

            if (person != 0)
                RequirePerson(person);

            if (action == "Удалить")
            {
                if (Input.Confirm("Удалить человека и его связи?"))
                {
                    Database.Execute(
                        "DELETE FROM People WHERE Id=$p0;", person);
                }

                return;
            }

            string last = Input.Text("Фамилия");
            Console.WriteLine("Фамилия: [{0}], длина: {1}", last, last.Length);

            foreach (char symbol in last)
            {
                Console.Write("U+{0:X4} ", (int)symbol);
            }

            Console.WriteLine();
            string first = Input.Text("Имя");
            string middle = Input.Text("Отчество", false);

            string gender = Input.Pick(
                "Пол", "Не указан", "Мужской", "Женский");

            DateTime birth = Input.Date("Дата рождения");

            if (birth > DateTime.Today)
                throw new Exception("Дата рождения не может быть в будущем.");

            string category = Input.Pick("Категория", "Друг", "Коллега");

            Database.InTransaction((connection, transaction) =>
            {
                DataTable existing = Database.Read(
                    connection, transaction,
                    "SELECT * FROM People WHERE Id<>$p0;", person);

                foreach (DataRow row in existing.Rows)
                {
                    if (GreetingService.Normalize((string)row["LastName"])
                            == GreetingService.Normalize(last)
                        && GreetingService.Normalize((string)row["FirstName"])
                            == GreetingService.Normalize(first)
                        && GreetingService.Normalize((string)row["MiddleName"])
                            == GreetingService.Normalize(middle)
                        && (string)row["BirthDate"]
                            == GreetingService.FormatDate(birth))
                    {
                        throw new Exception("Такой человек уже существует.");
                    }
                }

                if (person != 0)
                {
                    DataTable events = Database.Read(
                        connection, transaction,
                        "SELECT EventDate FROM Events WHERE PersonId=$p0;",
                        person);

                    foreach (DataRow row in events.Rows)
                    {
                        if (GreetingService.ParseDate(
                            (string)row["EventDate"]) < birth)
                        {
                            throw new Exception(
                                "Новая дата рождения позже существующего события.");
                        }
                    }
                }

                string sql = person == 0
                    ? @"INSERT INTO People
                        (LastName,FirstName,MiddleName,Gender,BirthDate,Category)
                        VALUES($p0,$p1,$p2,$p3,$p4,$p5);"
                    : @"UPDATE People SET LastName=$p0,FirstName=$p1,
                        MiddleName=$p2,Gender=$p3,BirthDate=$p4,Category=$p5
                        WHERE Id=$p6;";

                using (var command = Database.Command(
                    connection, transaction, sql,
                    last, first, middle, gender,
                    GreetingService.FormatDate(birth), category, person))
                {
                    if (command.ExecuteNonQuery() != 1)
                        throw new Exception("Человек не найден.");
                }
            });

            Console.WriteLine("Человек сохранён.");
        }

        private void Hobbies()
        {
            Input.Show(Database.Query("SELECT * FROM Hobbies;"));

            string action = Input.Pick(
                "Справочник",
                "Добавить увлечение", "Переименовать увлечение",
                "Удалить увлечение", "Пожелания", "Назад");

            if (action == "Назад")
                return;

            if (action == "Пожелания")
            {
                Wishes();
                return;
            }

            long id = action == "Добавить увлечение"
                ? 0 : Input.Number("Номер увлечения", 1);

            if (action == "Удалить увлечение")
            {
                DataTable links = Database.Query(
                    @"SELECT HobbyId FROM PersonHobbies WHERE HobbyId=$p0
                      UNION ALL
                      SELECT HobbyId FROM TemplateHobbies WHERE HobbyId=$p0;",
                    id);

                if (links.Rows.Count > 0)
                    throw new Exception("Увлечение используется людьми или шаблонами.");

                if (Input.Confirm("Удалить увлечение вместе с пожеланиями?"))
                {
                    CheckChanged(Database.Execute(
                        "DELETE FROM Hobbies WHERE Id=$p0;", id));
                }

                return;
            }

            string name = Input.Text("Название");

            foreach (DataRow row in Database.Query(
                "SELECT Id,Name FROM Hobbies;").Rows)
            {
                if (Convert.ToInt64(row["Id"]) != id
                    && GreetingService.Normalize((string)row["Name"])
                        == GreetingService.Normalize(name))
                {
                    throw new Exception("Такое увлечение уже есть.");
                }
            }

            if (id == 0)
                Database.Execute("INSERT INTO Hobbies(Name) VALUES($p0);", name);
            else
                CheckChanged(Database.Execute(
                    "UPDATE Hobbies SET Name=$p0 WHERE Id=$p1;", name, id));

            Console.WriteLine("Сохранено.");
        }

        private void Wishes()
        {
            long hobby = Input.Number("Номер увлечения", 1);

            if (Database.Query(
                "SELECT Id FROM Hobbies WHERE Id=$p0;", hobby).Rows.Count != 1)
            {
                throw new Exception("Увлечение не найдено.");
            }

            Input.Show(Database.Query(
                "SELECT Id,Body FROM Wishes WHERE HobbyId=$p0;", hobby));

            string action = Input.Pick(
                "Пожелания", "Добавить", "Изменить", "Удалить", "Назад");

            if (action == "Назад")
                return;

            long id = action == "Добавить"
                ? 0 : Input.Number("Номер пожелания", 1);

            if (action == "Удалить")
            {
                if (Input.Confirm("Удалить пожелание?"))
                {
                    CheckChanged(Database.Execute(
                        "DELETE FROM Wishes WHERE Id=$p0 AND HobbyId=$p1;",
                        id, hobby));
                }

                return;
            }

            string body = Input.Text("Текст пожелания");

            if (body.Contains("{") || body.Contains("}"))
                throw new Exception("В пожеланиях не используются подстановки.");

            foreach (DataRow row in Database.Query(
                "SELECT Id,Body FROM Wishes WHERE HobbyId=$p0;", hobby).Rows)
            {
                if (Convert.ToInt64(row["Id"]) != id
                    && GreetingService.Normalize((string)row["Body"])
                        == GreetingService.Normalize(body))
                {
                    throw new Exception("Такое пожелание уже существует.");
                }
            }

            if (id == 0)
                Database.Execute(
                    "INSERT INTO Wishes(HobbyId,Body) VALUES($p0,$p1);",
                    hobby, body);
            else
                CheckChanged(Database.Execute(
                    "UPDATE Wishes SET Body=$p0 WHERE Id=$p1 AND HobbyId=$p2;",
                    body, id, hobby));

            Console.WriteLine("Пожелание сохранено.");
        }

        private void Acquaintances()
        {
            ShowPeople();

            Input.Show(Database.Query(
                @"SELECT a.PersonAId,a.PersonBId,
                         p.LastName AS FirstPerson,q.LastName AS SecondPerson
                  FROM Acquaintances a
                  JOIN People p ON p.Id=a.PersonAId
                  JOIN People q ON q.Id=a.PersonBId;"));

            string action = Input.Pick(
                "Знакомства", "Добавить", "Удалить", "Назад");

            if (action == "Назад")
                return;

            long first = Input.Number("Номер первого человека", 1);
            long second = Input.Number("Номер второго человека", 1);

            RequirePerson(first);
            RequirePerson(second);

            if (first == second)
                throw new Exception("Нельзя указать знакомство с самим собой.");

            long a = Math.Min(first, second);
            long b = Math.Max(first, second);

            if (action == "Добавить")
            {
                Database.Execute(
                    "INSERT INTO Acquaintances VALUES($p0,$p1);", a, b);
            }
            else if (Input.Confirm("Удалить знакомство?"))
            {
                CheckChanged(Database.Execute(
                    @"DELETE FROM Acquaintances
                      WHERE PersonAId=$p0 AND PersonBId=$p1;", a, b));
            }
        }

        private void ShowEvents()
        {
            Input.Show(Database.Query(
                @"SELECT e.Id,p.LastName,p.FirstName,
                         e.Occasion,e.EventDate,e.Notes
                  FROM Events e
                  JOIN People p ON p.Id=e.PersonId
                  ORDER BY e.EventDate,e.Id;"));
        }

        private void Events()
        {
            ShowEvents();

            string action = Input.Pick(
                "События", "Добавить", "Изменить",
                "Удалить", "Предстоящие", "Назад");

            if (action == "Назад")
                return;

            if (action == "Предстоящие")
            {
                Input.Show(Database.Query(
                    @"SELECT e.Id,p.LastName,p.FirstName,e.Occasion,e.EventDate
                      FROM Events e JOIN People p ON p.Id=e.PersonId
                      WHERE e.EventDate >= $p0 ORDER BY e.EventDate;",
                    GreetingService.FormatDate(DateTime.Today)));

                return;
            }

            long id = action == "Добавить"
                ? 0 : Input.Number("Номер события", 1);

            if (id != 0)
            {
                if (Database.Query(
                    "SELECT Id FROM Events WHERE Id=$p0;", id).Rows.Count != 1)
                    throw new Exception("Событие не найдено.");

                if (Database.Query(
                    "SELECT Id FROM History WHERE EventId=$p0;", id).Rows.Count > 0)
                    throw new Exception("Событие с историей нельзя менять или удалять.");
            }

            if (action == "Удалить")
            {
                if (Input.Confirm("Удалить событие?"))
                    CheckChanged(Database.Execute(
                        "DELETE FROM Events WHERE Id=$p0;", id));

                return;
            }

            ShowPeople();
            long person = Input.Number("Номер получателя", 1);
            DataRow selected = RequirePerson(person);
            string occasion = Occasion();
            DateTime date = Input.Date("Дата события");

            GreetingService.Age(
                GreetingService.ParseDate((string)selected["BirthDate"]), date);

            string notes = Input.Text("Примечание", false);

            if (id == 0)
            {
                Database.Execute(
                    @"INSERT INTO Events(PersonId,Occasion,EventDate,Notes)
                      VALUES($p0,$p1,$p2,$p3);",
                    person, occasion, GreetingService.FormatDate(date), notes);
            }
            else
            {
                CheckChanged(Database.Execute(
                    @"UPDATE Events SET PersonId=$p0,Occasion=$p1,
                      EventDate=$p2,Notes=$p3 WHERE Id=$p4;",
                    person, occasion, GreetingService.FormatDate(date), notes, id));
            }

            Console.WriteLine("Событие сохранено.");
        }

        private void Templates()
        {
            Input.Show(Database.Query(
                @"SELECT Id,Name,Occasion,Gender,MinAge,MaxAge,IsActive
                  FROM Templates ORDER BY Id;"));

            string action = Input.Pick(
                "Шаблоны", "Добавить", "Изменить", "Назад");

            if (action == "Назад")
                return;

            long id = action == "Добавить"
                ? 0 : Input.Number("Номер шаблона", 1);

            if (id != 0)
            {
                DataTable existing = Database.Query(
                    "SELECT * FROM Templates WHERE Id=$p0;", id);

                if (existing.Rows.Count != 1)
                    throw new Exception("Шаблон не найден.");

                Input.Show(existing);
                Console.WriteLine("Введите полный новый набор значений.");
            }

            string name = Input.Text("Название");
            string occasion = Occasion();

            string gender = Input.Pick(
                "Пол", "Любой", "Мужской", "Женский", "Не указан");

            int min = (int)Input.Number("Минимальный возраст", 0, 150);
            int max = (int)Input.Number("Максимальный возраст", min, 150);

            string body = Input.Text("Текст; доступны {Имя} и {Возраст}");

            string remainder = body
                .Replace("{Имя}", "")
                .Replace("{Возраст}", "");

            if (remainder.Contains("{") || remainder.Contains("}"))
                throw new Exception("Неизвестная подстановка.");

            bool active = Input.Confirm("Использовать шаблон?");

            Input.Show(Database.Query("SELECT * FROM Hobbies;"));

            List<long> hobbies = Input.Ids(
                "Ограничение по увлечениям");

            Database.InTransaction((connection, transaction) =>
            {
                long savedId = id;

                string sql = id == 0
                    ? @"INSERT INTO Templates
                        (Name,Occasion,Gender,MinAge,MaxAge,Body,IsActive)
                        VALUES($p0,$p1,$p2,$p3,$p4,$p5,$p6);"
                    : @"UPDATE Templates SET Name=$p0,Occasion=$p1,
                        Gender=$p2,MinAge=$p3,MaxAge=$p4,Body=$p5,
                        IsActive=$p6 WHERE Id=$p7;";

                using (var command = Database.Command(
                    connection, transaction, sql,
                    name, occasion, gender, min, max, body,
                    active ? 1 : 0, id))
                {
                    CheckChanged(command.ExecuteNonQuery());
                }

                if (id == 0)
                {
                    using (var command = Database.Command(
                        connection, transaction,
                        "SELECT last_insert_rowid();"))
                    {
                        savedId = Convert.ToInt64(command.ExecuteScalar());
                    }
                }

                using (var command = Database.Command(
                    connection, transaction,
                    "DELETE FROM TemplateHobbies WHERE TemplateId=$p0;",
                    savedId))
                {
                    command.ExecuteNonQuery();
                }

                foreach (long hobby in hobbies)
                {
                    using (var command = Database.Command(
                        connection, transaction,
                        "INSERT INTO TemplateHobbies VALUES($p0,$p1);",
                        savedId, hobby))
                    {
                        command.ExecuteNonQuery();
                    }
                }
            });

            Console.WriteLine("Шаблон сохранён.");
        }

        private void History()
        {
            Input.Show(Database.Query(
                @"SELECT h.Id,p.LastName,p.FirstName,
                         e.Occasion,e.EventDate,h.Status,h.CreatedAt
                  FROM History h
                  JOIN Events e ON e.Id=h.EventId
                  JOIN People p ON p.Id=e.PersonId
                  ORDER BY h.Id DESC;"));

            string action = Input.Pick(
                "История",
                "Открыть текст", "Подтвердить",
                "Отметить отправленным", "Отменить", "Назад");

            if (action == "Назад")
                return;

            long id = Input.Number("Номер записи истории", 1);

            if (action == "Открыть текст")
            {
                DataTable table = Database.Query(
                    "SELECT Text FROM History WHERE Id=$p0;", id);

                if (table.Rows.Count != 1)
                    throw new Exception("Запись не найдена.");

                Console.WriteLine(table.Rows[0]["Text"]);
                return;
            }

            string status = action == "Подтвердить"
                ? "Подтверждено"
                : action == "Отметить отправленным"
                    ? "Отправлено"
                    : "Отменено";

            if (Input.Confirm(
                "Изменить состояние? Отправка выполняется вами вручную."))
            {
                greetingService.ChangeStatus(id, status);
                Console.WriteLine("Состояние изменено.");
            }
        }

        private static DataRow RequirePerson(long id)
        {
            DataTable table = Database.Query(
                "SELECT * FROM People WHERE Id=$p0;", id);

            if (table.Rows.Count != 1)
                throw new Exception("Человек не найден.");

            return table.Rows[0];
        }

        private static string Occasion()
        {
            return Input.Pick(
                "Повод",
                "День рождения",
                "Новый год",
                "Профессиональный праздник");
        }

        private static void CheckChanged(int count)
        {
            if (count != 1)
                throw new Exception("Запись не найдена.");
        }
    }
}