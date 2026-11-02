namespace GreetingsSystem.WinForms.Models
{
    public class Hobby
    {
        public long Id { get; set; }

        public string Name { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }
}