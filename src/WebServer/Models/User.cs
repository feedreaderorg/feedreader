using System;

namespace FeedReader.WebServer.Models
{
    public static class UserRoles
    {
        public const string Guest = "guest";
        public const string Unregistered = "unregistered";
        public const string Normal = "normal";
    }

    public class User
    {
        public string Username { get; set; }
        public string Role { get; set; }
        public string DisplayName { get; set; }
        public string AvatarUri { get; set; }
        public DateTime RegistrationTime { get; set; }
        public string Token { get; set; }
    }
}