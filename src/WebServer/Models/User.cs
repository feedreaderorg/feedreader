using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace FeedReader.WebServer.Models
{
    public static class OAuthIssuers
    {
        public const string FeedReader = "feedreader";
        public const string Microsoft = "microsoft";
    }

    public class User
    {
        public Guid Id { get; set; }
        public DateTime RegistrationTime { get; set; }

        [NotMapped]
        public string Token { get; set; }
    }

    public class UserOAuthIds
    {
        public string OAuthIssuer { get; set; }

        public string OAuthId { get; set; }

        public Guid UserId { get; set; }
    }
}