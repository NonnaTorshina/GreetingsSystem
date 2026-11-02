using Microsoft.Data.Sqlite;
using System;
using System.IO;

namespace GreetingsSystem.WinForms.Data
{
    public static class Database
    {
        private const int CurrentVersion = 1;

        public static string DataDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "GreetingsSystem");
            }
        }

        public static string DatabasePath
        {
            get
            {
                return Path.Combine(DataDirectory, "greetings.db");
            }
        }

        public static SqliteConnection OpenConnection()
        {
            Directory.CreateDirectory(DataDirectory);

            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = DatabasePath,
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

        public static void Initialize()
        {
            using (var connection = OpenConnection())
            {
                int version;

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "PRAGMA user_version;";
                    version = Convert.ToInt32(command.ExecuteScalar());
                }

                if (version > CurrentVersion)
                {
                    throw new InvalidOperationException(
                        "Эта база создана более новой версией приложения.");
                }

                if (version == CurrentVersion)
                    return;

                using (var transaction = connection.BeginTransaction())
                {
                    Execute(connection, transaction, CreateTablesSql);
                    Execute(connection, transaction, SeedDataSql);

                    Execute(
                        connection,
                        transaction,
                        "PRAGMA user_version = 1;");

                    transaction.Commit();
                }
            }
        }

        private static void Execute(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string sql)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }

        private const string CreateTablesSql = @"
            CREATE TABLE IF NOT EXISTS People
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,

                LastName TEXT NOT NULL
                    CHECK (length(trim(LastName)) > 0),

                FirstName TEXT NOT NULL
                    CHECK (length(trim(FirstName)) > 0),

                MiddleName TEXT NOT NULL DEFAULT '',

                Gender TEXT NOT NULL
                    CHECK (
                        Gender IN ('Мужской', 'Женский', 'Не указан')
                    ),

                BirthDate TEXT NOT NULL,

                Category TEXT NOT NULL
                    CHECK (Category IN ('Друг', 'Коллега'))
            );

            CREATE TABLE IF NOT EXISTS Hobbies
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,

                Name TEXT NOT NULL UNIQUE
                    CHECK (length(trim(Name)) > 0)
            );

            CREATE TABLE IF NOT EXISTS PersonHobbies
            (
                PersonId INTEGER NOT NULL,
                HobbyId INTEGER NOT NULL,

                PRIMARY KEY (PersonId, HobbyId),

                FOREIGN KEY (PersonId)
                    REFERENCES People(Id) ON DELETE CASCADE,

                FOREIGN KEY (HobbyId)
                    REFERENCES Hobbies(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS Acquaintances
            (
                PersonAId INTEGER NOT NULL,
                PersonBId INTEGER NOT NULL,

                PRIMARY KEY (PersonAId, PersonBId),

                CHECK (PersonAId < PersonBId),

                FOREIGN KEY (PersonAId)
                    REFERENCES People(Id) ON DELETE CASCADE,

                FOREIGN KEY (PersonBId)
                    REFERENCES People(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS EventTypes
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,

                Name TEXT NOT NULL UNIQUE
                    CHECK (length(trim(Name)) > 0)
            );

            CREATE TABLE IF NOT EXISTS Events
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                PersonId INTEGER NOT NULL,
                EventTypeId INTEGER NOT NULL,
                EventDate TEXT NOT NULL,
                Notes TEXT NOT NULL DEFAULT '',

                UNIQUE (PersonId, EventTypeId, EventDate),

                FOREIGN KEY (PersonId)
                    REFERENCES People(Id) ON DELETE RESTRICT,

                FOREIGN KEY (EventTypeId)
                    REFERENCES EventTypes(Id) ON DELETE RESTRICT
            );

            CREATE TABLE IF NOT EXISTS AgeGroups
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,

                Name TEXT NOT NULL UNIQUE
                    CHECK (length(trim(Name)) > 0),

                MinAge INTEGER NOT NULL CHECK (MinAge >= 0),

                MaxAge INTEGER NOT NULL
                    CHECK (MaxAge >= MinAge)
            );

            CREATE TABLE IF NOT EXISTS GreetingTemplates
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,

                Name TEXT NOT NULL UNIQUE
                    CHECK (length(trim(Name)) > 0),

                EventTypeId INTEGER NOT NULL,

                Gender TEXT NOT NULL DEFAULT 'Любой'
                    CHECK (
                        Gender IN (
                            'Любой',
                            'Мужской',
                            'Женский',
                            'Не указан'
                        )
                    ),

                AgeGroupId INTEGER,

                Body TEXT NOT NULL
                    CHECK (length(trim(Body)) > 0),

                IsActive INTEGER NOT NULL DEFAULT 1
                    CHECK (IsActive IN (0, 1)),

                FOREIGN KEY (EventTypeId)
                    REFERENCES EventTypes(Id) ON DELETE RESTRICT,

                FOREIGN KEY (AgeGroupId)
                    REFERENCES AgeGroups(Id) ON DELETE RESTRICT
            );

            CREATE TABLE IF NOT EXISTS TemplateHobbies
            (
                TemplateId INTEGER NOT NULL,
                HobbyId INTEGER NOT NULL,

                PRIMARY KEY (TemplateId, HobbyId),

                FOREIGN KEY (TemplateId)
                    REFERENCES GreetingTemplates(Id) ON DELETE CASCADE,

                FOREIGN KEY (HobbyId)
                    REFERENCES Hobbies(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS HobbyWishes
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                HobbyId INTEGER NOT NULL,

                Body TEXT NOT NULL
                    CHECK (length(trim(Body)) > 0),

                UNIQUE (HobbyId, Body),

                FOREIGN KEY (HobbyId)
                    REFERENCES Hobbies(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS GreetingHistory
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                EventId INTEGER NOT NULL,
                TemplateId INTEGER,

                Text TEXT NOT NULL
                    CHECK (length(trim(Text)) > 0),

                ContentKey TEXT NOT NULL
                    CHECK (length(trim(ContentKey)) > 0),

                CreatedAt TEXT NOT NULL
                    DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),

                Status TEXT NOT NULL DEFAULT 'Сформировано'
                    CHECK (
                        Status IN (
                            'Сформировано',
                            'Подтверждено',
                            'Отправлено',
                            'Отменено'
                        )
                    ),

                FOREIGN KEY (EventId)
                    REFERENCES Events(Id) ON DELETE RESTRICT,

                FOREIGN KEY (TemplateId)
                    REFERENCES GreetingTemplates(Id) ON DELETE SET NULL
            );

            CREATE INDEX IF NOT EXISTS IX_People_Name
                ON People(LastName, FirstName);

            CREATE INDEX IF NOT EXISTS IX_PersonHobbies_Hobby
                ON PersonHobbies(HobbyId);

            CREATE INDEX IF NOT EXISTS IX_Acquaintances_PersonB
                ON Acquaintances(PersonBId);

            CREATE INDEX IF NOT EXISTS IX_Events_Date
                ON Events(EventDate);

            CREATE INDEX IF NOT EXISTS IX_Events_Type
                ON Events(EventTypeId);

            CREATE INDEX IF NOT EXISTS IX_Templates_Event
                ON GreetingTemplates(EventTypeId);

            CREATE INDEX IF NOT EXISTS IX_TemplateHobbies_Hobby
                ON TemplateHobbies(HobbyId);

            CREATE INDEX IF NOT EXISTS IX_History_Event
                ON GreetingHistory(EventId);

            CREATE UNIQUE INDEX IF NOT EXISTS UX_History_ContentKey
                ON GreetingHistory(ContentKey)
                WHERE Status <> 'Отменено';

            CREATE UNIQUE INDEX IF NOT EXISTS UX_History_UsedEvent
                ON GreetingHistory(EventId)
                WHERE Status IN ('Подтверждено', 'Отправлено');
        ";

        private const string SeedDataSql = @"
            INSERT OR IGNORE INTO Hobbies(Name)
            VALUES
                ('Музыка'),
                ('Спорт'),
                ('Путешествия'),
                ('Чтение'),
                ('Кулинария'),
                ('Программирование'),
                ('Фотография'),
                ('Садоводство');

            INSERT OR IGNORE INTO EventTypes(Name)
            VALUES
                ('День рождения'),
                ('Новый год'),
                ('Профессиональный праздник');

            INSERT OR IGNORE INTO AgeGroups(Name, MinAge, MaxAge)
            VALUES
                ('Дети', 0, 12),
                ('Подростки', 13, 17),
                ('Взрослые', 18, 59),
                ('Старшая возрастная группа', 60, 150);

            INSERT INTO HobbyWishes(HobbyId, Body)
            SELECT Id,
                CASE Name
                    WHEN 'Музыка'
                        THEN 'Пусть музыка дарит вдохновение и яркие эмоции!'
                    WHEN 'Спорт'
                        THEN 'Желаю энергии, отличной формы и новых спортивных побед!'
                    WHEN 'Путешествия'
                        THEN 'Пусть впереди ждут удивительные маршруты и открытия!'
                    WHEN 'Чтение'
                        THEN 'Желаю увлекательных книг и интересных открытий!'
                    WHEN 'Кулинария'
                        THEN 'Пусть новые рецепты радуют тебя и близких!'
                    WHEN 'Программирование'
                        THEN 'Желаю интересных проектов и красивых решений!'
                    WHEN 'Фотография'
                        THEN 'Пусть каждый день дарит кадры, которые хочется сохранить!'
                    WHEN 'Садоводство'
                        THEN 'Пусть любимый сад радует цветением и богатым урожаем!'
                END
            FROM Hobbies
            WHERE Name IN (
                'Музыка', 'Спорт', 'Путешествия', 'Чтение',
                'Кулинария', 'Программирование',
                'Фотография', 'Садоводство'
            );

            INSERT INTO GreetingTemplates
                (Name, EventTypeId, Gender, AgeGroupId, Body)
            SELECT
                'День рождения — универсальный 1',
                Id,
                'Любой',
                NULL,
                '{Имя}, с днём рождения! Желаю здоровья, радости и исполнения желаний.'
            FROM EventTypes WHERE Name = 'День рождения';

            INSERT INTO GreetingTemplates
                (Name, EventTypeId, Gender, AgeGroupId, Body)
            SELECT
                'День рождения — универсальный 2',
                Id,
                'Любой',
                NULL,
                '{Имя}, поздравляю с днём рождения! Пусть рядом будут близкие люди, а каждый день приносит повод улыбнуться.'
            FROM EventTypes WHERE Name = 'День рождения';

            INSERT INTO GreetingTemplates
                (Name, EventTypeId, Gender, AgeGroupId, Body)
            SELECT
                'День рождения — мужской',
                Id,
                'Мужской',
                NULL,
                'Дорогой {Имя}, с днём рождения! Желаю уверенности в своих силах и успеха в любимом деле.'
            FROM EventTypes WHERE Name = 'День рождения';

            INSERT INTO GreetingTemplates
                (Name, EventTypeId, Gender, AgeGroupId, Body)
            SELECT
                'День рождения — женский',
                Id,
                'Женский',
                NULL,
                'Дорогая {Имя}, с днём рождения! Желаю вдохновения, душевного тепла и счастливых событий.'
            FROM EventTypes WHERE Name = 'День рождения';

            INSERT INTO GreetingTemplates
                (Name, EventTypeId, Gender, AgeGroupId, Body)
            SELECT
                'День рождения — детский',
                e.Id,
                'Любой',
                a.Id,
                '{Имя}, с днём рождения! Желаю весёлых игр, хороших друзей и удивительных приключений!'
            FROM EventTypes e
            CROSS JOIN AgeGroups a
            WHERE e.Name = 'День рождения'
              AND a.Name = 'Дети';

            INSERT INTO GreetingTemplates
                (Name, EventTypeId, Gender, AgeGroupId, Body)
            SELECT
                'День рождения — подростковый',
                e.Id,
                'Любой',
                a.Id,
                '{Имя}, с днём рождения! Смело пробуй новое, находи свои увлечения и верь в свои возможности!'
            FROM EventTypes e
            CROSS JOIN AgeGroups a
            WHERE e.Name = 'День рождения'
              AND a.Name = 'Подростки';

            INSERT INTO GreetingTemplates
                (Name, EventTypeId, Gender, AgeGroupId, Body)
            SELECT
                'День рождения — взрослый',
                e.Id,
                'Любой',
                a.Id,
                '{Имя}, с днём рождения! Желаю удачных начинаний, времени для себя и радости от достигнутых целей.'
            FROM EventTypes e
            CROSS JOIN AgeGroups a
            WHERE e.Name = 'День рождения'
              AND a.Name = 'Взрослые';

            INSERT INTO GreetingTemplates
                (Name, EventTypeId, Gender, AgeGroupId, Body)
            SELECT
                'День рождения — старшая группа',
                e.Id,
                'Любой',
                a.Id,
                '{Имя}, с днём рождения! Желаю крепкого здоровья, семейного тепла и благополучия.'
            FROM EventTypes e
            CROSS JOIN AgeGroups a
            WHERE e.Name = 'День рождения'
              AND a.Name = 'Старшая возрастная группа';

            INSERT INTO GreetingTemplates
                (Name, EventTypeId, Gender, AgeGroupId, Body)
            SELECT
                'Новый год — универсальный',
                Id,
                'Любой',
                NULL,
                '{Имя}, с Новым годом! Пусть он принесёт добрые встречи, приятные перемены и новые возможности.'
            FROM EventTypes WHERE Name = 'Новый год';

            INSERT INTO GreetingTemplates
                (Name, EventTypeId, Gender, AgeGroupId, Body)
            SELECT
                'Профессиональный праздник — универсальный',
                Id,
                'Любой',
                NULL,
                '{Имя}, поздравляю с профессиональным праздником! Желаю интересных задач, признания и удовольствия от работы.'
            FROM EventTypes WHERE Name = 'Профессиональный праздник';
        ";
    }
}