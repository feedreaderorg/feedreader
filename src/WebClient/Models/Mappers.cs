using System;

namespace FeedReader.WebClient.Models
{
    static class Mappers
    {
        public static Feed ToModelFeed(this Share.Protocols.FeedInfo f)
        {
            return new Feed()
            {
                Id = f.Id,
                SubscriptionName = f.SubscriptionName,
                Description = f.Description,
                IconUri = f.IconUri,
                Name = f.Name,
                TotalFavorites = f.TotalFavorites,
                TotalPosts = f.TotalPosts,
                TotalSubscribers = f.TotalSubscribers,
                SiteLink = f.SiteLink,
                LastReadedTime = f.LastReadedTime == null ? default(DateTime) : f.LastReadedTime.ToDateTime(),
            };
        }

        public static FeedItem ToModelFeedItem(this Share.Protocols.FeedItem f)
        {
            return new FeedItem()
            {
                Id = f.Id,
                Link = f.Link,
                PictureUri = f.PictureUri,
                PublishTime = f.PublishTime.ToDateTime(),
                Summary = f.Summary,
                Title = f.Title,
                TotalFavorites = f.TotalFavorites,
                Feed = f.Feed?.ToModelFeed(),
            };
        }
    }
}