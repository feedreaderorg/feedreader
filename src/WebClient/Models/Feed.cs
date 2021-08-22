using System;

namespace FeedReader.WebClient.Models
{
    public class Feed
    {
        public string Id { get; set; }
        public string SubscriptionName { get; set; }
        public string IconUri { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int TotalSubscribers { get; set; }
    }
}