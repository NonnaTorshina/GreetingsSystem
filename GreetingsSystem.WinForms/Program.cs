using System;
using System.Windows.Forms;

namespace GreetingsSystem.WinForms
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                Data.Database.Initialize();
                Application.Run(new Form1());
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.ToString(),
                    "Ошибка запуска",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}