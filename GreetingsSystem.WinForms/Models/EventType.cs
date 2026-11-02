namespace GreetingsSystem.WinForms.Models
{
    public class EventType
    {
        public long Id { get; set; }

        public string Name { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }
}