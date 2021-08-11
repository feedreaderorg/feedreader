using System;
using System.Threading.Tasks;

namespace FeedReader.WebClient.Models
{
    public enum UserRole
    {
        Guest,
        Normal
    };

    public class User
    {
        public UserRole Role { get; set; }

        public async Task LoginAsync(string jwtToken)
        {
            await Task.Delay(3000);
            throw new Exception("User is not found.");
            // Role = UserRole.Normal;
        }
    }
}