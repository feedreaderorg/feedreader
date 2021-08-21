using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace FeedReader.ServerCore.Models
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

    [Index(nameof(UserId), IsUnique = false)]
    [Index(nameof(FeedId), IsUnique = false)]
    public class FeedSubscription
    {
        public Guid UserId { get; set; }
        public User User { get; set; }

        public Guid FeedId { get; set; }
        public FeedInfo Feed { get; set; }
    }
}