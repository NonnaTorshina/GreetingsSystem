using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;

namespace GreetingsSystem.Waterfall.ConsoleApp.UI
{
    public static class Input
    {
        public static string Text(string title, bool required = true)
        {
            while (true)
            {
                Console.Write(title + ": ");
                string value = Console.ReadLine();

                if (value == null)
                    throw new OperationCanceledException("Ввод завершён.");
                if (value.IndexOf('\0') >= 0)
                {
                    Console.WriteLine(
                        "Ввод содержит нулевые символы. " +
                        "Проверьте кодировку консоли и повторите ввод.");

                    continue;
                }
                value = value.Trim();

                if (!required || value.Length > 0)
                    return value;

                Console.WriteLine("Значение не должно быть пустым.");
            }
        }

        public static long Number(string title, long min = 0, long max = long.MaxValue)
        {
            while (true)
            {
                long value;

                if (long.TryParse(Text(title), out value)
                    && value >= min && value <= max)
                {
                    return value;
                }

                Console.WriteLine("Введите целое число от {0} до {1}.", min, max);
            }
        }

        public static DateTime Date(string title)
        {
            while (true)
            {
                DateTime value;

                if (DateTime.TryParseExact(
                    Text(title + " (дд.мм.гггг)"),
                    "dd.MM.yyyy",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out value))
                {
                    return value.Date;
                }

                Console.WriteLine("Некорректная дата.");
            }
        }

        public static string Pick(string title, params string[] choices)
        {
            Console.WriteLine(title);

            for (int i = 0; i < choices.Length; i++)
                Console.WriteLine("{0}. {1}", i + 1, choices[i]);

            return choices[(int)Number("Выбор", 1, choices.Length) - 1];
        }

        public static List<long> Ids(string title)
        {
            while (true)
            {
                string text = Text(title + " — через запятую; пусто: ни одного", false);

                if (text.Length == 0)
                    return new List<long>();

                var ids = new HashSet<long>();
                bool valid = true;

                foreach (string part in text.Split(','))
                {
                    long id;

                    if (!long.TryParse(part.Trim(), out id) || id <= 0)
                    {
                        valid = false;
                        break;
                    }

                    ids.Add(id);
                }

                if (valid)
                    return ids.ToList();

                Console.WriteLine("Пример корректного ввода: 1,2,5");
            }
        }

        public static bool Confirm(string title)
        {
            return Pick(title, "Нет", "Да") == "Да";
        }

        public static void Show(DataTable table)
        {
            if (table.Rows.Count == 0)
            {
                Console.WriteLine("Записей нет.");
                return;
            }

            Console.WriteLine(string.Join(
                " | ",
                table.Columns.Cast<DataColumn>().Select(c => c.ColumnName)));

            foreach (DataRow row in table.Rows)
            {
                Console.WriteLine(string.Join(
                    " | ",
                    row.ItemArray.Select(v =>
                        Convert.ToString(v)
                            .Replace("\r", " ")
                            .Replace("\n", " "))));
            }
        }
    }
}