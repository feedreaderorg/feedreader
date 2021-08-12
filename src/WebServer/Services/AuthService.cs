using System;
using System.Threading.Tasks;
using FeedReader.WebServer.Models;

namespace FeedReader.WebServer.Services
{
    public class AuthService
    {
        public async Task<User> LoginAsync(string token)
        {
            var user = new User()
            {
                Token = "faketoken",
                Username = "fakeuser",
                Role = UserRoles.Normal,
                DisplayName = "Fake User",
                AvatarUri = "",
                RegistrationTime = DateTime.UtcNow
            };
            return await Task.FromResult(user);
        }
    }
}