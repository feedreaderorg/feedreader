using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FeedReader.ServerCore.Models
{
    public static class OAuthIssuers
    {
        public const string FeedReader = "feedreader";
        public const string Microsoft = "microsoft";
        public const string Google = "google";
    }

    public class User
    {
        public Guid Id { get; set; }
        public DateTime RegistrationTime { get; set; }
        public List<FeedSubscription> SubscribedFeeds { get; set; }

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

        public DateTime LastReadedTime { get; set; }
    }

    [Index(nameof(UserId), IsUnique = false)]
    public class Favorite
    {
        public Guid UserId { get; set; }
        public User User { get; set; }

        public Guid FeedItemId { get; set; }
        public FeedItem FeedItem { get; set; }

        [Required]
        public Guid FeedInfoId { get; set; }
        public FeedInfo FeedInfo { get; set; }
    }
}