using System;
using System.Linq;
using System.Web;

namespace FeedReader.WebServer
{
    public static class Mappers
    {
        public static string StaticServer { get; set; }

        public static Share.Protocols.FeedInfo ToProtocolFeedInfo(this ServerCore.Models.FeedInfo f)
        {
            var feedInfo = new Share.Protocols.FeedInfo()
            {
                Id = f.Id.ToString() ?? string.Empty,
                SubscriptionName = f.SubscriptionName ?? string.Empty,
                Description = f.Description ?? string.Empty,
                IconUri = (f.IconUri ?? string.Empty).ToSafeImageUri(),
                Name = f.Name ?? string.Empty,
                TotalFavorites = f.TotalFavorites,
                TotalPosts = f.TotalPosts,
                TotalSubscribers = f.TotalSubscribers,
                SiteLink = f.WebsiteLink ?? string.Empty
            };
            return feedInfo;
        }

        public static Share.Protocols.User ToProtocolUser(this ServerCore.Models.User u)
        {
            var user = new Share.Protocols.User()
            {
                Id = u.Id.ToString()
            };
            user.SubscribedFeeds.AddRange(u.SubscribedFeeds.Select(f => f.Feed.ToProtocolFeedInfo()));
            return user;
        }

        public static Share.Protocols.FeedItem ToProtocolFeedItem(this ServerCore.Models.FeedItem f)
        {
            return new Share.Protocols.FeedItem()
            {
                FeedId = f.FeedId.ToString() ?? string.Empty,
                Id = f.Id.ToString() ?? string.Empty,
                Link = f.Link ?? string.Empty,
                PictureUri = (f.PictureUri ?? string.Empty).ToSafeImageUri(),
                PublishTime = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(f.PublishTime.ToUniversalTime()),
                Summary = f.Summary ?? string.Empty,
                Title = f.Title ?? string.Empty,
                TotalFavorites = f.TotalFavorites
            };
        }

        static string ToSafeImageUri(this string str)
        {
            return str.ToLower().StartsWith("http://") ? $"{StaticServer}/imgproxy?uri={HttpUtility.UrlEncode(str)}" : str;
        }
    }
}