using FeedReader.ServerCore.Models;
using FeedReader.ServerCore.Processors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FeedReader.ServerCore.Services
{
    public class FeedService
    {
        IDbContextFactory<DbContext> DbFactory { get; set; }
        HttpClient HttpClient { get; set; }
        ILogger Logger { get; set; }
        private FeedProcessor FeedProcessor { get; }

        public FeedService(IDbContextFactory<DbContext> dbFactory, HttpClient httpClient, ILogger<FeedService> logger, IConfiguration configuration)
        {
            DbFactory = dbFactory;
            HttpClient = httpClient;
            Logger = logger;
            FeedProcessor = new FeedProcessor(HttpClient);
        }

        public async Task<List<FeedInfo>> DiscoverFeedsAsync(string query, int startIndex, int count)
        {
            if (string.IsNullOrEmpty(query))
            {
                using (var db = await DbFactory.CreateDbContextAsync())
                {
                    return await db.FeedInfos
                        .OrderByDescending(f => f.TotalSubscribers)
                        .ThenByDescending(f => f.LatestItemPublishTime)
                        .Skip(startIndex)
                        .Take(count)
                        .ToListAsync();
                }
            }

            var feeds = new List<FeedInfo>();

            // Normalize query string.
            query = query.Trim().TrimEnd('/');
            if (string.IsNullOrEmpty(query))
            {
                return feeds;
            }

            // Treat it as uri.
            Uri uri;
            if (Uri.TryCreate(query, UriKind.Absolute, out uri))
            {
                // Try to get from https if this is http.
                var httpsUri = uri.GetHttpsVersion();
                await TryToDiscoverFeedsFromUriAsync(httpsUri, feeds);
                if (feeds.Count > 0)
                {
                    return feeds;
                }

                if (httpsUri != uri)
                {
                    // Ok, try uri directly.
                    await TryToDiscoverFeedsFromUriAsync(uri, feeds);
                }
                return feeds;
            }
            else
            {
                // Search in name and description.
                using (var db = await DbFactory.CreateDbContextAsync())
                {
                    feeds = await db.FeedInfos
                        .Where(f => f.SearchVector.Matches(query))
                        .Skip(startIndex)
                        .ToListAsync();
                }
            }

            return feeds;
        }

        public async Task RefreshFeedsAsync(CancellationToken cancellationToken)
        {
            using (var db = DbFactory.CreateDbContext())
            {
                await BatchOperation(db.FeedInfos.OrderBy(f => f.DbId).Select(f => f.Id), batchSize: 50, cancellationToken,
                    perItemOp: async (feedId) =>
                    {
                        try
                        {
                            await RefreshFeedsAsync(feedId, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            // TODO: save error in db?
                            Logger.LogError(ex, "Refresh feedId: {0} failed.", feedId);
                        }
                    });
            }
        }

        public async Task<FeedInfo> GetFeedInfoById(Guid feedId)
        {
            using (var db = await DbFactory.CreateDbContextAsync())
            {
                return await db.FeedInfos.FindAsync(feedId);
            }
        }

        public async Task<FeedInfo> GetFeedInfoBySubscriptionName(string subscriptionName)
        {
            using (var db = await DbFactory.CreateDbContextAsync())
            {
                return await db.FeedInfos.FirstOrDefaultAsync(f => f.SubscriptionName == subscriptionName);
            }
        }

        public async Task<List<FeedItem>> GetFeedItemsByIdAsync(Guid? feedId, int startIndex, int count)
        {
            using (var db = DbFactory.CreateDbContext())
            {
                IQueryable<FeedItem> query = db.FeedItems;
                if (feedId != null)
                {
                    query = query.Where(f => f.FeedId == feedId);
                }
                else
                {
                    query = query.Include(f => f.Feed);
                }
                return await query
                    .OrderByDescending(f => f.PublishTime)
                    .Skip(startIndex)
                    .Take(count).ToListAsync();
            }
        }

        public async Task RefreshFeedItemStatisticsAsync(CancellationToken cancellationToken)
        {
            using (var db = DbFactory.CreateDbContext())
            {
                await BatchOperation(db.FeedItems.OrderBy(f => f.DbId).Select(f => f.Id), batchSize: 50, cancellationToken, 
                    perItemOp: async (feedItemId) =>
                    {
                        var totalFavorites = await db.Favorites.CountAsync(f => f.FeedItemId == feedItemId);
                        db.UpdateEntity(() => new FeedItem()
                        {
                            Id = feedItemId,
                            TotalFavorites = totalFavorites
                        });
                    },
                    perBatchOp: () => db.SaveChangesAsync());
            }
        }

        async Task TryToDiscoverFeedsFromUriAsync(Uri uri, List<FeedInfo> feeds)
        {
            var idFromUri = new Guid(MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(uri.ToString().ToLower())));

            // Try to find from idFromUri.
            using (var db = DbFactory.CreateDbContext())
            {
                var feedInDb = await db.FeedInfos.FirstOrDefaultAsync(f => f.IdFromUri == idFromUri);
                if (feedInDb != null)
                {
                    feeds.Add(feedInDb);
                    return;
                }
            }

            // Get the content from the uri.
            string content = null;
            try
            {
                content = await HttpClient.GetStringAsync(uri);
            }
            catch
            {
            }
            if (string.IsNullOrEmpty(content))
            {
                return;
            }

            var feed = await FeedProcessor.TryToParseFeedFromContent(content, parseItems: false);
            if (feed != null)
            {
                feed.Id = Guid.NewGuid();
                feed.IdFromUri = idFromUri;
                feed.RegistrationTime = DateTime.UtcNow;
                feed.Uri = uri.ToString();

                // Save to db.
                using (var db = DbFactory.CreateDbContext())
                {
                    if (await db.FeedInfos.FirstOrDefaultAsync(f => f.IdFromUri == idFromUri) == null)
                    {
                        bool findUnusedSubscriptionName = false;
                        for (var i = 0; i < 10; ++i)
                        {
                            feed.SubscriptionName = GenerateFeedSubscriptionName(feed, feed.SubscriptionName);
                            if (await db.FeedInfos.FirstOrDefaultAsync(f => f.SubscriptionName == feed.SubscriptionName) == null)
                            {
                                findUnusedSubscriptionName = true;
                                break;
                            }
                        }
                        if (findUnusedSubscriptionName)
                        {
                            db.FeedInfos.Add(feed);
                            await db.SaveChangesAsync();
                            _ = RefreshFeedsAsync(feed.Id, default(CancellationToken));
                        }
                        else
                        {
                            throw new Exception("Failed to generate unused subscription name");
                        }
                    }
                }

                // Return
                feeds.Add(feed);
                return;
            }

            // Try to discover feed from html content directly.
            await TryToDiscoverFeedsFromHtmlAsync(content, feeds);
        }

        string GenerateFeedSubscriptionName(FeedInfo feed, string lastProposedName)
        {
            var proposedName = lastProposedName;
            if (!string.IsNullOrEmpty(proposedName))
            {
                var lastHyphen = proposedName.LastIndexOf("-0");
                if (lastHyphen > -1)
                {
                    proposedName.Substring(0, lastHyphen);
                }
                return $"{proposedName}-{new Random().Next(1000)}";
            }
            else
            {
                proposedName = "";
                var uri = new Uri(feed.Uri);
                var domainsPart = uri.Host.ToLower().Split('.');
                for (var i = 0; i < domainsPart.Length - 1; ++i)
                {
                    var str = new string(domainsPart[i].Where(c => char.IsLetterOrDigit(c)).ToArray());
                    if (str == "www")
                    {
                        continue;
                    }
                    else
                    {
                        if (proposedName == "")
                        {
                            proposedName = str;
                        }
                        else
                        {
                            proposedName = $"{proposedName}-{str}";
                        }
                    }
                }
                if (proposedName == "")
                {
                    proposedName = new Random().Next(1000).ToString();
                }
                return proposedName;
            }
        }

        Task TryToDiscoverFeedsFromHtmlAsync(string content, List<FeedInfo> feeds)
        {
            return Task.CompletedTask;
        }

        async Task RefreshFeedsAsync(Guid feedId, CancellationToken cancellationToken)
        {
            using (var db = DbFactory.CreateDbContext())
            {
                var feed = await db.FeedInfos.FindAsync(new object[] { feedId }, cancellationToken);
                if (feed == null)
                {
                    return;
                }

                DateTime starTime = DateTime.Now;
                Logger.LogInformation($"Refresh feed: {feed.Uri} at {starTime}");

                var content = await HttpClient.GetStringAsync(feed.Uri, cancellationToken);
                if (string.IsNullOrEmpty(content))
                {
                    feed.LastParseError = "Fetch content failed.";
                    return;
                }

                var parsedFeed = await FeedProcessor.TryToParseFeedFromContent(content, parseItems: true, cancellationToken);
                if (parsedFeed == null)
                {
                    feed.LastParseError = "Parse feed failed.";
                }

                // Update feed information.
                feed.Description = parsedFeed.Description;
                feed.IconUri = parsedFeed.IconUri;
                feed.Name = parsedFeed.Name;
                feed.WebsiteLink = parsedFeed.WebsiteLink;
                feed.LastUpdatedTime = DateTime.UtcNow;

                // Update feed items.
                foreach (var parsedFeedItem in parsedFeed.FeedItems.Where(f => f.Id != Guid.Empty).OrderByDescending(f => f.PublishTime).GroupBy(f => f.Id).Select(g => g.First()))
                {
                    parsedFeedItem.Id = $"{feed.Id}-{parsedFeedItem.Id}".Md5HashToGuid();
                    parsedFeedItem.FeedId = feed.Id;
                    var feedItem = await db.FeedItems.FindAsync(parsedFeedItem.Id);
                    if (feedItem != null)
                    {
                        // Update feedItemInDb
                        feedItem.Content = parsedFeedItem.Content;
                        feedItem.Link = parsedFeedItem.Link;
                        feedItem.PictureUri = parsedFeedItem.PictureUri;
                        feedItem.Summary = parsedFeedItem.Summary;
                        feedItem.Title = parsedFeedItem.Title;

                        if (parsedFeedItem.PublishTime == default(DateTime))
                        {
                            parsedFeedItem.PublishTime = feedItem.PublishTime;
                        }
                        else
                        {
                            feedItem.PublishTime = parsedFeedItem.PublishTime;
                        }
                    }
                    else
                    {
                        if (parsedFeedItem.PublishTime == default(DateTime))
                        {
                            parsedFeedItem.PublishTime = DateTime.UtcNow;
                        }

                        db.FeedItems.Add(parsedFeedItem);
                    }

                    if (feed.LatestItemPublishTime < parsedFeedItem.PublishTime)
					{
                        feed.LatestItemPublishTime = parsedFeedItem.PublishTime;
					}
                }

                // Update feed stat.
                feed.TotalFavorites = await db.Favorites.CountAsync(f => f.FeedInfoId == feed.Id);
                feed.TotalSubscribers = await db.FeedSubscriptions.Where(f => f.FeedId == feed.Id).CountAsync(cancellationToken);
                feed.TotalPosts = await db.FeedItems.Where(f => f.FeedId == feed.Id).CountAsync();

                // Save to db.
                await db.SaveChangesAsync(cancellationToken);
                                
                DateTime endTime = DateTime.Now;
                Logger.LogInformation($"Refresh feed: {feed.Uri} finished at {endTime}, elasped {(endTime - starTime).TotalSeconds} seconds");
            }
        }

        private async Task BatchOperation<T>(IQueryable<T> query, int batchSize, CancellationToken cancellationToken, Func<T, Task> perItemOp, Func<Task> perBatchOp = null)
        {
            try
            {
                int skip = 0;
                while (!cancellationToken.IsCancellationRequested)
                {
                    var items = await query.Skip(skip).Take(batchSize).ToArrayAsync(cancellationToken);
                    foreach (var item in items)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }

                        await perItemOp(item);
                    }

                    if (perBatchOp != null && !cancellationToken.IsCancellationRequested)
                    {
                        await perBatchOp();
                    }

                    if (items.Length < batchSize)
                    {
                        break;
                    }

                    skip += batchSize;
                }
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}