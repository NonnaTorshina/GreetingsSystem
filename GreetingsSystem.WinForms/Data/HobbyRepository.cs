using System.Collections.Generic;
using GreetingsSystem.WinForms.Models;

namespace GreetingsSystem.WinForms.Data
{
    public class HobbyRepository
    {
        public List<Hobby> GetAll()
        {
            var hobbies = new List<Hobby>();

            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT Id, Name
                    FROM Hobbies
                    ORDER BY Name;
                ";

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        hobbies.Add(new Hobby
                        {
                            Id = reader.GetInt64(0),
                            Name = reader.GetString(1)
                        });
                    }
                }
            }

            return hobbies;
        }

        public List<long> GetForPerson(long personId)
        {
            var ids = new List<long>();

            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT HobbyId
                    FROM PersonHobbies
                    WHERE PersonId = $personId;
                ";

                command.Parameters.AddWithValue(
                    "$personId", personId);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        ids.Add(reader.GetInt64(0));
                    }
                }
            }

            return ids;
        }
    }
}