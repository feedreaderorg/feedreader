using Google.Protobuf.WellKnownTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FeedReader.WebClient.Models
{
    public class Feed
    {
        private static List<FeedItem> RecomendedFeedItems { get; set; } = new List<FeedItem>();

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
        public bool ForceSubscribed { get; set; }
        public bool HasNewItems
		{
            get
			{
                return FeedItems.Count > 0 && FeedItems.First().PublishTime > LastReadedTime;
			}
		}

        private List<FeedItem> FeedItems { get; set; } = new List<FeedItem>();
        public event EventHandler OnStateChanged;

        public async Task RefreshAsync()
        {
            // Todo: refresh feed info.
            await RefreshInfoAsync();

            // Update local cache.
            await GetFeedItems(startIndex: 0, count: 50);
            OnStateChanged?.Invoke(this, null);
        }

        private async Task RefreshInfoAsync()
        {
            var response = await App.CurrentUser.AnonymousService.GetFeedInfoAsync(new Share.Protocols.GetFeedInfoRequest()
            {
                FeedId = Id
            });

            if (!string.IsNullOrEmpty(response.Feed.Id))
            {
                UpdateFromProtcolFeedInfo(response.Feed.ToModelFeed());
            }
        }

        public void UpdateFromProtcolFeedInfo(Feed feed)
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
            ForceSubscribed = feed.ForceSubscribed;
            if (feed.LastReadedTime != default(DateTime))
            {
                LastReadedTime = feed.LastReadedTime;
            }
        }

        public async Task<IEnumerable<FeedItem>> GetFeedItems(int startIndex, int count)
        {
            return await GetFeedItems(FeedItems, startIndex, count, async (s, c) =>
            {
                var request = new Share.Protocols.GetFeedItemsRequest()
                {
                    FeedId = Id,
                    StartIndex = s,
                    Count = c,
                };
                return (await App.CurrentUser.AnonymousService.GetFeedItemsAsync(request)).FeedItems;
            }, perItemOp: item => item.Feed = this);
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
            OnStateChanged?.Invoke(this, null);
        }

        public static async Task<IEnumerable<FeedItem>> GetRecomendedFeedItems(int startIndex, int count)
        {
            return await GetFeedItems(RecomendedFeedItems, startIndex, count, async (s, c) =>
            {
                var request = new Share.Protocols.GetRecommedFeedItemsRequest()
                {
                    StartIndex = s,
                    Count = c,
                };
                return (await App.CurrentUser.AnonymousService.GetRecommedFeedItemsAsync(request)).FeedItems;
            });
        }

        private static async Task<IEnumerable<FeedItem>> GetFeedItems(List<FeedItem> cacheList, int startIndex, int count, Func<int, int, Task<IEnumerable<Share.Protocols.FeedItem>>> op, Action<FeedItem> perItemOp = null)
        {
            while (cacheList.Count < startIndex + count)
            {
                var response = await op(cacheList.Count, 50);
                var newItems = response.Select(i => i.ToModelFeedItem()).ToArray();
                if (perItemOp != null)
                {
                    foreach (var item in newItems)
                    {
                        perItemOp(item);
                    }
                }
                cacheList.AddRange(newItems);
                cacheList.Sort((x, y) => x.PublishTime.DescCompareTo(y.PublishTime));
                if (newItems.Count() < 50)
                {
                    break;
                }
            }
            return cacheList.Skip(startIndex).Take(count);
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