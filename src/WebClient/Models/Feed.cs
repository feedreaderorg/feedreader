using System;
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
        public int TotalFavorites { get; set; }
        public int TotalPosts { get; set; }
        public int TotalSubscribers { get; set; }
        public string SiteLink { get; set; }
        public bool IsSubscribed { get; set; }
        public DateTime LastReadedTime { get; set; }
        public RangeEnabledObservableCollection<FeedItem> FeedItems { get; set; }
        public event EventHandler OnStateChanged;

        public async Task RefreshAsync()
        {
            // Todo: refresh feed info.
            await RefreshInfoAsync();

            // Refresh feed items.
            var response = await App.CurrentUser.WebServerApi.GetFeedItemsAsync(new Share.Protocols.GetFeedItemsRequest()
            {
                FeedId = Id,
                Page = 0
            });

            // Update local cache.
            FeedItems = new RangeEnabledObservableCollection<FeedItem>();
            FeedItems.AddRange(response.FeedItems.Select(i =>
            {
                var f = i.ToModelFeedItem();
                f.Feed = this;
                return f;
            }), (f1, f2) => f1.PublishTime.DescCompareTo(f2.PublishTime));
            OnStateChanged?.Invoke(this, null);
        }

        public async Task RefreshInfoAsync()
        {
            var response = await App.CurrentUser.WebServerApi.GetFeedInfoAsync(new Share.Protocols.GetFeedInfoRequest()
            {
                FeedId = Id
            });

            if (!string.IsNullOrEmpty(response.Feed.Id))
            {
                UpdateFromProtoclFeedInfo(response.Feed);
            }
        }

        public void UpdateFromProtoclFeedInfo(Share.Protocols.FeedInfo feed)
        {
            Description = feed.Description;
            IconUri = feed.IconUri;
            Name = feed.Name;
            SubscriptionName = feed.SubscriptionName;
            TotalFavorites = feed.TotalFavorites;
            TotalPosts = feed.TotalPosts;
            TotalSubscribers = feed.TotalSubscribers;
            SiteLink = feed.SiteLink;
            if (feed.LastReadedTime != null)
            {
                LastReadedTime = feed.LastReadedTime.ToDateTime();
            }
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
        public Feed Feed { get; set; }
    }
}