using System;
using System.Collections.Generic;

namespace GreetingsSystem.WinForms.Models
{
    public class Person
    {
        public long Id { get; set; }

        public string LastName { get; set; }

        public string FirstName { get; set; }

        public string MiddleName { get; set; }

        public string Gender { get; set; }

        public DateTime BirthDate { get; set; }

        public string Category { get; set; }

        public List<long> HobbyIds { get; set; } = new List<long>();
    }
}