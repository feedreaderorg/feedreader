using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
        public string SiteLink { get; set; }
        public List<FeedItem> FeedItems { get; set; }
        public event EventHandler OnStateChanged;

        public async Task RefreshAsync()
        {
            // Todo: refresh feed info.

            // Refresh feed items.
            var response = await App.CurrentUser.WebServerApi.GetFeedItemsAsync(new Share.Protocols.GetFeedItemsRequest()
            {
                FeedId = Id,
                Page = 0
            });

            // Update local cache.
            FeedItems = response.FeedItems.Select(f => f.ToModelFeedItem()).ToList();
            OnStateChanged?.Invoke(this, null);
        }
    }

    public class FeedItem
    {
        public string Id { get; set; }
        public string Link { get; set; }
        public DateTime PublishTime { get; set; }
        public string Summary { get; set; }
        public string Title { get; set; }
        public string PictureUri { get; set; }
        public int TotalFavorites { get; set; }
    }
}