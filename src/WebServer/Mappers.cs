using System;
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
                SiteLink = f.WebsiteLink ?? string.Empty,
                RssUri = f.Uri ?? string.Empty,
                ForceSubscribed = f.ForceSubscribed,
            };
            return feedInfo;
        }

        public static Share.Protocols.FeedInfo ToProtocolFeedInfo(this ServerCore.Models.FeedSubscription f)
        {
            var feedInfo = f.Feed.ToProtocolFeedInfo();
            feedInfo.LastReadedTime = f.LastReadedTime.ToProtocolTime();
            return feedInfo;
        }

        public static Share.Protocols.User ToProtocolUser(this ServerCore.Models.User u)
        {
            var user = new Share.Protocols.User()
            {
                Id = u.Id.ToString()
            };
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
                PublishTime = f.PublishTime.ToProtocolTime(),
                Summary = f.Summary ?? string.Empty,
                Title = f.Title ?? string.Empty,
                TotalFavorites = f.TotalFavorites,
                Feed = f.Feed?.ToProtocolFeedInfo(),
            };
        }

        public static Google.Protobuf.WellKnownTypes.Timestamp ToProtocolTime(this DateTime f)
        {
            if (f == default(DateTime))
            {
                f = DateTime.SpecifyKind(f, DateTimeKind.Utc);
            }
            return Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(f);
        }

        static string ToSafeImageUri(this string str)
        {
            return str.ToLower().StartsWith("http://") ? $"{StaticServer}/imgproxy?uri={HttpUtility.UrlEncode(str)}" : str;
        }
    }
}