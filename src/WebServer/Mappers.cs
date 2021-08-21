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
                Name = f.Name ?? string.Empty
            };
            return feedInfo;
        }
    }
}