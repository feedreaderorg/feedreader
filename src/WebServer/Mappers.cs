namespace FeedReader.WebServer
{
    public static class Mappers
    {
        public static Share.Protocols.FeedInfo ToProtocolFeedInfo(this Models.FeedInfo f)
        {
            var feedInfo = new Share.Protocols.FeedInfo()
            {
                SubscriptionName = f.SubscriptionName ?? string.Empty,
                Description = f.Description ?? string.Empty,
                IconUri = f.IconUri ?? string.Empty,
                Name = f.Name ?? string.Empty
            };
            return feedInfo;
        }
    }
}