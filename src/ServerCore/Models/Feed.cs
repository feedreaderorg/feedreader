using Microsoft.EntityFrameworkCore;
using System;

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

        public DateTime LastUpdatedTime { get; set; }
    }
}