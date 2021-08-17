namespace FeedReader.WebClient.Models
{
    static class Mappers
    {
        public static Feed ToModelFeed(this Share.Protocols.FeedInfo f)
        {
            return new Feed()
            {
                SubscriptionName = f.SubscriptionName,
                Description = f.Description,
                IconUri = f.IconUri,
                Name = f.Name
            };
        }
    }
}