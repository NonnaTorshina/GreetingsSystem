namespace GreetingsSystem.WinForms.Models
{
    public class GreetingResult
    {
        public long Id { get; set; }

        public string Text { get; set; }

        public bool AlreadyExists { get; set; }
    }
}