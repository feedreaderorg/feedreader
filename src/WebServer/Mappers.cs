using System.Linq;

namespace FeedReader.WebServer
{
    public static class Mappers
    {
        public static Share.Protocols.FeedInfo ToProtocolFeedInfo(this ServerCore.Models.FeedInfo f)
        {
            var feedInfo = new Share.Protocols.FeedInfo()
            {
                Id = f.Id.ToString() ?? string.Empty,
                SubscriptionName = f.SubscriptionName ?? string.Empty,
                Description = f.Description ?? string.Empty,
                IconUri = f.IconUri ?? string.Empty,
                Name = f.Name ?? string.Empty,
                TotalSubscribers = f.TotalSubscribers,
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
    }
}