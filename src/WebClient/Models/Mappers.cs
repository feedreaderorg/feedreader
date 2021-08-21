using System;

namespace FeedReader.WebClient.Models
{
    static class Mappers
    {
        public static Feed ToModelFeed(this Share.Protocols.FeedInfo f)
        {
            return new Feed()
            {
                Id = Guid.Parse(f.Id),
                SubscriptionName = f.SubscriptionName,
                Description = f.Description,
                IconUri = f.IconUri,
                Name = f.Name,
                TotalSubscribers = f.TotalSubscribers
            };
        }
    }
}