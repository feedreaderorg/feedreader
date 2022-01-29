using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using System;
using System.Collections.Generic;

namespace FeedReader.ServerCore.Models
{
    [Index(nameof(IdFromUri), IsUnique = true)]
    [Index(nameof(SubscriptionName), IsUnique = true)]
    public class FeedInfo
    {
        public Guid Id { get; set; }

        public Guid IdFromUri { get; set; }

        public string SubscriptionName { get; set; }

        public string Uri { get; set; }

        public string IconUri { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public string WebsiteLink { get; set; }

        public DateTime RegistrationTime { get; set; }

        public string LastParseError { get; set; }

        public int TotalSubscribers { get; set; }

        public int TotalPosts { get; set; }

        public int TotalFavorites { get; set; }

        public DateTime LastUpdatedTime { get; set; }

        public List<FeedItem> FeedItems { get; set; }

        public NpgsqlTsVector SearchVector { get; set; }
    }

    public class FeedItem
    {
        public Guid Id { get; set; }

        public Guid FeedId { get; set; }
        public FeedInfo Feed { get; set; }

        public string Link { get; set;}

        public DateTime PublishTime { get; set; }

        public string Summary { get; set; }

        public string Content { get; set; }

        public string Title { get; set; }

        public string PictureUri { get; set; }

        public int TotalFavorites { get; set; }
    }
}