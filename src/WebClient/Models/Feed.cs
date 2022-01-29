using Google.Protobuf.WellKnownTypes;
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
        public int TotalFavorites { get; set; }
        public int TotalPosts { get; set; }
        public int TotalSubscribers { get; set; }
        public string SiteLink { get; set; }
        public DateTime LastReadedTime { get; set; }
        public string RssUri { get; set; }
        private List<FeedItem> FeedItems { get; set; } = new List<FeedItem>();
        public event EventHandler OnStateChanged;

        public async Task RefreshAsync()
        {
            // Todo: refresh feed info.
            await RefreshInfoAsync();

            // Refresh feed items.
            var response = await App.CurrentUser.WebServerApi.GetFeedItemsAsync(new Share.Protocols.GetFeedItemsRequest()
            {
                FeedId = Id,
                StartIndex = 0,
                Count = 50,
            });

            // Update local cache.
            await GetFeedItems(startIndex: 0, count: 50);
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
            RssUri = feed.RssUri;
            if (feed.LastReadedTime != null)
            {
                LastReadedTime = feed.LastReadedTime.ToDateTime();
            }
        }

        public async Task<IEnumerable<FeedItem>> GetFeedItems(int startIndex, int count)
        {
            while (FeedItems.Count < startIndex + count)
            {
                var response = await App.CurrentUser.WebServerApi.GetFeedItemsAsync(new Share.Protocols.GetFeedItemsRequest()
                {
                    FeedId = Id,
                    StartIndex = startIndex,
                    Count = 50,
                });
                var newItems = response.FeedItems.Select(i =>
                {
                    var f = i.ToModelFeedItem();
                    f.Feed = this;
                    return f;
                });
                FeedItems.AddRange(newItems);
                FeedItems = FeedItems.DistinctBy(f => f.Id).OrderByDescending(f => f.PublishTime).ToList();
                if (newItems.Count() < 50)
                {
                    break;
                }
            }

            if (startIndex >= FeedItems.Count)
            {
                return new List<FeedItem>();
            }
            else
            {
                count = Math.Min(FeedItems.Count - startIndex, count);
                return FeedItems.GetRange(startIndex, count);
            }
        }

        public async Task MarkAsReaded()
        {
            if (FeedItems.Count == 0)
            {
                return;
            }

            LastReadedTime = FeedItems.First().PublishTime;
            await App.CurrentUser.WebServerApi.UpdateFeedSubscriptionAsync(new Share.Protocols.UpdateFeedSubscriptionRequest()
            {
                FeedId = Id.ToString(),
                LastReadedTime = LastReadedTime.ToTimestamp(),
            });
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