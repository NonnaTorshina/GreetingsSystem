using System;
using System.Text;
using GreetingsSystem.Waterfall.ConsoleApp.Data;
using GreetingsSystem.Waterfall.ConsoleApp.UI;

namespace GreetingsSystem.Waterfall.ConsoleApp
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.InputEncoding = Encoding.GetEncoding(1251);
            Console.OutputEncoding = Encoding.GetEncoding(1251);

            try
            {
                Database.Initialize();

                Console.WriteLine("Консольная система поздравлений");
                Console.WriteLine("База: " + Database.FilePath);

                new ConsoleMenu().Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка запуска:");
                Console.WriteLine(ex.ToString());
                Console.WriteLine("Нажмите Enter для завершения.");
                Console.ReadLine();
            }
        }
    }
}