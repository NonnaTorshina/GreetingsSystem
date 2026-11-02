using System;
using System.Data;
using System.IO;
using Microsoft.Data.Sqlite;

namespace GreetingsSystem.Waterfall.ConsoleApp.Data
{
    public static class Database
    {
        public static string FilePath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "GreetingsSystem",
                    "greetings_waterfall_console.db");
            }
        }

        public static SqliteConnection Open()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));

            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = FilePath,
                ForeignKeys = true
            };

            var connection = new SqliteConnection(builder.ToString());

            try
            {
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "PRAGMA busy_timeout = 5000;";
                    command.ExecuteNonQuery();
                }

                return connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        public static SqliteCommand Command(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string sql,
            params object[] values)
        {
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;

            for (int i = 0; i < values.Length; i++)
            {
                command.Parameters.AddWithValue(
                    "$p" + i,
                    values[i] ?? DBNull.Value);
            }

            return command;
        }

        public static DataTable Read(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string sql,
            params object[] values)
        {
            using (var command = Command(
                connection, transaction, sql, values))
            using (var reader = command.ExecuteReader())
            {
                var table = new DataTable();
                table.Load(reader);
                return table;
            }
        }

        public static DataTable Query(string sql, params object[] values)
        {
            using (var connection = Open())
            {
                return Read(connection, null, sql, values);
            }
        }

        public static int Execute(string sql, params object[] values)
        {
            using (var connection = Open())
            using (var command = Command(connection, null, sql, values))
            {
                return command.ExecuteNonQuery();
            }
        }

        public static void InTransaction(
            Action<SqliteConnection, SqliteTransaction> action)
        {
            using (var connection = Open())
            using (var transaction = connection.BeginTransaction())
            {
                action(connection, transaction);
                transaction.Commit();
            }
        }

        public static void Initialize()
        {
            InTransaction((connection, transaction) =>
            {
                int version;

                using (var command = Command(
                    connection, transaction, "PRAGMA user_version;"))
                {
                    version = Convert.ToInt32(command.ExecuteScalar());
                }

                if (version > 1)
                    throw new Exception("База создана более новой версией.");

                if (version == 1)
                    return;

                using (var command = Command(
                    connection, transaction, Schema))
                {
                    command.ExecuteNonQuery();
                }

                string[] hobbies =
                {
                    "Музыка", "Спорт", "Путешествия",
                    "Чтение", "Программирование"
                };

                string[] wishes =
                {
                    "Пусть музыка дарит вдохновение!",
                    "Желаю энергии и новых спортивных побед!",
                    "Пусть впереди ждут удивительные путешествия!",
                    "Желаю интересных книг и ярких открытий!",
                    "Желаю интересных проектов и красивых решений!"
                };

                for (int i = 0; i < hobbies.Length; i++)
                {
                    using (var command = Command(
                        connection,
                        transaction,
                        "INSERT INTO Hobbies(Name) VALUES($p0);",
                        hobbies[i]))
                    {
                        command.ExecuteNonQuery();
                    }

                    using (var command = Command(
                        connection,
                        transaction,
                        @"INSERT INTO Wishes(HobbyId,Body)
                          SELECT Id,$p1 FROM Hobbies WHERE Name=$p0;",
                        hobbies[i], wishes[i]))
                    {
                        command.ExecuteNonQuery();
                    }
                }

                string[] bodies =
                {
                    "{Имя}, с днём рождения! Желаю здоровья и радости.",
                    "{Имя}, с днём рождения! Пусть сбываются мечты.",
                    "{Имя}, поздравляю с днём рождения! Желаю счастливых событий.",
                    "{Имя}, с Новым годом! Пусть он принесёт добрые перемены.",
                    "{Имя}, с профессиональным праздником! Желаю успехов в работе."
                };

                for (int i = 0; i < bodies.Length; i++)
                {
                    string occasion = i < 3
                        ? "День рождения"
                        : i == 3
                            ? "Новый год"
                            : "Профессиональный праздник";

                    using (var command = Command(
                        connection,
                        transaction,
                        @"INSERT INTO Templates
                            (Name,Occasion,Gender,MinAge,MaxAge,Body)
                          VALUES($p0,$p1,'Любой',0,150,$p2);",
                        "Начальный шаблон " + (i + 1),
                        occasion,
                        bodies[i]))
                    {
                        command.ExecuteNonQuery();
                    }
                }

                using (var command = Command(
                    connection, transaction, "PRAGMA user_version=1;"))
                {
                    command.ExecuteNonQuery();
                }
            });
        }

        private const string Schema = @"
CREATE TABLE People(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    LastName TEXT NOT NULL CHECK(length(trim(LastName))>0),
    FirstName TEXT NOT NULL CHECK(length(trim(FirstName))>0),
    MiddleName TEXT NOT NULL DEFAULT '',
    Gender TEXT NOT NULL CHECK(Gender IN('Мужской','Женский','Не указан')),
    BirthDate TEXT NOT NULL,
    Category TEXT NOT NULL CHECK(Category IN('Друг','Коллега'))
);

CREATE TABLE Hobbies(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE CHECK(length(trim(Name))>0)
);

CREATE TABLE PersonHobbies(
    PersonId INTEGER NOT NULL REFERENCES People(Id) ON DELETE CASCADE,
    HobbyId INTEGER NOT NULL REFERENCES Hobbies(Id) ON DELETE CASCADE,
    PRIMARY KEY(PersonId,HobbyId)
);

CREATE TABLE Acquaintances(
    PersonAId INTEGER NOT NULL REFERENCES People(Id) ON DELETE CASCADE,
    PersonBId INTEGER NOT NULL REFERENCES People(Id) ON DELETE CASCADE,
    PRIMARY KEY(PersonAId,PersonBId),
    CHECK(PersonAId<PersonBId)
);

CREATE TABLE Events(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    PersonId INTEGER NOT NULL REFERENCES People(Id) ON DELETE RESTRICT,
    Occasion TEXT NOT NULL CHECK(length(trim(Occasion))>0),
    EventDate TEXT NOT NULL,
    Notes TEXT NOT NULL DEFAULT '',
    UNIQUE(PersonId,Occasion,EventDate)
);

CREATE TABLE Templates(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE CHECK(length(trim(Name))>0),
    Occasion TEXT NOT NULL,
    Gender TEXT NOT NULL CHECK(Gender IN('Любой','Мужской','Женский','Не указан')),
    MinAge INTEGER NOT NULL CHECK(MinAge>=0),
    MaxAge INTEGER NOT NULL CHECK(MaxAge>=MinAge),
    Body TEXT NOT NULL CHECK(length(trim(Body))>0),
    IsActive INTEGER NOT NULL DEFAULT 1 CHECK(IsActive IN(0,1))
);

CREATE TABLE Wishes(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    HobbyId INTEGER NOT NULL REFERENCES Hobbies(Id) ON DELETE CASCADE,
    Body TEXT NOT NULL CHECK(length(trim(Body))>0),
    UNIQUE(HobbyId,Body)
);

CREATE TABLE TemplateHobbies(
    TemplateId INTEGER NOT NULL REFERENCES Templates(Id) ON DELETE CASCADE,
    HobbyId INTEGER NOT NULL REFERENCES Hobbies(Id) ON DELETE CASCADE,
    PRIMARY KEY(TemplateId,HobbyId)
);

CREATE TABLE History(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    EventId INTEGER NOT NULL REFERENCES Events(Id) ON DELETE RESTRICT,
    TemplateId INTEGER REFERENCES Templates(Id) ON DELETE SET NULL,
    Text TEXT NOT NULL,
    ContentKey TEXT NOT NULL,
    CreatedAt TEXT NOT NULL DEFAULT(strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    Status TEXT NOT NULL DEFAULT 'Сформировано'
        CHECK(Status IN('Сформировано','Подтверждено','Отправлено','Отменено'))
);

CREATE UNIQUE INDEX UX_History_Content
    ON History(ContentKey) WHERE Status<>'Отменено';

CREATE UNIQUE INDEX UX_History_Event
    ON History(EventId) WHERE Status<>'Отменено';

CREATE INDEX IX_Events_Date ON Events(EventDate);
CREATE INDEX IX_Acquaintances_B ON Acquaintances(PersonBId);
";
    }
}